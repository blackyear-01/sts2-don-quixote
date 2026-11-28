"""Spine JSON 预览渲染器（本项目专用，零依赖除 Pillow）。

用途：在没有 Spine 编辑器 / Godot 的环境里，把 `*.spjson` + `*.spatlas` + `*.png`
按动画逐帧渲染成 PNG / 拼图 / GIF，方便用肉眼（或 read_image 工具）检查骨架。

支持：
  * 骨骼 setup pose + rotate / translate / scale 时间线
  * 槽位的 attachment 切换时间线（本项目是逐帧序列图，全靠这个）
  * region 附件的 x/y/rotation/scale
  * atlas 里 rotate: true 的区域
  * .spatlas 既可以是标准文本，也可以是本项目这种 {"atlas_data": "..."} 的 JSON 包装

用法：
  python spine_render.py --list
  python spine_render.py --anim idle_loop --frames 8
  python spine_render.py --anim attack --frames 12 --gif
  python spine_render.py --all --frames 6
"""

import argparse
import io
import json
import math
import os
import sys

from PIL import Image, ImageDraw

BASE = r"D:\sts2mod\Don Quixote\assets\animations"
DEFAULT_JSON = os.path.join(BASE, "don_quixote.spjson")
DEFAULT_ATLAS = os.path.join(BASE, "don_quixote.spatlas")
DEFAULT_PNG = os.path.join(BASE, "don_quixote.png")
OUT_DIR = r"D:\sts2mod\tools\dump\spine"

FONT = None


# ---------------------------------------------------------------- 解析


def parse_atlas(path):
    """返回 ({region_name: dict(page, xy, size, rotate, offset)}, {page_name: 文件路径})。"""
    raw = io.open(path, encoding="utf-8").read()
    text = raw
    if raw.lstrip().startswith("{"):
        data = json.loads(raw)
        text = data.get("atlas_data", "")
    lines = [ln.rstrip() for ln in text.splitlines()]

    regions = {}
    pages = []
    current_page = None
    current = None
    for line in lines:
        if not line.strip():
            current = None
            continue
        if not line.startswith(" ") and ":" not in line:
            name = line.strip()
            # libgdx 图集里「页文件名」和「区域名」都在行首：
            # 页文件名一定带扩展名，区域名不带。
            if name.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                current_page = os.path.join(os.path.dirname(path), name)
                pages.append(current_page)
                current = None
                continue
            current = {"name": name, "page": current_page}
            regions[name] = current
            continue
        stripped = line.strip()
        if current is None and ":" not in stripped:
            current = {"name": stripped, "page": current_page}
            regions[stripped] = current
            continue
        if current is not None and ":" in stripped:
            key, _, value = stripped.partition(":")
            key = key.strip()
            value = value.strip()
            if key == "xy":
                current["xy"] = tuple(int(v) for v in value.split(","))
            elif key == "size":
                current["size"] = tuple(int(v) for v in value.split(","))
            elif key == "orig":
                current["orig"] = tuple(int(v) for v in value.split(","))
            elif key == "offset":
                current["offset"] = tuple(int(float(v)) for v in value.split(","))
            elif key == "rotate":
                current["rotate"] = value.lower() == "true"
            elif key == "index":
                current["index"] = int(value)
    return regions, pages


def load_skeleton(path):
    with io.open(path, encoding="utf-8") as fh:
        return json.load(fh)


# ---------------------------------------------------------------- 变换


def mat_mul(a, b):
    a0, a1, a2, a3, a4, a5 = a
    b0, b1, b2, b3, b4, b5 = b
    return (
        a0 * b0 + a1 * b3,
        a0 * b1 + a1 * b4,
        a0 * b2 + a1 * b5 + a2,
        a3 * b0 + a4 * b3,
        a3 * b1 + a4 * b4,
        a3 * b2 + a4 * b5 + a5,
    )


def mat_trs(x, y, rot_deg, sx, sy):
    r = math.radians(rot_deg)
    c, s = math.cos(r), math.sin(r)
    return (c * sx, -s * sy, x, s * sx, c * sy, y)


def mat_inv(m):
    a0, a1, a2, a3, a4, a5 = m
    det = a0 * a4 - a1 * a3
    if abs(det) < 1e-12:
        return None
    i0 = a4 / det
    i1 = -a1 / det
    i3 = -a3 / det
    i4 = a0 / det
    i2 = -(i0 * a2 + i1 * a5)
    i5 = -(i3 * a2 + i4 * a5)
    return (i0, i1, i2, i3, i4, i5)


def sample_curve(keys, time, fields):
    """在关键帧列表里按 step/linear（本项目以 step 为主）取值。"""
    if not keys:
        return {}
    prev = keys[0]
    nxt = None
    for k in keys:
        if k["time"] <= time:
            prev = k
        else:
            nxt = k
            break
    if prev.get("curve") == "stepped" or nxt is None:
        return prev
    span = nxt["time"] - prev["time"]
    if span <= 0:
        return prev
    t = (time - prev["time"]) / span
    out = {"time": time}
    for field in fields:
        a = prev.get(field, 0.0)
        b = nxt.get(field, a)
        out[field] = a + (b - a) * t
    return out


