"""把 资源\\人物\\骨骼部件 组装成真正的多骨骼 spine 骨架（don_quixote_rig）。

输入：`资源\\人物\\骨骼部件\\rig.json`（canvas / parts，pivot_master 为主画布坐标，y 向下）
输出：`assets\\animations\\rig\\don_quixote_rig.spatlas` / `.atlas` / `.png` / `.spjson` / `.json`

坐标换算：画布 y 向下，spine y 向上 => spine_y = canvas_h - master_y。
每根骨骼的原点 = 该部件的 pivot_master（关节），附件偏移 = 部件中心 - 关节。
"""

import io
import json
import math
import os

from PIL import Image

SRC = r"D:\sts2mod\资源\人物\骨骼部件"
OUT = r"D:\sts2mod\Don Quixote\assets\animations\rig"
PAGE_W = 2048

# 骨骼层级：名字 -> (部件, 父骨骼)
BONES = [
    ("root", None, None),
    ("body", "torso", "root"),
    ("coat_back", "coat_back", "body"),
    ("leg_r", "leg_r", "body"),
    ("leg_l", "leg_l", "body"),
    ("head", "head", "body"),
    ("arm_r_1", "arm_r_1", "body"),
    ("arm_r_2", "arm_r_2", "arm_r_1"),
    ("hand_r", "hand_r", "arm_r_2"),
    ("weapon", "weapon", "hand_r"),
    ("arm_l_1", "arm_l_1", "body"),
    ("arm_l_2", "arm_l_2", "arm_l_1"),
]


def load_rig():
    rig = json.load(io.open(os.path.join(SRC, "rig.json"), encoding="utf-8-sig"))
    canvas = rig["canvas"]
    parts = {p["name"]: p for p in rig["parts"]}
    return canvas, parts


def pack_atlas(parts):
    """按 z 顺序（大图先放）做简单的货架式排版。"""
    order = sorted(parts.values(), key=lambda p: (-p["w"] * p["h"], p["name"]))
    x = y = 0
    shelf = 0
    coords = {}
    for p in order:
        if x + p["w"] > PAGE_W:
            x = 0
            y += shelf
            shelf = 0
        coords[p["name"]] = (x, y)
        x += p["w"]
        shelf = max(shelf, p["h"])
    height = y + shelf
    page = Image.new("RGBA", (PAGE_W, height), (0, 0, 0, 0))
    for name, (px, py) in coords.items():
        img = Image.open(os.path.join(SRC, parts[name]["file"])).convert("RGBA")
        page.alpha_composite(img, (px, py))
    return page, coords


def write_atlas(page, coords, parts):
    text = ["don_quixote_rig.png", "size: %d,%d" % page.size, "format: RGBA8888",
            "filter: Linear,Linear", "repeat: none"]
    for name, (px, py) in sorted(coords.items()):
        p = parts[name]
        text += [name, "  rotate: false", "  xy: %d, %d" % (px, py),
                 "  size: %d, %d" % (p["w"], p["h"]),
                 "  orig: %d, %d" % (p["w"], p["h"]),
                 "  offset: 0, 0", "  index: -1"]
    body = "\n".join(text) + "\n"
    os.makedirs(OUT, exist_ok=True)
    io.open(os.path.join(OUT, "don_quixote_rig.atlas"), "w", encoding="utf-8", newline="").write(body)
    io.open(os.path.join(OUT, "don_quixote_rig.spatlas"), "w", encoding="utf-8", newline="").write(
        json.dumps({"atlas_data": body}, ensure_ascii=False))
    page.save(os.path.join(OUT, "don_quixote_rig.png"))
    return body


