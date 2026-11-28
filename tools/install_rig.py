"""把 rig 产物装成游戏用的 don_quixote.*（覆盖前先备份旧的逐帧骨架），
并把「血宴时刻」的 16 帧作为第二图集页 + feast_moment 动画合并进同一份骨架。
"""

import io
import json
import os
import shutil

from PIL import Image

ANIM = r"D:\sts2mod\Don Quixote\assets\animations"
RIG = os.path.join(ANIM, "rig")
BACKUP = os.path.join(ANIM, "frames_backup")
PAGE1 = "don_quixote.png"
PAGE2 = "don_quixote2.png"
FEAST_SRC = r"D:\sts2mod\资源\效果\血宴时刻-帧"


def ensure_backup():
    if os.path.isdir(BACKUP) and os.listdir(BACKUP):
        return
    os.makedirs(BACKUP, exist_ok=True)
    for name in ("don_quixote.spjson", "don_quixote.json", "don_quixote.spatlas",
                 "don_quixote.atlas", "don_quixote.png"):
        src = os.path.join(ANIM, name)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(BACKUP, name))
    print("backup ->", BACKUP)


def load_rig():
    skel = json.load(io.open(os.path.join(RIG, "don_quixote_rig.spjson"), encoding="utf-8"))
    page = Image.open(os.path.join(RIG, "don_quixote_rig.png")).convert("RGBA")
    atlas = json.load(io.open(os.path.join(RIG, "don_quixote_rig.spatlas"), encoding="utf-8"))["atlas_data"]
    return skel, page, atlas


def add_feast(skel, atlas):
    """追加第二页 + feast0..15 附件 + feast_moment 动画。"""
    frames = sorted(f for f in os.listdir(FEAST_SRC) if f.endswith(".png")) if os.path.isdir(FEAST_SRC) else []
    if not frames:
        print("no feast frames, skip")
        return atlas, None

    imgs = [Image.open(os.path.join(FEAST_SRC, f)).convert("RGBA") for f in frames]
    w = max(i.size[0] for i in imgs)
    h = max(i.size[1] for i in imgs)
    cols = 6
    rows = (len(imgs) + cols - 1) // cols
    page2 = Image.new("RGBA", (w * cols, h * rows), (0, 0, 0, 0))
    coords = []
    for i, img in enumerate(imgs):
        cx = (i % cols) * w
        cy = (i // cols) * h
        page2.alpha_composite(img, (cx, cy))
        coords.append((cx, cy, img.size[0], img.size[1]))

    atlas = atlas.replace("don_quixote_rig.png", PAGE1, 1)
    atlas += "%s\nsize: %d,%d\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n" % (PAGE2, page2.size[0], page2.size[1])
    for i, (x, y, iw, ih) in enumerate(coords):
        atlas += ("feast%d\n  rotate: false\n  xy: %d, %d\n  size: %d, %d\n  orig: %d, %d\n  offset: 0, 0\n  index: -1\n"
                  % (i, x, y, iw, ih, iw, ih))

    # 槽位：贴在 body 上（原始逐帧骨架也是挂在 body_slot）
    skel["slots"].append({"name": "feast_slot", "bone": "body", "attachment": None})
    att = {}
    for i, (x, y, iw, ih) in enumerate(coords):
        att["feast%d" % i] = {"type": "region", "path": "feast%d" % i,
                              "x": 0, "y": 276, "scaleX": 1.0, "scaleY": 1.0,
                              "rotation": 0.0, "width": iw, "height": ih}
    skel["skins"][0]["attachments"]["feast_slot"] = att

    keys = [{"time": round(i * 0.09, 3), "name": "feast%d" % i} for i in range(len(coords))]
    keys.append({"time": round(len(coords) * 0.09, 3)})
    skel["animations"]["feast_moment"] = {"slots": {"feast_slot": {"attachment": keys}}}
    print("feast merged:", len(coords), "frames, page2", page2.size)
    return atlas, page2


def main():
    ensure_backup()
    skel, page1, atlas = load_rig()
    atlas, page2 = add_feast(skel, atlas)

    io.open(os.path.join(ANIM, "don_quixote.spjson"), "w", encoding="utf-8", newline="").write(
        json.dumps(skel, ensure_ascii=False, indent=1))
    io.open(os.path.join(ANIM, "don_quixote.json"), "w", encoding="utf-8", newline="").write(
        json.dumps(skel, ensure_ascii=False, indent=1))
    body = atlas
    io.open(os.path.join(ANIM, "don_quixote.atlas"), "w", encoding="utf-8", newline="").write(body)
    io.open(os.path.join(ANIM, "don_quixote.spatlas"), "w", encoding="utf-8", newline="").write(
        json.dumps({"atlas_data": body}, ensure_ascii=False))
    page1.save(os.path.join(ANIM, PAGE1))
    if page2 is not None:
        page2.save(os.path.join(ANIM, PAGE2))
    print("installed", os.path.join(ANIM, "don_quixote.spjson"))
    print("anims", len(skel["animations"]), "slots", len(skel["slots"]))


if __name__ == "__main__":
    main()
