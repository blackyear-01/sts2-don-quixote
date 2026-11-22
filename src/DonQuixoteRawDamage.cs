using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DonQuixote;

/// <summary>
/// 流血是「生命伤害」：无视防御、难以杀灭、无实体、滑溜等一切伤害修正。
/// 伤害仍然走 CreatureCmd.Damage（保证死亡动画/特效正常），
/// 但结算期间由这里把 Hook.ModifyDamage 的结果还原成原始数值。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DonQuixoteRawDamagePatch
{
    private static decimal DonQuixote_rawDamage;

    private static bool DonQuixote_tracking;

    private static void Prefix(
        IRunState __0,
        ICombatState __1,
        Creature __2,
        Creature __3,
        decimal __4,
        ValueProp __5,
        CardModel __6,
        CardPlay __7,
        ModifyDamageHookType __8,
        CardPreviewMode __9)
    {
        if (!DonQuixoteBloodMagic.DonQuixote_IgnoreDamageCap)
        {
            DonQuixote_tracking = false;
            return;
        }

        // 「白月骑士-决斗对象」把这一下完全归零时，这里也不要记原始值 ——
        // 记了就会在 Postfix 里把 0 又还原成原伤害，免疫直接失效。
        if (DonQuixoteWhiteMoonDuelRules.DonQuixote_Zeroed(__2, __3))
        {
            DonQuixote_tracking = false;
            return;
        }

        DonQuixote_tracking = true;
        DonQuixote_rawDamage = __4;
    }

    private static void Postfix(ref decimal __result)
    {
        if (DonQuixote_tracking)
        {
            DonQuixote_tracking = false;
            __result = DonQuixote_rawDamage;
        }
    }
}

/// <summary>
/// 伤害上限（难以杀灭 / 无实体）走 ModifyDamage 就够，但「滑溜」「无实体」还会在
/// 生命损失阶段把数值压到 1，所以生命损失也要还原一次。只对本模组指定的那个目标生效，
/// 避免影响同一帧里其它来源的伤害。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class DonQuixoteRawHpLossPatch
{
    private static decimal DonQuixote_rawHpLoss;

    private static bool DonQuixote_tracking;

    private static void Prefix(
        IRunState __0,
        ICombatState __1,
        Creature __2,
        decimal __3,
        ValueProp __4)
    {
        Creature DonQuixote_target = DonQuixoteRawHpLossState.DonQuixote_Target;
        if (DonQuixote_target == null || !ReferenceEquals(__2, DonQuixote_target))
        {
            DonQuixote_tracking = false;
            return;
        }

        DonQuixote_tracking = true;
        DonQuixote_rawHpLoss = __3;
    }

    private static void Postfix(ref decimal __result)
    {
        if (DonQuixote_tracking)
        {
            DonQuixote_tracking = false;
            __result = DonQuixote_rawHpLoss;
        }
    }
}

/// <summary>记录「当前正在结算无视上限伤害」的目标。</summary>
internal static class DonQuixoteRawHpLossState
{
    public static Creature DonQuixote_Target { get; set; }
}
