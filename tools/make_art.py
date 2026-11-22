"""Derive the mod's texture assets from the source art in 资源\\人物.

Sizes mirror the vanilla assets so the character select button, the top panel
icon and the map marker line up with every existing character:
    char_select_icon.png   132x195  (vanilla char_select_ironclad.png)
    icon.png                85x85   (vanilla ui/top_panel/character_icon_ironclad.png)
    icon_outline.png        85x85   (vanilla ..._outline.png)
    map_marker.png          49x64   (vanilla map_marker_ironclad.png)
    char_select_bg.png    1920x1080 (vanilla char select background)
    death_screen.png      1920x1080 (game over screen background)
    cookie.png              128x128 (relic icon canvas)
"""
import os
from PIL import Image, ImageDraw, ImageFilter

SRC = r"D:\sts2mod\资源\人物"
SRC_ROOT = r"D:\sts2mod\资源"
ART = r"D:\sts2mod\Don Quixote\assets\art"
os.makedirs(ART, exist_ok=True)

SELECT_ICON_SIZE = (132, 195)
CHAR_ICON_SIZE = (85, 85)
MAP_MARKER_SIZE = (49, 64)
RELIC_ICON_SIZE = (128, 128)
SCREEN_SIZE = (1920, 1080)


def save(img, name):
    path = os.path.join(ART, name)
    img.save(path)
    print("%-22s %s" % (name, img.size))