def translate_of(entry, default=(0.0, 0.0)):
    if "x" in entry or "y" in entry:
        return (entry.get("x", 0.0), entry.get("y", 0.0))
    return default


def bone_pose(skel, anim, time):
    """返回 {bone_name: world_matrix}。"""
    anim_bones = (anim or {}).get("bones", {})
    local = {}
    for bone in skel.get("bones", []):
        name = bone["name"]
        x = bone.get("x", 0.0)
        y = bone.get("y", 0.0)
        rot = bone.get("rotation", 0.0)
        sx = bone.get("scaleX", 1.0)
        sy = bone.get("scaleY", 1.0)

        tl = anim_bones.get(name, {})
        if "rotate" in tl:
            key = sample_curve(tl["rotate"], time, ["angle", "value"])
            rot += key.get("angle", key.get("value", 0.0))
        if "translate" in tl:
            key = sample_curve(tl["translate"], time, ["x", "y"])
            dx, dy = translate_of(key, (0.0, 0.0))
            x += dx
            y += dy
        if "scale" in tl:
            key = sample_curve(tl["scale"], time, ["x", "y"])
            sx *= key.get("x", 1.0)
            sy *= key.get("y", 1.0)

        local[name] = mat_trs(x, y, rot, sx, sy)

    world = {}

    def resolve(name, guard=0):
        if name in world:
            return world[name]
        if guard > 64:
            return (1, 0, 0, 0, 1, 0)
        bone = next((b for b in skel.get("bones", []) if b["name"] == name), None)
        if bone is None:
            world[name] = (1, 0, 0, 0, 1, 0)
            return world[name]
        parent = bone.get("parent")
        if parent:
            world[name] = mat_mul(resolve(parent, guard + 1), local[name])
        else:
            world[name] = local[name]
        return world[name]

    for bone in skel.get("bones", []):
        resolve(bone["name"])
    return world


def attachment_at(anim, slot_name, time):
    tl = (anim or {}).get("slots", {}).get(slot_name, {})
    keys = tl.get("attachment")
    if not keys:
        return None, False
    # attachment 是离散切换，永远是「取最后一个 time <= t 的关键帧」
    chosen = keys[0]
    for key in keys:
        if float(key.get("time", 0.0)) <= time:
            chosen = key
        else:
            break
    return chosen.get("name"), True


def animation_duration(anim):
    end = 0.0
    for group in ("bones", "slots", "ik", "transform", "deform", "drawOrder", "events"):
        block = anim.get(group) or {}
        for _, timelines in block.items():
            if isinstance(timelines, dict):
                for tl_name, keys in timelines.items():
                    if isinstance(keys, list) and keys:
                        end = max(end, float(keys[-1].get("time", 0.0)))
    return end


# ---------------------------------------------------------------- 渲染


def region_image(pages, region, cache, png_override=None):
    page = png_override or region.get("page")
    key = (page, region.get("xy"), region.get("size"), region.get("rotate"))
    if key in cache:
        return cache[key]
    src = Image.open(page).convert("RGBA")
    x, y = region.get("xy", (0, 0))
    w, h = region.get("size", (0, 0))
    if region.get("rotate"):
        # 旋转区域在 atlas 里是横躺的：裁剪后逆时针旋转 90°
        crop = src.crop((x, y, x + h, y + w)).rotate(90, expand=True)
    else:
        crop = src.crop((x, y, x + w, y + h))
    cache[key] = crop
    return crop


def render_frame(skel, anim, time, regions, pages, cache, scale=1.0, extra=0.0):
    meta = skel.get("skeleton", {})
    sw = meta.get("width", 512)
    sh = meta.get("height", 600)
    width = int(round((sw + extra * 2) * scale))
    height = int(round((sh + extra * 2) * scale))
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    bones = bone_pose(skel, anim, time)
    skins = skel.get("skins") or []
    skin = skins[0] if skins else {"attachments": {}}

    for slot in skel.get("slots", []):
        slot_name = slot["name"]
        bone_name = slot.get("bone")
        attach_name, animated = attachment_at(anim, slot_name, time)
        if not animated:
            attach_name = slot.get("attachment")
        if not attach_name:
            continue
        per_slot = skin.get("attachments", {}).get(slot_name, {})
        attach = per_slot.get(attach_name)
        if attach is None:
            continue
        if attach.get("type", "region") != "region":
            continue
        path = attach.get("path") or attach_name
        region = regions.get(path)
        if region is None:
            continue

        bone_mat = bones.get(bone_name, (1, 0, 0, 0, 1, 0))
        local = mat_trs(
            attach.get("x", 0.0),
            attach.get("y", 0.0),
            attach.get("rotation", 0.0),
            attach.get("scaleX", 1.0),
            attach.get("scaleY", 1.0),
        )
        world = mat_mul(bone_mat, local)
        # spine 坐标（y 向上）→ 画布坐标（y 向下），水平居中并整体缩放
        flip = (scale, 0, width / 2.0, 0, -scale, height - extra * scale)
        screen = mat_mul(flip, world)

        img = region_image(pages, region, cache)
        iw, ih = img.size
        inv = mat_inv(screen)
        if inv is None:
            continue
        m0, m1, m2, m3, m4, m5 = inv
        coeffs = (m0, m1, m2 + iw / 2.0, -m3, -m4, ih / 2.0 - m5)
        layer = img.transform(canvas.size, Image.AFFINE, coeffs, resample=Image.BICUBIC)
        canvas.alpha_composite(layer)
    return canvas


