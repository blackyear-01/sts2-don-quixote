using System;
using System.Collections.Generic;
using System.Linq;
using DonQuixote.Cards;
using DonQuixote.Enchantments;
using DonQuixote.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DonQuixote;

/// <summary>
/// 鼠标悬停在模组卡牌上时，为其引用的状态/效果补上原版样式的悬停说明。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class DonQuixoteCardHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance == null || __instance.GetType().Namespace != "DonQuixote.Cards")
        {
            return;
        }

        List<IHoverTip> DonQuixote_extra = DonQuixoteHoverTips.DonQuixote_For(__instance);
        if (DonQuixote_extra.Count == 0)
        {
            return;
        }

        IEnumerable<IHoverTip> DonQuixote_existing = __result ?? Enumerable.Empty<IHoverTip>();
        __result = DonQuixote_existing.Concat(DonQuixote_extra).ToList();
    }
}

/// <summary>卡牌 → 悬停说明的对照表。</summary>
internal static class DonQuixoteHoverTips
{
    private static readonly Dictionary<string, string[]> DonQuixote_Table = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        // ── 早期卡牌 ──
        ["DonQuixoteHardBloodGuard"] = new[] { "wavering" },
        ["DonQuixoteBloodHunger"] = new[] { "bleed" },
        ["DonQuixoteFragileFragility"] = new[] { "weak", "bleed" },
        ["DonQuixoteHardBloodCape14"] = new[] { "bloodfeast", "wavering" },
        ["DonQuixoteSanchoIdea"] = new[] { "corruption" },
        ["DonQuixoteDuelDeclaration"] = new[] { "corruption", "dueltarget", "tempstrength" },
        ["DonQuixoteKnightDream"] = new[] { "corruption" },
        ["DonQuixoteIrresponsibleDream"] = new[] { "corruption" },
        ["DonQuixoteBurningHunger"] = new[] { "bloodfeast", "impulse", "bleed" },
        ["DonQuixoteForgedOfBlood"] = new[] { "bloodfeast", "hardblood" },
        ["DonQuixoteClotIntoArmor"] = new[] { "bloodfeast", "bleed", "plating", "bloodarmor" },
        ["DonQuixoteFreezeBloodAroundUs"] = new[] { "bloodfeast", "bleed", "bloodarmor", "hardblood" },
        ["DonQuixoteHardBloodPile"] = new[] { "hardblood", "bleed" },
        ["DonQuixoteBePierced"] = new[] { "bloodfeast", "impulse" },
        ["DonQuixoteBloodBackflow"] = new[] { "bloodfeast" },
        ["DonQuixoteFollowerConversion"] = new[] { "follower" },
        ["DonQuixoteFamilyResponsibility"] = new[] { "responsibility" },

