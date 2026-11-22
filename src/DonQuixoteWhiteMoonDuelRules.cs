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
/// 「白月骑士-决斗对象」的攻防隔离（效果.docx / powers.json 里那句
/// 「场上有该状态时，白月骑士对未拥有该状态单位造成与受到的伤害降低为0」）。
///
/// 两个方向一起管：
///  ・巴里出手 —— 只打得中挂了这个状态的玩家，其他人一律 0；
///  ・巴里挨打 —— 没挂这个状态的玩家打不出伤害（近战、能力、流血都算）。
///
/// 为什么做在 <see cref="Hook.ModifyDamage"/> 的 Prefix 上
/// ------------------------------------------------
/// ・这是所有伤害的唯一汇合点，卡牌 / 能力 / 流血 / 怪物意图都会经过它；
/// ・必须是 Prefix（改传入的伤害值），不能像「不可摧毁 -80%」那样改 __result ——
///   <see cref="DonQuixoteRawDamagePatch"/> 会在 Postfix 里把结果还原成传入值
///   （流血无视一切修正），两个 Postfix 谁先谁后不确定，改 __result 会被它抹掉。
///   改传入值就与顺序无关：那个补丁的 Prefix 也会先问一句 <see cref="DonQuixoteWhiteMoonDuelRules.DonQuixote_Zeroed"/>，
///   被归零的伤害不会再被还原。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DonQuixoteWhiteMoonDuelDamagePatch
{
    /// <summary>__2 = target，__3 = dealer，__4 = damage，__6 = cardSource，__7 = cardPlay。</summary>
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
    /// 收尾再兜一次：<see cref="Hook.ModifyDamage"/> 内部第一段会给「附魔」加
    /// <c>EnchantDamageAdditive</c>，把归零后的 0 又加回正数，光靠 Prefix 挡不住。
    /// 这里在最终结果上再归零一次，保证「无标记单位伤不到巴里」是硬约束。
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
/// 纯函数，补丁与诊断都可以直接调。
/// </summary>
internal static class DonQuixoteWhiteMoonDuelRules
{
    public static bool DonQuixote_Zeroed(Creature? DonQuixote_target, Creature? DonQuixote_dealer)
    {
        return DonQuixote_Zeroed(DonQuixote_target, DonQuixote_dealer, null, null);
    }

    /// <summary>
    /// 同 <see cref="DonQuixote_Zeroed(Creature?,Creature?)"/>，但允许在没有 dealer 时
    /// 从卡牌 / 本次打出的卡反查来源。某些伤害路径只把来源放在 <c>cardPlay</c> / <c>cardSource</c> 上，
    /// 只靠 dealer 参数会漏判成「没有来源」，于是没挂标记的玩家就能打伤巴里。
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
            // 否则巴里会变成谁也打不动、谁也打不到 —— 那不是设计意图。
            if (!DonQuixote_AnyMarked(DonQuixote_barry))
            {
                // 诊断：巴里在场上但一条标记都查不到。正常情况只在「标记已全部到期」时出现；
                // 如果战斗中频繁出现而场上明明有标记，就说明标记没挂到玩家身上（施加端的问题）。
                DonQuixoteWhiteMoonDuelRules.DonQuixote_Probe(
                    "[DonQuixote] duel gate open: no marked player found while barry is in play");
                return false;
            }

            if (ReferenceEquals(DonQuixote_source, DonQuixote_barry))
            {
                // 巴里出手：只打得中挂了标记的人。
                return DonQuixote_target != null && !DonQuixote_Marked(DonQuixote_target);
            }

            // 巴里挨打：没有标记的玩家伤不到它。没有来源（环境伤害等）不拦。
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
            // 按类型名再认一次，避免整条隔离因为「认不出巴里」而静默失效。
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
    /// 用 <c>Creatures</c> 全表按 <c>IsPlayer</c> 过滤，不用 <c>PlayerCreatures</c> ——
    /// 联机时后者在某些结算环节（客机预测、回放）可能只含本地玩家，
    /// 会让「场上已有标记」误判为 false，于是整条攻防隔离直接失效。
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

    /// <summary>限流的诊断输出（每局最多 40 条），用于定位「隔离没生效」是哪一段没命中。</summary>
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
