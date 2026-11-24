using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using HarmonyLib;

namespace DonQuixote;

/// <summary>
/// 堕落 X：堕落值达到阈值时。
/// </summary>
internal static class DonQuixoteCorruption
{
    internal static async Task<IEnumerable<CardModel>> DonQuixote_WrapDraw(
        Task<IEnumerable<CardModel>> DonQuixote_task,
        PlayerChoiceContext DonQuixote_context,
        Player DonQuixote_player)
    {
        IEnumerable<CardModel> DonQuixote_result = await DonQuixote_task;
        await DonQuixote_TryCorruptHandOfPlayer(DonQuixote_context, DonQuixote_player);
        return DonQuixote_result;
    }

    internal static async Task DonQuixote_TryCorruptHandOfPlayer(
        PlayerChoiceContext DonQuixote_context,
        Player DonQuixote_player)
    {
        if (DonQuixote_context == null || DonQuixote_player == null)
        {
            return;
        }

        DonQuixotePowerCorruption DonQuixote_power = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerCorruption>(DonQuixote_player.Creature);
        if (DonQuixote_power == null || DonQuixote_power.Amount <= 0)
        {
            return;
        }

        await DonQuixote_TryCorruptHand(DonQuixote_context, DonQuixote_power, DonQuixote_player);
    }

    internal static async Task DonQuixote_TryCorruptOnDraw(
        PlayerChoiceContext DonQuixote_context,
        DonQuixotePowerCorruption DonQuixote_power,
        CardModel DonQuixote_card)
    {
        if (DonQuixote_power == null || DonQuixote_card == null || DonQuixote_card.Owner == null)
        {
            return;
        }

        await DonQuixote_TryCorruptCard(DonQuixote_context, DonQuixote_power.Amount, DonQuixote_card);
    }

    /// <summary>
    /// 把玩家手牌里所有已满足条件的牌都替换掉。
    /// </summary>
    internal static async Task DonQuixote_TryCorruptHand(
        PlayerChoiceContext DonQuixote_context,
        DonQuixotePowerCorruption DonQuixote_power,
        Player DonQuixote_player)
    {
        if (DonQuixote_power == null || DonQuixote_player == null || DonQuixote_context == null)
        {
            return;
        }

        int DonQuixote_corruption = DonQuixote_power.Amount;
        if (DonQuixote_corruption <= 0)
        {
            return;
        }

        CardPile DonQuixote_hand = CardPile.Get(PileType.Hand, DonQuixote_player);
        if (DonQuixote_hand == null || DonQuixote_hand.IsEmpty)
        {
            return;
        }

        List<CardModel> DonQuixote_cards = DonQuixote_hand.Cards.ToList();
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] corrupt scan: corruption=" + DonQuixote_corruption
            + " hand=" + DonQuixote_cards.Count, 0);

        foreach (CardModel DonQuixote_card in DonQuixote_cards)
        {
            await DonQuixote_TryCorruptCard(DonQuixote_context, DonQuixote_corruption, DonQuixote_card);
        }
    }

    private static async Task DonQuixote_TryCorruptCard(
        PlayerChoiceContext DonQuixote_context,
        int DonQuixote_corruption,
        CardModel DonQuixote_card)
    {
        if (DonQuixote_context == null || DonQuixote_card is not IDonQuixoteCorruptible DonQuixote_source)
        {
            return;
        }

        if (DonQuixote_corruption < DonQuixote_source.DonQuixote_CorruptAt)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] corrupt skip " + DonQuixote_card.GetType().Name
                + ": corruption=" + DonQuixote_corruption
                + " < threshold=" + DonQuixote_source.DonQuixote_CorruptAt, 0);
            return;
        }

        Player DonQuixote_owner = DonQuixote_card.Owner;
        if (DonQuixote_owner == null)
        {
            return;
        }

        // 不在任何牌堆里的牌不能「变化」（抽牌刚结束、牌还在搬运。
        if (DonQuixote_card.Pile == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] corrupt skip " + DonQuixote_card.GetType().Name + ": card has no pile", 0);
            return;
        }

        try
        {
            // 附魔要跟着走：原卡带附魔（例如「不可摧毁卡牌」）时。
            EnchantmentModel DonQuixote_enchantment = DonQuixote_card.Enchantment;
            int DonQuixote_enchantAmount = DonQuixote_enchantment != null ? DonQuixote_enchantment.Amount : 0;

            CardModel DonQuixote_replacement = DonQuixote_source.DonQuixote_CorruptedVersion(DonQuixote_owner);
            if (DonQuixote_replacement == null || ReferenceEquals(DonQuixote_replacement, DonQuixote_card))
            {
                return;
            }

            // 升级状态要跟着走：已经升级过的原卡，转化出来的堕落卡也必须是升级状态。
            if (DonQuixote_card.IsUpgraded && !DonQuixote_replacement.IsUpgraded)
            {
                CardCmd.Upgrade(DonQuixote_replacement, CardPreviewStyle.None);
            }

            await CardCmd.Transform(DonQuixote_card, DonQuixote_replacement, CardPreviewStyle.None);

            if (DonQuixote_enchantment != null && DonQuixote_replacement.Enchantment == null)
            {
                EnchantmentModel DonQuixote_fresh = DonQuixote_enchantment.CanonicalInstance != null
                    ? DonQuixote_enchantment.CanonicalInstance.ToMutable()
                    : DonQuixote_enchantment;
                CardCmd.Enchant(DonQuixote_fresh, DonQuixote_replacement, DonQuixote_enchantAmount);
            }
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] corrupted card: " + DonQuixote_source.GetType().Name
                + " -> " + DonQuixote_replacement.GetType().Name
                + " (corruption=" + DonQuixote_corruption
                + (DonQuixote_card.IsUpgraded ? ", upgraded" : "") + ")", 0);
        }
        catch (System.Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] corruption transform failed: " + DonQuixote_ex, 0);
        }
    }
}

