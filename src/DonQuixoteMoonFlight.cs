using System;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 「飞天而奔月」的记账本：只记住「本场战斗里已经打出过」的那张堕落牌。
///
/// 遗物的效果是「每张堕落牌首次被打出后，本场战斗内这张牌费用 -1」，所以折扣必须按
/// 「卡」而不是按「角色」记：原来的写法是给玩家挂一层
/// <see cref="Powers.DonQuixotePowerMoonFlight"/>，然后费用钩子看到这层能力就给所有
/// 堕落牌都减 1，结果抽牌堆里没打过的堕落牌也一起变便宜了。
///
/// 这里用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 按卡牌实例记账：
/// 键是弱引用，卡牌被回收条目自动消失，不需要手动清理；同时记下当时的
/// <see cref="ICombatState"/>，换一场战斗（CombatState 变了）就自动视为没打出过。
/// 状态完全由「出牌」这个已经被同步的事件推导出来，两端算出来的结果一致。
/// </summary>
internal static class DonQuixoteMoonFlightState
{
    private sealed class DonQuixote_PlayedMark
    {
        public ICombatState DonQuixote_combat;
    }

    private static readonly ConditionalWeakTable<CardModel, DonQuixote_PlayedMark> DonQuixote_marks =
        new ConditionalWeakTable<CardModel, DonQuixote_PlayedMark>();

    /// <summary>记下这张牌在本场战斗里被打出过。</summary>
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

    /// <summary>这张牌在本场战斗里是否已经被打出过。</summary>
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
