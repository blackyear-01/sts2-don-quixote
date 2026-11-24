using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using DonQuixote.Powers;

namespace DonQuixote;

/// <summary>
/// 「白月骑士-决斗对象」的攻防隔离（效果.docx / powers.json 里那句 「场上有该状态时。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DonQuixoteWhiteMoonDuelDamagePatch
{
    /// <summary>
    /// __2 = target，__3 = dealer，__4 = damage，__6 = cardSource，__7 = cardPlay。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(Creature? __2, Creature? __3, ref decimal __4, CardModel? __6, CardPlay? __7)
    {
        if (__4 <= 0m)
        {
            return;
        }

        if (DonQuixoteWhiteMoonDuelRules.DonQuixote_Zeroed(__2, __3, __6, __7))
        {
            DonQuixoteWhiteMoonDuelRules.DonQuixote_Probe(
                "[DonQuixote] duel isolation: zeroed " + __4.ToString("0.#") + " from "
                + (__3 != null ? __3.GetType().Name : (__6 != null ? __6.GetType().Name : "unknown")));
            __4 = 0m;
        }
    }

    /// <summary>
    /// 收尾再兜一次：<see cref="Hook.ModifyDamage"/> 内部第一段会给「附魔」加 EnchantDamageAdditive。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(
        Creature? __2, Creature? __3, CardModel? __6, CardPlay? __7, ref decimal __result)
    {
        if (__result <= 0m)
        {
            return;
        }

        if (DonQuixoteWhiteMoonDuelRules.DonQuixote_Zeroed(__2, __3, __6, __7))
        {
            __result = 0m;
        }
    }
}

/// <summary>
/// 判定：这一次伤害是不是应该被「白月骑士-决斗对象」压成 0。
/// </summary>
internal static class DonQuixoteWhiteMoonDuelRules
{
    public static bool DonQuixote_Zeroed(Creature? DonQuixote_target, Creature? DonQuixote_dealer)
    {
        return DonQuixote_Zeroed(DonQuixote_target, DonQuixote_dealer, null, null);
    }

    /// <summary>
    /// 同 <see cref="DonQuixote_Zeroed(Creature?,Creature?)"/>.
    /// </summary>
    public static bool DonQuixote_Zeroed(
        Creature? DonQuixote_target,
        Creature? DonQuixote_dealer,
        CardModel? DonQuixote_cardSource,
        CardPlay? DonQuixote_cardPlay)
    {
        try
        {
            Creature? DonQuixote_source = DonQuixote_dealer;
            if (DonQuixote_source == null)
            {
                DonQuixote_source = DonQuixote_cardPlay?.Player?.Creature
                    ?? DonQuixote_cardSource?.Owner?.Creature;
            }

            // 自己打自己（自伤 / 反噬）不归这条规则管。
            if (DonQuixote_target != null && ReferenceEquals(DonQuixote_target, DonQuixote_source))
            {
                return false;
            }

            Creature? DonQuixote_barry = null;
            if (DonQuixote_target != null && DonQuixote_IsWhiteMoonKnight(DonQuixote_target))
            {
                DonQuixote_barry = DonQuixote_target;
            }
            else if (DonQuixote_source != null && DonQuixote_IsWhiteMoonKnight(DonQuixote_source))
            {
                DonQuixote_barry = DonQuixote_source;
            }

            if (DonQuixote_barry == null)
            {
                return false;
            }

            // 「场上有这个效果时」：至少还有一名活着的玩家挂着这个状态。
            if (!DonQuixote_AnyMarked(DonQuixote_barry))
            {
                // 诊断：巴里在场上但一条标记都查不到。
                DonQuixoteWhiteMoonDuelRules.DonQuixote_Probe(
                    "[DonQuixote] duel gate open: no marked player found while barry is in play");
                return false;
            }

            if (ReferenceEquals(DonQuixote_source, DonQuixote_barry))
            {
                // 巴里出手：只打得中挂了标记的人。
                return DonQuixote_target != null && !DonQuixote_Marked(DonQuixote_target);
            }

            // 巴里挨打：没有标记的玩家伤不到它。
            return DonQuixote_source != null && !DonQuixote_Marked(DonQuixote_source);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool DonQuixote_IsWhiteMoonKnight(Creature DonQuixote_creature)
    {
        try
        {
            if (DonQuixote_creature == null)
            {
                return false;
            }

            if (DonQuixote_creature.Monster is Monsters.DonQuixoteWhiteMoonKnight)
            {
                return true;
            }

            // 兜底：某些战况下 Creature.Monster 可能是包装过的模型，类型匹配不上。
            string? DonQuixote_monsterName = DonQuixote_creature.Monster?.GetType().Name;
            if (DonQuixote_monsterName != null
                && DonQuixote_monsterName.IndexOf("WhiteMoonKnight", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerWhiteMoonKnight>(DonQuixote_creature) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool DonQuixote_Marked(Creature? DonQuixote_creature)
    {
        try
        {
            if (DonQuixote_creature == null)
            {
                return false;
            }

            return DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerWhiteMoonDuel>(DonQuixote_creature) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 场上（玩家侧）还有没有人挂着这个状态。
    /// </summary>
    public static bool DonQuixote_AnyMarked(Creature DonQuixote_context)
    {
        try
        {
            MegaCrit.Sts2.Core.Combat.ICombatState? DonQuixote_state = DonQuixote_context?.CombatState;
            if (DonQuixote_state?.Creatures == null)
            {
                return false;
            }

            foreach (Creature DonQuixote_player in DonQuixote_state.Creatures)
            {
                if (DonQuixote_player == null || !DonQuixote_player.IsAlive || !DonQuixote_player.IsPlayer)
                {
                    continue;
                }

                if (DonQuixote_Marked(DonQuixote_player))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
        }

        return false;
    }

    private static int DonQuixote_probeBudget = 40;

    /// <summary>
    /// 限流的诊断输出（每局最多 40 条），用于定位「隔离没生效」是哪一段没命中。
    /// </summary>
    public static void DonQuixote_Probe(string DonQuixote_message)
    {
        if (DonQuixote_probeBudget <= 0)
        {
            return;
        }

        DonQuixote_probeBudget--;
        try
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(DonQuixote_message, 0);
        }
        catch (Exception)
        {
        }
    }
}
