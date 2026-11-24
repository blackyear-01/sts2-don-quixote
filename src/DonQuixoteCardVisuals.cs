using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 卡框 / 能量图标替换：- 本模组的普通卡统一使用「卡框 1」。
/// </summary>
internal static class DonQuixoteCardVisuals
{
    public const string DonQuixote_FramePath = "res://DonQuixote/images/cards/frame1.png";

    public const string DonQuixote_CorruptedFramePath = "res://DonQuixote/images/cards/frame2.png";

    public const string DonQuixote_EnergyIconPath = "res://DonQuixote/images/ui/energy_icon.png";

    private static readonly Dictionary<string, Texture2D> DonQuixote_Cache = new Dictionary<string, Texture2D>();

    private static Texture2D DonQuixote_Load(string DonQuixote_path)
    {
        Texture2D DonQuixote_texture;
        if (DonQuixote_Cache.TryGetValue(DonQuixote_path, out DonQuixote_texture) && GodotObject.IsInstanceValid(DonQuixote_texture))
        {
            return DonQuixote_texture;
        }

        DonQuixote_texture = ResourceLoader.Load<Texture2D>(DonQuixote_path, null, ResourceLoader.CacheMode.Reuse);
        if (DonQuixote_texture != null)
        {
            DonQuixote_Cache[DonQuixote_path] = DonQuixote_texture;
        }

        return DonQuixote_texture;
    }

    /// <summary>
    /// 是否为本模组的卡（含堕落版本）。
    /// </summary>
    public static bool DonQuixote_IsOurCard(CardModel DonQuixote_card)
    {
        return DonQuixote_card != null && DonQuixote_card.GetType().Namespace == "DonQuixote.Cards";
    }

    /// <summary>
    /// 是否为堕落版本的卡。
    /// </summary>
    public static bool DonQuixote_IsCorrupted(CardModel DonQuixote_card)
    {
        return DonQuixote_card is IDonQuixoteCorruptedCard;
    }

    public static Texture2D DonQuixote_EnergyIcon()
    {
        return DonQuixote_Load(DonQuixote_EnergyIconPath);
    }

    public static Texture2D DonQuixote_FrameFor(CardModel DonQuixote_card)
    {
        if (!DonQuixote_IsOurCard(DonQuixote_card))
        {
            return null;
        }

        return DonQuixote_Load(
            DonQuixote_IsCorrupted(DonQuixote_card) ? DonQuixote_CorruptedFramePath : DonQuixote_FramePath);
    }
}

/// <summary>
/// 卡框：普通卡 -> 卡框 1，堕落卡 -> 卡框 2。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Frame), MethodType.Getter)]
internal static class DonQuixoteCardFramePatch
{
    private static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        Texture2D DonQuixote_texture = DonQuixoteCardVisuals.DonQuixote_FrameFor(__instance);
        if (DonQuixote_texture != null)
        {
            __result = DonQuixote_texture;
        }
    }
}

/// <summary>
/// 卡面费用图标：换成模组的能量图标。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.EnergyIcon), MethodType.Getter)]
internal static class DonQuixoteCardEnergyIconPatch
{
    private static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        if (!DonQuixoteCardVisuals.DonQuixote_IsOurCard(__instance))
        {
            return;
        }

        Texture2D DonQuixote_texture = DonQuixoteCardVisuals.DonQuixote_EnergyIcon();
        if (DonQuixote_texture != null)
        {
            __result = DonQuixote_texture;
        }
    }
}

/// <summary>
/// 卡池能量图标路径：卡池路径由 EnergyColorName 拼出来，这里改成模组的图标。
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.EnergyIconPath), MethodType.Getter)]
internal static class DonQuixoteCardPoolEnergyIconPatch
{
    private static void Postfix(CardPoolModel __instance, ref string __result)
    {
        if (__instance is DonQuixoteCardPool)
        {
            __result = DonQuixoteCardVisuals.DonQuixote_EnergyIconPath;
        }
    }
}

/// <summary>
/// 关键词的升级效果：原版卡在升级前就把关键词抄进本地集合。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetKeywordsWithSources))]
internal static class DonQuixoteKeywordUpgradePatch
{
    private static void Postfix(CardModel __instance, ref IReadOnlySet<CardKeyword> __result)
    {
        if (__instance == null || !DonQuixoteCardVisuals.DonQuixote_IsOurCard(__instance) || !__instance.IsUpgraded)
        {
            return;
        }

        bool DonQuixote_dropsExhaust =
            __instance is Cards.DonQuixoteFollowerConversion || __instance is Cards.DonQuixoteBeRunThrough;

        bool DonQuixote_gainsInnate =
            __instance is Cards.DonQuixoteKnightDream
            || __instance is Cards.DonQuixoteFamilyResponsibility
            || __instance is Cards.DonQuixoteIrresponsibleDream;

        if (!DonQuixote_dropsExhaust && !DonQuixote_gainsInnate)
        {
            return;
        }

        HashSet<CardKeyword> DonQuixote_keywords = __result == null
            ? new HashSet<CardKeyword>()
            : new HashSet<CardKeyword>(__result);

        if (DonQuixote_dropsExhaust)
        {
            DonQuixote_keywords.Remove(CardKeyword.Exhaust);
        }

        if (DonQuixote_gainsInnate)
        {
            DonQuixote_keywords.Add(CardKeyword.Innate);
        }

        __result = DonQuixote_keywords;
    }
}