def build_skeleton(canvas, parts):
    cw, ch = canvas
    bones = []
    world = {}

    for name, part, parent in BONES:
        if part is None:
            pos = (0.0, 0.0)
        else:
            pm = parts[part]["pivot_master"]
            pos = (float(pm[0]), float(ch - pm[1]))
        world[name] = pos
        entry = {"name": name}
        if parent:
            px, py = world[parent]
            entry["parent"] = parent
            entry["x"] = round(pos[0] - px, 3)
            entry["y"] = round(pos[1] - py, 3)
        else:
            entry["x"] = 0.0
            entry["y"] = 0.0
        bones.append(entry)

    slots = []
    attachments = {}
    # 手收在袖子里、长枪平时不显示：这两个槽位默认空着，靠动画切换
    HIDDEN_SLOTS = {"hand_r_slot", "weapon_slot"}
    draw = sorted(parts.values(), key=lambda p: p["z"])
    for p in draw:
        bone = p["name"]
        if bone not in world:
            bone = "body"
        slot_name = p["name"] + "_slot"
        if p["name"] == "hand_r":
            # 手收在袖子里：整个槽位不给附件
            slots.append({"name": slot_name, "bone": bone})
        elif p["name"] == "weapon":
            # 长枪常驻附件，但默认全透明；lance_pose 把它淡入
            slots.append({"name": slot_name, "bone": bone, "attachment": p["name"],
                          "color": "ffffff00"})
        else:
            slots.append({"name": slot_name, "bone": bone, "attachment": p["name"]})
        bx, by = world[bone]
        cx = p["x"] + p["w"] / 2.0
        cy = p["y"] + p["h"] / 2.0
        attachments[p["name"] + "_slot"] = {
            p["name"]: {
                "type": "region", "path": p["name"],
                "x": round(cx - bx, 3), "y": round(ch - cy - by, 3),
                "scaleX": 1.0, "scaleY": 1.0, "rotation": 0.0,
                "width": p["w"], "height": p["h"],
            }
        }

    return {
        "skeleton": {"hash": "don_quixote_rig", "spine": "4.2.43", "x": -cw / 2, "y": 0,
                     "width": cw, "height": ch},
        "bones": bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": build_animations(),
    }


def rot(bone, keys):
    return {bone: {"rotate": [{"time": t, "angle": a} for t, a in keys]}}


def move(bone, keys):
    return {bone: {"translate": [{"time": t, "x": x, "y": y} for t, x, y in keys]}}


