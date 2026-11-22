using System;
using System.Reflection;
using DonQuixote.Cards;
using DonQuixote.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 卡面写「需消耗 X 点硬血才能打出」的卡。
/// 和「血铸」不同：血铸是打出去后按实际消耗加长枪伤害，硬血不足也能打；
/// 这个接口是**出牌门槛**，硬血不足时整张牌不能打出。
/// </summary>
public interface IDonQuixoteHardBloodCost
{
    int DonQuixote_HardBloodCost { get; }
}

/// <summary>
/// 血宴相关的「能不能打出」判定。
///
/// 现在的规则（卡牌.docx / 效果.docx 更新后）：
/// 1. 「血宴 X」是**条件触发**——血宴不足时卡牌照样能打出，只是不触发那一段效果，
///    所以默认不再拦截。
/// 2. 「血铸 X」同理：硬血不足也能打出，按实际消耗的硬血算增伤。
/// 3. 仍然会拦截的只剩两种：
///    - 「为了共存必须舍弃」挂的「血宴封锁」；
///    - 卡面明写「需消耗 X 点血宴才能打出」的卡（<see cref="DonQuixoteBloodFeastCard.DonQuixote_BloodFeastMustPay"/>）。
/// </summary>
internal static class DonQuixoteBloodFeastGate
{
    private static bool DonQuixote_LoggedOnce;

    public static void DonQuixote_Register(Harmony DonQuixote_Harmony)
    {
        MethodInfo DonQuixote_target = AccessTools.Method(
            typeof(CardModel),
            "CanPlay",
            new[] { typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType() });

        if (DonQuixote_target == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] CardModel.CanPlay(out reason, out preventer) not found, blood feast gate disabled",
                0);
            return;
        }

        MethodInfo DonQuixote_postfix = typeof(DonQuixoteBloodFeastGate).GetMethod(
            nameof(DonQuixote_CanPlayPostfix),
            BindingFlags.Static | BindingFlags.NonPublic);

        DonQuixote_Harmony.Patch(DonQuixote_target, postfix: new HarmonyMethod(DonQuixote_postfix));

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] blood feast gate patch applied",
            0);
    }

    private static void DonQuixote_CanPlayPostfix(
        CardModel __instance,
        ref bool __result,
        ref UnplayableReason __0,
        ref AbstractModel __1)
    {
        if (!__result)
        {
            return;
        }

        Creature DonQuixote_owner = __instance.Owner?.Creature;
        if (DonQuixote_owner == null)
        {
            return;
        }

        // 「为了共存必须舍弃」：本回合禁止再打出消耗血宴的牌。
        if (DonQuixote_IsBloodFeastConsumer(__instance)
            && DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerBloodFeastLock>(DonQuixote_owner) > 0)
        {
            __result = false;
            __0 = UnplayableReason.BlockedByCardLogic;
            __1 = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBloodFeastLock>(DonQuixote_owner);
            return;
        }

        // 「需消耗 X 点硬血才能打出」：硬血不足时直接不能打出。
        if (__instance is IDonQuixoteHardBloodCost DonQuixote_hardCost)
        {
            int DonQuixote_need = DonQuixote_hardCost.DonQuixote_HardBloodCost;
            if (DonQuixote_need > 0
                && DonQuixoteBloodMagic.DonQuixote_HardBlood(DonQuixote_owner) < DonQuixote_need)
            {
                __result = false;
                __0 = UnplayableReason.BlockedByCardLogic;
                __1 = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerHardBlood>(DonQuixote_owner);
                return;
            }
        }

        if (__instance is not DonQuixoteBloodFeastCard DonQuixote_card)
        {
            return;
        }

        // 只有卡面明写「需消耗 X 点血宴才能打出」的卡才继续拦截（La Sangre）。
        if (!DonQuixote_card.DonQuixote_BloodFeastMustPay)
        {
            return;
        }

        int DonQuixote_cost = DonQuixote_card.DonQuixote_BloodFeastCost;
        if (DonQuixote_cost <= 0)
        {
            return;
        }

        if (DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_owner) >= DonQuixote_cost)
        {
            return;
        }

        // 场上有血袋时，不足的血宴由生命代付，仍然可以打出。
        if (DonQuixoteBloodMagic.DonQuixote_BloodBagCount(DonQuixote_owner) > 0 && !DonQuixote_owner.IsDead)
        {
            return;
        }

        __result = false;
        __0 = UnplayableReason.BlockedByCardLogic;
        __1 = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBloodFeast>(DonQuixote_owner);

        if (!DonQuixote_LoggedOnce)
        {
            DonQuixote_LoggedOnce = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] blood feast gate active: card blocked because blood feast is too low",
                0);
        }
    }

    /// <summary>血宴封锁只对「会消耗血宴」的卡生效：血宴卡本身，以及写有「消耗至多 X 点血宴」的卡。</summary>
    private static bool DonQuixote_IsBloodFeastConsumer(CardModel DonQuixote_card)
    {
        if (DonQuixote_card is DonQuixoteBloodFeastCard)
        {
            return true;
        }

        try
        {
            return DonQuixote_card.DynamicVars.ContainsKey("MaxFeast");
        }
        catch (Exception)
        {
            return false;
        }
    }
}