def fit(img: Image.Image, size) -> Image.Image:
    """Scale to fit inside ``size`` without distortion, centred, transparent."""
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    scale = min(size[0] / img.width, size[1] / img.height)
    scaled = img.resize(
        (max(1, round(img.width * scale)), max(1, round(img.height * scale))),
        Image.LANCZOS,
    )
    canvas.paste(
        scaled,
        ((size[0] - scaled.width) // 2, (size[1] - scaled.height) // 2),
        scaled,
    )
    return canvas


def cover(img: Image.Image, size) -> Image.Image:
    """Scale to fill ``size`` then centre-crop the overflow (keeps framing)."""
    scale = max(size[0] / img.width, size[1] / img.height)
    scaled = img.resize(
        (max(1, round(img.width * scale)), max(1, round(img.height * scale))),
        Image.LANCZOS,
    )
    left = (scaled.width - size[0]) // 2
    top = (scaled.height - size[1]) // 2
    return scaled.crop((left, top, left + size[0], top + size[1]))


def outline_of(img: Image.Image, grow: int = 5) -> Image.Image:
    alpha = img.getchannel("A").filter(ImageFilter.MaxFilter(grow))
    out = Image.new("RGBA", img.size, (255, 255, 255, 0))
    out.putalpha(alpha)
    return out


select_icon = Image.open(os.path.join(SRC, "选人界面按钮图标.jpg")).convert("RGBA")
save(cover(select_icon, SELECT_ICON_SIZE), "char_select_icon.png")
save(cover(select_icon, MAP_MARKER_SIZE), "map_marker.png")

avatar = Image.open(os.path.join(SRC, "游戏左上角头像.png")).convert("RGBA")
char_icon = cover(avatar, CHAR_ICON_SIZE)
save(char_icon, "icon.png")
save(outline_of(char_icon), "icon_outline.png")

bg = Image.open(os.path.join(SRC, "选人界面背景.png")).convert("RGBA")
save(cover(bg, SCREEN_SIZE), "char_select_bg.png")

death_screen = Image.open(os.path.join(SRC, "死亡画面.png")).convert("RGBA")
save(cover(death_screen, SCREEN_SIZE), "death_screen.png")

cookie = Image.open(os.path.join(SRC, "美味饼干.png")).convert("RGBA")
cookie_icon = fit(cookie, RELIC_ICON_SIZE)
save(cookie_icon, "cookie.png")
save(outline_of(cookie_icon), "cookie_outline.png")

idle_src_path = os.path.join(SRC, "战斗立绘-闲置.gif")
if not os.path.exists(idle_src_path):
    idle_src_path = os.path.join(SRC, "战斗立绘-闲置.png")
hit = Image.open(os.path.join(SRC, "战斗立绘-受伤.png")).convert("RGBA")
guard = Image.open(os.path.join(SRC, "战斗立绘-防御.png")).convert("RGBA")

# Idle is a frame-by-frame breathing loop.  Every frame is kept (the motion
# covers the whole body, so dropping frames is visible), but the frames are
# packed at 50% resolution: 54 frames at the authored 591x1104 would need
# ~35M pixels, which does not fit a single texture, and the character only
# renders ~290 px tall in combat anyway.
DonQuixote_IDLE_SCALE = 0.5

idle_gif = Image.open(idle_src_path)
DonQuixote_idle_frames = []
DonQuixote_idle_durations = []
for i in range(getattr(idle_gif, "n_frames", 1)):
    idle_gif.seek(i)
    frame = idle_gif.convert("RGBA")
    frame = frame.resize(
        (max(1, round(frame.width * DonQuixote_IDLE_SCALE)), max(1, round(frame.height * DonQuixote_IDLE_SCALE))),
        Image.LANCZOS,
    )
    DonQuixote_idle_frames.append(frame)
    DonQuixote_idle_durations.append(idle_gif.info.get("duration", 50))
    save(frame, "idle_f%d.png" % i)

# idle.png stays the still used by the 2D fallback scene / cue set
save(DonQuixote_idle_frames[0].copy(), "idle.png")

# frame timing travels with the frames so make_spine.py can rebuild the exact
# breathing cadence instead of guessing it
import json as _json

with open(os.path.join(ART, "idle_meta.json"), "w", encoding="utf-8", newline="\n") as fh:
    _json.dump({"frames": len(DonQuixote_idle_frames), "duration_ms": DonQuixote_idle_durations}, fh)
print(
    "%-22s %d frames x %s ms"
    % ("idle_meta.json", len(DonQuixote_idle_frames), sorted(set(DonQuixote_idle_durations)))
)

# The remaining combat illustrations are shipped pixel-for-pixel as authored:
# no scaling, flipping, recolouring or outline work happens here.
for img, name in ((hit, "hit.png"), (guard, "defend.png")):
    path = os.path.join(ART, name)
    img.save(path)
    print("%-22s %s (verbatim copy)" % (name, img.size))

# combat animation frames: the attack gif is used exactly as authored
attack_src = os.path.join(SRC, "战斗立绘-攻击.gif")
attack_gif = Image.open(attack_src)
DonQuixote_attack_frames = []
DonQuixote_attack_durations = []
for i in range(getattr(attack_gif, "n_frames", 1)):
    attack_gif.seek(i)
    frame = attack_gif.convert("RGBA")
    DonQuixote_attack_frames.append(frame)
    DonQuixote_attack_durations.append(attack_gif.info.get("duration", 200))
    save(frame, "attack_f%d.png" % i)

# cast / dead reuse existing frames verbatim so the texture table in
# build_pck.py and the 2D fallback cue set keep their entries
save(Image.open(os.path.join(ART, "attack_f1.png")).convert("RGBA"), "cast.png")
save(hit.copy(), "dead.png")

# "正在凝固的血-显示效果"：一根竖直血刺。为了做出「被贯穿」的层次，把它从正中间
# 切成上下两半分别出图：下半截压到怪物背后（z_index = -1），上半截盖在怪物身前，
# 两半共用同一个中心点，运行时按 45° 旋转拼回去。像素本身不做任何缩放/调色。
clot_src = os.path.join(SRC_ROOT, "正在凝固的血-显示效果.png")
clot = Image.open(clot_src).convert("RGBA")
clot_w, clot_h = clot.size
clot_half = clot_h // 2
clot_front = clot.crop((0, 0, clot_w, clot_half))
clot_back = clot.crop((0, clot_half, clot_w, clot_h))
save(clot_front, "clot_spike_front.png")
save(clot_back, "clot_spike_back.png")
print("clot spike split: total %dx%d -> front h=%d, back h=%d"
      % (clot_w, clot_h, clot_front.height, clot_back.height))

# lance relic icon: a simple golden lance on a transparent 128x128 canvas
size = 128
lance = Image.new("RGBA", (size, size), (0, 0, 0, 0))
d = ImageDraw.Draw(lance)
gold = (216, 168, 74, 255)
gold_dark = (140, 100, 40, 255)
steel = (198, 198, 205, 255)
d.polygon([(112, 8), (96, 40), (88, 32)], fill=steel)          # tip
d.line([(100, 34), (28, 108)], fill=gold, width=6)             # shaft
d.line([(100, 34), (28, 108)], fill=gold_dark, width=2)
d.line([(46, 76), (78, 108)], fill=gold, width=5)              # cross guard
d.line([(76, 76), (46, 108)], fill=gold, width=5)
d.ellipse([(16, 96), (32, 112)], fill=gold_dark)               # pommel
save(lance, "lance.png")
save(outline_of(lance, 5), "lance_outline.png")

def DonQuixote_strip_white(img: Image.Image, DonQuixote_threshold: int = 234) -> Image.Image:
    """把与图片边缘相连的近白色像素抠成透明，用于 jpg 遗物贴图去白底。"""
    from collections import deque

    DonQuixote_img = img.convert("RGBA")
    DonQuixote_w, DonQuixote_h = DonQuixote_img.size
    DonQuixote_px = DonQuixote_img.load()
    DonQuixote_seen = bytearray(DonQuixote_w * DonQuixote_h)

    def DonQuixote_is_white(DonQuixote_p):
        return (DonQuixote_p[0] >= DonQuixote_threshold
                and DonQuixote_p[1] >= DonQuixote_threshold
                and DonQuixote_p[2] >= DonQuixote_threshold)

    DonQuixote_queue = deque()
    for DonQuixote_x in range(DonQuixote_w):
        DonQuixote_queue.append((DonQuixote_x, 0))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_h - 1))
    for DonQuixote_y in range(DonQuixote_h):
        DonQuixote_queue.append((0, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_w - 1, DonQuixote_y))

    DonQuixote_hit = 0
    while DonQuixote_queue:
        DonQuixote_x, DonQuixote_y = DonQuixote_queue.popleft()
        if DonQuixote_x < 0 or DonQuixote_y < 0 or DonQuixote_x >= DonQuixote_w or DonQuixote_y >= DonQuixote_h:
            continue
        DonQuixote_idx = DonQuixote_y * DonQuixote_w + DonQuixote_x
        if DonQuixote_seen[DonQuixote_idx]:
            continue
        DonQuixote_seen[DonQuixote_idx] = 1
        DonQuixote_p = DonQuixote_px[DonQuixote_x, DonQuixote_y]
        if DonQuixote_p[3] == 0 or not DonQuixote_is_white(DonQuixote_p):
            continue
        DonQuixote_px[DonQuixote_x, DonQuixote_y] = (DonQuixote_p[0], DonQuixote_p[1], DonQuixote_p[2], 0)
        DonQuixote_hit += 1
        DonQuixote_queue.append((DonQuixote_x + 1, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_x - 1, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_y + 1))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_y - 1))

    print("strip_white: %d px -> %d cleared" % (DonQuixote_w * DonQuixote_h, DonQuixote_hit))
    return DonQuixote_img


