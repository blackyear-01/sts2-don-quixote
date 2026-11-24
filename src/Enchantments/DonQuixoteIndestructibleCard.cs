using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Enchantments;

/// <summary>
/// 附魔「不可摧毁卡牌」：此卡被消耗或虚无时。
/// </summary>
[RegisterEnchantment]
public sealed class DonQuixoteIndestructibleCard : ModEnchantmentTemplate
{
    public override string CustomIconPath => "res://DonQuixote/images/enchantments/indestructible_coin.png";

    public override bool HasExtraCardText => true;

    public override bool CanEnchantCardType(CardType DonQuixote_cardType)
    {
        return DonQuixote_cardType == CardType.Attack
            || DonQuixote_cardType == CardType.Skill
            || DonQuixote_cardType == CardType.Power;
    }

    /// <summary>
    /// 被消耗/虚无而自动打出时，造成的最终伤害 -80%。
    /// </summary>
    public override decimal EnchantDamageMultiplicative(decimal DonQuixote_originalDamage, ValueProp DonQuixote_props)
    {
        return 1m;
    }
}

/// <summary>
/// 附魔状态与「消耗→自动打出→进弃牌堆」的实现。
/// </summary>
internal static class DonQuixoteIndestructibleState
{
    /// <summary>
    /// 永久附魔的 Amount（硬血传授这类「给了就一直留着」的卡）。
    /// </summary>
    public const int DonQuixote_PermanentAmount = 1;

    /// <summary>
    /// 临时附魔的 Amount（过人的毅力：只保留到本回合结束，敌方回合结束时回收）。
    /// </summary>
    public const int DonQuixote_TemporaryAmount = 2;

    /// <summary>
    /// 自动打出的嵌套深度。
    /// </summary>
    private static int DonQuixote_AutoPlayDepth;

    /// <summary>
    /// 是否正处在「消耗→自动打出」的流程中（伤害 -80% 用）。
    /// </summary>
    public static bool DonQuixote_AutoPlaying => DonQuixote_AutoPlayDepth > 0;

    /// <summary>
    /// 正在自动打出中的卡，防止消耗流程递归。
    /// </summary>
    private static readonly HashSet<CardModel> DonQuixote_Consuming =
        new HashSet<CardModel>(ReferenceEqualityComparer.Instance);

    public static void DonQuixote_EnterAutoPlay()
    {
        DonQuixote_AutoPlayDepth++;
    }

    public static void DonQuixote_ExitAutoPlay()
    {
        if (DonQuixote_AutoPlayDepth > 0)
        {
            DonQuixote_AutoPlayDepth--;
        }
    }

    /// <summary>
    /// 只有「这张卡自己正在被自动打出」才放行消耗拦截。
    /// </summary>
    public static bool DonQuixote_IsAutoPlaying(CardModel DonQuixote_card)
    {
        return DonQuixote_card != null
            && DonQuixote_AutoPlayDepth > 0
            && DonQuixote_Consuming.Contains(DonQuixote_card);
    }

    /// <summary>
    /// 附魔状态只认卡牌自身的 <see cref="CardModel.Enchantment"/>。
    /// </summary>
    public static bool DonQuixote_HasEnchantment(CardModel DonQuixote_card)
    {
        return DonQuixote_card?.Enchantment is DonQuixoteIndestructibleCard;
    }

    /// <summary>
    /// 消耗一次附魔：只看这张牌现在是否带着「不可摧毁卡牌」。
    /// </summary>
    public static bool DonQuixote_TryUse(CardModel DonQuixote_card)
    {
        return DonQuixote_HasEnchantment(DonQuixote_card);
    }

