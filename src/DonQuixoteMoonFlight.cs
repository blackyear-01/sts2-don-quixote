using System;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「飞天而奔月」的记账本：只记住「本场战斗里已经打出过」的那张堕落牌。
/// </summary>
internal static class DonQuixoteMoonFlightState
{
    private sealed class DonQuixote_PlayedMark
    {
        public ICombatState DonQuixote_combat;
    }

    private static readonly ConditionalWeakTable<CardModel, DonQuixote_PlayedMark> DonQuixote_marks =
        new ConditionalWeakTable<CardModel, DonQuixote_PlayedMark>();

    /// <summary>
    /// 记下这张牌在本场战斗里被打出过。
    /// </summary>
    public static void DonQuixote_MarkPlayed(CardModel DonQuixote_card)
    {
        ICombatState DonQuixote_combat = DonQuixote_card?.CombatState;
        if (DonQuixote_combat == null)
        {
            return;
        }

        if (DonQuixote_marks.TryGetValue(DonQuixote_card, out DonQuixote_PlayedMark DonQuixote_mark))
        {
            DonQuixote_mark.DonQuixote_combat = DonQuixote_combat;
            return;
        }

        DonQuixote_marks.Add(DonQuixote_card, new DonQuixote_PlayedMark
        {
            DonQuixote_combat = DonQuixote_combat,
        });
    }

    /// <summary>
    /// 这张牌在本场战斗里是否已经被打出过。
    /// </summary>
    public static bool DonQuixote_WasPlayedThisCombat(CardModel DonQuixote_card)
    {
        ICombatState DonQuixote_combat = DonQuixote_card?.CombatState;
        if (DonQuixote_combat == null)
        {
            return false;
        }

        return DonQuixote_marks.TryGetValue(DonQuixote_card, out DonQuixote_PlayedMark DonQuixote_mark)
            && ReferenceEquals(DonQuixote_mark.DonQuixote_combat, DonQuixote_combat);
    }
}
