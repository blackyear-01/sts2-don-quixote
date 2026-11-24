using System;
using DonQuixote.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「飞天而奔月」的堕落牌费用折扣。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class DonQuixoteCorruptedCostPatch
{
    private static void Postfix(CardModel __1, ref decimal __result)
    {
        if (__1 == null)
        {
            return;
        }

        // 「心-勇敢的骑士」：拥有骑士词条的卡费用 -1（不叠加，只减 1）。
        if (DonQuixoteKeywords.DonQuixote_IsKnightCard(__1)
            && DonQuixoteBloodMagic.DonQuixote_AmountOf<Powers.DonQuixotePowerBraveHeart>(__1.Owner?.Creature) > 0)
        {
            decimal DonQuixote_knightCost = __result - 1m;
            __result = DonQuixote_knightCost < 0m ? 0m : DonQuixote_knightCost;
            return;
        }

        if (__1 is not IDonQuixoteCorruptedCard)
        {
            return;
        }

        if (!DonQuixoteMoonFlightState.DonQuixote_WasPlayedThisCombat(__1))
        {
            return;
        }

        decimal DonQuixote_cost = __result - 1m;
        __result = DonQuixote_cost < 0m ? 0m : DonQuixote_cost;
    }
}
