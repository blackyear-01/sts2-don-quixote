"""Build a Spine (4.2 JSON) rig for 白月骑士-巴里 from the frame-by-frame art.

为什么要有它
------------
巴里原来是一张静态 Sprite2D，换姿势靠 C# 直接换 Texture。那样做有一个死穴：
原版 <c>NCreature</c> 只有在 <c>%Visuals</c> 是 SpineSprite（<c>HasSpineAnimation</c>）时
才会创建 <c>CreatureAnimator</c>，也才会在死亡时 <c>SetAnimationTrigger("Dead")</c>。
没有骨架 = 死亡时没有死亡图，只能靠代码硬换一张 png，而且要另外处理尺寸/锚点。

所以这里把 <c>assets/art/barry_*.png</c> 全部打进一张图集、做成一副极简骨架
（一根 body 骨骼 + 一个 body_slot 槽位，每个姿势是一个 region 附件），
动画名和原版怪物状态机对齐（idle_loop / attack / hurt / cast / die），
这样死亡图就是骨架里的 <c>die</c> 动画，原版自己会播。

输出（DonQuixote/assets/animations）：
    barry.png            图集页
    barry.atlas          spine 文本图集
    barry.spatlas        godot 包装（把 atlas 文本塞进 json）
    barry.spjson         骨架
    barry.json           同一份骨架（备用探测）
    barry_skel_data.tres SpineSkeletonDataResource

以及一张给人工检查用的 preview_overlay.png（idle 与 die 按骨架里的位置叠一次）。

坐标口径
--------
tools/barry_frames.py 已经把每一帧归一化到同一块 422x546 画布、地平线 y=470。
场景里原来的 Sprite2D 是 position=(0,-276.2)、scale=1.4，也就是「脚底落在节点原点」。
这里用同一套：附件 scale=1.4，中心 y = 1.4 * (470 - 546/2) = 275.8。

死亡图（376x165）不是同尺寸画布，按「底边贴地」单独算：y = 1.4 * 82.5 ≈ 115.5，
再往下压一点点让它像趴在地上（`DIE_SINK`）。
"""
import json
import os
import sys

from PIL import Image

MOD_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(MOD_ROOT, "assets", "art")
OUT = os.path.join(MOD_ROOT, "assets", "animations")
os.makedirs(OUT, exist_ok=True)

PAGE = "barry.png"
PAGE_W = 2048

# ---- 姿势表 -------------------------------------------------------------
# 名字会写进 atlas 的 region 名和 spjson 的附件 path，两者必须完全一致。
ATK = ["barry_atk_f%d.png" % i for i in range(4)]
OATH = ["barry_oath_f%d.png" % i for i in range(16)]

POSES = [
    ("idle", "barry_idle.png"),
    ("guard", "barry_guard.png"),
    ("hurt", "barry_hurt.png"),
]
POSES += [("atk%d" % i, name) for i, name in enumerate(ATK)]
POSES += [("oath%d" % i, name) for i, name in enumerate(OATH)]
POSES += [("die", "barry_die.png")]

# 帧时长（毫秒），和 C# 侧 DonQuixoteWhiteMoonSpritePose 的老时间轴一致。
ATK_MS = [200, 160, 160, 520]
OATH_MS = [200, 160, 160, 160, 160, 160, 200, 160, 160, 160, 160, 160, 200, 160, 160, 560]

CANVAS_W, CANVAS_H = 422, 546
GROUND_Y = 470          # 画布里脚底的位置（从顶部算）
SCALE = 1.4             # 和原 Sprite2D 的 scale 一致

# 死亡图（376x165）不是同尺寸画布：它是一滩血，按「底边贴地」单独摆，
# 并且缩得比角色小一点（1.15），否则 1.4 倍后高 231px，会盖到角色腰上。
DIE_SCALE = 1.15

DIE_SINK = 8.0          # 死亡图底部再往下 8px，看起来是趴在地上

images = []
for name, filename in POSES:
    path = os.path.join(ART, filename)
    if not os.path.exists(path):
        raise SystemExit("missing pose art: %s (run tools/barry_frames.py)" % path)
    images.append((name, Image.open(path).convert("RGBA")))

