using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「骑士」词条的固有增伤：拥有该词条的卡，对有「决斗对象」的敌人伤害 +50%。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DonQuixoteKnightDuelBonusPatch
{
    /// <summary>
    /// __2 = target，__3 = dealer，__4 = damage，__6 = cardSource。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(Creature __2, Creature __3, ref decimal __4, CardModel __6)
    {
        if (__4 <= 0m)
        {
            return;
        }

        if (!DonQuixoteKnightDuelBonus.DonQuixote_Applies(__2, __3, __6))
        {
            return;
        }

        __4 *= DonQuixoteKnightDuelBonus.DonQuixote_Multiplier;
    }
}

/// <summary>
/// 判定「骑士」词条的 +50% 增伤这次是否成立。
/// </summary>
internal static class DonQuixoteKnightDuelBonus
{
    public const decimal DonQuixote_Multiplier = 1.5m;

    public static bool DonQuixote_Applies(Creature DonQuixote_target, Creature DonQuixote_dealer, CardModel DonQuixote_cardSource)
    {
        try
        {
            if (DonQuixote_target == null || DonQuixote_dealer == null || DonQuixote_cardSource == null)
            {
                return false;
            }

            // 只有玩家自己打出的骑士卡才算（敌人身上挂同名卡不会触发）。
            if (!DonQuixote_dealer.IsPlayer)
            {
                return false;
            }

            if (!DonQuixoteKeywords.DonQuixote_IsKnightCard(DonQuixote_cardSource))
            {
                return false;
            }

            // 流血是「生命伤害」，不吃这条增伤（旧实现也只作用于卡面攻击）。
            if (DonQuixoteBloodMagic.DonQuixote_IgnoreDamageCap)
            {
                return false;
            }

            return DonQuixoteBloodMagic.DonQuixote_Find<Powers.DonQuixotePowerDuelTarget>(DonQuixote_target) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 自检用：确认描述文本与判定表一致。
    /// </summary>
    public static string DonQuixote_Probe()
    {
        return "multiplier=" + DonQuixote_Multiplier.ToString("0.##");
    }
}