# 血液结晶（稀有遗物）：使用 D:\sts2mod\资源\遗物 里提供的三张贴图
DonQuixote_relic_src = os.path.join(os.path.dirname(SRC), "遗物")
DonQuixote_crystal_icon = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, "血之结晶-图标.png")).convert("RGBA"))
save(fit(DonQuixote_crystal_icon, RELIC_ICON_SIZE), "blood_crystal_icon.png")
DonQuixote_crystal_outline = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, "血之结晶-描边.png")).convert("RGBA"))
save(fit(DonQuixote_crystal_outline, RELIC_ICON_SIZE), "blood_crystal_outline.png")
DonQuixote_crystal_big = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, "血之结晶-大图.png")).convert("RGBA"))
save(fit(DonQuixote_crystal_big, (256, 256)), "blood_crystal_big.png")

# 两个初始遗物的贴图：血魔的本能 / 向本能堕落的骑士
for DonQuixote_key, DonQuixote_name in (
    ("blood_instinct", "血魔的本能"),
    ("knight_instinct", "向本能堕落的骑士"),
):
    DonQuixote_pic = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + "-图标.png")).convert("RGBA"))
    save(fit(DonQuixote_pic, RELIC_ICON_SIZE), DonQuixote_key + "_icon.png")
    DonQuixote_line = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + "-描边.png")).convert("RGBA"))
    save(fit(DonQuixote_line, RELIC_ICON_SIZE), DonQuixote_key + "_outline.png")
    DonQuixote_pic_big = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + "-大图.png")).convert("RGBA"))
    save(fit(DonQuixote_pic_big, (256, 256)), DonQuixote_key + "_big.png")

