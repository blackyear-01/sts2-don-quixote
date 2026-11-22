using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using DonQuixote.Monsters;

namespace DonQuixote;

/// <summary>
/// 白月骑士的「挨打反应」：用两段贴图区分「真的掉血」和「被格挡吃干净」。
///
/// 原版伤害结算在 <c>CreatureCmd.Damage</c> 里逐目标依次做：
/// <c>DamageBlockInternal</c>（扣格挡）→ <c>LoseHpInternal</c>（扣血）。
/// 两个都是同步方法，所以在 Block 的后缀里记下「这次挡了多少」，
/// 在 LoseHp 的后缀里一次性判定：
/// ・UnblockedDamage &gt; 0  → 受伤（掉了血）
/// ・否则刚挡过伤害      → 防御（打铁）
///
/// 用后缀而不是重写 <c>CreatureCmd.Damage</c>，是因为它是 async，
/// Harmony 的 async 后缀要额外接管 Task 续体，容易在联机下乱序。
/// </summary>
internal static class DonQuixoteWhiteMoonReactions
{
    private static Creature DonQuixote_PendingBlockTarget;

    private static int DonQuixote_PendingBlocked;

    /// <summary>DamageBlockInternal 后缀：记录这一下被格挡吸收的量（只关心白月骑士）。</summary>
    public static void DonQuixote_NoteBlocked(Creature DonQuixote_Target, decimal DonQuixote_Blocked)
    {
        if (DonQuixote_Target == null || DonQuixote_Blocked <= 0m)
        {
            return;
        }

        if (DonQuixote_Target.Monster is not DonQuixoteWhiteMoonKnight)
        {
            return;
        }

        DonQuixote_PendingBlockTarget = DonQuixote_Target;
        DonQuixote_PendingBlocked = (int)DonQuixote_Blocked;
    }

    /// <summary>LoseHpInternal 后缀：按有没有真掉血决定播哪张图。</summary>
    public static void DonQuixote_NoteHpLoss(Creature DonQuixote_Target, DamageResult DonQuixote_Result)
    {
        if (DonQuixote_Target == null || DonQuixote_Result == null)
        {
            return;
        }

        if (DonQuixote_Target.Monster is not DonQuixoteWhiteMoonKnight)
        {
            return;
        }

        int DonQuixote_Blocked = 0;
        if (ReferenceEquals(DonQuixote_PendingBlockTarget, DonQuixote_Target))
        {
            DonQuixote_Blocked = DonQuixote_PendingBlocked;
        }

        DonQuixote_PendingBlockTarget = null;
        DonQuixote_PendingBlocked = 0;

        try
        {
            if (DonQuixote_Result.UnblockedDamage > 0)
            {
                DonQuixoteWhiteMoonPose.DonQuixote_PlayHurt(DonQuixote_Target);
                DonQuixote_Log($"hurt unblocked={DonQuixote_Result.UnblockedDamage} blocked={DonQuixote_Blocked}");
                return;
            }

            if (DonQuixote_Blocked > 0)
            {
                DonQuixoteWhiteMoonPose.DonQuixote_PlayGuard(DonQuixote_Target);
                DonQuixote_Log($"guard blocked={DonQuixote_Blocked}");
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] white moon reaction failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    private static void DonQuixote_Log(string DonQuixote_Message)
    {
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] white moon " + DonQuixote_Message, 0);
    }
}

/// <summary>扣格挡的后缀：只记录，不改结算。</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class DonQuixoteWhiteMoonBlockPatch
{
    private static void Postfix(Creature __instance, decimal __result)
    {
        DonQuixoteWhiteMoonReactions.DonQuixote_NoteBlocked(__instance, __result);
    }
}

/// <summary>扣血的后缀：判定受伤 / 防御。</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class DonQuixoteWhiteMoonHpLossPatch
{
    private static void Postfix(Creature __instance, DamageResult __result)
    {
        DonQuixoteWhiteMoonReactions.DonQuixote_NoteHpLoss(__instance, __result);
    }
}

/// <summary>
/// 意图刷新时（玩家回合开始就会刷）：只要下一手是「意图3」，
/// 就把拉弓动作放到瞄准帧并一直端着，直到这手打完再播射击。
///
/// 意图3 期间挨打也不再切受伤/防御图（见 <see cref="DonQuixoteWhiteMoonPose"/> 的
/// <c>DonQuixote_OathInProgress</c> 判定），所以这一整套拉弓一定完整播完。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
internal static class DonQuixoteWhiteMoonIntentPatch
{
    /// <summary>和 <c>DonQuixoteWhiteMoonKnight.DonQuixote_MoveOath</c> 保持一致。</summary>
    private const string DonQuixote_OathStateId = "WHITE_MOON_OATH";

    private static void Postfix(NCreature __instance)
    {
        try
        {
            Creature DonQuixote_Entity = __instance?.Entity;
            if (DonQuixote_Entity == null || DonQuixote_Entity.Monster is not DonQuixoteWhiteMoonKnight)
            {
                return;
            }

            bool DonQuixote_Oath = DonQuixote_Entity.Monster.NextMove?.StateId == DonQuixote_OathStateId;
            if (DonQuixote_Oath)
            {
                DonQuixoteWhiteMoonPose.DonQuixote_HoldBowAim(DonQuixote_Entity);
                return;
            }

            // 兜底：意图离开 OATH 时如果还端着弓（正常情况下松弦已经收势了），放回闲置。
            if (DonQuixoteWhiteMoonPose.DonQuixote_IsAiming(DonQuixote_Entity))
            {
                DonQuixoteWhiteMoonPose.DonQuixote_ResetOathCycle(DonQuixote_Entity);
                DonQuixoteWhiteMoonPose.DonQuixote_PlayIdle(DonQuixote_Entity);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] white moon intent hook failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}
