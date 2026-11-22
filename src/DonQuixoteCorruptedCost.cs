using System;
using DonQuixote.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「飞天而奔月」的堕落牌费用折扣。
///
/// 一开始是把折扣写进每张堕落牌的 <c>CanonicalEnergyCost</c> 覆写里，但那个值只在
/// 卡牌实例创建（CardEnergyCost 构造）时被读一次，之后玩家再打出第一张堕落牌拿到
/// 折扣，手里那些牌的费用不会重新计算 —— 看起来就是「遗物效果没触发」。
///
/// 改成挂在原版的费用钩子上：只要这张堕落牌本场战斗里已经被打出过（见
/// <see cref="DonQuixoteMoonFlightState"/>），就把它的费用减 1。这个钩子每次计算
/// 费用都会跑，所以手牌/抽牌堆里的牌立刻跟着变，而且折扣只落在真正打出过的那些
/// 牌上，没打过的堕落牌价格不变。
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
