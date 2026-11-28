"""用骨骼做「血宴时刻」：生成 3 张纯程序化的血光素材，追加到现有骨架里，
再用旋转/缩放/透明度关键帧演一遍（不再逐帧换图）。"""

import io
import json
import math
import os

from PIL import Image, ImageDraw

ANIM = r"D:\sts2mod\Don Quixote\assets\animations"
SKEL = os.path.join(ANIM, "don_quixote.spjson")
ATLAS = os.path.join(ANIM, "don_quixote.spatlas")
PAGE = "don_quixote3.png"
SIZE = 512
SS = 4
RED = (196, 18, 24)
DEEP = (110, 6, 10)


def _canvas():
    img = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def make_ring():
    img, d = _canvas()
    c = SIZE * SS / 2
    for i, (r, w, a) in enumerate([(215, 26, 70), (196, 18, 150), (176, 10, 255)]):
        rr = r * SS / 256 * (SIZE / 256) * 0.5 + SIZE * SS * 0.5 * 0.0
        rad = (r / 256.0) * SIZE * SS * 0.5
        d.ellipse([c - rad, c - rad, c + rad, c + rad],
                  outline=(RED[0], RED[1], RED[2], a), width=int(w * SS * 1.4))
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def make_swirl():
    img, d = _canvas()
    c = SIZE * SS / 2
    for k in range(3):
        r = SIZE * SS * (0.30 + 0.07 * k)
        box = [c - r, c - r, c + r, c + r]
        d.arc(box, start=200 + k * 40, end=470 + k * 40,
              fill=(RED[0], RED[1], RED[2], 200 - k * 50), width=int(10 * SS))
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def make_splash():
    img, d = _canvas()
    c = SIZE * SS / 2
    spikes = 12
    for i in range(spikes):
        ang = 2 * math.pi * i / spikes
        r0, r1 = SIZE * SS * 0.10, SIZE * SS * (0.20 + 0.05 * (i % 3))
        w = int(14 * SS * (0.6 + 0.4 * (i % 2)))
        d.line([c + math.cos(ang) * r0, c + math.sin(ang) * r0,
                c + math.cos(ang) * r1, c + math.sin(ang) * r1],
               fill=(DEEP[0], DEEP[1], DEEP[2], 210), width=w)
    d.ellipse([c - SIZE * SS * 0.10, c - SIZE * SS * 0.10,
               c + SIZE * SS * 0.10, c + SIZE * SS * 0.10],
              fill=(RED[0], RED[1], RED[2], 235))
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def atlas_entries(regions):
    out = "%s\nsize: %d,%d\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n" % (
        PAGE, SIZE * len(regions), SIZE)
    for i, (name, _) in enumerate(regions):
        out += ("%s\n  rotate: false\n  xy: %d, 0\n  size: %d, %d\n  orig: %d, %d\n  offset: 0, 0\n  index: -1\n"
                % (name, i * SIZE, SIZE, SIZE, SIZE, SIZE))
    return out


def main():
    parts = [("fx_ring", make_ring()), ("fx_swirl", make_swirl()), ("fx_splash", make_splash())]

    page = Image.new("RGBA", (SIZE * len(parts), SIZE), (0, 0, 0, 0))
    for i, (_, img) in enumerate(parts):
        page.alpha_composite(img, (i * SIZE, 0))
    page.save(os.path.join(ANIM, PAGE))

    wrapper = json.load(io.open(ATLAS, encoding="utf-8"))
    body = wrapper["atlas_data"] + atlas_entries(parts)
    io.open(ATLAS, "w", encoding="utf-8", newline="").write(json.dumps({"atlas_data": body}, ensure_ascii=False))
    io.open(os.path.join(ANIM, "don_quixote.atlas"), "w", encoding="utf-8", newline="").write(body)

    skel = json.load(io.open(SKEL, encoding="utf-8"))
    existing = {b["name"] for b in skel["bones"]}
    for name, _ in parts:
        if name not in existing:
            skel["bones"].append({"name": name, "parent": "body", "y": 276})
    slots = {s["name"] for s in skel["slots"]}
    for name, _ in parts:
        slot = name + "_slot"
        if slot not in slots:
            skel["slots"].append({"name": slot, "bone": name, "attachment": name,
                                  "color": "ffffff00"})
        skel["skins"][0]["attachments"].setdefault(slot, {})[name] = {
            "type": "region", "path": name, "x": 0, "y": 0,
            "scaleX": 1.0, "scaleY": 1.0, "rotation": 0.0,
            "width": SIZE, "height": SIZE,
        }

    def fade(name, keys):
        return {name + "_slot": {"color": [{"time": t, "color": c} for t, c in keys]}}

    skel["animations"]["feast_bones"] = {
        "bones": {
            "fx_ring": {
                "rotate": [{"time": 0.0, "angle": 0}, {"time": 1.4, "angle": 220}],
                "scale": [{"time": 0.0, "x": 0.35, "y": 0.35},
                          {"time": 0.5, "x": 0.95, "y": 0.95},
                          {"time": 1.4, "x": 1.35, "y": 1.35}],
            },
            "fx_swirl": {
                "rotate": [{"time": 0.0, "angle": -60}, {"time": 1.4, "angle": 260}],
                "scale": [{"time": 0.0, "x": 0.4, "y": 0.4},
                          {"time": 1.4, "x": 1.25, "y": 1.25}],
            },
            "fx_splash": {
                "scale": [{"time": 0.0, "x": 0.15, "y": 0.15},
                          {"time": 0.35, "x": 0.75, "y": 0.75},
                          {"time": 1.4, "x": 1.05, "y": 1.05}],
            },
            "body": {"translate": [{"time": 0.0, "x": 0, "y": 0},
                                   {"time": 0.35, "x": 0, "y": 6},
                                   {"time": 1.4, "x": 0, "y": 0}]},
        },
        "slots": {
            "fx_ring_slot": {"color": [{"time": 0.0, "color": "ffffff00"},
                                       {"time": 0.25, "color": "ffffffff"},
                                       {"time": 1.1, "color": "ffffffb0"},
                                       {"time": 1.4, "color": "ffffff00"}]},
            "fx_swirl_slot": {"color": [{"time": 0.0, "color": "ffffff00"},
                                        {"time": 0.35, "color": "ffffffd0"},
                                        {"time": 1.4, "color": "ffffff00"}]},
            "fx_splash_slot": {"color": [{"time": 0.0, "color": "ffffff00"},
                                         {"time": 0.3, "color": "ffffffff"},
                                         {"time": 0.9, "color": "ffffff00"}]},
        },
    }

    io.open(SKEL, "w", encoding="utf-8", newline="").write(json.dumps(skel, ensure_ascii=False, indent=1))
    io.open(os.path.join(ANIM, "don_quixote.json"), "w", encoding="utf-8", newline="").write(
        json.dumps(skel, ensure_ascii=False, indent=1))
    print("feast bones added:", [p[0] for p in parts])


if __name__ == "__main__":
    main()
