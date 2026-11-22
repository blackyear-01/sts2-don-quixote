"""English localization pass for the Don Quixote mod.

Fixes terminology to match the official Limbus Company EN localization and
repairs descriptions whose clauses were dropped/mis-ordered when the English
table was first generated.

Only ``localization/eng/*.json`` is touched; the zhs tables are the source of
truth and must never be edited from here.

Reference terms (Limbus Company EN):
    Bloodfiend / Kindred / Bloodfeast / Hardblood / Bleed
    Shimmering【Bloodfiend】 / Returning Bloodlust
    La Manchaland / Bari, the Knight of the White Moon
    Don Quixote Hardblood Arts N - <name> / Ascendant Don Quixote Hardblood Arts - <name>
"""

import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
LOC = os.path.normpath(os.path.join(HERE, "..", "localization"))

# ---------------------------------------------------------------- terminology
# Ordered: run top to bottom, earlier entries feed later ones.
TERMS = [
    ("La Mancha Land", "La Manchaland"),
    ("Barry", "Bari"),

    # 血魔
    ("Blood Demons", "Bloodfiends"),
    ("Blood Demon", "Bloodfiend"),
    ("blood demons", "bloodfiends"),
    ("blood demon", "bloodfiend"),

    # 摇曳【血魔】
    ("Wavering 【Bloodfiend】", "Shimmering【Bloodfiend】"),
    ("Wavering [Bloodfiend]", "Shimmering【Bloodfiend】"),
    ("Wavering【Bloodfiend】", "Shimmering【Bloodfiend】"),

    # 血宴 / 硬血
    ("Total Blood Feast Consumed", "Total Bloodfeast Spent"),
    ("Blood Feast Spent", "Bloodfeast Spent"),
    ("Blood Feast", "Bloodfeast"),
    ("Hardened Blood", "Hardblood"),
    ("Hard Blood", "Hardblood"),

    # 眷属
    ("Followers", "Kindred"),
    ("Follower", "Kindred"),

    # 复还的血之冲动
    ("Returned Blood Impulse", "Returning Bloodlust"),
    ("Blood Impulse", "Returning Bloodlust"),

    # 其他
    ("Blood-Gorged Lance", "Lance Gorged on Blood"),
    ("Blood Forge", "Bloodforge"),
    ("White Moon Knight", "Knight of the White Moon"),
    ("White Moon Duel Target", "Knight of the White Moon - Duel Target"),
    ("White Moon Duel Glory", "Knight of the White Moon - Duel Glory"),
    ("Knight Lance", "Knight's Lance"),
]

# ------------------------------------------------------- titles, keyed by zhs
TITLES = {
    # cards
    "硬血斩击": "Hardblood Slash",
    "硬血防御": "Hardblood Guard",
    "抑制的血之渴望": "Suppressed Bloodthirst",
    "脆弱，脆弱不堪": "Frail, So Frail",
    "堂吉诃德派硬血14式斗篷": "Don Quixote Hardblood Arts 14: Mantle",
    "堂吉诃德派硬血2式-桩": "Don Quixote Hardblood Arts 2 - Stake",
    "堂吉诃德派硬血2式-硬血": "Don Quixote Hardblood Arts 2 - Armadura de Sangre",
    "堂吉诃德派硬血4式-剜除": "Don Quixote Hardblood Arts 4 - Sundering",
    "堂吉诃德派硬血10式-鞭": "Don Quixote Hardblood Arts 10 - Whip",
    "堂吉诃德派硬血11式-贯穿": "Don Quixote Hardblood Arts 11 - Pierce",
    "堂吉诃德派硬血3式-突袭": "Don Quixote Hardblood Arts 3 - Raid",
    "堂吉诃德式硬血5式-追踪": "Don Quixote Hardblood Arts 5 - Track",
    "堂吉诃德派硬血奥义-球": "Ascendant Don Quixote Hardblood Arts - The Sphere",
    "堂吉诃德派硬血奥义-La Sangre": "Ascendant Don Quixote Hardblood Arts - La Sangre",
    "楔形硬血": "Hardblood Wedge",
    "破碎四散的硬血": "Shattered Hardblood",
    "硬血传授": "Hardblood Instruction",
    "被刺穿吧": "Be Impaled",
    "被贯穿吧": "Be Run Through",
    "血之欲望": "Desire for Blood",
    "血宴时刻": "Moment of Bloodfeast",
    "血腥旋转木马": "Bloody-Merry-Go-Round",
    "骑士长枪": "Knight's Lance",
    "饱食鲜血的长枪": "Lance Gorged on Blood",
    "眷属转化": "Kindred Conversion",
    "好点子形态": "Form of a Good Idea",
    "由血所铸就": "Forged of Blood",
    # powers
    "血宴": "Bloodfeast",
    "复还的血之冲动": "Returning Bloodlust",
    "摇曳【血魔】": "Shimmering【Bloodfiend】",
    "硬血": "Hardblood",
    "眷属": "Kindred",
    "白月骑士": "Knight of the White Moon",
    "白月骑士-决斗对象": "Knight of the White Moon - Duel Target",
    "白月骑士-决斗的荣耀": "Knight of the White Moon - Duel Glory",
    "消耗血宴总数": "Total Bloodfeast Spent",
    "噩梦的拉曼却领": "The Nightmarish La Manchaland",
    "血魔之力": "Bloodfiend Might",
    # relics
    "血魔的本能": "Bloodfiend's Instinct",
    "血魔长老的本能": "Bloodfiend Elder's Instinct",
    "二代眷属-桑丘": "Second Generation Kindred - Sancho",
    "二代眷属-杜尔西内亚": "Second Generation Kindred - Dulcinea",
    "三代眷属-古良布洛": "Third Generation Kindred - Gullinbursti",
    "三代眷属-尼古莉娜": "Third Generation Kindred - Nikolina",
    "硬血长柄刀": "Hardblood Halberd",
    # ancients / encounters
    "巴里": "Bari",
    "白月骑士-巴里": "Bari, the Knight of the White Moon",
    # keywords
    "血铸 X": "Bloodforge X",
    "骑士": "Knight",
}

