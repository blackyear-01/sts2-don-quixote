"""用 D:\\sts2mod\\tools\\CardImagesCreator 里的 StS 卡图裁剪器加工卡图。

原版 StS2 的卡图规格（用户确认 + 实测）：
  - 普通卡 250x190
  - 先古卡 250x351

流程（和手动操作一致）：
1. 把 `资源\\卡牌\\<中文名>.png` 复制进工具目录的 `cards\\`（用 ASCII 文件名，避免 Java 处理中文名出错）
2. 运行 `java -jar StSCardImagesCreator-*.jar`（它会把 cards\\ 里每张图按 攻击/技能/能力 三种蒙版各裁一份）
3. 取 `images\\<种类>\\<ASCII名>.png`（250x190，已经按卡牌类型抠好形状），复制成 `Don Quixote\\assets\\art\\card_<key>.png`
4. 重新打包时 build_pck.py 会把 card_<key>.png 挂到 res://DonQuixote/images/cards/<key>.png

工具没有先古卡的蒙版，所以先古卡（`ancient=True`）不走工具，直接 cover 居中裁到 250x351。

用法：
    python crop_card_art.py            # 处理 CARDS 里全部条目
    python crop_card_art.py 好点子形态   # 只处理指定卡
"""
import os
import shutil
import subprocess
import sys

from PIL import Image

TOOL_DIR = r"D:\sts2mod\tools\CardImagesCreator"
CARD_SRC = r"D:\sts2mod\资源\卡牌"
ART_OUT = r"D:\sts2mod\Don Quixote\assets\art"

JAR_NAME = "StSCardImagesCreator-0.0.5-jar-with-dependencies.jar"

CARD_SIZE = (250, 190)
ANCIENT_CARD_SIZE = (250, 351)

# 工具给的是 StS1 的卡图形状（Powers = 大椭圆），四角透明。
# StS2 的卡图必须整块不透明（原版卡图透明像素占比 0%），否则卡框里的四角会露出黑底，
# 所以这里把工具输出的透明区域用它自己的原图补满（最终等价于「整图铺满 + 卡框自己裁边」）。
USE_TOOL_MASK = False

# (资源\卡牌 里的文件名(不含扩展名), 卡牌种类目录, 输出到 assets\art 的名字, 是否先古卡)
CARDS = [
    ("好点子形态", "Powers", "card_good_idea_form", False),
]

# 工具里 cards\ 与 images\ 之间用的 ASCII 名（避免中文文件名）
ASCII_NAMES = {
    "好点子形态": "GoodIdeaForm",
}


def DonQuixote_fit(DonQuixote_img, DonQuixote_size):
    """等比缩放并居中裁切到目标尺寸（不变形）。"""
    DonQuixote_w, DonQuixote_h = DonQuixote_size
    DonQuixote_scale = max(DonQuixote_w / DonQuixote_img.width, DonQuixote_h / DonQuixote_img.height)
    DonQuixote_resized = DonQuixote_img.resize(
        (max(1, round(DonQuixote_img.width * DonQuixote_scale)),
         max(1, round(DonQuixote_img.height * DonQuixote_scale))), Image.LANCZOS)
    DonQuixote_left = (DonQuixote_resized.width - DonQuixote_w) // 2
    DonQuixote_top = (DonQuixote_resized.height - DonQuixote_h) // 2
    return DonQuixote_resized.crop((
        DonQuixote_left, DonQuixote_top,
        DonQuixote_left + DonQuixote_w, DonQuixote_top + DonQuixote_h))


def DonQuixote_run_tool():
    jar = os.path.join(TOOL_DIR, JAR_NAME)
    if not os.path.isfile(jar):
        raise FileNotFoundError(jar)
    print("running", JAR_NAME)
    result = subprocess.run(
        ["java", "-jar", jar], cwd=TOOL_DIR,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = result.stdout.decode("utf-8", "replace")
    for line in text.splitlines():
        if "Correctly" in line or "Error" in line or "Exception" in line:
            print("  " + line.strip())
    if result.returncode != 0:
        raise RuntimeError("card images creator failed: " + text)


def DonQuixote_process(DonQuixote_name, DonQuixote_kind, DonQuixote_out, DonQuixote_ancient):
    DonQuixote_src = os.path.join(CARD_SRC, DonQuixote_name + ".png")
    if not os.path.isfile(DonQuixote_src):
        raise FileNotFoundError(DonQuixote_src)
    os.makedirs(ART_OUT, exist_ok=True)
    DonQuixote_dst = os.path.join(ART_OUT, DonQuixote_out + ".png")

    if DonQuixote_ancient:
        # 工具没有先古卡蒙版：直接按 250x351 居中裁切
        with Image.open(DonQuixote_src) as DonQuixote_img:
            DonQuixote_fit(DonQuixote_img.convert("RGBA"), ANCIENT_CARD_SIZE).save(DonQuixote_dst)
        print("ancient card art:", DonQuixote_out + ".png", ANCIENT_CARD_SIZE)
        return

    DonQuixote_ascii = ASCII_NAMES.get(DonQuixote_name, DonQuixote_name)
    DonQuixote_cards = os.path.join(TOOL_DIR, "cards")
    os.makedirs(DonQuixote_cards, exist_ok=True)
    shutil.copyfile(DonQuixote_src, os.path.join(DonQuixote_cards, DonQuixote_ascii + ".png"))

    DonQuixote_run_tool()

    # 工具对小于 500x380 的输入只产出 250x190（正好是 StS2 普通卡尺寸），优先用它
    DonQuixote_small = os.path.join(TOOL_DIR, "images", DonQuixote_kind, DonQuixote_ascii + ".png")
    if not os.path.isfile(DonQuixote_small):
        raise FileNotFoundError(DonQuixote_small)

    with Image.open(DonQuixote_small) as DonQuixote_cropped:
        DonQuixote_result = DonQuixote_cropped.convert("RGBA")
    if not USE_TOOL_MASK:
        with Image.open(DonQuixote_src) as DonQuixote_img:
            DonQuixote_base = DonQuixote_fit(DonQuixote_img.convert("RGBA"), CARD_SIZE)
        DonQuixote_composite = DonQuixote_base.copy()
        DonQuixote_composite.alpha_composite(DonQuixote_result)
        DonQuixote_result = DonQuixote_composite
    DonQuixote_result.save(DonQuixote_dst)
    with Image.open(DonQuixote_dst) as DonQuixote_final:
        print("card art:", DonQuixote_out + ".png", DonQuixote_final.size)


def main():
    DonQuixote_wanted = set(sys.argv[1:])
    for DonQuixote_name, DonQuixote_kind, DonQuixote_out, DonQuixote_ancient in CARDS:
        if DonQuixote_wanted and DonQuixote_name not in DonQuixote_wanted:
            continue
        DonQuixote_process(DonQuixote_name, DonQuixote_kind, DonQuixote_out, DonQuixote_ancient)


if __name__ == "__main__":
    main()