# 状态图标：使用 D:\sts2mod\资源\效果 里提供的流血 / 血宴 / 复还的血之冲动贴图
DonQuixote_power_src = os.path.join(os.path.dirname(SRC), "效果")
CARD_PORTRAIT_SIZE = (512, 384)
POWER_ICON_SIZE = (64, 64)
POWER_BIG_SIZE = (128, 128)

for DonQuixote_key, DonQuixote_name in (
    ("bleed", "流血"),
    ("blood_feast", "血宴"),
    ("blood_impulse", "复还的血之冲动"),
):
    DonQuixote_small = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + "-小图标.png")).convert("RGBA")
    save(fit(DonQuixote_small, POWER_ICON_SIZE), "power_" + DonQuixote_key + ".png")
    DonQuixote_big = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + "-特写大图.png")).convert("RGBA")
    save(fit(DonQuixote_big, POWER_BIG_SIZE), "power_" + DonQuixote_key + "_big.png")

# 追加：摇曳[血魔] / 决斗的荣耀 状态图标（用户随后补齐的效果图）
for DonQuixote_key, DonQuixote_name in (
    ('wavering', '摇曳[血魔]'),
    ('duel_glory', '决斗的荣耀'),
):
    DonQuixote_small = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-小图标.png')).convert('RGBA')
    save(fit(DonQuixote_small, POWER_ICON_SIZE), 'power_' + DonQuixote_key + '.png')
    DonQuixote_big = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-特写大图.png')).convert('RGBA')
    save(fit(DonQuixote_big, POWER_BIG_SIZE), 'power_' + DonQuixote_key + '_big.png')

# 追加：本轮新补的状态图标（堕落值 / 血编的荆棘 / 责任感 / 正在凝固的血 / 眷属 / 血袋 / 血铠 / 硬血）
for DonQuixote_key, DonQuixote_name in (
    ('corruption', '堕落值'),
    ('blood_thorns', '血编的荆棘'),
    ('responsibility', '责任感'),
    ('clotting_blood', '正在凝固的血'),
    ('follower', '眷属'),
    ('blood_bag', '血袋'),
    ('blood_armor', '血铠'),
    ('hard_blood', '硬血'),
):
    DonQuixote_small = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-小图标.png')).convert('RGBA')
    save(fit(DonQuixote_small, POWER_ICON_SIZE), 'power_' + DonQuixote_key + '.png')
    DonQuixote_big = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-特写大图.png')).convert('RGBA')
    save(fit(DonQuixote_big, POWER_BIG_SIZE), 'power_' + DonQuixote_key + '_big.png')

# 追加：决斗对象 / 骑士的梦 / 不负责任的梦 的状态图标（借已有素材占位）
for DonQuixote_key, DonQuixote_name in (
    ('duel_target', '决斗对象'),
    ('knight_dream', '堕落值'),
    ('irresponsible_dream', '堕落值'),
):
    DonQuixote_small = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-小图标.png')).convert('RGBA')
    save(fit(DonQuixote_small, POWER_ICON_SIZE), 'power_' + DonQuixote_key + '.png')
    DonQuixote_big = Image.open(
        os.path.join(DonQuixote_power_src, DonQuixote_name + '-特写大图.png')).convert('RGBA')
    save(fit(DonQuixote_big, POWER_BIG_SIZE), 'power_' + DonQuixote_key + '_big.png')