def build_animations():
    anims = {}

    # 待机：躯干轻微起伏 + 头与手臂跟着摆
    anims["idle_loop"] = {
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.6, "x": 0, "y": -4},
                                   {"time": 1.2, "x": 0, "y": 0}]},
            "head": rot("head", [(0.0, 0), (0.6, 2.0), (1.2, 0)]),
            "arm_r_1": rot("arm_r_1", [(0.0, 0), (0.6, 2.5), (1.2, 0)]),
            "arm_l_1": rot("arm_l_1", [(0.0, 0), (0.6, -2.5), (1.2, 0)]),
        }
    }
    anims["Idle"] = json.loads(json.dumps(anims["idle_loop"]))

    # 攻击：抬枪 -> 前刺 -> 收
    anims["attack"] = {
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.28, "x": -22, "y": -6},
                                   {"time": 0.5, "x": 34, "y": -2}, {"time": 0.9, "x": 0, "y": 0}]},
            "arm_r_1": rot("arm_r_1", [(0.0, 0), (0.28, -38), (0.5, 12), (0.9, 0)]),
            "arm_r_2": rot("arm_r_2", [(0.0, 0), (0.28, -26), (0.5, 8), (0.9, 0)]),
            "weapon": rot("weapon", [(0.0, 0), (0.28, -18), (0.5, 4), (0.9, 0)]),
            "head": rot("head", [(0.0, 0), (0.28, -4), (0.5, 6), (0.9, 0)]),
        }
    }
    anims["Attack"] = anims["attack"]

    # 受击
    anims["hit"] = {
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.1, "x": -14, "y": 0},
                                   {"time": 0.3, "x": 0, "y": 0}]},
            "head": rot("head", [(0.0, 0), (0.1, -8), (0.3, 0)]),
        }
    }
    anims["hit_alias"] = anims["hit"]
    del anims["hit_alias"]
    anims["Hit"] = anims["hit"]
    anims["hurt"] = anims["hit"]

    # 防御：后仰 + 双臂收拢
    anims["defend"] = {
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.25, "x": -10, "y": 2},
                                   {"time": 0.7, "x": 0, "y": 0}]},
            "arm_r_1": rot("arm_r_1", [(0.0, 0), (0.25, 46), (0.7, 0)]),
            "arm_l_1": rot("arm_l_1", [(0.0, 0), (0.25, -46), (0.7, 0)]),
            "head": rot("head", [(0.0, 0), (0.25, -6), (0.7, 0)]),
        }
    }
    anims["Defend"] = anims["defend"]

    # 施法/蓄力
    anims["cast"] = {
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.4, "x": 0, "y": 8},
                                   {"time": 0.8, "x": 0, "y": 0}]},
            "arm_l_1": rot("arm_l_1", [(0.0, 0), (0.4, -52), (0.8, 0)]),
            "arm_r_1": rot("arm_r_1", [(0.0, 0), (0.4, 30), (0.8, 0)]),
            "head": rot("head", [(0.0, 0), (0.4, 8), (0.8, 0)]),
        }
    }
    anims["Cast"] = anims["cast"]

    # 死亡：向后倒 + 下沉
    anims["die"] = {
        "bones": {
            "body": {"rotate": [{"time": 0.0, "angle": 0}, {"time": 0.9, "angle": -78}],
                     "translate": [{"time": 0.0, "x": 0, "y": 0}, {"time": 0.9, "x": -10, "y": -26}]},
            "head": rot("head", [(0.0, 0), (0.9, 22)]),
            "arm_r_1": rot("arm_r_1", [(0.0, 0), (0.9, 34)]),
            "arm_l_1": rot("arm_l_1", [(0.0, 0), (0.9, -30)]),
        }
    }
    anims["Dead"] = anims["die"]
    anims["dead"] = anims["die"]

    # 商店/火堆用 relaxed_loop（轻微左右晃）
    anims["relaxed_loop"] = {
        "bones": {
            "body": {"rotate": [{"time": 0.0, "angle": -1.5}, {"time": 1.0, "angle": 1.5},
                                {"time": 2.0, "angle": -1.5}]},
            "head": rot("head", [(0.0, 1.5), (1.0, -1.5), (2.0, 1.5)]),
        }
    }
    anims["Relaxed"] = anims["relaxed_loop"]
    anims["Revive"] = anims["idle_loop"]
    # 长枪只在使用长枪卡时出现：单独一条 lance_pose，武器槽从这里开始才有附件
    anims["lance_pose"] = {
        "slots": {"weapon_slot": {"color": [{"time": 0.0, "color": "ffffffff"}]}},
        "bones": {
            "body": {"translate": [{"time": 0.0, "x": -6, "y": -2}]},
            "arm_r_1": rot("arm_r_1", [(0.0, -34)]),
            "arm_r_2": rot("arm_r_2", [(0.0, -18)]),
            "weapon": rot("weapon", [(0.0, -38)]),
            "head": rot("head", [(0.0, -3)]),
        },
    }
    anims["LancePose"] = anims["lance_pose"]
    anims["HeavyHit"] = anims["hit"]

    # 除 lance_pose 外，所有动画一开场就把长枪淡出（避免上一条留下的枪一直挂着）
    for name, anim in anims.items():
        if name in ("lance_pose", "LancePose"):
            continue
        anim.setdefault("slots", {})
        anim["slots"]["weapon_slot"] = {"color": [{"time": 0.0, "color": "ffffff00"}]}
    return anims


def main():
    canvas, parts = load_rig()
    page, coords = pack_atlas(parts)
    write_atlas(page, coords, parts)
    skel = build_skeleton(canvas, parts)
    for name in ("don_quixote_rig.spjson", "don_quixote_rig.json"):
        io.open(os.path.join(OUT, name), "w", encoding="utf-8", newline="").write(
            json.dumps(skel, ensure_ascii=False, indent=1))
    print("rig built:", os.path.join(OUT, "don_quixote_rig.spjson"))
    print("bones", len(skel["bones"]), "slots", len(skel["slots"]),
          "anims", len(skel["animations"]), "page", page.size)


if __name__ == "__main__":
    main()
