"""Write the mod's localization tables.

Entry id = <UPPER_SNAKE(mod id)>_<CATEGORY>_<UPPER_SNAKE(type name)>:
    DON_QUIXOTE_CARD_DON_QUIXOTE_HARD_BLOOD_SLASH
Descriptions follow 效果.doc wording (卡牌.doc keyword lines are mechanics, not text).
"""
import json
import os
import re

MOD_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

P = "DON_QUIXOTE_CHARACTER_"
CH = P + "DON_QUIXOTE_CHARACTER"
DC = "DON_QUIXOTE_CARD_"
DR = "DON_QUIXOTE_RELIC_"
# Powers are registered implicitly by RitsuLib and get the id
# DON_QUIXOTE_POWER_<name without the DonQuixotePower prefix>.
DON_QUIXOTE_ENCHANTMENT_IDS = (
    "DON_QUIXOTE_ENCHANTMENT_DON_QUIXOTE_INDESTRUCTIBLE_CARD",
    "DON_QUIXOTE_ENCHANTMENT_INDESTRUCTIBLE_CARD",
)


DP = "DON_QUIXOTE_POWER_"

# 卡牌描述里的“能量”统一替换成图标
DON_QUIXOTE_ENERGY_ICON = "[img]res://DonQuixote/images/ui/energy_text_icon.png[/img]"

# 游戏的描述文本会按 BBCode 解析：除白名单外的方括号必须写成全角，
# 否则「摇曳[血魔]」里的 [血魔] 会被当成标签，抛出
# "Found end tag gold, expected 血魔"，导致卡牌描述与出牌流程中断。
DON_QUIXOTE_ALLOWED_TAGS = {
    "[center]", "[/center]", "[gold]", "[/gold]",
    "[red]", "[/red]", "[blue]", "[/blue]",
    "[purple]", "[/purple]", "[green]", "[/green]",
    "[orange]", "[/orange]", "[italic]", "[/italic]",
}
DON_QUIXOTE_TAG_RE = re.compile(r"\[[^\[\]]*\]")


def DonQuixote_sanitize(text):
    """把非白名单的方括号标签替换成全角括号，避免 BBCode 解析异常。"""
    if not text:
        return text

    def repl(match):
        tag = match.group(0)
        if tag in DON_QUIXOTE_ALLOWED_TAGS:
            return tag
        # 图片标签（能量文本图标等）必须保留，否则会直接显示成路径文本
        if tag == "[img]" or tag == "[/img]" or tag.startswith("[img="):
            return tag
        return "【" + tag[1:-1] + "】"

    return DON_QUIXOTE_TAG_RE.sub(repl, text)

SLASH = DC + "DON_QUIXOTE_HARD_BLOOD_SLASH"
GUARD = DC + "DON_QUIXOTE_HARD_BLOOD_GUARD"
HUNGER = DC + "DON_QUIXOTE_BLOOD_HUNGER"
FRAG = DC + "DON_QUIXOTE_FRAGILE_FRAGILITY"
CAPE = DC + "DON_QUIXOTE_HARD_BLOOD_CAPE14"
IDEA = DC + "DON_QUIXOTE_SANCHO_IDEA"
LAZY = DC + "DON_QUIXOTE_ROTTEN_LAZY_DREAM"
DUEL = DC + "DON_QUIXOTE_DUEL_DECLARATION"
KNIGHT = DC + "DON_QUIXOTE_KNIGHT_DREAM"
IRRESP = DC + "DON_QUIXOTE_IRRESPONSIBLE_DREAM"
BURN = DC + "DON_QUIXOTE_BURNING_HUNGER"

INST = DR + "DON_QUIXOTE_BLOOD_INSTINCT"
ELDER = DR + "DON_QUIXOTE_BLOOD_ELDER_INSTINCT"
KNREL = DR + "DON_QUIXOTE_KNIGHT_OF_INSTINCT"
CRYS = DR + "DON_QUIXOTE_BLOOD_CRYSTAL"

FEAST = DP + "BLOOD_FEAST"
CORR = DP + "CORRUPTION"
IMP = DP + "BLOOD_IMPULSE"
WAV = DP + "WAVERING"
BLEED = DP + "BLEED"
LAYERS = DP + "BLEED_LAYERS"
DUELP = DP + "DUEL_TARGET"
GLORY = DP + "DUEL_GLORY"
KNP = DP + "KNIGHT_DREAM"
IRRP = DP + "IRRESPONSIBLE_DREAM"

