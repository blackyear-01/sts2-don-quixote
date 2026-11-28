"""Spine 骨架静态体检（本项目专用）。

对照三份东西：
  1. `don_quixote.spjson`（骨架 / 皮肤 / 动画）
  2. `don_quixote.spatlas`（图集区域）
  3. `D:\\sts2mod\\Don Quixote\\src\\**\\*.cs`（代码里实际用到的动画名）

输出 ERROR / WARN / INFO 三档，有 ERROR 时退出码 1。

用法：
  python spine_lint.py
  python spine_lint.py --json ... --atlas ... --src ...
"""

import argparse
import glob
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from spine_render import animation_duration, load_skeleton, parse_atlas  # noqa: E402

BASE = r"D:\sts2mod\Don Quixote\assets\animations"
DEFAULT_JSON = os.path.join(BASE, "don_quixote.spjson")
DEFAULT_ATLAS = os.path.join(BASE, "don_quixote.spatlas")
DEFAULT_SRC = r"D:\sts2mod\Don Quixote\src"

# 引擎/本模组约定会去播放的动画（CombatPose 等处直接写死的）
KNOWN_REQUIRED = ("idle_loop", "relaxed_loop", "die", "attack", "hit", "cast", "defend")

problems = []


def err(msg):
    problems.append(("ERROR", msg))


def warn(msg):
    problems.append(("WARN", msg))


def info(msg):
    problems.append(("INFO", msg))


def check_timelines(skel):
    for anim_name, anim in sorted((skel.get("animations") or {}).items()):
        for group in ("bones", "slots", "ik", "transform", "deform", "drawOrder", "events"):
            block = anim.get(group) or {}
            for target, timelines in block.items():
                if not isinstance(timelines, dict):
                    continue
                for tl_name, keys in timelines.items():
                    if not isinstance(keys, list) or not keys:
                        continue
                    times = [float(k.get("time", 0.0)) for k in keys]
                    if any(b < a for a, b in zip(times, times[1:])):
                        err("%s.%s.%s：关键帧时间不是递增（%s）" % (anim_name, group, target, times[:8]))
                    if times[0] < 0:
                        err("%s.%s.%s：存在负时间关键帧" % (anim_name, group, target))
                    if tl_name == "attachment":
                        for k in keys:
                            if not k.get("name"):
                                warn("%s.%s.%s：有 attachment 关键帧没写 name" % (anim_name, group, target))


def check_attachments(skel, regions):
    used_regions = set()
    for skin in skel.get("skins") or []:
        for slot_name, attachments in (skin.get("attachments") or {}).items():
            slot = next((s for s in skel.get("slots", []) if s["name"] == slot_name), None)
            if slot is None:
                err("皮肤引用了不存在的槽位：%s" % slot_name)
            for name, att in attachments.items():
                kind = att.get("type", "region")
                path = att.get("path") or name
                if kind in ("region", "mesh"):
                    if path not in regions:
                        err("附件 %s/%s 在图集里找不到区域 %s" % (slot_name, name, path))
                    else:
                        used_regions.add(path)
                elif kind in ("boundingbox", "point"):
                    continue
                else:
                    warn("附件 %s/%s 类型 %s 未检查" % (slot_name, name, kind))
    unused = sorted(set(regions) - used_regions)
    if unused:
        info("图集里未被附件引用的区域 %d 个：%s" % (len(unused), ", ".join(unused[:10])))
    return used_regions


def check_slots(skel):
    bone_names = {b["name"] for b in skel.get("bones", [])}
    for slot in skel.get("slots", []):
        if slot.get("bone") not in bone_names:
            err("槽位 %s 指向不存在的骨骼 %s" % (slot.get("name"), slot.get("bone")))
        att = slot.get("attachment")
        if att:
            names = set()
            for skin in skel.get("skins") or []:
                names |= set((skin.get("attachments") or {}).get(slot["name"], {}).keys())
            if att not in names:
                err("槽位 %s 默认附件 %s 不在皮肤里" % (slot.get("name"), att))
    for bone in skel.get("bones", []):
        parent = bone.get("parent")
        if parent and parent not in bone_names:
            err("骨骼 %s 的父骨骼 %s 不存在" % (bone.get("name"), parent))


def check_animation_targets(skel):
    bones = {b["name"] for b in skel.get("bones", [])}
    slots = {s["name"] for s in skel.get("slots", [])}
    for anim_name, anim in sorted((skel.get("animations") or {}).items()):
        for bone_name in (anim.get("bones") or {}):
            if bone_name not in bones:
                err("%s：动画引用了不存在的骨骼 %s" % (anim_name, bone_name))
        for slot_name in (anim.get("slots") or {}):
            if slot_name not in slots:
                err("%s：动画引用了不存在的槽位 %s" % (anim_name, slot_name))