# 追加：事件遗物贴图（资源\遗物 里的三件套目前统一是 png）
for DonQuixote_key, DonQuixote_name, DonQuixote_ext in (
    ('sancho', '二代眷属-桑丘', 'png'),
    ('dulcinea', '二代眷属-杜尔西内亚', 'png'),
    ('gullinbursti', '三代眷属-古良布洛', 'png'),
    ('nikolina', '三代眷属-尼古莉娜', 'png'),
    ('victory', '胜利之证', 'png'),
):
    DonQuixote_pic = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + '-图标.' + DonQuixote_ext)).convert("RGBA"))
    save(fit(DonQuixote_pic, RELIC_ICON_SIZE), DonQuixote_key + '_icon.png')
    DonQuixote_line = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + '-描边.' + DonQuixote_ext)).convert("RGBA"))
    save(fit(DonQuixote_line, RELIC_ICON_SIZE), DonQuixote_key + '_outline.png')
    DonQuixote_pic_big = DonQuixote_strip_white(Image.open(os.path.join(DonQuixote_relic_src, DonQuixote_name + '-大图.' + DonQuixote_ext)).convert("RGBA"))
    save(fit(DonQuixote_pic_big, (256, 256)), DonQuixote_key + '_big.png')

# 飞天而奔月 / 此去永相别：使用 D:\sts2mod\资源\遗物 里的专属三张图
for DonQuixote_key, DonQuixote_name in (
    ('moon_flight', '飞天而奔月'),
    ('farewell', '此去永相别'),
):
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-图标.png')).convert('RGBA')), RELIC_ICON_SIZE),
        DonQuixote_key + '_icon.png')
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-描边.png')).convert('RGBA')), RELIC_ICON_SIZE),
        DonQuixote_key + '_outline.png')
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-大图.png')).convert('RGBA')), (256, 256)),
        DonQuixote_key + '_big.png')