        // ── 第二批（补全：卡面描述里出现了对应关键词，但此前没有挂悬停说明） ──
        ["DonQuixoteKnightCharge"] = new[] { "corruption", "dueltarget", "bleed" },
        ["DonQuixoteFeastMoment"] = new[] { "bloodfeast", "impulse", "hardblood" },
        ["DonQuixoteBloodCarousel"] = new[] { "bleed" },
        ["DonQuixoteGiantCharge"] = new[] { "corruption", "vigor" },
        ["DonQuixoteHardBloodForm2"] = new[] { "bloodfeast", "hardblood", "bleed" },
        ["DonQuixoteHardBloodWhip10"] = new[] { "hardblood", "bleed" },
        ["DonQuixoteHardBloodGouge"] = new[] { "impulse", "bleed" },
        ["DonQuixoteBeRunThrough"] = new[] { "bloodfeast", "clot" },
        ["DonQuixoteHardBloodOrb"] = new[] { "bloodfeast", "corruption", "hardblood", "impulse", "vulnerable", "bleed" },
        ["DonQuixoteBloodDesire"] = new[] { "bloodfeast", "impulse", "bleed" },
        ["DonQuixoteJustAWindmill"] = new[] { "bloodfeast", "vulnerable", "hardblood" },
        ["DonQuixoteLatentBlood"] = new[] { "bloodfeast" },
        ["DonQuixoteGoodIdeaForm"] = new[] { "bloodfeast" },
        ["DonQuixoteHardBloodWedge"] = new[] { "bleed", "impulse" },
        ["DonQuixoteShatteredHardBlood"] = new[] { "bleed" },
        ["DonQuixoteEmptyComfortDream"] = new[] { "corruption", "bloodfeast", "wavering" },
        ["DonQuixoteSelfishWishOfFamily"] = new[] { "hardblood", "impulse", "bleed" },
        ["DonQuixoteCrush"] = new[] { "bloodfeast", "hardblood" },
        ["DonQuixoteWound"] = new[] { "bleed" },
        ["DonQuixoteEnergyInfusion"] = new[] { "bloodfeast", "strength", "bloodarmor" },
        ["DonQuixoteSetOutOnAdventure"] = new[] { "corruption", "wavering", "hardblood", "bleed" },
        ["DonQuixoteBetrayalOfKin"] = new[] { "bloodfeast", "impulse", "hardblood", "bleed" },
        ["DonQuixoteBloodJoy"] = new[] { "bleed" },
        ["DonQuixoteEndlessHunger"] = new[] { "bloodfeast", "bleed", "hardblood" },
        ["DonQuixoteBloodCheer"] = new[] { "bloodfeast", "hardblood", "bleed" },
        ["DonQuixoteAllClottedBlood"] = new[] { "bleed" },
        ["DonQuixoteCladInBlood"] = new[] { "bloodarmor" },
        ["DonQuixoteFairDuel"] = new[] { "corruption", "dueltarget" },