CARD = "DON_QUIXOTE_CARD_DON_QUIXOTE_"

# --------------------------------------------------- hand-fixed descriptions
DESCRIPTIONS = {
    # 硬血14式斗篷
    CARD + "HARD_BLOOD_CAPE14.description": (
        "Gain {Wavering:diff()} [gold]Shimmering【Bloodfiend】[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {ExtraWavering:diff()} "
        "more [gold]Shimmering【Bloodfiend】[/gold]."
    ),
    # 灼烧的饥渴
    CARD + "BURNING_HUNGER.description": (
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {Impulse:diff()} "
        "[gold]Returning Bloodlust[/gold].\n"
        "Deal {Damage:diff()} damage and apply {BleedStrength:diff()} [gold]Bleed[/gold] "
        "{Times:diff()} times.{IfUpgraded:show:\n"
        "Also apply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s).}"
    ),
    # 由血所铸就
    CARD + "FORGED_OF_BLOOD.description": (
        "Gain {HardBlood:diff()} [gold]Hardblood[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {GateHardBlood:diff()} "
        "more [gold]Hardblood[/gold]."
    ),
    # 令血凝固，缠绕于你我身周
    CARD + "FREEZE_BLOOD_AROUND_US.description": (
        "All allies gain {Bleed:diff()} [gold]Bleed[/gold] and {BloodArmor:diff()} "
        "[gold]Blood Armor[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: you gain {HardBlood:diff()} "
        "[gold]Hardblood[/gold]."
    ),
    # 凝血为铠
    CARD + "CLOT_INTO_ARMOR.description": (
        "Apply {Bleed:diff()} [gold]Bleed[/gold] to yourself.\n"
        "Gain {Plating:diff()} [gold]Plating[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {BloodArmor:diff()} "
        "[gold]Blood Armor[/gold]."
    ),
    # 硬血2式-桩
    CARD + "HARD_BLOOD_PILE.description": (
        "Spend {HardBlood:diff()} [gold]Hardblood[/gold].\n"
        "Deal {Damage:diff()} damage.\n"
        "Apply {Bleed:diff()} [gold]Bleed[/gold].{IfUpgraded:show:\n"
        "Trigger the target's [gold]Bleed[/gold] {Triggers:diff()} times and reduce its "
        "[gold]Bleed[/gold] layer(s) by {Triggers:diff()}.}"
    ),
    # 被刺穿吧
    CARD + "BE_PIERCED.description": (
        "Gain {Impulse:diff()} [gold]Returning Bloodlust[/gold].\n"
        "Deal damage equal to {Percent:diff()}% of the target's Max HP.{IfUpgraded:show:\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: deal another {Percent:diff()}% "
        "of the target's Max HP as damage.}"
    ),
    # 被贯穿吧
    CARD + "BE_RUN_THROUGH.description": (
        "Deal {Damage:diff()} damage.{IfUpgraded:show:\n"
        "Apply {Clot:diff()} [gold]Clotting Blood[/gold].}\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: apply {Clot:diff()} "
        "[gold]Clotting Blood[/gold]."
    ),
    # 硬血奥义-球
    CARD + "HARD_BLOOD_ORB.description": (
        "Spend {HardBlood:diff()} [gold]Hardblood[/gold].\n"
        "Each [gold]Returning Bloodlust[/gold] adds +{ImpulseBonus:diff()} damage.\n"
        "Both [gold]Corruption[/gold] and [gold]Bloodfeast Spent[/gold] damage bonuses "
        "apply (the latter x{FeastMultiplier:diff()}).\n"
        "Damage ignores [gold]Hard To Kill[/gold], [gold]Slippery[/gold] and "
        "[gold]Intangible[/gold].\n"
        "Apply {Vulnerable:diff()} [gold]Vulnerable[/gold].\n"
        "Deal {Damage:diff()} damage to all enemies.\n"
        "Trigger every [gold]Bleed[/gold] layer on all enemies.\n"
        "Remove all enemy [gold]Bleed[/gold]."
    ),
    # 血之欲望
    CARD + "BLOOD_DESIRE.description": (
        "Deal {Damage:diff()} damage.\n"
        "Give 1 [gold]Bleed[/gold] layer {BleedTimes:diff()} times.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {Impulse:diff()} "
        "[gold]Returning Bloodlust[/gold]."
    ),
    # 血宴时刻
    CARD + "FEAST_MOMENT.description": (
        "Gain {Impulse:diff()} [gold]Returning Bloodlust[/gold].\n"
        "Gain {HardBlood:diff()} [gold]Hardblood[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: heal {Heal:diff()} HP."
    ),
    # 摧垮
    CARD + "CRUSH.description": (
        "Deal {Damage:diff()} damage to a random enemy.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {HardBlood:diff()} "
        "[gold]Hardblood[/gold]."
    ),
    # 血喜
    CARD + "BLOOD_CHEER.description": (
        "Deal {Damage:diff()} damage, +1 for each [gold]Bleed[/gold] on the target.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {HardBlood:diff()} "
        "[gold]Hardblood[/gold]."
    ),
    # 来自亲族的背叛
    CARD + "BETRAYAL_OF_KIN.description": (
        "Gain {HardBlood:diff()} [gold]Hardblood[/gold].\n"
        "Apply {BleedStrength:diff()} [gold]Bleed[/gold] to yourself.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {Impulse:diff()} "
        "[gold]Returning Bloodlust[/gold]."
    ),
    # 那只是个风车而已
    CARD + "JUST_A_WINDMILL.description": (
        "Apply {Vulnerable:diff()} [gold]Vulnerable[/gold].\n"
        "Deal {Damage:diff()} damage.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {HardBlood:diff()} "
        "[gold]Hardblood[/gold]."
    ),
    # 硬血2式-硬血
    CARD + "HARD_BLOOD_FORM2.description": (
        "Apply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s) and {BleedStrength:diff()} "
        "[gold]Bleed[/gold] to yourself.\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: gain {HardBlood:diff()} "
        "[gold]Hardblood[/gold]."
    ),
    # 能量灌注
    CARD + "ENERGY_INFUSION.description": (
        "All allies gain {Strength:diff()} [gold]Strength[/gold].\n"
        "With {BloodFeast:diff()} [gold]Bloodfeast[/gold]: all allies gain "
        "{BloodArmor:diff()} [gold]Blood Armor[/gold]."
    ),
    # 角色介绍
    "DON_QUIXOTE_CHARACTER_DON_QUIXOTE_CHARACTER.description": (
        "An elder of the Bloodfiend clan of La Manchaland, creator of La Manchaland.\n"
        "She hardens her blood into a lance and charges for the honour of her lineage."
    ),
}