def check_sequences(skel, regions):
    """逐帧动画要有连续编号；缺帧通常是导图时漏了。"""
    groups = {}
    for name in regions:
        m = re.match(r"^([a-z_]+?)(\d+)$", name)
        if m:
            groups.setdefault(m.group(1), set()).add(int(m.group(2)))
    for prefix, nums in sorted(groups.items()):
        # 第 0 帧通常就叫 base 名（idle / attack0 两种命名都有），不算缺帧
        if prefix in regions:
            nums = nums | {0}
        missing = [i for i in range(0, max(nums) + 1) if i not in nums]
        if missing:
            warn("序列 %s 缺编号：%s" % (prefix, missing[:12]))


def check_code_coverage(animations, src_root):
    """代码里 SetAnimation("x…") 的动画名是否都存在于骨架。"""
    if not os.path.isdir(src_root):
        warn("源码目录不存在，跳过代码覆盖检查：%s" % src_root)
        return
    referenced = {}
    for path in glob.glob(os.path.join(src_root, "**", "*.cs"), recursive=True):
        try:
            text = open(path, encoding="utf-8").read()
        except OSError:
            continue
        for m in re.finditer(r'SetAnimation\w*\(\s*"([^"]+)"', text):
            referenced.setdefault(m.group(1), set()).add(os.path.basename(path))
        for m in re.finditer(r'const\s+string\s+DonQuixote_\w*Anim\w*\s*=\s*"([^"]+)"', text):
            referenced.setdefault(m.group(1), set()).add(os.path.basename(path))
    missing = {k: v for k, v in referenced.items() if k not in animations}
    for name, files in sorted(missing.items()):
        err("代码里播放的动画 %s 不存在于骨架（来自 %s）" % (name, ", ".join(sorted(files))))
    unused = sorted(a for a in animations if a not in referenced)
    if unused:
        info("骨架里未被代码直接引用的动画 %d 个：%s" % (len(unused), ", ".join(unused[:12])))
    for name in KNOWN_REQUIRED:
        if name not in animations:
            warn("约定动画缺失：%s" % name)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", action="append", default=None,
                    help="可重复；默认取 assets/animations 下所有 *.spjson（多人共用一份代码时要用全部骨架核对动画名）")
    ap.add_argument("--atlas", default=DEFAULT_ATLAS)
    ap.add_argument("--src", default=DEFAULT_SRC)
    args = ap.parse_args(argv)

    json_paths = args.json or sorted(glob.glob(os.path.join(BASE, "*.spjson")))
    skeletons = {p: load_skeleton(p) for p in json_paths}
    all_animations = set()
    for s in skeletons.values():
        all_animations |= set((s.get("animations") or {}).keys())

    for json_path, skel in skeletons.items():
        stem = os.path.splitext(json_path)[0]
        atlas_path = args.atlas if args.atlas != DEFAULT_ATLAS else next(
            (p for p in (stem + ".spatlas", stem + ".atlas") if os.path.exists(p)), None)
        if atlas_path is None or not os.path.exists(atlas_path):
            warn("骨架 %s 没找到配套图集，跳过附件检查" % os.path.basename(json_path))
            continue
        regions, pages = parse_atlas(atlas_path)

        meta = skel.get("skeleton", {})
        print("== %s  (%s v%s  %sx%s)" % (os.path.basename(json_path), meta.get("hash"), meta.get("spine"),
                                          meta.get("width"), meta.get("height")))
        print("   bones %d  slots %d  skins %d  animations %d  regions %d"
              % (len(skel.get("bones", [])), len(skel.get("slots", [])), len(skel.get("skins") or []),
                 len(skel.get("animations") or {}), len(regions)))

        missing_pages = [p for p in pages if not os.path.exists(p)]
        for page in missing_pages:
            err("图集页文件不存在：%s" % page)

        check_slots(skel)
        check_attachments(skel, regions)
        check_animation_targets(skel)
        check_timelines(skel)
        check_sequences(skel, regions)

        for name in sorted((skel.get("animations") or {})):
            dur = animation_duration(skel["animations"][name])
            if dur <= 0:
                warn("动画 %s 时长为 0（%s）" % (name, os.path.basename(json_path)))

    check_code_coverage(all_animations, args.src)

    order = {"ERROR": 0, "WARN": 1, "INFO": 2}
    problems.sort(key=lambda p: order[p[0]])
    for level, msg in problems:
        print("%-5s %s" % (level, msg))

    counts = {k: sum(1 for lv, _ in problems if lv == k) for k in ("ERROR", "WARN", "INFO")}
    print("summary ERROR=%d WARN=%d INFO=%d" % (counts["ERROR"], counts["WARN"], counts["INFO"]))
    return 1 if counts["ERROR"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