    /// <summary>
    /// 回收本回合临时挂上的「不可摧毁卡牌」附魔（过人的毅力用）。
    /// </summary>
    public static void DonQuixote_ReclaimTemporary(Creature DonQuixote_creature)
    {
        Player DonQuixote_player = DonQuixote_creature?.Player;
        if (DonQuixote_player == null)
        {
            return;
        }

        int DonQuixote_cleared = 0;
        PileType[] DonQuixote_piles =
        {
            PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust,
        };

        foreach (PileType DonQuixote_type in DonQuixote_piles)
        {
            CardPile DonQuixote_pile = DonQuixote_type.GetPile(DonQuixote_player);
            if (DonQuixote_pile == null)
            {
                continue;
            }

            foreach (CardModel DonQuixote_card in DonQuixote_pile.Cards.ToList())
            {
                if (DonQuixote_card?.Enchantment is DonQuixoteIndestructibleCard DonQuixote_enchant
                    && DonQuixote_enchant.Amount == DonQuixote_TemporaryAmount)
                {
                    CardCmd.ClearEnchantment(DonQuixote_card);
                    DonQuixote_cleared++;
                }
            }
        }

        if (DonQuixote_cleared > 0)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] reclaimed temporary indestructible enchantments: " + DonQuixote_cleared, 0);
        }
    }

    public static async Task<CardPileAddResult?> DonQuixote_ExhaustToDiscard(
        PlayerChoiceContext DonQuixote_ctx,
        CardModel DonQuixote_card,
        bool DonQuixote_markForRedirect = true)
    {
        if (!DonQuixote_Consuming.Add(DonQuixote_card))
        {
            return null;
        }

        // 只有「虚无」那条路径需要这个标记：它的调用方在 Exhaust 之后还会无条件 CardPileCmd.Add(card, Exhaust)。
        if (DonQuixote_markForRedirect)
        {
            DonQuixoteIndestructibleRedirect.DonQuixote_Mark(DonQuixote_card);
        }

        bool DonQuixote_isPower = DonQuixote_card.Type == CardType.Power;
        DonQuixote_EnterAutoPlay();
        try
        {
            // 最左侧的存活敌人。
            Creature DonQuixote_target = null;
            MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state = DonQuixote_card.Owner?.Creature?.CombatState;
            if (DonQuixote_state?.Enemies != null)
            {
                foreach (Creature DonQuixote_enemy in DonQuixote_state.Enemies)
                {
                    if (DonQuixote_enemy != null && !DonQuixote_enemy.IsDead)
                    {
                        DonQuixote_target = DonQuixote_enemy;
                        break;
                    }
                }
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] indestructible card consumed -> auto play " + DonQuixote_card.GetType().Name, 0);

            await CardCmd.AutoPlay(
                DonQuixote_ctx, DonQuixote_card, DonQuixote_target, AutoPlayType.Default, false, false);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] indestructible autoplay failed: " + DonQuixote_ex.Message, 0);
        }
        finally
        {
            // 一定得在 finally 里注销：中途抛异常时如果漏了。
            DonQuixote_Consuming.Remove(DonQuixote_card);
            DonQuixote_ExitAutoPlay();
        }

        if (!DonQuixote_isPower)
        {
            // 「打出后返回手牌」的卡（骑士长枪这类）在 AutoPlay 收尾时已经按 GetResultLocationForCardPlay 回到手牌了。
            CardPile? DonQuixote_pile = DonQuixote_card.Pile;
            if (DonQuixote_pile == null || DonQuixote_pile.Type != PileType.Hand)
            {
                await CardPileCmd.Add(DonQuixote_card, PileType.Discard, CardPilePosition.Top, (AbstractModel)null, true);
            }
            else
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] indestructible autoplay returned to hand: " + DonQuixote_card.GetType().Name, 0);
            }
        }

        // 兜底：自动打出中途失败时卡可能不属于任何牌堆（悬空）。
        if (DonQuixote_card.Pile == null)
        {
            await CardPileCmd.Add(DonQuixote_card, PileType.Discard, CardPilePosition.Top, (AbstractModel)null, true);
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] indestructible card was orphaned, moved to discard: " + DonQuixote_card.GetType().Name, 0);
        }

        return null;
    }

}

/// <summary>
/// 附魔「不可摧毁卡牌」被消耗而导致自动打出时，最终伤害 -80%。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class DonQuixoteIndestructibleDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CardModel __6,
        CardPlay __7,
        CardPreviewMode __9,
        ref decimal __result)
    {
        // 只处理真实结算；手牌上的伤害预览不打折。
        if (__9 != CardPreviewMode.None || __result <= 0m)
        {
            return;
        }

        CardModel? DonQuixote_card = __7?.Card ?? __6;
        if (DonQuixote_card?.Enchantment is not DonQuixoteIndestructibleCard)
        {
            return;
        }

        // 主判定走 CardPlay.IsAutoPlay。
        bool DonQuixote_autoPlaying = __7?.IsAutoPlay == true
            || DonQuixoteIndestructibleState.DonQuixote_IsAutoPlaying(DonQuixote_card);
        if (!DonQuixote_autoPlaying)
        {
            return;
        }

        __result *= 0.2m;
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible autoplay damage x0.2 -> " + __result
            + " (" + DonQuixote_card.GetType().Name + ")", 0);
    }
}

/// <summary>
/// 拦截消耗流程：带有「不可摧毁卡牌」附魔的卡不进入消耗牌堆。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust))]
internal static class DonQuixoteIndestructibleExhaustPatch
{
    private static bool Prefix(
        PlayerChoiceContext __0,
        CardModel __1,
        ref Task<CardPileAddResult?> __result)
    {
        if (__1 == null)
        {
            return true;
        }

        // 只放行「这张卡自己的自动打出」：它打完还要走原版的进堆流程。
        if (DonQuixoteIndestructibleState.DonQuixote_IsAutoPlaying(__1))
        {
            // 例外：「打出后返回手牌」的卡（骑士长枪这类）在 AutoPlay 收尾时已经回到手牌。
            if (__1.Pile?.Type == PileType.Hand)
            {
                DonQuixoteIndestructibleRedirect.DonQuixote_Consume(__1);
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] indestructible exhaust suppressed (back in hand): " + __1.GetType().Name, 0);
                __result = Task.FromResult<CardPileAddResult?>(default);
                return false;
            }

            return true;
        }