def checker(size):
    w, h = size
    tile = 20
    board = Image.new("RGBA", (w, h))
    px = board.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = (206, 206, 212, 255) if ((x // tile) + (y // tile)) % 2 == 0 else (255, 255, 255, 255)
    return board


def labelled(image, text):
    pad = 18
    out = Image.new("RGBA", (image.size[0], image.size[1] + pad), (24, 24, 28, 255))
    out.alpha_composite(image, (0, pad))
    draw = ImageDraw.Draw(out)
    draw.text((4, 3), text, fill=(235, 235, 240, 255))
    return out


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default=DEFAULT_JSON)
    ap.add_argument("--atlas", default=DEFAULT_ATLAS)
    ap.add_argument("--png", default=DEFAULT_PNG)
    ap.add_argument("--anim", action="append", default=None)
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--frames", type=int, default=8)
    ap.add_argument("--scale", type=float, default=0.5)
    ap.add_argument("--gif", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--out", default=OUT_DIR)
    ap.add_argument("--extra", type=float, default=170.0, help="画布四周额外留白（spine 单位），0 表示严格按 skeleton 尺寸")
    ap.add_argument("--no-fit", action="store_true", help="不按内容自动裁边")
    args = ap.parse_args(argv)

    skel = load_skeleton(args.json)
    animations = skel.get("animations") or {}
    names = sorted(animations)

    if args.list:
        for name in names:
            dur = animation_duration(animations[name])
            print("%-14s duration=%.2fs" % (name, dur))
        return 0

    targets = names if args.all else (args.anim or ["idle_loop"])
    regions, pages = parse_atlas(args.atlas)
    cache = {}
    os.makedirs(args.out, exist_ok=True)

    for name in targets:
        if name not in animations:
            print("missing animation:", name, file=sys.stderr)
            continue
        anim = animations[name]
        dur = animation_duration(anim)
        frames = max(1, args.frames)
        times = [dur * i / (frames - 1) for i in range(frames)] if frames > 1 else [0.0]
        imgs = []
        for idx, t in enumerate(times):
            frame = render_frame(skel, anim, t, regions, pages, cache, scale=args.scale, extra=args.extra)
            imgs.append(frame)

        # 按「整段动画的内容外框」统一裁边，避免每帧大小不一
        if not args.no_fit:
            boxes = [img.getbbox() for img in imgs if img.getbbox() is not None]
            if boxes:
                left = min(b[0] for b in boxes)
                top = min(b[1] for b in boxes)
                right = max(b[2] for b in boxes)
                bottom = max(b[3] for b in boxes)
                margin = 4
                crop = (
                    max(0, left - margin),
                    max(0, top - margin),
                    min(imgs[0].size[0], right + margin),
                    min(imgs[0].size[1], bottom + margin),
                )
                imgs = [img.crop(crop) for img in imgs]
        sheet_w = sum(img.size[0] for img in imgs)
        sheet_h = max(img.size[1] for img in imgs)
        sheet = Image.new("RGBA", (sheet_w, sheet_h + 18), (24, 24, 28, 255))
        draw = ImageDraw.Draw(sheet)
        cursor = 0
        for idx, (t, img) in enumerate(zip(times, imgs)):
            sheet.alpha_composite(img, (cursor, 18))
            draw.text((cursor + 4, 3), "%s t=%.2f" % (name, t), fill=(235, 235, 240, 255))
            cursor += img.size[0]

        out_png = os.path.join(args.out, "sheet_%s.png" % name)
        sheet.save(out_png)
        print("sheet", out_png, sheet.size)

        if args.gif:
            gif_frames = []
            for img in imgs:
                board = checker(img.size)
                board.alpha_composite(img)
                gif_frames.append(board.convert("P", palette=Image.ADAPTIVE))
            if gif_frames:
                out_gif = os.path.join(args.out, "anim_%s.gif" % name)
                gif_frames[0].save(out_gif, save_all=True, append_images=gif_frames[1:], duration=90, loop=0)
                print("gif", out_gif)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