# ---- 图集排版（shelf packing，按高度降序少浪费） -------------------------
ordered = sorted(images, key=lambda item: -item[1].height)
rects = {}
x = 0
y = 0
row_h = 0
for name, img in ordered:
    if x + img.width > PAGE_W:
        x = 0
        y += row_h
        row_h = 0
    rects[name] = (x, y, img.width, img.height)
    x += img.width
    row_h = max(row_h, img.height)
page_h = y + row_h

page = Image.new("RGBA", (PAGE_W, page_h), (0, 0, 0, 0))
for name, img in images:
    px, py, _, _ = rects[name]
    page.paste(img, (px, py))
page.save(os.path.join(OUT, PAGE))
print("page %s %s" % (os.path.join(OUT, PAGE), page.size))

# ---- atlas 文本 ---------------------------------------------------------
lines = [
    PAGE,
    "size: %d,%d" % (PAGE_W, page_h),
    "format: RGBA8888",
    "filter: Linear,Linear",
    "repeat: none",
]
for name, img in images:
    px, py, w, h = rects[name]
    lines += [
        name,
        "  rotate: false",
        "  xy: %d, %d" % (px, py),
        "  size: %d, %d" % (w, h),
        "  orig: %d, %d" % (w, h),
        "  offset: 0, 0",
        "  index: -1",
    ]
atlas_text = "\n".join(lines) + "\n"

with open(os.path.join(OUT, "barry.atlas"), "w", encoding="utf-8", newline="\n") as fh:
    fh.write(atlas_text)

with open(os.path.join(OUT, "barry.spatlas"), "w", encoding="utf-8", newline="\n") as fh:
    json.dump(
        {
            "atlas_data": atlas_text,
            "normal_texture_prefix": "",
            "source_path": "res://DonQuixote/animations/barry.atlas",
            "specular_texture_prefix": "",
        },
        fh,
        ensure_ascii=False,
    )

# ---- 附件（每个姿势一个 region） ---------------------------------------
SKIN = {}
for name, img in images:
    w, h = img.width, img.height
    if name == "die":
        # 血滩：底边贴地。spine 的 y 与 make_spine.py 同一口径
        # （attachment.y = scale * (底边在画布中的 y - 画布高/2) 的等价写法）。
        pose_scale = DIE_SCALE
        y_off = round(pose_scale * (h / 2.0) - DIE_SINK, 3)
        x_off = 0.0
    else:
        pose_scale = SCALE
        y_off = round(SCALE * (GROUND_Y - CANVAS_H / 2.0), 3)
        x_off = 0.0
    SKIN[name] = {
        "type": "region",
        "path": name,
        "x": x_off,
        "y": y_off,
        "scaleX": pose_scale,
        "scaleY": pose_scale,
        "rotation": 0,
        "width": w,
        "height": h,
    }


def key(name, time):
    return {"time": round(time, 4), "name": name}


def seconds(ms_list, start=0):
    """把毫秒时长表转成每帧的起始秒。"""
    out = []
    t = 0.0
    for i, ms in enumerate(ms_list):
        if i >= start:
            out.append(round(t, 4))
        t += ms / 1000.0
    return out, round(t, 4)


def timeline(pairs):
    return {"attachment": [key(n, t) for n, t in pairs]}


def frames_anim(prefix, count, ms_list, tail_name, tail_ms):
    """prefix0..prefixN 按毫秒表依次出现，最后一帧再停 tail_ms 毫秒。"""
    pairs = []
    t = 0.0
    for i in range(count):
        pairs.append(("%s%d" % (prefix, i), t))
        t += ms_list[i] / 1000.0
    pairs.append((tail_name, round(t + tail_ms / 1000.0, 4)))
    return timeline(pairs)


# ---- 动画 ---------------------------------------------------------------
animations = {}

