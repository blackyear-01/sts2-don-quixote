using System.Collections.Generic;
using DonQuixote.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 腐化版本的卡（化脓腐烂的怠惰之梦 / 不负责任的梦）只能由堕落值阈值转化而来。
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class DonQuixoteCorruptedCardPoolPatch
{
    private static bool DonQuixote_Logged;

    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance is not DonQuixoteCardPool || __result == null)
        {
            return;
        }

        List<CardModel> DonQuixote_kept = new List<CardModel>();
        bool DonQuixote_dropped = false;

        foreach (CardModel DonQuixote_card in __result)
        {
            // 堕落版本 / 衍生卡 / 「不可通过选卡获得」的卡都不进随机卡池 （最后一种仍然留在卡牌图鉴里。
            if (DonQuixote_card is IDonQuixoteCorruptedCard or IDonQuixoteTokenCard or IDonQuixoteUnselectableCard)
            {
                DonQuixote_dropped = true;
                continue;
            }

            DonQuixote_kept.Add(DonQuixote_card);
        }

        if (DonQuixote_dropped)
        {
            __result = DonQuixote_kept;

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] corrupted cards are excluded from card rewards",
                    0);
            }
        }
    }
}

/// <summary>
/// 堕落版本的卡不进入卡牌图鉴（视为衍生卡）。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.ShouldShowInCardLibrary), MethodType.Getter)]
internal static class DonQuixoteCorruptedCardLibraryPatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (__result && __instance is IDonQuixoteCorruptedCard or IDonQuixoteTokenCard)
        {
            __result = false;
        }
    }
}
