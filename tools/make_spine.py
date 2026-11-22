"""Build a Spine (4.2 JSON) rig for Don Quixote from the derived combat art.

The rig is intentionally simple: one body bone with one slot that swaps a set
of region attachments.  Every region is packed into a single atlas page so the
runtime only has to load one texture, mirroring how the vanilla characters ship
one png per skeleton.

Outputs (under DonQuixote/animations):
    don_quixote.png            atlas page (sprite sheet of every pose)
    don_quixote.atlas          text atlas (spine format)
    don_quixote.spatlas        godot wrapper around the atlas text
    don_quixote.spjson         JSON skeleton (bone, slot, skin, animations)
    don_quixote.json           same skeleton, plain extension (fallback probe)
    don_quixote_skel_data.tres SpineSkeletonDataResource binding the two above
"""
import json
import os

from PIL import Image

ART = r"D:\sts2mod\Don Quixote\assets\art"
OUT = os.path.join(ART, "..", "animations")
OUT = os.path.abspath(OUT)
os.makedirs(OUT, exist_ok=True)

PAGE = "don_quixote.png"

# Idle is a frame-by-frame breathing loop exported by make_art.py as
# idle_f0.png .. idle_fN.png.  Frame 0 doubles as the still "idle" region.
DonQuixote_idle_files = []
DonQuixote_i = 0
while os.path.exists(os.path.join(ART, "idle_f%d.png" % DonQuixote_i)):
    DonQuixote_idle_files.append("idle_f%d.png" % DonQuixote_i)
    DonQuixote_i += 1
if not DonQuixote_idle_files:
    DonQuixote_idle_files = ["idle.png"]

DonQuixote_idle_names = ["idle"] + ["idle%d" % i for i in range(1, len(DonQuixote_idle_files))]

# per-frame timing written by make_art.py (defaults to the authored 50 ms)
DonQuixote_IDLE_FRAME_TIME = 0.05
DonQuixote_idle_times = []
try:
    with open(os.path.join(ART, "idle_meta.json"), encoding="utf-8") as fh:
        _meta = json.load(fh)
    _ms = _meta.get("duration_ms") or []
    for i in range(len(DonQuixote_idle_names)):
        _d = _ms[i] if i < len(_ms) else 50
        DonQuixote_idle_times.append(round((_d or 50) / 1000.0, 4))
except (OSError, ValueError):
    DonQuixote_idle_times = [DonQuixote_IDLE_FRAME_TIME] * len(DonQuixote_idle_names)
DonQuixote_idle_start = []
_t = 0.0
for _d in DonQuixote_idle_times:
    DonQuixote_idle_start.append(round(_t, 4))
    _t += _d
DonQuixote_idle_total = round(_t, 4)

POSES = list(zip(DonQuixote_idle_names, DonQuixote_idle_files)) + [
    ("attack0", "attack_f0.png"),
    ("attack1", "attack_f1.png"),
    ("attack2", "attack_f2.png"),
    ("attack3", "attack_f3.png"),
    ("hit", "hit.png"),
    ("defend", "defend.png"),
    ("dead", "dead.png"),
    ("cast", "cast.png"),
    # 「骑士的长枪 / 饱食鲜血的长枪」的举枪突刺姿势（资源\人物\长枪.png 裁掉透明边）。
    # 这张图里角色在左边、枪身向右伸出很远，所以对齐用下面的 char_region 覆盖，
    # 只按角色本人的包围盒算锚点和缩放，枪身自然向右（敌人方向）伸展。
    ("lance", "lance_pose.png"),
]

# ---- 重击立绘：举手蓄力 -> 挥手投出 ------------------------------------
# 两帧由 tools/make_heavy_art.py 从 资源/人物/战斗立绘-重击.gif 水平翻转生成
# （原画朝左，游戏里敌人在右侧）。画布同为 425x613、脚底都在 y=598。
# 逐帧自动 bbox 会被「举手」和「伸手」拉偏锚点，所以对齐用下面的固定包围盒。
HEAVY_FRAMES = ["heavy0", "heavy1"]
HEAVY_RAISE = 0.75         # 0.00-0.75 举手蓄力（拉长，给瞄准留时间），0.75 挥手投出
HEAVY_END = 2.0            # 投出后保持姿势到 2.0s，C# 侧在 2.04s 清轨
POSES += [(name, "heavy/%s.png" % name) for name in HEAVY_FRAMES]