        if (!DonQuixoteIndestructibleState.DonQuixote_TryUse(__1))
        {
            return true;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible exhaust intercepted: " + __1.GetType().Name, 0);
        __result = DonQuixoteIndestructibleState.DonQuixote_ExhaustToDiscard(__0, __1, false);
        return false;
    }
}

/// <summary>
/// 虚无 / 未打出的卡离开手牌时走的是这条路径，同样拦下来。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.MoveToResultPileWithoutPlaying))]
internal static class DonQuixoteIndestructibleMovePatch
{
    private static bool Prefix(CardModel __instance, PlayerChoiceContext __0, ref Task __result)
    {
        if (__instance == null || DonQuixoteIndestructibleState.DonQuixote_IsAutoPlaying(__instance))
        {
            return true;
        }

        if (!DonQuixoteIndestructibleState.DonQuixote_TryUse(__instance))
        {
            return true;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible move intercepted: " + __instance.GetType().Name, 0);
        __result = DonQuixoteIndestructibleState.DonQuixote_ExhaustToDiscard(__0, __instance, true);
        return false;
    }
}

/// <summary>
/// 虚无的结算方（CombatManager.ResolveTurnEndCardEffects）在 CardCmd.Exhaust 之后 还会无条件 CardPileCmd.Add(card, PileType.Exhaust.GetPile(player), ...).
/// </summary>
internal static class DonQuixoteIndestructibleRedirect
{
    private static readonly HashSet<CardModel> DonQuixote_Pending =
        new HashSet<CardModel>(ReferenceEqualityComparer.Instance);

    public static void DonQuixote_Mark(CardModel DonQuixote_card)
    {
        if (DonQuixote_card != null)
        {
            DonQuixote_Pending.Add(DonQuixote_card);
        }
    }

    public static bool DonQuixote_Consume(CardModel DonQuixote_card)
    {
        return DonQuixote_card != null && DonQuixote_Pending.Remove(DonQuixote_card);
    }

    public static bool DonQuixote_AlreadyInDiscard(CardModel DonQuixote_card)
    {
        if (DonQuixote_card == null || DonQuixote_card.Owner == null)
        {
            return false;
        }

        foreach (CardModel DonQuixote_other in PileType.Discard.GetPile(DonQuixote_card.Owner).Cards)
        {
            if (ReferenceEquals(DonQuixote_other, DonQuixote_card))
            {
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(CardPileCmd), "Add", new[] { typeof(CardModel), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
internal static class DonQuixoteIndestructiblePilePatch
{
    private static bool Prefix(CardModel __0, ref CardPile __1, ref Task<CardPileAddResult> __result)
    {
        // 能力牌被自动打出后不进入弃牌堆，保持原版的消耗堆行为。
        if (__0 == null || __1 == null || __1.Type != PileType.Exhaust || __0.Type == CardType.Power)
        {
            return true;
        }

        if (!DonQuixoteIndestructibleRedirect.DonQuixote_Consume(__0))
        {
            return true;
        }

        // 已经回到手牌的「回手类卡」不要再搬走。
        if (__0.Pile?.Type == PileType.Hand)
        {
            __result = Task.FromResult(default(CardPileAddResult));
            return false;
        }

        Player DonQuixote_owner = __0.Owner;
        if (DonQuixote_owner == null)
        {
            return true;
        }

        if (DonQuixoteIndestructibleRedirect.DonQuixote_AlreadyInDiscard(__0))
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] indestructible skipped duplicate exhaust add: " + __0.GetType().Name, 0);
            __result = Task.FromResult(default(CardPileAddResult));
            return false;
        }

        __1 = PileType.Discard.GetPile(DonQuixote_owner);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible redirected exhaust pile -> discard pile: " + __0.GetType().Name, 0);
        return true;
    }
}

[HarmonyPatch(typeof(CardPileCmd), "Add", new[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
internal static class DonQuixoteIndestructiblePileTypePatch
{
    private static bool Prefix(CardModel __0, ref PileType __1, ref Task<CardPileAddResult> __result)
    {
        if (__0 == null || __1 != PileType.Exhaust || __0.Type == CardType.Power)
        {
            return true;
        }

        if (!DonQuixoteIndestructibleRedirect.DonQuixote_Consume(__0))
        {
            return true;
        }

        // 已经回到手牌的「回手类卡」不要再搬走。
        if (__0.Pile?.Type == PileType.Hand)
        {
            __result = Task.FromResult(default(CardPileAddResult));
            return false;
        }

        if (DonQuixoteIndestructibleRedirect.DonQuixote_AlreadyInDiscard(__0))
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] indestructible skipped duplicate exhaust add: " + __0.GetType().Name, 0);
            __result = Task.FromResult(default(CardPileAddResult));
            return false;
        }

        __1 = PileType.Discard;
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible redirected exhaust -> discard: " + __0.GetType().Name, 0);
        return true;
    }
}