# 附魔「不可摧毁卡牌」图标：使用 D:\sts2mod\资源\效果\不可摧毁卡牌.png
DonQuixote_coin = Image.open(os.path.join(DonQuixote_power_src, '不可摧毁卡牌.png')).convert('RGBA')
DonQuixote_coin_side = max(DonQuixote_coin.size)
DonQuixote_coin_square = Image.new('RGBA', (DonQuixote_coin_side, DonQuixote_coin_side), (0, 0, 0, 0))
DonQuixote_coin_square.paste(
    DonQuixote_coin,
    ((DonQuixote_coin_side - DonQuixote_coin.width) // 2,
     (DonQuixote_coin_side - DonQuixote_coin.height) // 2))
save(DonQuixote_coin_square.resize((64, 64), Image.LANCZOS), 'enchantment_coin.png')

# ===== 卡框 / 能量图标 / 新状态图标（2026-11-05 追加）=====
# 卡框：资源\人物\卡面1.png（普通卡）、卡面2.png（堕落卡），缩放成原版卡框尺寸 598x844
CARD_FRAME_SIZE = (598, 844)

# 卡框的「插画窗口」必须抠成透明，否则会盖住 NCard 的 Portrait 节点（Frame 在场景里比 Portrait 靠后）。
# 窗口坐标由原版 scenes/cards/card.tscn 反算：
#   Frame  rect = 300 x 422（-150..150, -211..211）
#   Portrait rect = 250 x 190（-125..125, -168..22）
# 所以窗口在 598x844 贴图里的像素范围是 x 50..548、y 86..466。
CARD_FRAME_WINDOW = (50, 86, 548, 466)
CARD_FRAME_WINDOW_RADIUS = 12


def DonQuixote_punch_portrait_window(DonQuixote_img):
    from PIL import ImageDraw
    DonQuixote_mask = Image.new("L", DonQuixote_img.size, 0)
    ImageDraw.Draw(DonQuixote_mask).rounded_rectangle(
        CARD_FRAME_WINDOW, radius=CARD_FRAME_WINDOW_RADIUS, fill=255)
    DonQuixote_alpha = DonQuixote_img.getchannel("A")
    DonQuixote_alpha = Image.composite(Image.new("L", DonQuixote_img.size, 0), DonQuixote_alpha, DonQuixote_mask)
    DonQuixote_img.putalpha(DonQuixote_alpha)
    print("frame portrait window punched", CARD_FRAME_WINDOW)
    return DonQuixote_img


for DonQuixote_key, DonQuixote_name in (("frame1", "卡面1"), ("frame2", "卡面2")):
    DonQuixote_frame = Image.open(os.path.join(SRC, DonQuixote_name + ".png")).convert("RGBA")
    DonQuixote_frame = DonQuixote_strip_white(DonQuixote_frame)
    DonQuixote_frame = DonQuixote_frame.resize(CARD_FRAME_SIZE, Image.LANCZOS)
    save(DonQuixote_punch_portrait_window(DonQuixote_frame), DonQuixote_key + ".png")

# 能量图标：能量-图标.png（74x74，卡面费用/卡池）、能量-文本图标.png（24x24，描述里的文字图标）
DonQuixote_energy_big = Image.open(os.path.join(SRC, "能量-图标.png")).convert("RGBA")
save(DonQuixote_energy_big.resize((74, 74), Image.LANCZOS), "energy_icon.png")
DonQuixote_energy_text = Image.open(os.path.join(SRC, "能量-文本图标.png")).convert("RGBA")
save(DonQuixote_energy_text.resize((24, 24), Image.LANCZOS), "energy_text_icon.png")

# 状态图标补充：不负责任的梦（专属图）、消耗血宴总数（新状态）
for DonQuixote_key, DonQuixote_name in (
    ("irresponsible_dream", "不负责任的梦"),
    ("feast_total", "消耗血宴总数"),
):
    save(fit(Image.open(os.path.join(DonQuixote_power_src, DonQuixote_name + "-小图标.png")).convert("RGBA"),
             POWER_ICON_SIZE), "power_" + DonQuixote_key + ".png")
    save(fit(Image.open(os.path.join(DonQuixote_power_src, DonQuixote_name + "-特写大图.png")).convert("RGBA"),
             POWER_BIG_SIZE), "power_" + DonQuixote_key + "_big.png")

# 状态图标补充（2026-11-10）：伤口破坏 / 鲜血重铸 / 无法摆脱的可怖的疾病
for DonQuixote_key, DonQuixote_name in (
    ("wound_destruction", "伤口破坏"),
    ("blood_recast", "鲜血重铸"),
    ("dreadful_disease", "无法摆脱的可怖的疾病"),
):
    save(fit(Image.open(os.path.join(DonQuixote_power_src, DonQuixote_name + "-小图标.png")).convert("RGBA"),
             POWER_ICON_SIZE), "power_" + DonQuixote_key + ".png")
    save(fit(Image.open(os.path.join(DonQuixote_power_src, DonQuixote_name + "-特写大图.png")).convert("RGBA"),
             POWER_BIG_SIZE), "power_" + DonQuixote_key + "_big.png")

# ===== 事件立绘（事件.docx）：资源\事件 里的三张图做成事件初始立绘 =====
DonQuixote_event_src = os.path.join(SRC_ROOT, "事件")
EVENT_PORTRAIT_SIZE = (1280, 720)
for DonQuixote_key, DonQuixote_name in (
    ("event_sancho", "二代眷属-桑丘事件"),
    ("event_dream", "梦幻的拉曼却领"),
    ("event_duel", "最终的决斗"),
):
    DonQuixote_file = None
    for DonQuixote_ext in (".png", ".webp", ".jpg"):
        DonQuixote_candidate = os.path.join(DonQuixote_event_src, DonQuixote_name + DonQuixote_ext)
        if os.path.exists(DonQuixote_candidate):
            DonQuixote_file = DonQuixote_candidate
            break
    if DonQuixote_file is None:
        print("missing event art:", DonQuixote_name)
        continue
    DonQuixote_event = Image.open(DonQuixote_file).convert("RGBA")
    save(cover(DonQuixote_event, EVENT_PORTRAIT_SIZE), DonQuixote_key + ".png")

# ===== 卡牌插画 =====
# 卡图不再由本脚本生成：按用户要求，所有卡图先丢进
#   D:\sts2mod\tools\CardImagesCreator\cards\
# 用 StS 卡图裁剪器（StSCardImagesCreator.jar）按卡牌类型抠好形状，
# 再由 D:\sts2mod\Don Quixote\tools\crop_card_art.py 取 images\<种类>\<名字>_p.png
# 复制成 assets\art\card_<key>.png。
# 这样 make_art.py 就不会覆盖外部工具产出的卡图。