/// <summary>
/// 任何牌被放进手牌之后都扫描一次手牌：抽牌之外还有大量来源会把牌直接塞进手牌 （血铸/技巧/增幅把骑士长枪加入手牌、各种「生成一张牌加入手牌」的效果）。
/// </summary>
internal static class DonQuixoteHandAddCorruption
{
    internal static async Task<CardPileAddResult> DonQuixote_WrapAdd(
        Task<CardPileAddResult> DonQuixote_task,
        CardModel DonQuixote_card)
    {
        CardPileAddResult DonQuixote_result = await DonQuixote_task;
        if (DonQuixote_card != null)
        {
            await DonQuixote_Scan(DonQuixote_card.Owner);
        }
        return DonQuixote_result;
    }

    internal static async Task<IReadOnlyList<CardPileAddResult>> DonQuixote_WrapAddMany(
        Task<IReadOnlyList<CardPileAddResult>> DonQuixote_task,
        IEnumerable<CardModel> DonQuixote_cards)
    {
        IReadOnlyList<CardPileAddResult> DonQuixote_result = await DonQuixote_task;
        if (DonQuixote_cards != null)
        {
            foreach (Player DonQuixote_player in DonQuixote_cards
                .Where(DonQuixote_c => DonQuixote_c != null && DonQuixote_c.Owner != null)
                .Select(DonQuixote_c => DonQuixote_c.Owner)
                .Distinct())
            {
                await DonQuixote_Scan(DonQuixote_player);
            }
        }
        return DonQuixote_result;
    }

    private static async Task DonQuixote_Scan(Player DonQuixote_player)
    {
        if (DonQuixote_player == null)
        {
            return;
        }

        try
        {
            await DonQuixoteCorruption.DonQuixote_TryCorruptHandOfPlayer(
                new BlockingPlayerChoiceContext(),
                DonQuixote_player);
        }
        catch (System.Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] hand corruption scan failed: " + DonQuixote_ex, 0);
        }
    }
}

/// <summary>
/// 牌被放进手牌后（PileType 重载）。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "Add", new[]
{
    typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)
})]
internal static class DonQuixoteAddToHandPileTypePatch
{
    private static void Postfix(CardModel __0, PileType __1, ref Task<CardPileAddResult> __result)
    {
        if (__result == null || __0 == null || __1 != PileType.Hand)
        {
            return;
        }

        __result = DonQuixoteHandAddCorruption.DonQuixote_WrapAdd(__result, __0);
    }
}

/// <summary>
/// 牌被放进手牌后（CardPile 重载）。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "Add", new[]
{
    typeof(CardModel), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)
})]
internal static class DonQuixoteAddToHandPilePatch
{
    private static void Postfix(CardModel __0, CardPile __1, ref Task<CardPileAddResult> __result)
    {
        if (__result == null || __0 == null || __1 == null || __1.Type != PileType.Hand)
        {
            return;
        }

        __result = DonQuixoteHandAddCorruption.DonQuixote_WrapAdd(__result, __0);
    }
}

/// <summary>
/// 多张牌被放进手牌后（PileType 重载）。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "Add", new[]
{
    typeof(IEnumerable<CardModel>), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)
})]
internal static class DonQuixoteAddManyToHandPileTypePatch
{
    private static void Postfix(IEnumerable<CardModel> __0, PileType __1, ref Task<IReadOnlyList<CardPileAddResult>> __result)
    {
        if (__result == null || __0 == null || __1 != PileType.Hand)
        {
            return;
        }

        __result = DonQuixoteHandAddCorruption.DonQuixote_WrapAddMany(__result, __0);
    }
}

/// <summary>
/// 多张牌被放进手牌后（CardPile 重载）。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "Add", new[]
{
    typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool),
    typeof(bool)
})]
internal static class DonQuixoteAddManyToHandPilePatch
{
    private static void Postfix(IEnumerable<CardModel> __0, CardPile __1, ref Task<IReadOnlyList<CardPileAddResult>> __result)
    {
        if (__result == null || __0 == null || __1 == null || __1.Type != PileType.Hand)
        {
            return;
        }

        __result = DonQuixoteHandAddCorruption.DonQuixote_WrapAddMany(__result, __0);
    }
}

/// <summary>
/// 抽牌结束后立刻扫描手牌，保证堕落转化一定会被检查到。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw), new[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
internal static class DonQuixoteDrawCorruptPatch
{
    private static void Postfix(PlayerChoiceContext __0, Player __2, ref Task<IEnumerable<CardModel>> __result)
    {
        if (__result == null || __2 == null)
        {
            return;
        }

        __result = DonQuixoteCorruption.DonQuixote_WrapDraw(__result, __0, __2);
    }
}
