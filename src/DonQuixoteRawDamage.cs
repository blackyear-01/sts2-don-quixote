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

/// <summary>
/// 限伤的最后一层：有些 Boss（例如「下界亚波伦」）根本不走 Hook，
/// 而是自己挂一个 <c>Creature.CurrentHp</c> setter 的 Harmony Prefix，
/// 在「写血量」这一步把本次损失量压到它自己的能力层数上。
/// 那种拦截在 Hook 之外，只还原 ModifyDamage / ModifyHpLost 是打不穿的。
///
/// 这里挂在同一个 setter 上，用优先级夹住它：
/// ・Priority.High 的 Prefix 最先执行 → 记下调用方原本要写的值；
/// ・Priority.Low 的 Prefix 最后执行 → 把值改回去。
/// 只在「无视上限伤害」结算期间、且目标正是本次结算对象时生效，
/// 不会影响其它来源的掉血（Boss 自己的阶段转换、回血等）。
/// </summary>
[HarmonyPatch(typeof(Creature), "CurrentHp", MethodType.Setter)]
internal static class DonQuixotePiercingCurrentHpPatch
{
    private static int DonQuixote_rawValue;

    private static bool DonQuixote_tracking;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    private static void Capture(Creature __instance, int value)
    {
        DonQuixote_tracking = false;

        if (!DonQuixoteBloodMagic.DonQuixote_IgnoreDamageCap)
        {
            return;
        }

        Creature DonQuixote_target = DonQuixoteRawHpLossState.DonQuixote_Target;
        if (DonQuixote_target == null || !ReferenceEquals(__instance, DonQuixote_target))
        {
            return;
        }

        DonQuixote_rawValue = value;
        DonQuixote_tracking = true;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Low)]
    private static void Restore(ref int value)
    {
        if (!DonQuixote_tracking)
        {
            return;
        }

        DonQuixote_tracking = false;
        value = DonQuixote_rawValue;
    }
}

/// <summary>
/// 过人的毅力（升级版）的硬上限：整条 <see cref="Hook.ModifyHpLost"/> 跑完后再 clamp 一次。
///
/// 能力自己提供的 ModifyDamageCap / ModifyHpLostAfterOsty 只是链中的一环，
/// 任何「突破限伤」类效果只要排在更晚的阶段（AfterOstyLate、或别的 mod 的 Postfix）
/// 就能把数值改回去。这里作为最终兜底，保证持有者每次生命损失不超过 Amount。
/// Postfix 用 Priority.High：Harmony 的 Postfix 按优先级升序执行，High 最后跑。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class DonQuixoteGritHardCapPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Postfix(Creature __2, ref decimal __result)
    {
        if (__2 == null || __result <= 0m)
        {
            return;
        }

        int DonQuixote_cap = DonQuixoteBloodMagic.DonQuixote_AmountOf<Powers.DonQuixotePowerGritCap>(__2);
        if (DonQuixote_cap <= 0)
        {
            return;
        }

        if (__result > DonQuixote_cap)
        {
            __result = DonQuixote_cap;
        }
    }
}