DonQuixote_images = []
for name, filename in POSES:
    img = Image.open(os.path.join(ART, filename)).convert("RGBA")
    DonQuixote_images.append((name, img))

# ---- shelf packing -------------------------------------------------------
PAGE_W = 2048
ordered = sorted(DonQuixote_images, key=lambda item: -item[1].height)
DonQuixote_rects = {}
x = 0
y = 0
row_h = 0
for name, img in ordered:
    if x + img.width > PAGE_W:
        x = 0
        y += row_h
        row_h = 0
    DonQuixote_rects[name] = (x, y, img.width, img.height)
    x += img.width
    row_h = max(row_h, img.height)
page_h = y + row_h

page = Image.new("RGBA", (PAGE_W, page_h), (0, 0, 0, 0))
for name, img in DonQuixote_images:
    px, py, _, _ = DonQuixote_rects[name]
    # Raw copy, no mask: `paste(img, box, img)` re-multiplies the source alpha
    # against the destination and `alpha_composite` rounds by one on very faint
    # edges, so neither reproduced the source art byte for byte.  The packed
    # rectangles never overlap, so a straight 4-channel copy is exact.
    page.paste(img, (px, py))
page.save(os.path.join(OUT, PAGE))
print("page %s %s" % (os.path.join(OUT, PAGE), page.size))

# ---- atlas text ----------------------------------------------------------
DonQuixote_lines = [
    PAGE,
    "size: %d,%d" % (PAGE_W, page_h),
    "format: RGBA8888",
    "filter: Linear,Linear",
    "repeat: none",
]
for name, img in DonQuixote_images:
    px, py, w, h = DonQuixote_rects[name]
    DonQuixote_lines += [
        name,
        "  rotate: false",
        "  xy: %d, %d" % (px, py),  # this runtime reads top-left origins (verified against the watcher atlas)
        "  size: %d, %d" % (w, h),
        "  orig: %d, %d" % (w, h),
        "  offset: 0, 0",
        "  index: -1",
    ]
DonQuixote_atlas_text = "\n".join(DonQuixote_lines) + "\n"

with open(os.path.join(OUT, "don_quixote.atlas"), "w", encoding="utf-8", newline="\n") as fh:
    fh.write(DonQuixote_atlas_text)

DonQuixote_spatlas = {
    "atlas_data": DonQuixote_atlas_text,
    "normal_texture_prefix": "",
    "source_path": "res://DonQuixote/animations/don_quixote.atlas",
    "specular_texture_prefix": "",
}
with open(os.path.join(OUT, "don_quixote.spatlas"), "w", encoding="utf-8", newline="\n") as fh:
    json.dump(DonQuixote_spatlas, fh, ensure_ascii=False)

# ---- skeleton json -------------------------------------------------------
# Every pose is anchored by the bottom centre of its opaque pixels so the
# character keeps standing on the same spot, and each pose is scaled to a
# common character height so the hand-drawn frames do not pop in size.
DonQuixote_ALPHA_THRESH = 16

# Per-pose character region overrides (minx, miny, maxx, maxy) in source image
# coordinates.  Used when the frame contains large non-character effects that
# would skew an automatic bbox: the defend art has a huge red slash sweeping
# across the whole canvas, so anchoring/scaling on the full bbox made the
# character render at roughly half size and float above the ground.
# Values below were measured off a coordinate-grid overlay of the art.
DonQuixote_char_region = {
    "defend": (0, 265, 335, 795),
    # 长枪姿势：整幅 1357x494 里只有 113..519 这一段是角色本人（再往右全是枪身），
    # 用自动 bbox 会把锚点挪到枪的中心、把角色缩到一半。
    "lance": (113, 0, 519, 493),
    # 重击两帧共用同一个「身体」包围盒：中轴 x=168、发顶 y=21、脚底 y=598。
    # 不这么做的话，举手帧的指尖和投出帧的伸手会把锚点分别拉到两侧，
    # 换帧时角色会左右横跳。
    "heavy0": (167, 21, 169, 598),
    "heavy1": (167, 21, 169, 598),
}