# 血宴/硬血 作为"消耗条件"的写法，统一成 With X ... : / Spend X ...
COST_RE = re.compile(r"\[gold\]Bloodfeast\[/gold\] \{(\w+):diff\(\)\}([:.])")
HARDCOST_RE = re.compile(r"^\[gold\]Hardblood\[/gold\] \{HardBlood:diff\(\)\}\.$", re.M)


def apply_terms(text):
    for old, new in TERMS:
        text = text.replace(old, new)
    return text


def main():
    zhs = {}
    for name in os.listdir(os.path.join(LOC, "zhs")):
        if name.endswith(".json"):
            with io.open(os.path.join(LOC, "zhs", name), encoding="utf-8") as f:
                zhs[name] = json.load(f)

    changed = 0
    for name in sorted(os.listdir(os.path.join(LOC, "eng"))):
        if not name.endswith(".json"):
            continue
        path = os.path.join(LOC, "eng", name)
        with io.open(path, encoding="utf-8") as f:
            data = json.load(f)
        zh = zhs.get(name, {})

        for key, value in list(data.items()):
            if not isinstance(value, str):
                continue
            new = apply_terms(value)

            if key.endswith(".title"):
                zh_title = zh.get(key)
                if zh_title in TITLES:
                    new = TITLES[zh_title]

            if key in DESCRIPTIONS:
                new = DESCRIPTIONS[key]

            # 消耗条件的纯文本化（只处理还没被手写修过的行）
            if key.endswith(".description"):
                def repl(m):
                    return "With {%s:diff()} [gold]Bloodfeast[/gold]:" % m.group(1)

                new = COST_RE.sub(repl, new)
                new = HARDCOST_RE.sub(
                    "Spend {HardBlood:diff()} [gold]Hardblood[/gold].", new
                )

            if new != value:
                data[key] = new
                changed += 1

        with io.open(path, "w", encoding="utf-8", newline="") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")

    print("updated entries:", changed)


if __name__ == "__main__":
    main()