        // ── 第三批（第三批新增卡牌与后续补写） ──
        ["DonQuixoteAbandonAll"] = new[] { "wavering" },
        ["DonQuixoteAdventureOver"] = new[] { "bloodfeast", "wavering", "hardblood" },
        ["DonQuixoteAdventureSight"] = new[] { "corruption", "bloodfeast", "vulnerable" },
        ["DonQuixoteBloodFeastLance"] = new[] { "bloodfeast", "bleed" },
        ["DonQuixoteBloodRecast"] = new[] { "bloodrecast" },
        ["DonQuixoteBloodSpread"] = new[] { "bleed" },
        ["DonQuixoteBloodfiendMight"] = new[] { "bloodfiendmight", "tempstrength" },
        ["DonQuixoteCallOfKin"] = new[] { "bloodfeast" },
        ["DonQuixoteCrimsonClaw"] = new[] { "hardblood" },
        ["DonQuixoteDreadfulDisease"] = new[] { "bleed" },
        ["DonQuixoteDuelWithLance"] = new[] { "dueltarget" },
        ["DonQuixoteEndFoolishFight"] = new[] { "vulnerable" },
        ["DonQuixoteEternalThirst"] = new[] { "bloodfeast", "bleed" },
        ["DonQuixoteExtraordinaryGrit"] = new[] { "grit" },
        ["DonQuixoteFatherDeath"] = new[] { "wavering" },
        ["DonQuixoteGuardRiposte"] = new[] { "guardassist" },
        ["DonQuixoteGuardRiposteStrike"] = new[] { "bleed" },
        ["DonQuixoteHardBloodPierce"] = new[] { "hardblood", "bleed" },
        ["DonQuixoteHardBloodRaid"] = new[] { "bloodfeast", "hardblood", "bleed" },
        ["DonQuixoteHardBloodSlash"] = new[] { "bleed" },
        ["DonQuixoteHardBloodTeaching"] = new[] { "hardblood" },
        ["DonQuixoteHardBloodTrack"] = new[] { "bloodfeast", "hardblood", "bleed" },
        ["DonQuixoteHazyMemory"] = new[] { "bloodfeast", "impulse" },
        ["DonQuixoteHope"] = new[] { "hope" },
        ["DonQuixoteKnightLance"] = new[] { "corruption", "bloodarmor", "wavering" },
        ["DonQuixoteLaSangre"] = new[] { "bloodfeast" },
        ["DonQuixoteListenToStories"] = new[] { "corruption", "tempstrength", "wavering" },
        ["DonQuixoteMustAbandon"] = new[] { "bloodfeast", "bloodfeastlock" },
        ["DonQuixoteNoLongerWaver"] = new[] { "wavering" },
        ["DonQuixoteNoReflection"] = new[] { "intangible" },
        ["DonQuixotePleaseHelp"] = new[] { "corruption" },
        ["DonQuixoteReinforceDefense"] = new[] { "reinforceddefense", "wavering" },
        ["DonQuixoteResoluteHeart"] = new[] { "corruption", "braveheart" },
        ["DonQuixoteThreeDayDuel"] = new[] { "corruption", "dueltarget", "duelrenewal" },
        ["DonQuixoteWoundBloom"] = new[] { "wounddestruction" },
    };

    /// <summary>
    /// 会把「不可摧毁卡牌」附魔贴到别的牌上的卡：悬停时把附魔说明挂在旁边。
    /// </summary>
    private static readonly HashSet<string> DonQuixote_IndestructibleCards = new HashSet<string>(StringComparer.Ordinal)
    {
        "DonQuixoteExtraordinaryGrit",
        "DonQuixoteHardBloodTeaching",
    };

    public static List<IHoverTip> DonQuixote_For(CardModel DonQuixote_card)
    {
        List<IHoverTip> DonQuixote_tips = new List<IHoverTip>();

        // 「堕落 X」的牌：把堕落版本整张卡作为悬停说明显示在旁边，
        // 和原版「先制打击」悬停时会显示「小刀」是同一种表现。
        DonQuixote_AddCorruptedPreview(DonQuixote_card, DonQuixote_tips);

        // 「血铸 / 技巧 / 增幅」的牌：把它会给出的骑士长枪也挂在旁边
        DonQuixote_AddDerivativePreviews(DonQuixote_card, DonQuixote_tips);

        // 附魔类效果（不可摧毁卡牌）
        DonQuixote_AddEnchantmentPreviews(DonQuixote_card, DonQuixote_tips);

        string[] DonQuixote_keys;
        if (!DonQuixote_Table.TryGetValue(DonQuixote_card.GetType().Name, out DonQuixote_keys))
        {
            return DonQuixote_tips;
        }

        // 过人的毅力升级后是「每次最高受 2 点」，说明要换成上限型那个 power。
        if (DonQuixote_card is Cards.DonQuixoteExtraordinaryGrit && DonQuixote_card.IsUpgraded)
        {
            DonQuixote_keys = new[] { "gritcap" };
        }

        foreach (string DonQuixote_key in DonQuixote_keys)
        {
            try
            {
                PowerModel DonQuixote_power = DonQuixote_Power(DonQuixote_key);
                if (DonQuixote_power == null)
                {
                    continue;
                }

                IHoverTip DonQuixote_tip = HoverTipFactory.FromPower(DonQuixote_power, DonQuixote_Amount(DonQuixote_card, DonQuixote_key));
                if (DonQuixote_tip != null)
                {
                    DonQuixote_tips.Add(DonQuixote_tip);
                }
            }
            catch (Exception DonQuixote_error)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] hover tip failed for " + DonQuixote_key + ": " + DonQuixote_error.Message, 0);
            }
        }

        return DonQuixote_tips;
    }

    /// <summary>
    /// 把「堕落版本」整张卡挂到悬停说明里，位置紧跟在原卡的说明之后。
    /// 只在卡实现 <see cref="IDonQuixoteCorruptible"/> 时生效；取不到就静默跳过，
    /// 不能因为悬停说明把整张卡的信息面板弄崩。
    /// 原卡已升级时，旁边的堕落版本也显示升级后的样子（数值与费用一起变）。
    /// </summary>
    private static void DonQuixote_AddCorruptedPreview(CardModel DonQuixote_card, List<IHoverTip> DonQuixote_tips)
    {
        if (DonQuixote_card is not IDonQuixoteCorruptible DonQuixote_corruptible)
        {
            return;
        }

        try
        {
            CardModel DonQuixote_canonical = DonQuixote_corruptible.DonQuixote_CorruptedCanonical;
            if (DonQuixote_canonical == null)
            {
                return;
            }

            // 第二个参数是 upgrade：true 时原版会自己 MutableClone + Upgrade，不污染 Canonical 实例。
            IHoverTip DonQuixote_tip = HoverTipFactory.FromCard(DonQuixote_canonical, DonQuixote_card.IsUpgraded);
            if (DonQuixote_tip != null)
            {
                DonQuixote_tips.Add(DonQuixote_tip);
            }
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] corrupted card hover tip failed: " + DonQuixote_error.Message, 0);
        }
    }

    /// <summary>
    /// 「血铸 / 技巧 / 增幅」会在战斗中把骑士长枪（或堕落版的饱食鲜血的长枪）放进手牌，
    /// 这里把那张衍生卡作为悬停说明挂在带这些关键词的卡旁边，方便直接看到它会变成什么。
    /// </summary>
    private static readonly Dictionary<string, Func<CardModel>> DonQuixote_DerivativePreviews =
        new Dictionary<string, Func<CardModel>>(StringComparer.Ordinal)
        {
            ["DonQuixoteCondenseBlood"] = () => ModelDb.Card<DonQuixoteKnightLance>(),
            ["DonQuixoteTacticalTechnique"] = () => ModelDb.Card<DonQuixoteKnightLance>(),
            ["DonQuixoteKnightlyProtection"] = () => ModelDb.Card<DonQuixoteKnightLance>(),
            ["DonQuixoteBloodSweep"] = () => ModelDb.Card<DonQuixoteBloodFeastLance>(),
        };

    private static void DonQuixote_AddDerivativePreviews(CardModel DonQuixote_card, List<IHoverTip> DonQuixote_tips)
    {
        Func<CardModel> DonQuixote_factory;
        if (!DonQuixote_DerivativePreviews.TryGetValue(DonQuixote_card.GetType().Name, out DonQuixote_factory))
        {
            return;
        }

        try
        {
            CardModel DonQuixote_canonical = DonQuixote_factory();
            if (DonQuixote_canonical == null)
            {
                return;
            }

            IHoverTip DonQuixote_tip = HoverTipFactory.FromCard(DonQuixote_canonical, false);
            if (DonQuixote_tip != null)
            {
                DonQuixote_tips.Add(DonQuixote_tip);
            }
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] derivative card hover tip failed: " + DonQuixote_error.Message, 0);
        }
    }

    /// <summary>「不可摧毁卡牌」附魔的悬停说明。</summary>
    private static void DonQuixote_AddEnchantmentPreviews(CardModel DonQuixote_card, List<IHoverTip> DonQuixote_tips)
    {
        if (!DonQuixote_IndestructibleCards.Contains(DonQuixote_card.GetType().Name))
        {
            return;
        }

        try
        {
            foreach (IHoverTip DonQuixote_tip in HoverTipFactory.FromEnchantment<DonQuixoteIndestructibleCard>(1))
            {
                if (DonQuixote_tip != null)
                {
                    DonQuixote_tips.Add(DonQuixote_tip);
                }
            }
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] enchantment hover tip failed: " + DonQuixote_error.Message, 0);
        }
    }

    private static PowerModel DonQuixote_Power(string DonQuixote_key)
    {
        switch (DonQuixote_key)
        {
            case "bloodfeast":
                return ModelDb.Power<DonQuixotePowerBloodFeast>();
            case "bloodfeastlock":
                return ModelDb.Power<DonQuixotePowerBloodFeastLock>();
            case "bleed":
                return ModelDb.Power<DonQuixotePowerBleed>();
            case "wavering":
                return ModelDb.Power<DonQuixotePowerWavering>();
            case "corruption":
                return ModelDb.Power<DonQuixotePowerCorruption>();
            case "impulse":
                return ModelDb.Power<DonQuixotePowerBloodImpulse>();
            case "hardblood":
                return ModelDb.Power<DonQuixotePowerHardBlood>();
            case "bloodarmor":
                return ModelDb.Power<DonQuixotePowerBloodArmor>();
            case "clot":
                return ModelDb.Power<DonQuixotePowerClottingBlood>();
            case "dueltarget":
                return ModelDb.Power<DonQuixotePowerDuelTarget>();
            case "duelrenewal":
                return ModelDb.Power<DonQuixotePowerDuelRenewal>();
            case "follower":
                return ModelDb.Power<DonQuixotePowerFollower>();
            case "responsibility":
                return ModelDb.Power<DonQuixotePowerResponsibility>();
            case "bloodfiendmight":
                return ModelDb.Power<DonQuixotePowerBloodfiendMight>();
            case "bloodrecast":
                return ModelDb.Power<DonQuixotePowerBloodRecast>();
            case "braveheart":
                return ModelDb.Power<DonQuixotePowerBraveHeart>();
            case "grit":
                return ModelDb.Power<DonQuixotePowerGrit>();
            case "gritcap":
                return ModelDb.Power<DonQuixotePowerGritCap>();
            case "guardassist":
                return ModelDb.Power<DonQuixotePowerGuardAssist>();
            case "hope":
                return ModelDb.Power<DonQuixotePowerHope>();
            case "reinforceddefense":
                return ModelDb.Power<DonQuixotePowerReinforcedDefense>();
            case "wounddestruction":
                return ModelDb.Power<DonQuixotePowerWoundDestruction>();
            case "tempstrength":
                // 「临时强壮」是原版状态，模组只覆盖了它的说明文本。
                return ModelDb.Power<TemporaryStrengthPower>();
            case "strength":
                return ModelDb.Power<StrengthPower>();
            case "weak":
                return ModelDb.Power<WeakPower>();
            case "vulnerable":
                return ModelDb.Power<VulnerablePower>();
            case "vigor":
                return ModelDb.Power<VigorPower>();
            case "plating":
                return ModelDb.Power<PlatingPower>();
            case "intangible":
                return ModelDb.Power<IntangiblePower>();
            default:
                return null;
        }
    }

    /// <summary>把卡牌上的对应数值传给提示，让说明里带上具体数字。</summary>
    private static int? DonQuixote_Amount(CardModel DonQuixote_card, string DonQuixote_key)
    {
        string DonQuixote_varName;
        switch (DonQuixote_key)
        {
            case "bloodfeast":
                DonQuixote_varName = "BloodFeast";
                break;
            case "bleed":
                DonQuixote_varName = DonQuixote_card.DynamicVars.ContainsKey("BleedStrength") ? "BleedStrength" : "Bleed";
                break;
            case "wavering":
                DonQuixote_varName = "Wavering";
                break;
            case "hardblood":
                DonQuixote_varName = DonQuixote_card.DynamicVars.ContainsKey("HardBlood") ? "HardBlood" : "Forge";
                break;
            case "bloodarmor":
                DonQuixote_varName = "BloodArmor";
                break;
            case "clot":
                DonQuixote_varName = "Clot";
                break;
            case "strength":
            case "tempstrength":
                DonQuixote_varName = "Strength";
                break;
            case "weak":
                DonQuixote_varName = "Weak";
                break;
            case "vulnerable":
                DonQuixote_varName = "Vulnerable";
                break;
            case "vigor":
                DonQuixote_varName = "Vigor";
                break;
            case "plating":
                DonQuixote_varName = "Plating";
                break;
            case "intangible":
                DonQuixote_varName = "Intangible";
                break;
            case "grit":
                DonQuixote_varName = "Reduction";
                break;
            case "gritcap":
                DonQuixote_varName = "Cap";
                break;
            case "hope":
                DonQuixote_varName = "Hope";
                break;
            case "wounddestruction":
                DonQuixote_varName = "Wounds";
                break;
            default:
                return null;
        }

        DynamicVar DonQuixote_var;
        if (!DonQuixote_card.DynamicVars.TryGetValue(DonQuixote_varName, out DonQuixote_var))
        {
            return null;
        }

        int DonQuixote_value = DonQuixote_var.IntValue;
        return DonQuixote_value > 0 ? DonQuixote_value : (int?)null;
    }
}
