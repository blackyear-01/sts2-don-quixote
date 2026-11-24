using System;
using DonQuixote.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interactions.RightClick;

namespace DonQuixote;

/// <summary>
/// 右键交互：持有「向本能堕落的骑士」时，右键手牌消耗 30 点血宴，为其附魔「不可摧毁卡牌」。
/// </summary>
internal static class DonQuixoteRightClick
{
    public const int DonQuixote_EnchantCost = 30;

    /// <summary>
    /// 优先级高于 RitsuLib 内置的右键同步处理器（int.MinValue）。
    /// </summary>
    private sealed class DonQuixoteEnchantHandler : IModRightClickHandler
    {
        public int Priority => 1000;

        public bool TryHandle(ModRightClickContext DonQuixote_context)
        {
            return DonQuixoteRightClick.DonQuixote_TryHandle(DonQuixote_context);
        }
    }

    private static DonQuixoteEnchantHandler DonQuixote_handler;

    public static void DonQuixote_Register()
    {
        try
        {
            DonQuixoteEnchantAction.DonQuixote_Register();

            DonQuixote_handler = new DonQuixoteEnchantHandler();
            ModRightClickRegistry.Register(DonQuixote_handler);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] right click enchant registered", 0);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] right click register failed: " + DonQuixote_ex.Message, 0);
        }
    }

    private static bool DonQuixote_TryHandle(ModRightClickContext DonQuixote_context)
    {
        if (DonQuixote_context.Model is not CardModel DonQuixote_card)
        {
            return false;
        }

        Player DonQuixote_player = DonQuixote_context.Player;
        if (!DonQuixote_CanHandle(DonQuixote_player, DonQuixote_card))
        {
            return false;
        }

        // 只有玩家完全掌控回合（CombatPlayPhaseOnly）时才能入队。
        MegaCrit.Sts2.Core.Runs.RunManager DonQuixote_run = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
        if (DonQuixote_run == null)
        {
            return false;
        }

        if (MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress
            && DonQuixote_run.ActionQueueSynchronizer != null
            && DonQuixote_run.ActionQueueSynchronizer.CombatState
                != MegaCrit.Sts2.Core.Entities.Multiplayer.ActionSynchronizerCombatState.PlayPhase)
        {
            return false;
        }

        // 发起同步行动；单机时内部会直接本地结算。
        return DonQuixoteEnchantAction.DonQuixote_Request(DonQuixote_player, DonQuixote_card);
    }

    private static bool DonQuixote_CanHandle(Player DonQuixote_player, CardModel DonQuixote_card)
    {
        if (DonQuixote_player?.Creature == null || DonQuixote_card?.Owner == null)
        {
            return false;
        }

        if (!ReferenceEquals(DonQuixote_card.Owner, DonQuixote_player))
        {
            return false;
        }

        if (DonQuixote_card.Enchantment != null)
        {
            return false;
        }

        if (DonQuixote_card.Pile == null)
        {
            return false;
        }

        if (DonQuixote_FindRelic<DonQuixoteKnightOfInstinct>(DonQuixote_player) == null)
        {
            return false;
        }

        return DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_player.Creature) >= DonQuixote_EnchantCost;
    }

    private static T DonQuixote_FindRelic<T>(Player DonQuixote_player) where T : RelicModel
    {
        if (DonQuixote_player?.Relics == null)
        {
            return null;
        }

        foreach (RelicModel DonQuixote_relic in DonQuixote_player.Relics)
        {
            if (DonQuixote_relic is T DonQuixote_match)
            {
                return DonQuixote_match;
            }
        }

        return null;
    }
}
