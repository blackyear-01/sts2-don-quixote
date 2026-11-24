using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// Plays Don Quixote's guard pose when an attack is completely absorbed by block (the base game would play the regular hit pose there).
/// </summary>
internal static class DonQuixoteCombatPose
{
    private const string DonQuixote_DefendAnim = "defend";

    private const string DonQuixote_IdleAnim = "idle_loop";

    private const float DonQuixote_DefendLength = 0.5f;

    private static readonly Dictionary<Creature, DateTime> DonQuixote_RecentGuard = new();

    private static bool DonQuixote_DefendAnimMissing;

    public static void DonQuixote_PlayGuard(Creature DonQuixote_Creature)
    {
        NCreature? DonQuixote_Node = DonQuixote_FindNode(DonQuixote_Creature);
        if (DonQuixote_Node == null || !DonQuixote_Node.HasSpineAnimation)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn("[DonQuixote] guard pose skipped: creature node or spine animation missing", 0);
            return;
        }

        if (DonQuixote_DefendAnimMissing)
        {
            return;
        }

        try
        {
            DonQuixote_Node.SpineAnimation.SetAnimation(DonQuixote_DefendAnim, false, 0);

            // 用户手改的 spine 里可能没有 defend 动画。
            string DonQuixote_current = DonQuixote_Node.SpineAnimation.GetCurrentAnimationName(0);
            if (!string.Equals(DonQuixote_current, DonQuixote_DefendAnim, StringComparison.OrdinalIgnoreCase))
            {
                DonQuixote_DefendAnimMissing = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] spine has no '{DonQuixote_DefendAnim}' animation (current: '{DonQuixote_current}'), guard pose disabled", 0);
                return;
            }

            DonQuixote_Node.SpineAnimation.AddAnimation(DonQuixote_IdleAnim, DonQuixote_DefendLength, true, 0);
            DonQuixote_RecentGuard[DonQuixote_Creature] = DateTime.UtcNow;
            DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] guard pose", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_DefendAnimMissing = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn($"[DonQuixote] guard pose failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>
    /// True when the creature was just fully blocked, in which case the regular hit trigger must not overwrite the guard pose.
    /// </summary>
    public static bool DonQuixote_IsGuarding(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_RecentGuard.TryGetValue(DonQuixote_Creature, out DateTime DonQuixote_Stamp))
        {
            return false;
        }

        if ((DateTime.UtcNow - DonQuixote_Stamp).TotalSeconds > 0.65)
        {
            DonQuixote_RecentGuard.Remove(DonQuixote_Creature);
            return false;
        }

        return true;
    }

    public static void DonQuixote_ClearGuard(Creature DonQuixote_Creature)
    {
        DonQuixote_RecentGuard.Remove(DonQuixote_Creature);
    }

    private static NCreature? DonQuixote_FindNode(Creature DonQuixote_Creature)
    {
        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        if (DonQuixote_Room == null)
        {
            return null;
        }

        foreach (NCreature DonQuixote_Node in DonQuixote_Room.CreatureNodes)
        {
            if (ReferenceEquals(DonQuixote_Node.Entity, DonQuixote_Creature))
            {
                return DonQuixote_Node;
            }
        }

        return null;
    }
}

/// <summary>
/// A guard pose only makes sense when block absorbed the whole attack.
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class DonQuixoteBlockedDamagePatch
{
    private static void Postfix(Creature __instance, decimal __0, decimal __result)
    {
        if (__result <= 0m || __result < __0)
        {
            return;
        }

        DonQuixoteCombatPose.DonQuixote_PlayGuard(__instance);
    }
}

/// <summary>
/// The base game fires the hit trigger for fully blocked attacks.
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class DonQuixoteAnimationTriggerPatch
{
    private static bool Prefix(NCreature __instance, string trigger)
    {
        Creature? DonQuixote_Creature = __instance.Entity;
        if (DonQuixote_Creature == null)
        {
            return true;
        }

        if (trigger == "Hit" && DonQuixoteCombatPose.DonQuixote_IsGuarding(DonQuixote_Creature))
        {
            return false;
        }

        if (trigger == "Hit")
        {
            DonQuixoteCombatPose.DonQuixote_ClearGuard(DonQuixote_Creature);
        }

        return true;
    }
}