# 原版怪物状态机的默认动画名（MonsterModel.GenerateAnimator：idle_loop/cast/attack/hurt/die）。
# 静止类动画刻意给个 1 秒的长度（两个同名 key）：spine 里 duration=0 的动画
# 循环播放时行为不确定，撑一段长度最稳（setup pose 的 slot 附件本来也是 idle）。
animations["idle_loop"] = {"slots": {"body_slot": timeline([("idle", 0.0), ("idle", 1.0)])}}
animations["idle"] = {"slots": {"body_slot": timeline([("idle", 0.0), ("idle", 1.0)])}}
animations["cast"] = {"slots": {"body_slot": timeline([("idle", 0.0), ("idle", 0.4)])}}
animations["attack"] = {
    "slots": {"body_slot": frames_anim("atk", 4, ATK_MS, "idle", 0)}
}
animations["hurt"] = {"slots": {"body_slot": timeline([("hurt", 0.0), ("idle", 0.45)])}}
animations["defend"] = {"slots": {"body_slot": timeline([("guard", 0.0), ("idle", 0.5)])}}

# 死亡：原版 StartDeathAnim 会触发 "Dead" -> 状态机的 die 状态。
# 时长对齐 DonQuixoteWhiteMoonKnight.DeathAnimLengthOverride = 0.8。
animations["die"] = {"slots": {"body_slot": timeline([("die", 0.0), ("die", 0.8)])}}

# 意图3：起手 10 帧停住在「拉满弓」，松弦 6 帧收势。
oath_start, oath_total = seconds(OATH_MS)
windup = [("oath%d" % i, oath_start[i]) for i in range(10)]
windup.append(("oath9", round(sum(OATH_MS[:10]) / 1000.0, 4)))
animations["bow_windup"] = {"slots": {"body_slot": timeline(windup)}}

release = [("oath%d" % i, round(oath_start[i] - sum(OATH_MS[:10]) / 1000.0, 4)) for i in range(10, 16)]
release.append(("idle", round(sum(OATH_MS[10:]) / 1000.0, 4)))
animations["bow_release"] = {"slots": {"body_slot": timeline(release)}}

skeleton = {
    "skeleton": {
        "hash": "barry",
        "spine": "4.2.43",
        "x": 0,
        "y": 0,
        "width": CANVAS_W,
        "height": CANVAS_H,
        "images": "./",
        "audio": "",
    },
    "bones": [{"name": "root"}, {"name": "body", "parent": "root", "y": 0}],
    "slots": [{"name": "body_slot", "bone": "body", "attachment": "idle"}],
    "skins": [{"name": "default", "attachments": {"body_slot": SKIN}}],
    "animations": animations,
}

text = json.dumps(skeleton, ensure_ascii=False)
for name in ("barry.spjson", "barry.json"):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
print("skeleton bytes", len(text))

# ---- godot 资源 ---------------------------------------------------------
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_pck import uid_for  # noqa: E402

atlas_res = "DonQuixote/animations/barry.spatlas"
skel_res = "DonQuixote/animations/barry.spjson"
tres_res = "DonQuixote/animations/barry_skel_data.tres"

tres = """[gd_resource type="SpineSkeletonDataResource" load_steps=3 format=3 uid="%s"]

[ext_resource type="SpineAtlasResource" uid="%s" path="res://%s" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" uid="%s" path="res://%s" id="2_skel"]

[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skel")
""" % (
    uid_for(tres_res),
    uid_for(atlas_res),
    atlas_res,
    uid_for(skel_res),
    skel_res,
)
with open(os.path.join(OUT, "barry_skel_data.tres"), "w", encoding="utf-8", newline="\n") as fh:
    fh.write(tres)
print("wrote barry_skel_data.tres")

# ---- 人工检查用的叠加预览 ----------------------------------------------
# 把 idle 和 die 按骨架里的位置叠到 422x546 画布里（脚底 = y470），
# 用来确认死亡图摆位对不对（游戏里改一次要重打包，这里先看一眼）。
preview = Image.new("RGBA", (CANVAS_W, CANVAS_H), (32, 32, 40, 255))
idle_img = dict(images)["idle"]
preview.alpha_composite(idle_img, (0, 0))
die_img = dict(images)["die"]
die_w = int(die_img.width * DIE_SCALE / SCALE)
die_h = int(die_img.height * DIE_SCALE / SCALE)
die_scaled = die_img.resize((die_w, die_h), Image.LANCZOS)
die_x = int(CANVAS_W / 2 - die_w / 2)
die_bottom = GROUND_Y + int(DIE_SINK / SCALE)
preview.alpha_composite(die_scaled, (die_x, die_bottom - die_h))
preview.save(os.path.join(OUT, "barry_preview_overlay.png"))
print("preview barry_preview_overlay.png")