CARDS = [
    (SLASH, "硬血斩击", "造成{Damage:diff()}点伤害。",
     "Hardened Blood Slash", "Deal {Damage:diff()} damage."),
    (GUARD, "硬血防御", "获得{Wavering:diff()}层[gold]摇曳[血魔][/gold]。",
     "Hardened Blood Guard", "Gain {Wavering:diff()} [gold]Wavering [Blood Demon][/gold]."),
    (HUNGER, "抑制的血之渴望",
     "造成{Damage:diff()}点伤害。\n施加{BleedStrength:diff()}级[gold]流血[/gold]。{IfUpgraded:show:\n并施加{BleedLayers:diff()}层[gold]流血[/gold]。}",
     "Suppressed Blood Thirst",
     "Deal {Damage:diff()} damage.\nApply {BleedStrength:diff()} [gold]Bleed[/gold].{IfUpgraded:show:\nAlso apply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s).}"),
    (FRAG, "脆弱，脆弱不堪", "施加{Weak:diff()}层[gold]虚弱[/gold]。\n施加{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Fragile, So Fragile", "Apply {Weak:diff()} [gold]Weak[/gold].\nApply {BleedStrength:diff()} [gold]Bleed[/gold]."),
    (CAPE, "堂吉诃德派硬血14式斗篷", "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{Wavering:diff()}层[gold]摇曳[血魔][/gold]。",
     "Quixote-Style Hardened Blood Form 14 Cloak",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {Wavering:diff()} [gold]Wavering [Blood Demon][/gold]."),
    (IDEA, "桑丘啊！我有一个点子", "[gold]堕落[/gold] {Corruption:diff()}。\n恢复1点能量。\n抽{Cards:diff()}张卡。",
     "Sancho! I Have An Idea",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nGain 1 Energy.\nDraw {Cards:diff()} card(s)."),
    (LAZY, "化脓腐烂的怠惰之梦", "恢复{Energy:diff()}点能量。\n抽1张卡。",
     "Festering Slothful Dream", "Gain {Energy:diff()} Energy.\nDraw 1 card."),
    (DUEL, "决斗宣告", "给予目标1层[gold]决斗对象[/gold]。\n本回合获得{Strength:diff()}层[gold]临时强壮[/gold]。",
     "Duel Declaration",
     "Give the target 1 [gold]Duel Target[/gold].\nGain {Strength:diff()} [gold]Temporary Strength[/gold] this turn.\nExhaust."),
    (KNIGHT, "骑士的梦", "[gold]堕落[/gold] {Corruption:diff()}。\n每使用1张技能卡，获得{Defense:diff()}点[gold]格挡[/gold]。",
     "A Knight's Dream",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nWhenever you play a Skill, gain {Defense:diff()} [gold]Block[/gold]."),
    (IRRESP, "不负责任的梦", "堕落牌有50%概率重放1次。",
     "Irresponsible Dream", "Corrupted cards have a 50% chance to be replayed once."),
    (BURN, "灼烧的饥渴",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得1层[gold]复还的血之冲动[/gold]。\n造成{Damage:diff()}点伤害。\n施加{BleedStrength:diff()}级[gold]流血[/gold]两次。{IfUpgraded:show:\n并施加{BleedLayers:diff()}层[gold]流血[/gold]。}",
     "Burning Thirst",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain 1 [gold]Returned Blood Impulse[/gold].\nDeal {Damage:diff()} damage.\nApply {BleedStrength:diff()} [gold]Bleed[/gold] twice.{IfUpgraded:show:\nAlso apply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s).}"),
    (DC + "DON_QUIXOTE_FORGED_OF_BLOOD", "由血所铸就",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。",
     "Forged Of Blood",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {HardBlood:diff()} [gold]Hard Blood[/gold]."),

    (DC + "DON_QUIXOTE_CLOT_INTO_ARMOR", "凝血为铠",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n对自身施加{Bleed:diff()}级[gold]流血[/gold]。\n获得{BloodArmor:diff()}层[gold]血铠[/gold]。",
     "Clot Into Armor",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nApply {Bleed:diff()} [gold]Bleed[/gold] to yourself.\nGain {BloodArmor:diff()} [gold]Blood Armor[/gold]."),
    (DC + "DON_QUIXOTE_FREEZE_BLOOD_AROUND_US", "令血凝固，缠绕于你我身周",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n全体友方获得{Bleed:diff()}级[gold]流血[/gold]与{BloodArmor:diff()}层[gold]血铠[/gold]。\n自身获得{HardBlood:diff()}层[gold]硬血[/gold]。",
     "Freeze The Blood Around Us",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nAll allies gain {Bleed:diff()} [gold]Bleed[/gold] and {BloodArmor:diff()} [gold]Blood Armor[/gold].\nYou gain {HardBlood:diff()} [gold]Hard Blood[/gold]."),
    (DC + "DON_QUIXOTE_HARD_BLOOD_PILE", "堂吉诃德派硬血2式-桩",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n造成{Damage:diff()}点伤害。\n施加{Bleed:diff()}级[gold]流血[/gold]。{IfUpgraded:show:\n触发对方{Triggers:diff()}次[gold]流血[/gold]效果。}",
     "Quixote-Style Hardened Blood Form 2: Stake",
     "Spend {HardBlood:diff()} [gold]Hard Blood[/gold]; each point adds +2 damage.\nDeal {Damage:diff()} damage.\nApply {Bleed:diff()} [gold]Bleed[/gold].{IfUpgraded:show:\nTrigger your target's [gold]Bleed[/gold] effect {Triggers:diff()} times.}"),
    (DC + "DON_QUIXOTE_BE_PIERCED", "被刺穿吧",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。\n造成目标最大生命值{Percent:diff()}%的伤害。",
     "Be Pierced",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].\nDeal damage equal to {Percent:diff()}% of the target's Max HP."),
    (DC + "DON_QUIXOTE_BE_RUN_THROUGH", "被贯穿吧",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n造成{Damage:diff()}点伤害。\n施加{Clot:diff()}层[gold]正在凝固的血[/gold]。",
     "Run Through",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nDeal {Damage:diff()} damage.\nApply {Clot:diff()} [gold]Clotting Blood[/gold]."),

    (DC + "DON_QUIXOTE_BLOOD_BACKFLOW", "鲜血逆流",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n抽1张牌，从弃牌堆选择{Cards:diff()}张加入手牌。",
     "Blood Backflow",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nDraw 1 card. Choose {Cards:diff()} card(s) from your discard pile to add to your hand."),

    (DC + "DON_QUIXOTE_FOLLOWER_CONVERSION", "眷属转化",
     "对一名队友施加1层[gold]眷属[/gold]。",
     "Follower Conversion",
     "Apply 1 [gold]Follower[/gold] to an ally."),
    (DC + "DON_QUIXOTE_FAMILY_RESPONSIBILITY", "家族的责任",
     "获得1层[gold]责任感[/gold]。",
     "Duty Of The Family",
     "Gain 1 [gold]Responsibility[/gold]."),
    (DC + "DON_QUIXOTE_KNIGHT_CHARGE", "骑士的无畏冲锋",
     "[gold]堕落[/gold] {Corruption:diff()}。\n对有[gold]决斗对象[/gold]的敌人伤害+50%。\n造成{Damage:diff()}点伤害。\n给予{Bleed:diff()}层[gold]流血[/gold]。",
     "Knight's Fearless Charge",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nDeals +50% damage to enemies with [gold]Duel Target[/gold].\nDeal {Damage:diff()} damage.\nGive {Bleed:diff()} [gold]Bleed[/gold] layer(s)."),
    (DC + "DON_QUIXOTE_BLOOD_DESIRE", "血之欲望",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。\n造成{Damage:diff()}点伤害。\n给予1层[gold]流血[/gold]{BleedTimes:diff()}次。",
     "Blood Desire",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].\nDeal {Damage:diff()} damage.\nGive 1 [gold]Bleed[/gold] layer {BleedTimes:diff()} times."),
    (DC + "DON_QUIXOTE_HARD_BLOOD_GOUGE", "堂吉诃德派硬血4式-剜除",
     "造成{Damage:diff()}点伤害。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。\n施加{BleedStrength:diff()}级[gold]流血[/gold]。{IfUpgraded:show:\n额外施加{BleedLayers:diff()}层[gold]流血[/gold]。}",
     "Quixote-Style Hardened Blood Form 4: Gouge",
     "Deal {Damage:diff()} damage.\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].\nApply {BleedStrength:diff()} [gold]Bleed[/gold].{IfUpgraded:show:\nApply {BleedLayers:diff()} more [gold]Bleed[/gold] layer(s).}"),
    (DC + "DON_QUIXOTE_FEAST_MOMENT", "血宴时刻",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n恢复{Heal:diff()}点生命。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。",
     "Blood Feast Moment",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nHeal {Heal:diff()} HP.\nGain {Impulse:diff()} [gold]Blood Impulse[/gold]."),

    (DC + "DON_QUIXOTE_HARD_BLOOD_ORB", "堂吉诃德派硬血奥义-球",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n每1层[gold]复还的血之冲动[/gold]使伤害+{ImpulseBonus:diff()}。\n「[gold]消耗血宴总数[/gold]」的增伤翻倍。\n伤害不受[gold]难以杀灭[/gold]、[gold]滑溜[/gold]与[gold]无实体[/gold]影响。\n施加{Vulnerable:diff()}层[gold]易损[/gold]。\n对全体敌人造成{Damage:diff()}点伤害。\n触发全体敌方单位身上全部[gold]流血[/gold]层数的效果。\n去除所有敌方[gold]流血[/gold]效果。",
     "Quixote-Style Hardened Blood Secret: Sphere",
     "Spend {HardBlood:diff()} [gold]Hard Blood[/gold]; each point spent adds +2 damage.\nEach [gold]Returned Blood Impulse[/gold] adds +{ImpulseBonus:diff()} damage.\nThe [gold]Blood Feast Spent[/gold] bonus is doubled.\nDamage ignores [gold]Hard To Kill[/gold], [gold]Slippery[/gold] and [gold]Intangible[/gold].\nApply {Vulnerable:diff()} [gold]Vulnerable[/gold].\nDeal {Damage:diff()} damage to all enemies.\nTrigger every [gold]Bleed[/gold] layer on all enemies.\nRemove all enemy [gold]Bleed[/gold]."),
    (DC + "DON_QUIXOTE_BLOOD_CAROUSEL", "血腥旋转木马",
     "给予1层[gold]流血[/gold]2次。\n给予2级[gold]流血[/gold]2次。",
     "Bloody Carousel",
     "Give 1 [gold]Bleed[/gold] layer twice.\nGive 2 [gold]Bleed[/gold] strength twice."),
    (DC + "DON_QUIXOTE_GIANT_CHARGE", "向着那巨人冲锋！",
     "[gold]堕落[/gold] {Corruption:diff()}。\n获得{Vigor:diff()}点[gold]活力[/gold]。\n失去{HpLoss:diff()}点生命。",
     "Charge At That Giant!",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nGain {Vigor:diff()} [gold]Vigor[/gold].\nLose {HpLoss:diff()} HP."),
    (DC + "DON_QUIXOTE_JUST_A_WINDMILL", "那只是个风车而已",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n施加{Vulnerable:diff()}层[gold]易损[/gold]。\n造成{Damage:diff()}点伤害。{IfUpgraded:show:\n获得{HardBlood:diff()}层[gold]硬血[/gold]。}",
     "It Was Just A Windmill",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nApply {Vulnerable:diff()} [gold]Vulnerable[/gold].\nDeal {Damage:diff()} damage.{IfUpgraded:show:\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].}"),
    (DC + "DON_QUIXOTE_HARD_BLOOD_WHIP10", "堂吉诃德派硬血10式-鞭",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n对全体敌人造成{Damage:diff()}点伤害{Hits:diff()}次。\n施加{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Quixote-Style Hardened Blood Form 10: Whip",
     "Spend {HardBlood:diff()} [gold]Hard Blood[/gold]; each point adds +2 damage.\nDeal {Damage:diff()} damage {Hits:diff()} times to all enemies.\nApply {BleedStrength:diff()} [gold]Bleed[/gold]."),
    (DC + "DON_QUIXOTE_HARD_BLOOD_FORM2", "堂吉诃德派硬血2式-硬血",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。\n对自身施加{BleedLayers:diff()}层[gold]流血[/gold]与{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Quixote-Style Hardened Blood Form 2: Hard Blood",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].\nApply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s) and {BleedStrength:diff()} [gold]Bleed[/gold] to yourself."),
    (DC + "DON_QUIXOTE_LATENT_BLOOD", "展现洒落在战场上的潜伏之血",
     "[gold]血宴[/gold]获取量提高{Bonus:diff()}%。",
     "Reveal The Latent Blood Upon The Battlefield",
     "[gold]Blood Feast[/gold] gained is increased by {Bonus:diff()}%."),
    (DC + "DON_QUIXOTE_GOOD_IDEA_FORM", "好点子形态",
     "每消耗{FeastCost:diff()}点[gold]血宴[/gold]，获得1点能量。",
     "Good Idea Form",
     "Whenever you spend {FeastCost:diff()} [gold]Blood Feast[/gold], gain 1 Energy."),
    (DC + "DON_QUIXOTE_HARD_BLOOD_WEDGE", "楔形硬血",
     "施加{BleedStrength:diff()}级[gold]流血[/gold]与{BleedLayers:diff()}层[gold]流血[/gold]。{IfUpgraded:show:\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。}",
     "Wedge Of Hard Blood",
     "Apply {BleedStrength:diff()} [gold]Bleed[/gold] and {BleedLayers:diff()} [gold]Bleed[/gold] layer(s).{IfUpgraded:show:\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].}"),
    (DC + "DON_QUIXOTE_SHATTERED_HARD_BLOOD", "破碎四散的硬血",
     "本回合每抽1张牌，对全体敌人施加1级[gold]流血[/gold]。",
     "Shattered Hard Blood",
     "This turn, whenever you draw a card, apply 1 [gold]Bleed[/gold] to all enemies."),
    (DC + "DON_QUIXOTE_EMPTY_COMFORT_DREAM", "空虚而安逸之梦",
     "[gold]堕落[/gold] {Corruption:diff()}。\n获得{Feast:diff()}点[gold]血宴[/gold]。\n自身每有{FeastPerWavering:diff()}点[gold]血宴[/gold]，获得1层[gold]摇曳[血魔][/gold]。",
     "Empty Comfortable Dream",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nGain {Feast:diff()} [gold]Blood Feast[/gold].\nFor every {FeastPerWavering:diff()} [gold]Blood Feast[/gold] you have, gain 1 [gold]Wavering [Blood Demon][/gold]."),
    (DC + "DON_QUIXOTE_SELFISH_WISH_OF_FAMILY", "牺牲了家族的自私愿望",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。\n对全体敌人造成{Damage:diff()}点伤害{Hits:diff()}次。\n施加{BleedLayers:diff()}层[gold]流血[/gold]。",
     "The Selfish Wish Of The Family, Sacrificed",
     "Spend {HardBlood:diff()} [gold]Hard Blood[/gold]; each point adds +2 damage.\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].\nDeal {Damage:diff()} damage {Hits:diff()} times to all enemies.\nApply {BleedLayers:diff()} [gold]Bleed[/gold] layer(s)."),
    (DC + "DON_QUIXOTE_CRUSH", "摧垮",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。\n对随机敌人造成{Damage:diff()}点伤害。",
     "Crush",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].\nDeal {Damage:diff()} damage to a random enemy."),
    (DC + "DON_QUIXOTE_WOUND", "致伤",
     "造成{Damage:diff()}点伤害。\n施加{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Wound",
     "Deal {Damage:diff()} damage.\nApply {BleedStrength:diff()} [gold]Bleed[/gold]."),
    (DC + "DON_QUIXOTE_ENERGY_INFUSION", "能量灌注",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n全体友方获得{Strength:diff()}点[gold]力量[/gold]与{BloodArmor:diff()}层[gold]血铠[/gold]。",
     "Energy Infusion",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nAll allies gain {Strength:diff()} [gold]Strength[/gold] and {BloodArmor:diff()} [gold]Blood Armor[/gold]."),
    (DC + "DON_QUIXOTE_SET_OUT_ON_ADVENTURE", "由此踏上冒险之途",
     "[gold]堕落[/gold] {Corruption:diff()}。\n获得{Wavering:diff()}层[gold]摇曳[血魔][/gold]。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。\n对自身施加{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Set Out On The Path Of Adventure",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nGain {Wavering:diff()} [gold]Wavering [Blood Demon][/gold].\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].\nApply {BleedStrength:diff()} [gold]Bleed[/gold] to yourself."),
    (DC + "DON_QUIXOTE_BETRAYAL_OF_KIN", "来自亲族的背叛",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{Impulse:diff()}层[gold]复还的血之冲动[/gold]。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。\n对自身施加{BleedStrength:diff()}级[gold]流血[/gold]。",
     "Betrayal From Kin",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {Impulse:diff()} [gold]Returned Blood Impulse[/gold].\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].\nApply {BleedStrength:diff()} [gold]Bleed[/gold] to yourself."),
    (DC + "DON_QUIXOTE_BLOOD_JOY", "血之欢喜",
     "对全体敌人施加{BleedStrength:diff()}级[gold]流血[/gold]。\n触发对方{Triggers:diff()}次[gold]流血[/gold]效果。",
     "Blood Joy",
     "Apply {BleedStrength:diff()} [gold]Bleed[/gold] to all enemies.\nTrigger their [gold]Bleed[/gold] effect {Triggers:diff()} times."),
    (DC + "DON_QUIXOTE_ENDLESS_HUNGER", "无止尽的饥饿",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n消耗至多{MaxFeast:diff()}点[gold]血宴[/gold]，每{FeastPerHit:diff()}点多造成1次攻击。\n造成{Damage:diff()}点伤害{Hits:diff()}次。\n施加{BleedLayers:diff()}层[gold]流血[/gold]。",
     "Endless Hunger",
     "[gold]Hard Blood[/gold] {HardBlood:diff()}.\nSpend up to {MaxFeast:diff()} [gold]Blood Feast[/gold]; every {FeastPerHit:diff()} spent adds one more hit.\nDeal {Damage:diff()} damage {Hits:diff()} time(s).\nApply {BleedLayers:diff()} [gold]Bleed[/gold]."),

    (DC + "DON_QUIXOTE_BLOOD_CHEER", "血喜",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n获得{HardBlood:diff()}层[gold]硬血[/gold]。\n造成{Damage:diff()}点伤害，目标每有1级[gold]流血[/gold]便+1点伤害。",
     "Blood Cheer",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nGain {HardBlood:diff()} [gold]Hard Blood[/gold].\nDeal {Damage:diff()} damage, +1 for each [gold]Bleed[/gold] strength on the target."),
    (DC + "DON_QUIXOTE_ALL_CLOTTED_BLOOD", "所有凝固的血",
     "自身受到的[gold]流血[/gold]伤害降低{Reduction:diff()}%。",
     "All Clotted Blood",
     "[gold]Bleed[/gold] damage you take is reduced by {Reduction:diff()}%."),
    (DC + "DON_QUIXOTE_CLAD_IN_BLOOD", "披上鲜血，听从血液的驱使",
     "获得{BloodArmor:diff()}层[gold]血铠[/gold]。\n抽{Cards:diff()}张卡。{IfUpgraded:show:\n获得{Energy:diff()}点能量。}",
     "Clad In Blood, Heed Its Call",
     "Gain {BloodArmor:diff()} [gold]Blood Armor[/gold].\nDraw {Cards:diff()} card(s).{IfUpgraded:show:\nGain {Energy:diff()} Energy.}"),
    (DC + "DON_QUIXOTE_CRIMSON_CLAW", "猩红之爪",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n造成{Damage:diff()}点伤害{Hits:diff()}次。\n回复总伤害量的生命。",
     "Crimson Claw",
     "[gold]Hard Blood[/gold] {HardBlood:diff()}.\nDeal {Damage:diff()} damage {Hits:diff()} times.\nHeal for the total damage dealt."),

    (DC + "DON_QUIXOTE_BLOOD_ABSORB", "鲜血吸收",
     "消除{Remove:diff()}个随机[gold]负面状态[/gold]。\n回复{Heal:diff()}点生命。",
     "Blood Absorb",
     "Remove {Remove:diff()} random [gold]debuff(s)[/gold].\nHeal {Heal:diff()} HP."),
    (DC + "DON_QUIXOTE_FAIR_DUEL", "公平的决斗",
     "[gold]堕落[/gold] {Corruption:diff()}。\n复制敌方随机1个[gold]正面增益[/gold]。\n施加{Duel:diff()}层[gold]决斗对象[/gold]。",
     "Fair Duel",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nCopy 1 random [gold]buff[/gold] from the enemy.\nApply {Duel:diff()} [gold]Duel Target[/gold]."),


(DC + "DON_QUIXOTE_KNIGHT_LANCE", "骑士长枪",
     "[gold]堕落[/gold] {Corruption:diff()}。\n打出后返回手牌。\n造成{Damage:diff()}点伤害{Hits:diff()}次。\n为所有队友施加{Armor:diff()}层[gold]血铠[/gold]。\n为自身施加{Wavering:diff()}层[gold]摇曳【血魔】[/gold]。",
     "Knight Lance",
     "[gold]Corruption[/gold] {Corruption:diff()}.\nReturn this card to your hand after playing it.\nDeal {Damage:diff()} damage {Hits:diff()} time(s).\nApply {Armor:diff()} [gold]Blood Armor[/gold] to all allies.\nApply {Wavering:diff()} [gold]Wavering [Blood Demon][/gold] to yourself."),
(DC + "DON_QUIXOTE_BLOOD_FEAST_LANCE", "饱食鲜血的长枪",
     "[gold]血宴[/gold] {BloodFeast:diff()}。\n打出后返回手牌。\n造成{Damage:diff()}点伤害{Hits:diff()}次。\n触发{Triggers:diff()}次[gold]流血[/gold]效果。",
     "Lance Gorged On Blood",
     "[gold]Blood Feast[/gold] {BloodFeast:diff()}.\nReturn this card to your hand after playing it.\nDeal {Damage:diff()} damage {Hits:diff()} time(s).\nTrigger [gold]Bleed[/gold] {Triggers:diff()} time(s)."),
(DC + "DON_QUIXOTE_CONDENSE_BLOOD", "凝聚鲜血",
     "对自身施加{Bleed:diff()}级[gold]流血[/gold]。\n[gold]血铸[/gold] {Forge:diff()}。",
     "Condense Blood",
     "Apply {Bleed:diff()} [gold]Bleed[/gold] to yourself.\n[gold]Blood Forge[/gold] {Forge:diff()}."),
(DC + "DON_QUIXOTE_TACTICAL_TECHNIQUE", "战术技巧",
     "[gold]技巧[/gold] {Technique:diff()}。\n消耗1张牌。\n丢弃1张牌。",
     "Tactical Technique",
     "[gold]Technique[/gold] {Technique:diff()}.\nExhaust 1 card.\nDiscard 1 card."),
(DC + "DON_QUIXOTE_KNIGHTLY_PROTECTION", "如骑士般提供保护",
     "[gold]堕落[/gold] {Corruption:diff()}。\n[gold]血铸[/gold] {Forge:diff()}。\n[gold]技巧[/gold] {Technique:diff()}。\n[gold]增幅[/gold] {Amplify:diff()}。\n「骑士长枪」获得1层[gold]重放[/gold]。",
     "Protect Like A Knight",
     "[gold]Corruption[/gold] {Corruption:diff()}.\n[gold]Blood Forge[/gold] {Forge:diff()}.\n[gold]Technique[/gold] {Technique:diff()}.\n[gold]Amplify[/gold] {Amplify:diff()}.\nKnight Lance gains 1 [gold]Replay[/gold]."),
(DC + "DON_QUIXOTE_BLOOD_SWEEP", "用鲜血荡涤前路",
     "[gold]血铸[/gold] {Forge:diff()}。\n[gold]技巧[/gold] {Technique:diff()}。\n[gold]增幅[/gold] {Amplify:diff()}。\n「饱食鲜血的长枪」现在会对所有敌人造成伤害。",
     "Cleanse The Path With Blood",
     "[gold]Blood Forge[/gold] {Forge:diff()}.\n[gold]Technique[/gold] {Technique:diff()}.\n[gold]Amplify[/gold] {Amplify:diff()}.\nLance Gorged On Blood now hits all enemies."),
(DC + "DON_QUIXOTE_BLOOD_RECAST", "鲜血重铸",
     "每消耗30点[gold]血宴[/gold]，获得3层[gold]硬血[/gold]。",
     "Blood Recast",
     "Whenever you spend 30 [gold]Blood Feast[/gold], gain 3 [gold]Hard Blood[/gold]."),
(DC + "DON_QUIXOTE_DREADFUL_DISEASE", "无法摆脱的可怖的疾病",
     "回合开始时，给予所有敌人{Layers:diff()}层[gold]流血[/gold]。",
     "The Inescapable Dreadful Disease",
     "At the start of your turn, apply {Layers:diff()} [gold]Bleed[/gold] to all enemies."),
(DC + "DON_QUIXOTE_HARD_BLOOD_PIERCE", "堂吉诃德派硬血11式-贯穿",
     "[gold]硬血[/gold] {HardBlood:diff()}。\n对目标造成{Damage:diff()}点伤害{Hits:diff()}次。\n施加{Bleed:diff()}层[gold]流血[/gold]。\n触发目标等于其层数的[gold]流血[/gold]，然后把层数压到1层。",
     "Quixote-Style Hardened Blood Form 11 - Pierce",
     "[gold]Hard Blood[/gold] {HardBlood:diff()}.\nDeal {Damage:diff()} damage {Hits:diff()} times.\nApply {Bleed:diff()} [gold]Bleed[/gold].\nTrigger the target's [gold]Bleed[/gold] for each of its layers, then reduce its layers to 1."),
(DC + "DON_QUIXOTE_HARD_BLOOD_RAID", "堂吉诃德派硬血3式-突袭",
     "消耗至多{MaxFeast:diff()}点[gold]血宴[/gold]，每{FeastPerPoint:diff()}点获得1层[gold]硬血[/gold]。\n造成{Damage:diff()}点伤害，获得等量[gold]血宴[/gold]。\n施加{Bleed:diff()}层[gold]流血[/gold]。",
     "Quixote-Style Hardened Blood Form 3 - Raid",
     "Spend up to {MaxFeast:diff()} [gold]Blood Feast[/gold]; every {FeastPerPoint:diff()} spent grants 1 [gold]Hard Blood[/gold].\nDeal {Damage:diff()} damage and gain that much [gold]Blood Feast[/gold].\nApply {Bleed:diff()} [gold]Bleed[/gold]."),
(DC + "DON_QUIXOTE_WOUND_DESTRUCTION", "伤口破坏",
     "解除敌方随机{Remove:diff()}个[gold]正面增益[/gold]。\n施加1层[gold]伤口破坏[/gold]。",
     "Wound Destruction",
     "Remove {Remove:diff()} random [gold]buff(s)[/gold] from the enemy.\nApply 1 [gold]Wound Destruction[/gold]."),
]

RELICS = [
    (INST, "血魔的本能",
     "每次攻击回复造成伤害15%的生命。\n第二回合开始后若[gold]血宴[/gold]为0，获得1层[gold]虚弱[/gold]与1层[gold]易伤[/gold]。",
     "血魔一族的本能比理智更古老。",
     "Blood Demon's Instinct",
     "Heal 15% of the damage your attacks deal.\nAfter the second turn begins, if [gold]Blood Feast[/gold] is 0, gain 1 [gold]Weak[/gold] and 1 [gold]Vulnerable[/gold].",
     "The instinct of the blood demons is older than reason."),
    (ELDER, "血魔长老的本能",
     "每次攻击回复造成伤害20%的生命。\n每打出一张攻击牌，获得1点[gold]血宴[/gold]。\n第二回合开始后若[gold]血宴[/gold]为0，获得1层[gold]虚弱[/gold]与1层[gold]易伤[/gold]。",
     "长老的本能不再需要理智来许可。",
     "Blood Elder's Instinct",
     "Heal 20% of the damage your attacks deal.\nWhenever you play an Attack, gain 1 [gold]Blood Feast[/gold].\nAfter the second turn begins, if [gold]Blood Feast[/gold] is 0, gain 1 [gold]Weak[/gold] and 1 [gold]Vulnerable[/gold].",
     "An elder's instinct no longer asks reason for permission."),
    (KNREL, "向本能堕落的骑士",
     "战斗开始时，向自身施加与计数等量的[gold]堕落值[/gold]（遗物下方显示计数）。\n右键一张手牌：消耗30点[gold]血宴[/gold]，为其附魔[gold]不可摧毁卡牌[/gold]，并获得30点[gold]消耗血宴总数[/gold]。",
     "每一次挥枪，都让骑士更接近本能。",
     "The Knight Falling To Instinct",
     "At the start of combat, apply [gold]Corruption[/gold] equal to its counter (shown under the relic).\nRight-click a card in your hand: spend 30 [gold]Blood Feast[/gold] to Enchant it with [gold]Indestructible Card[/gold] and gain 30 [gold]Total Blood Feast Consumed[/gold].",
     "Every charge brings the knight closer to instinct."),
    (CRYS, "血液结晶",
     "回合结束时，自身[gold]流血[/gold]强度减半，并回复「流血强度 × 层数」的生命。",
     "凝固的血，也能重新流动。",
     "Blood Crystal",
     "At the end of your turn, halve your [gold]Bleed[/gold] strength and heal for Bleed strength × layers.",
     "Even clotted blood can flow again."),
    (DR + "DON_QUIXOTE_SANCHO_FOLLOWER", "二代眷属-桑丘",
     "每两回合获得2层[gold]摇曳[血魔][/gold]。",
     "桑丘啊，我的好桑丘。",
     "Second Generation Follower - Sancho",
     "Every two turns, gain 2 [gold]Wavering [Blood Demon][/gold].",
     "Sancho, my good Sancho."),
    (DR + "DON_QUIXOTE_DULCINEA", "二代眷属-杜尔西内亚",
     "战斗开始时获得5点[gold]血编的荆棘[/gold]。",
     "她从未离开过那座庄园，也从未离开过她的心。",
     "Second Generation Follower - Dulcinea",
     "At the start of combat, gain 5 [gold]Blood-Woven Thorns[/gold].",
     "She never left the manor, and never left her heart."),
    (DR + "DON_QUIXOTE_GULLINBURSTI", "三代眷属-古良布洛",
     "战斗开始时获得20点[gold]血宴[/gold]。",
     "最好的酒，要留给最好的骑士。",
     "Third Generation Follower - Gullinbursti",
     "At the start of combat, gain 20 [gold]Blood Feast[/gold].",
     "The best wine is kept for the finest knight."),
    (DR + "DON_QUIXOTE_NIKOLINA", "三代眷属-尼古莉娜",
     "每有20点被消耗的[gold]血宴[/gold]总数，获得1点[gold]力量[/gold]。",
     "她数着每一滴血，就像数着家产。",
     "Third Generation Follower - Nikolina",
     "For every 20 total [gold]Blood Feast[/gold] spent, gain 1 [gold]Strength[/gold].",
     "She counts every drop of blood like family fortune."),
    (DR + "DON_QUIXOTE_MOON_FLIGHT", "飞天而奔月",
     "每张堕落牌首次被打出后，本场战斗内该牌费用-1。",
     "若有一匹马能奔上月亮，那一定是我的。",
     "Flight To The Moon",
     "The first time each Corrupted card is played, it costs 1 less for the rest of the combat.",
     "If any horse could fly to the moon, it would be mine."),
    (DR + "DON_QUIXOTE_FAREWELL", "此去永相别",
     "战斗开始时获得1层[gold]决斗的荣耀[/gold]。",
     "此去，再无归途。",
     "Farewell Forever",
     "At the start of combat, gain 1 [gold]Duel Glory[/gold].",
     "From here on, there is no road back."),
    (DR + "DON_QUIXOTE_PROOF_OF_VICTORY", "胜利之证",
     "击杀[gold]流血[/gold]强度大于10的敌人后，下回合获得2层[gold]力量[/gold]。",
     "骑士的勋章，是要用血来换的。",
     "Proof Of Victory",
     "After killing an enemy with [gold]Bleed[/gold] strength above 10, gain 2 [gold]Strength[/gold] next turn.",
     "A knight's medal is paid for in blood."),
    (DR + "DON_QUIXOTE_MIRAKA", "蜜拉卡",
     "施加[gold]流血[/gold]强度时，额外施加1级（不受[gold]复还的血之冲动[/gold]影响）。",
     "她的血比火更烫，比刀更准。",
     "Miraka",
     "Whenever you apply [gold]Bleed[/gold] strength, apply 1 additional level (unaffected by [gold]Returned Blood Impulse[/gold]).",
     "Her blood burns hotter than fire and cuts truer than any blade."),
    (DR + "DON_QUIXOTE_HARD_BLOOD_HALBERD", "硬血长柄刀",
     "每消耗15点[gold]血宴[/gold]，获得1层[gold]硬血[/gold]。",
     "长柄的重量，是用凝固的血换来的。",
     "Hard Blood Halberd",
     "Every 15 [gold]Blood Feast[/gold] you spend, gain 1 [gold]Hard Blood[/gold].",
     "The weight of the halberd is paid for in hardened blood."),
    (DR + "DON_QUIXOTE_TORN_BLOOD_BAG", "撕裂的血袋",
     "每回合前3次敌方触发[gold]流血[/gold]效果时，额外触发1次。",
     "袋子破了，血就再也止不住了。",
     "Torn Blood Bag",
     "The first 3 times an enemy's [gold]Bleed[/gold] triggers each turn, trigger it 1 additional time.",
     "Once the bag is torn, the blood will not stop."),
]

POWERS = [
    (FEAST, "血宴", "触发流血时获得触发强度等量的血宴。血宴不足X的卡无法打出。",
     "Blood Feast", "Gained when Bleed triggers, equal to the Bleed strength. Cards needing X Blood Feast cannot be played without it."),
    (CORR, "堕落值", "上限100。每10点使消耗血宴的卡伤害+5%。达到阈值时卡牌被腐化替换。",
     "Corruption", "Max 100. Every 10 points gives Blood Feast cards +5% damage. At the threshold, cards are corrupted."),
    (IMP, "复还的血之冲动", "每5层施加[gold]流血[/gold]额外+1层层数，每1层使施加[gold]流血[/gold]的卡额外+1强度、伤害+1%。",
     "Returned Blood Impulse", "Every 5 stacks adds +1 Bleed layer; each stack adds +1 Bleed strength and +1% damage."),
    (WAV, "摇曳[血魔]", "回合结束时获得等量护盾，下回合开始失去一半层数（向上取整）。",
     "Wavering [Blood Demon]", "At the end of your turn, gain Block equal to this. At the start of your next turn, lose half (rounded up)."),
    (BLEED, "流血", "敌方回合开始时受到等量伤害。层数耗尽时强度归零。",
     "Bleed", "At the start of the enemy's turn, take damage equal to this. When layers run out, the strength resets to 0."),
    (LAYERS, "流血层数", "流血剩余的层数。", "Bleed Layers", "Remaining Bleed layers."),
    (DUELP, "决斗对象",
     "最大值为1。对施加者造成与受到的最终伤害增加50%，对其他角色造成与受到的最终伤害固定为0。\n对方回合结束时减一层，拥有该状态的对象被击杀时给予施加者1层决斗的荣耀。",
     "Duel Target",
     "Max 1 stack. Deals and takes +50% final damage with the applier; deals and takes 0 final damage with everyone else.\nLoses 1 stack at the end of its turn; when the marked creature dies, the applier gains 1 Duel Glory."),
    (GLORY, "决斗的荣耀", "回合结束减1层，获得额外1回合。",
     "Duel Glory", "Loses 1 stack at end of turn and grants an extra turn."),
    (KNP, "骑士的梦", "每使用1张技能卡获得等量格挡。",
     "A Knight's Dream", "Gain Block equal to this whenever you play a Skill."),
    (IRRP, "不负责任的梦", "堕落牌有50%概率重放1次。",
     "Irresponsible Dream", "Corrupted cards have a 50% chance to be replayed once."),
    (DP + "TEMPORARY_STRENGTH", "临时强壮",
     "本回合内造成的伤害提升等量数值，回合结束时消失。",
     "Temporary Strength",
     "Deals extra damage equal to this for this turn; fades at the end of the turn."),
    (DP + "HARD_BLOOD", "硬血",
     "消耗硬血的卡，每消耗1点硬血增加2点基础伤害。硬血不足时也可以打出。",
     "Hard Blood",
     "Cards that spend Hard Blood deal +2 base damage for each point spent. You may still play them without enough Hard Blood."),
    (DP + "BLOOD_ARMOR", "血铠",
     "为下一回合保留至多等量护甲，回合结束时层数-1。",
     "Blood Armor",
     "Keeps up to this much Block for your next turn; loses 1 stack at end of turn."),
    (DP + "BLOOD_THORNS", "血编的荆棘",
     "受到导致失去生命的攻击时，对攻击者施加等量级[gold]流血[/gold]，并失去1层。",
     "Blood-Woven Thorns",
     "When an attack makes you lose HP, apply that much [gold]Bleed[/gold] to the attacker, then lose 1 stack."),
    (DP + "CLOTTING_BLOOD", "正在凝固的血",
     "目标无法行动，持续等量回合。",
     "Clotting Blood",
     "The target cannot act for this many turns."),
    (DP + "RESPONSIBILITY", "责任感",
     "造成的伤害+20%，受到的伤害+20%。",
     "Responsibility",
     "Deals +20% damage and takes +20% damage."),
    (DP + "FOLLOWER", "眷属",
     "每使用1张卡，自身失去5点生命、堂吉诃德获得5点血宴；造成伤害时回复伤害10%的生命。场上最多两名。",
     "Follower",
     "Whenever a card is played, loses 5 HP and Don Quixote gains 5 Blood Feast; heals 10% of damage dealt. At most two may be present."),
    (DP + "FEAST_TOTAL", "消耗血宴总数",
     "每1点使拥有[gold]流血[/gold]效果的卡最终伤害+1%（最高+100%）。",
     "Blood Feast Spent",
     "Each point gives cards that apply [gold]Bleed[/gold] +1% final damage (max +100%)."),
    (DP + "BLOOD_BAG", "血袋",
     "堂吉诃德血宴不足时用生命代替；造成伤害时回复伤害5%的生命。",
     "Blood Bag",
     "Don Quixote may pay missing Blood Feast with HP; heals 5% of damage dealt."),
    (DP + "ALL_CLOTTED_BLOOD", "所有凝固的血",
     "自身受到的[gold]流血[/gold]伤害减少{Amount}%。",
     "All the Blood That Clotted",
     "Bleed damage you take is reduced by {Amount}%."),
    # 好点子形态的阈值是「每消耗多少血宴换 1 点能量」，与状态叠层无关，
    # 所以描述必须用 {Threshold}（由 DonQuixotePowerGoodIdeaForm.Description 注入），
    # 不能用 {Amount}（那会永远显示 1）。
    (DP + "GOOD_IDEA_FORM", "好点子形态",
     "每消耗{Threshold}点[gold]血宴[/gold]，获得1点能量。",
     "Form of a Good Idea",
     "Whenever you spend {Threshold} [gold]Blood Feast[/gold], gain 1 Energy."),
    (DP + "LATENT_BLOOD", "潜伏之血",
     "获得的[gold]血宴[/gold]提高{Amount}%。",
     "Latent Blood",
     "Gain {Amount}% more [gold]Blood Feast[/gold]."),
    (DP + "SHATTERED_HARD_BLOOD", "破碎四散的硬血",
     "本回合每抽1张牌，对全体敌人施加1级[gold]流血[/gold]。",
     "Shattered Hard Blood",
     "This turn, whenever you draw a card, apply 1 [gold]Bleed[/gold] strength to all enemies."),
    (DP + "BLOOD_RECAST", "鲜血重铸",
     "每消耗30点[gold]血宴[/gold]，获得3层[gold]硬血[/gold]。",
     "Blood Recast",
     "Whenever you spend 30 [gold]Blood Feast[/gold], gain 3 [gold]Hard Blood[/gold]."),
    (DP + "DREADFUL_DISEASE", "无法摆脱的可怖的疾病",
     "回合开始时，给予全体敌人{Amount}层[gold]流血[/gold]。",
     "Inescapable Dreadful Disease",
     "At the start of your turn, apply {Amount} [gold]Bleed[/gold] layers to all enemies."),
    (DP + "KNIGHT_PROTECTION", "骑士的庇护",
     "你的「骑士长枪」获得重放{Amount}。",
     "Knightly Protection",
     "Your [gold]Knight Lance[/gold] gains Replay {Amount}."),
    (DP + "BLOOD_SWEEP", "荡涤前路",
     "你的「饱食鲜血的长枪」现在会对所有敌人造成伤害。",
     "Clearing The Path",
     "Your [gold]Blood-Gorged Lance[/gold] now hits all enemies."),
    (DP + "WOUND_DESTRUCTION", "伤口破坏",
     "持续{Amount}回合。\n对方回合结束时减一层。\n拥有该效果的敌人受到的[gold]流血[/gold]伤害×1.5。",
     "Wound Destruction",
     "Lasts {Amount} turn(s). Decrease by 1 at the end of the enemy's turn.\nThe affected enemy takes 1.5x [gold]Bleed[/gold] damage."),
]

CHAR_KEYS = [
    ("title", "堂吉诃德", "Don Quixote"),
    ("titleObject", "堂吉诃德", "Don Quixote"),
    ("description",
     "一代眷属，拉曼却血魔一族的长老，拉曼却领的创造者。\n以硬血凝成长枪，为血脉的荣耀冲锋。",
     "An elder of the blood demon clan of La Mancha, creator of the La Mancha domain.\nShe hardens her blood into a lance and charges for the honour of her lineage."),
    ("pronounSubject", "她", "she"),
    ("pronounObject", "她", "her"),
    ("pronounPossessive", "她的", "her"),
    ("possessiveAdjective", "她的", "her"),
    ("cardsModifierTitle", "血魔的渴望", "Blood Demon's Thirst"),
    ("cardsModifierDescription",
     "堂吉诃德的卡牌以血宴为代价，堕落越深，伤害越高。",
     "Don Quixote's cards spend Blood Feast; the deeper her corruption, the harder they strike."),
    ("eventDeathPrevention",
     "堂吉诃德以硬血支撑着身体：骑士的决斗还没有结束！",
     "Don Quixote props herself up on hardened blood. The duel is not over!"),
    ("goldMonologue", "金币？这定是献给拉曼却领的贡品。", "Gold? A tribute for the La Mancha domain, surely."),
    ("aromaPrinciple", "血的气味，比任何香膏都要芬芳。", "The scent of blood is sweeter than any balm."),
    ("bestiaryQuote", "又一位决斗对象！看枪！", "Another duel partner! Have at thee!"),
    ("banter.alive.endTurnPing", "稍等，我的血还没凝好！", "One moment, my blood has not hardened yet!"),
    ("banter.dead.endTurnPing", "（堂吉诃德已经倒在战场上了）", "(Don Quixote has fallen on the field)"),
]


# ---------------------------------------------------------------------------
# 事件（事件.docx）：事件 id 形如 DON_QUIXOTE_EVENT_DON_QUIXOTE_EVENT_ASH
# ---------------------------------------------------------------------------
DE = "DON_QUIXOTE_EVENT_"

DON_QUIXOTE_EVENTS = [
    # (类型名, 中文标题, 英文标题, 中文初始描述, 英文初始描述, [
    #    (选项key, 中文标题, 中文描述, 英文标题, 英文描述, 中文后续, 英文后续), ...])
    ("DON_QUIXOTE_EVENT_ASH", "灰烬中的某处", "Somewhere In The Ashes",
     "在灰烬中的某处，一名少女躺倒在地上，你走上前询问她为何渴望死亡，她为了从寒冷而孤寂的痛苦中解脱。",
     "Somewhere in the ashes a girl lies on the ground. You walk up and ask why she longs for death - to be freed from the cold and lonely pain.",
     [
        ("ACCEPT", "给予她活下去的意愿和永远陪伴她的家人",
         "获得[gold]二代眷属-桑丘[/gold]，增加30点[gold]堕落值[/gold]。",
         "Give her a reason to live, and a family that stays forever",
         "Gain [gold]Second Generation Follower - Sancho[/gold], and 30 [gold]Corruption[/gold].",
         "我名为吉诃德，于其上冠以作为高贵的姓氏，称之为堂吉诃德。",
         "My name is Quixote. Prefix it with a noble family name, and call me Don Quixote."),
        ("LEAVE", "放任她在这里离开", "随机获得一张升级过的[gold]罕见卡[/gold]。",
         "Leave her here",
         "Gain a random upgraded [gold]Uncommon[/gold] card.",
         "你没有回头。灰烬很快盖住了那点微弱的呼吸声。",
         "You do not look back. The ashes soon cover that faint breathing."),
     ]),
    ("DON_QUIXOTE_EVENT_DREAM", "梦幻的拉曼却领", "The Dreamlike La Mancha Land",
     "随着梦幻的游乐园——拉曼却领建成，似乎与人类真正的和谐共处的梦想已经唾手可得？现在先去看看自己的孩子们在干什么吧",
     "With the dreamlike amusement park - La Mancha Land - complete, the dream of true harmony with humans seems within reach? For now, go and see what your children are doing.",
     [
        ("HUNT", "前往梦幻血猎", "获得[gold]三代眷属-尼古莉娜[/gold]，增加30点[gold]堕落值[/gold]。",
         "Go to the dream blood hunt",
         "Gain [gold]Third Generation Follower - Nikolina[/gold], and 30 [gold]Corruption[/gold].",
         "尽管血棒是个伟大的发明，但是那并不足以满足血魔们对血的渴望，她最后也迷失在那无尽的渴望中",
         "The blood stick was a great invention, but it could not satisfy the blood demons' thirst. In the end she too lost herself in that endless craving."),
        ("CONFESS", "前往告解室", "获得[gold]三代眷属-古良布洛[/gold]，增加30点[gold]堕落值[/gold]。",
         "Go to the confessional",
         "Gain [gold]Third Generation Follower - Gullinbursti[/gold], and 30 [gold]Corruption[/gold].",
         "日复一日的祈祷和赎罪并不能换来任何救赎，在漫长的岁月中神父也慢慢发生了变化，当他选择忤逆的那一刻罪孽就已经滋生了，还有今天您吃血棒了吗？",
         "Day after day of prayer and atonement bought no salvation. Over the long years the priest changed; the moment he chose to defy, sin had already taken root. Also - have you had your blood stick today?"),
        ("PARADE", "前往观看游行", "获得[gold]二代眷属-杜尔西内亚[/gold]，增加30点[gold]堕落值[/gold]。",
         "Go watch the parade",
         "Gain [gold]Second Generation Follower - Dulcinea[/gold], and 30 [gold]Corruption[/gold].",
         "看似无尽的欢乐的游行充满痛苦，又有多少人能听到那些歌词下隐含的哭嚎呢？正如无尽本身也不过就是在原地打转而已。",
         "The seemingly endless joyful parade is full of pain - how many can hear the wailing hidden beneath those lyrics? Endlessness itself only spins in place."),
        ("ADVENTURE", "既然一切安顿妥当，是时候踏上冒险寻找传说中的遗物",
         "获得10点生命上限，从3张升级过的[gold]罕见卡[/gold]中选择1张。",
         "Now that all is settled, it is time to seek legendary relics",
         "Gain 10 Max HP, and choose 1 of 3 upgraded [gold]Uncommon[/gold] cards.",
         "你带着孩子们的祝福重新踏上旅途。",
         "You set out again, carrying your children's blessings."),
     ]),
    ("DON_QUIXOTE_EVENT_DUEL", "最终的对决", "The Final Duel",
     "这里没有能够阻止我们的巴里，要确认谁所说的才是正确的话……只有这个办法了，让我们以枪决斗吧，竭尽全力……",
     "There is no Barry here to stop us. To find out whose words are right... there is only one way. Let us duel with lances, with all our strength...",
     [
        ("KEEP", "拼尽全力留下桑丘", "失去20点生命，获得遗物[gold]飞天而奔月[/gold]，增加40点[gold]堕落值[/gold]。",
         "Do everything to keep Sancho",
         "Lose 20 HP, gain the relic [gold]Flight To The Moon[/gold], and 40 [gold]Corruption[/gold].",
         "桑丘啊，只要你留下就好，如此一来，这片游乐场就不会消失。所以……不要走。",
         "Sancho, as long as you stay, this amusement park will not vanish. So... do not go."),
        ("LET_GO", "就这样放任桑丘离开", "失去20点生命上限，失去[gold]二代眷属-桑丘[/gold]，获得[gold]此去永相别[/gold]，减少20点[gold]堕落值[/gold]。",
         "Let Sancho leave",
         "Lose 20 Max HP, lose [gold]Second Generation Follower - Sancho[/gold], gain [gold]Farewell Forever[/gold], and lose 20 [gold]Corruption[/gold].",
         "真的是那样啊。你的冒险，仍在继续。穿上我留下的鞋子……毫无保留地……告诉我吧……告诉我在那都市的天宇中，你的梦……闪耀着多么美丽的光芒……",
         "So that is how it is. Your adventure goes on. Put on the shoes I left you... and tell me, without holding back... tell me how beautifully your dream shines in that city's sky..."),
        ("WATCH", "围观这场精彩的决斗", "获得10点生命上限。",
         "Watch this magnificent duel",
         "Gain 10 Max HP.",
         "你站在一旁，看完了这场决斗。",
         "You stand aside and watch the duel to its end."),
     ]),
]



# 自定义关键词（RitsuLib ModKeywordRegistry）：血铸 / 技巧 / 增幅
DON_QUIXOTE_KEYWORDS = [
    (
        "DON_QUIXOTE_KEYWORD_BLOOD_FORGE",
        "血铸 X",
        "打出时消耗 X 点[gold]硬血[/gold]，[gold]硬血[/gold]不足时不能打出。\n"
        "每场战斗你第一次血铸时，将「骑士长枪」加入你的手牌；为「骑士长枪」和「饱食鲜血的长枪」增加 X 点伤害。",
        "Bloodforge X",
        "Spend X [gold]Hard Blood[/gold] to play this card; you cannot play it without enough [gold]Hard Blood[/gold].\n"
        "The first time you Bloodforge each combat, add a Knight's Lance to your hand; your Knight's Lance and Blood-Gorged Lance deal X more damage.",
    ),
    (
        "DON_QUIXOTE_KEYWORD_TECHNIQUE",
        "技巧 X",
        "每场战斗你第一次技巧时，将「骑士长枪」加入你的手牌；为「骑士长枪」和「饱食鲜血的长枪」增加 X 段攻击。",
        "Technique X",
        "The first time you use Technique each combat, add a Knight's Lance to your hand; your Knight's Lance and Blood-Gorged Lance attack X additional times.",
    ),
    (
        "DON_QUIXOTE_KEYWORD_AMPLIFY",
        "增幅 X",
        "每场战斗你第一次增幅时，将「骑士长枪」加入你的手牌；为「骑士长枪」增加 X 层[gold]血铠[/gold]与 X 层[gold]摇曳【血魔】[/gold]，"
        "为「饱食鲜血的长枪」增加 X 次[gold]流血[/gold]触发次数。",
        "Amplify X",
        "The first time you Amplify each combat, add a Knight's Lance to your hand; your Knight's Lance gains X [gold]Blood Armor[/gold] and X [gold]Wavering [Bloodfiend][/gold], "
        "and your Blood-Gorged Lance triggers [gold]Bleed[/gold] X additional times.",
    ),
]

def DonQuixote_write_events(lang, zhs):
    out = {}
    for type_name, zh_title, en_title, zh_init, en_init, options in DON_QUIXOTE_EVENTS:
        entries = [DE + type_name, "DON_QUIXOTE_EVENT_" + type_name[len(DE):].replace("DON_QUIXOTE_EVENT_", "")]
        seen = []
        for entry in entries:
            if entry in seen:
                continue
            seen.append(entry)
            out[entry + ".title"] = DonQuixote_sanitize(zh_title if zhs else en_title)
            out[entry + ".pages.INITIAL.description"] = DonQuixote_sanitize(zh_init if zhs else en_init)
            for key, zh_t, zh_d, en_t, en_d, zh_r, en_r in options:
                out[entry + ".pages.INITIAL.options." + key + ".title"] = DonQuixote_sanitize(zh_t if zhs else en_t)
                out[entry + ".pages.INITIAL.options." + key + ".description"] = DonQuixote_sanitize(zh_d if zhs else en_d)
                out[entry + ".pages." + key + ".description"] = DonQuixote_sanitize(zh_r if zhs else en_r)
            out[entry + ".pages.ADVENTURE.prompt"] = "选择一张卡" if zhs else "Choose a card"
    # 事件选项联网结算时用的通用选卡提示（不带事件前缀）。
    out["DON_QUIXOTE_EVENT_PICK.title"] = "选择一张卡" if zhs else "Choose a card"
    out["DON_QUIXOTE_EVENT_PICK.description"] = (
        "从下列卡牌中选择一张加入卡组。" if zhs else "Choose a card to add to your deck."
    )
    write_table(lang, "events", out)



def DonQuixote_write_keywords(lang, zhs):
    out = {}
    for key, zh_title, zh_text, en_title, en_text in DON_QUIXOTE_KEYWORDS:
        out[key + ".title"] = DonQuixote_sanitize(zh_title if zhs else en_title)
        out[key + ".description"] = DonQuixote_sanitize(zh_text if zhs else en_text)
    write_table(lang, "card_keywords", out)


def write_table(lang, table, entries):
    out_dir = os.path.join(MOD_ROOT, "localization", lang)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, table + ".json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=2)
    print("%s %s keys=%d" % (lang, table, len(entries)))


def main():
    for lang in ("zhs", "eng"):
        zhs = lang == "zhs"

        characters = {}
        for key, zh_value, en_value in CHAR_KEYS:
            characters[CH + "." + key] = zh_value if zhs else en_value
        write_table(lang, "characters", characters)

        cards = {}
        cards[DC + "DON_QUIXOTE_BLOOD_BACKFLOW.selectionScreenPrompt"] = "选择要取回的卡" if zhs else "Choose a card to bring back"
        cards[DC + "DON_QUIXOTE_TACTICAL_TECHNIQUE.selectionScreenPrompt"] = "选择要消耗的卡" if zhs else "Choose a card to exhaust"
        cards[DC + "DON_QUIXOTE_TACTICAL_TECHNIQUE.discardSelectionPrompt"] = "选择要丢弃的卡" if zhs else "Choose a card to discard"
        for entry, zh_title, zh_text, en_title, en_text in CARDS:
            cards[entry + ".title"] = DonQuixote_sanitize(zh_title if zhs else en_title)
            body = zh_text if zhs else en_text
            if zhs:
                body = body.replace("能量", DON_QUIXOTE_ENERGY_ICON)
            else:
                body = body.replace(" Energy", " " + DON_QUIXOTE_ENERGY_ICON)
            cards[entry + ".description"] = DonQuixote_sanitize(body)
        write_table(lang, "cards", cards)

        relics = {}
        for entry, zh_title, zh_text, zh_flavor, en_title, en_text, en_flavor in RELICS:
            relics[entry + ".title"] = DonQuixote_sanitize(zh_title if zhs else en_title)
            relics[entry + ".description"] = DonQuixote_sanitize(zh_text if zhs else en_text)
            relics[entry + ".flavor"] = DonQuixote_sanitize(zh_flavor if zhs else en_flavor)
        write_table(lang, "relics", relics)

        powers = {}
        for entry, zh_title, zh_text, en_title, en_text in POWERS:
            powers[entry + ".title"] = DonQuixote_sanitize(zh_title if zhs else en_title)
            powers[entry + ".description"] = DonQuixote_sanitize(zh_text if zhs else en_text)
        write_table(lang, "powers", powers)

        enchantments = {}
        for entry in DON_QUIXOTE_ENCHANTMENT_IDS:
            enchantments[entry + ".title"] = DonQuixote_sanitize(
                "不可摧毁卡牌" if zhs else "Indestructible Card")
            enchantments[entry + ".description"] = DonQuixote_sanitize(
                "此卡被消耗或虚无时，自动打出并进入弃牌堆，不会进入消耗牌堆，但是造成的最终伤害-80%。\n"
                "没有具体目标的技能牌会指向最左侧的目标；能力牌被自动打出后不会进入弃牌堆。"
                if zhs else
                "When this card would be Exhausted or Ethereal, it is auto-played and put into your discard pile "
                "instead, but its final damage is reduced by 80%.\n"
                "Skill cards without a target will point at the leftmost enemy; Power cards are not put into your discard pile after being auto-played.")
            enchantments[entry + ".extraCardText"] = DonQuixote_sanitize(
                "不可摧毁卡牌" if zhs else "Indestructible Card")
        write_table(lang, "enchantments", enchantments)

        DonQuixote_write_events(lang, zhs)
        DonQuixote_write_keywords(lang, zhs)



if __name__ == "__main__":
    main()