def pose_bbox(name, img):
    """Opaque bbox, shrunk to the character body when an override exists."""
    alpha = img.getchannel("A").point(lambda a: 255 if a > DonQuixote_ALPHA_THRESH else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        bbox = (0, 0, img.width, img.height)
    override = DonQuixote_char_region.get(name)
    if override:
        bbox = override
    return bbox


DonQuixote_bbox = {}
for name, img in DonQuixote_images:
    DonQuixote_bbox[name] = pose_bbox(name, img)

# every idle frame shares one bbox so the anchor/scale never jitters between
# frames of the breathing loop
DonQuixote_idle_bbox = (
    min(DonQuixote_bbox[n][0] for n in DonQuixote_idle_names),
    min(DonQuixote_bbox[n][1] for n in DonQuixote_idle_names),
    max(DonQuixote_bbox[n][2] for n in DonQuixote_idle_names),
    max(DonQuixote_bbox[n][3] for n in DonQuixote_idle_names),
)
for name in DonQuixote_idle_names:
    DonQuixote_bbox[name] = DonQuixote_idle_bbox

DonQuixote_ref_h = DonQuixote_bbox["idle"][3] - DonQuixote_bbox["idle"][1]
print("reference character height", DonQuixote_ref_h)


def pose_transform(name, img):
    bbox = DonQuixote_bbox[name]
    bh = max(1, bbox[3] - bbox[1])
    scale = DonQuixote_ref_h / float(bh)
    cx = (bbox[0] + bbox[2]) / 2.0
    by = float(bbox[3])
    x = scale * (img.width / 2.0 - cx)
    y = scale * (by - img.height / 2.0)
    return scale, x, y


# The attack frames are used exactly as authored, including their facing
# direction: no mirroring, no flipping.  Empty set = no mirrored poses.
DonQuixote_mirrored_poses = set()

DonQuixote_skin_attachments = {}

for name, img in DonQuixote_images:
    w, h = img.width, img.height
    scale, x, y = pose_transform(name, img)
    scale_x = scale
    if name in DonQuixote_mirrored_poses:
        scale_x = -scale
        x = -x
    DonQuixote_skin_attachments[name] = {
        "type": "region",
        "path": name,
        "x": round(x, 3),
        "y": round(y, 3),
        "scaleX": round(scale_x, 4),
        "scaleY": round(scale, 4),
        "rotation": 0,
        "width": w,
        "height": h,
    }
    print("pose %-8s scale=%.3f x=%.1f y=%.1f%s" % (name, scale, x, y, " mirrored" if scale_x < 0 else ""))


def attachment_timeline(name, time):
    return {"time": time, "name": name}


# 「骑士的长枪 / 饱食鲜血的长枪」举枪姿势的时间轴（秒）。
# C# 侧 DonQuixoteLancePose 用同一组数值决定什么时候清 track。
DonQuixote_lance_hold = 0.62
DonQuixote_lance_end = 0.86


def make_attack():
    return {
        "slots": {
            "body_slot": {
                "attachment": [
                    attachment_timeline("attack0", 0.0),
                    attachment_timeline("attack1", 0.2),
                    attachment_timeline("attack2", 0.36),
                    attachment_timeline("attack3", 0.52),
                    attachment_timeline("idle", 1.0),
                ]
            }
        },
        "bones": {
            "body": {
                "translate": [
                    {"time": 0.0, "x": 0, "y": 0},
                    {"time": 0.36, "x": 46, "y": 10},
                    {"time": 0.7, "x": 14, "y": 0},
                    {"time": 1.0, "x": 0, "y": 0},
                ],
                "rotate": [
                    {"time": 0.0, "angle": 0},
                    {"time": 0.36, "angle": -7},
                    {"time": 1.0, "angle": 0},
                ],
            }
        },
    }


def make_hit():
    return {
        "slots": {"body_slot": {"attachment": [attachment_timeline("hit", 0.0), attachment_timeline("idle", 0.35)]}},
        "bones": {
            "body": {
                "translate": [
                    {"time": 0.0, "x": 0, "y": 0},
                    {"time": 0.08, "x": -26, "y": 0},
                    {"time": 0.35, "x": 0, "y": 0},
                ],
                "rotate": [
                    {"time": 0.0, "angle": 0},
                    {"time": 0.1, "angle": 9},
                    {"time": 0.35, "angle": 0},
                ],
            }
        },
    }


def make_defend():
    return {
        "slots": {
            "body_slot": {"attachment": [attachment_timeline("defend", 0.0), attachment_timeline("idle", 0.35)]}
        },
        "bones": {"body": {"translate": [{"time": 0.0, "x": 0, "y": -6}, {"time": 0.35, "x": 0, "y": 0}]}},
    }


def make_cast():
    return {
        "slots": {"body_slot": {"attachment": [attachment_timeline("cast", 0.0), attachment_timeline("idle", 0.45)]}},
        "bones": {
            "body": {
                "translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.2, "x": 0, "y": 14}, {"time": 0.45, "x": 0, "y": 0}],
            }
        },
    }


def make_lance():
    """举枪突刺：换成 lance 姿势帧，略向前压一点，再收回站立帧。

    这段作用在 track 1 上（见 DonQuixoteLancePose.cs），所以出牌时游戏自己
    触发的 Attack 状态（track 0）不会把它顶掉；播完由代码清掉 track 1。
    """
    return {
        "slots": {
            "body_slot": {
                "attachment": [
                    attachment_timeline("lance", 0.0),
                    attachment_timeline("idle", DonQuixote_lance_end),
                ]
            }
        },
        "bones": {
            "body": {
                "translate": [
                    {"time": 0.0, "x": 0, "y": 0},
                    {"time": 0.12, "x": 24, "y": 4},
                    {"time": DonQuixote_lance_hold, "x": 26, "y": 4},
                    {"time": DonQuixote_lance_end, "x": 0, "y": 0},
                ],
                "rotate": [
                    {"time": 0.0, "angle": 0},
                    {"time": 0.12, "angle": -3},
                    {"time": DonQuixote_lance_hold, "angle": -3},
                    {"time": DonQuixote_lance_end, "angle": 0},
                ],
            }
        },
    }


def make_heavy():
    """重击：举手蓄力 0.75s -> 挥手投出，之后一直保持投出姿势到 2.0s。

    这段走 track 1（见 DonQuixoteHeavyHitSlash.cs），播完由代码清轨。
    投掷物（血马长枪）是 C# 侧单独的节点，不在这条时间轴里。
    举手和保持都刻意留长：投掷物的「旋转瞄准 → 停顿 → 速射」都落在这一段里，
    动作太快会看不清瞄准过程。
    """
    return {
        "slots": {
            "body_slot": {
                "attachment": [
                    attachment_timeline("heavy0", 0.0),
                    attachment_timeline("heavy1", HEAVY_RAISE),
                    # 尾部重复一次同名 key：把时长撑到 HEAVY_END，
                    # 否则非循环动画会在挥手那一刻就结束。
                    attachment_timeline("heavy1", HEAVY_END),
                ]
            }
        },
        "bones": {
            "body": {
                # 投出瞬间整体前压一点，收尾回正（位移很小，只是让动作不僵）
                "translate": [
                    {"time": 0.0, "x": 0, "y": 0},
                    {"time": HEAVY_RAISE, "x": 10, "y": 2},
                    {"time": HEAVY_END, "x": 10, "y": 2},
                ],
            }
        },
    }


def make_dead():
    return {
        "slots": {"body_slot": {"attachment": [attachment_timeline("dead", 0.0)]}},
        "bones": {
            "body": {
                "rotate": [{"time": 0.0, "angle": 0}, {"time": 0.6, "angle": -78}],
                "translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.6, "x": -20, "y": -10}],
            }
        },
    }


def make_idle():
    """Breathing loop: swap one attachment per source gif frame.

    The idle art is a frame-by-frame animation (no tweens between the frames),
    so the slot timeline carries a key for every frame at the authored 50 ms
    cadence and the whole clip loops seamlessly.
    """
    timeline = [
        attachment_timeline(name, DonQuixote_idle_start[i])
        for i, name in enumerate(DonQuixote_idle_names)
    ]
    total = DonQuixote_idle_total
    return {
        "slots": {"body_slot": {"attachment": timeline}},
        "bones": {
            "body": {
                "translate": [
                    {"time": 0.0, "x": 0, "y": 0},
                    {"time": round(total * 0.5, 4), "x": 0, "y": 4},
                    {"time": round(total, 4), "x": 0, "y": 0},
                ]
            }
        },
    }


DonQuixote_animations = {}
for name in ("Idle", "idle", "idle_loop", "Relaxed", "relaxed", "relaxed_loop"):
    DonQuixote_animations[name] = make_idle()
for name in ("Attack", "attack"):
    DonQuixote_animations[name] = make_attack()
for name in ("Hit", "hit", "hurt"):
    DonQuixote_animations[name] = make_hit()
for name in ("Cast", "cast"):
    DonQuixote_animations[name] = make_cast()
for name in ("Dead", "dead", "die"):
    DonQuixote_animations[name] = make_dead()
for name in ("Revive", "revive"):
    DonQuixote_animations[name] = make_idle()
for name in ("Defend", "defend"):
    DonQuixote_animations[name] = make_defend()
for name in ("LancePose", "lance_pose"):
    DonQuixote_animations[name] = make_lance()
for name in ("HeavyHit", "heavy_hit"):
    DonQuixote_animations[name] = make_heavy()

DonQuixote_skeleton = {
    "skeleton": {
        "hash": "don_quixote",
        "spine": "4.2.43",
        "x": 0,
        "y": 0,
        "width": 512,
        "height": 600,
        "images": "./",
        "audio": "",
    },
    "bones": [
        {"name": "root"},
        {"name": "body", "parent": "root", "y": 0},
    ],
    "slots": [{"name": "body_slot", "bone": "body", "attachment": "idle"}],
    "skins": [
        {
            "name": "default",
            "attachments": {"body_slot": DonQuixote_skin_attachments},
        }
    ],
    "animations": DonQuixote_animations,
}

DonQuixote_skeleton_text = json.dumps(DonQuixote_skeleton, ensure_ascii=False)
for name in ("don_quixote.spjson", "don_quixote.json"):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(DonQuixote_skeleton_text)
print("skeleton bytes", len(DonQuixote_skeleton_text))

# ---- godot resource binding the atlas + skeleton -------------------------
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
from build_pck import uid_for  # noqa: E402

DonQuixote_atlas_res = "DonQuixote/animations/don_quixote.spatlas"
DonQuixote_skel_res = "DonQuixote/animations/don_quixote.spjson"
DonQuixote_tres_res = "DonQuixote/animations/don_quixote_skel_data.tres"

DonQuixote_tres = """[gd_resource type="SpineSkeletonDataResource" load_steps=3 format=3 uid="%s"]

[ext_resource type="SpineAtlasResource" uid="%s" path="res://%s" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" uid="%s" path="res://%s" id="2_skel"]

[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skel")
""" % (
    uid_for(DonQuixote_tres_res),
    uid_for(DonQuixote_atlas_res),
    DonQuixote_atlas_res,
    uid_for(DonQuixote_skel_res),
    DonQuixote_skel_res,
)
with open(os.path.join(OUT, "don_quixote_skel_data.tres"), "w", encoding="utf-8", newline="\n") as fh:
    fh.write(DonQuixote_tres)
print("wrote don_quixote_skel_data.tres")
