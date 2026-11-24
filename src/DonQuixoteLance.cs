using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Cards;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 带「血铸X」关键词的卡：硬血不足时不能打出。
/// </summary>
public interface IDonQuixoteBloodForgeCard
{
    int DonQuixote_ForgeCost { get; }
}

/// <summary>
/// 「血铸X / 技巧X / 增幅X」三个关键词的实现。
/// </summary>
public static class DonQuixoteLance
{
    public const int DonQuixote_CorruptionThreshold = 40;

    /// <summary>
    /// 原版火堆锻造（升级卡牌）用的音效文件名。
    /// </summary>
    private const string DonQuixote_SmithClip = "card_smith.mp3";

    /// <summary>
    /// 叠在锻造音上的一层提示音，让触发更明显。
    /// </summary>
    private const string DonQuixote_ChimeClip = "relic_get.mp3";

    public static int DonQuixote_ForgeBonus(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerLanceForge>(DonQuixote_creature);
    }

    public static int DonQuixote_TechniqueBonus(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerLanceTechnique>(DonQuixote_creature);
    }

    public static int DonQuixote_AmplifyBonus(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerLanceAmplify>(DonQuixote_creature);
    }

    public static bool DonQuixote_HasLanceReplay(Creature DonQuixote_creature)
    {
        return DonQuixote_LanceReplayCount(DonQuixote_creature) > 0;
    }

    /// <summary>
    /// 「如骑士般提供保护」带来的长枪重放次数 = 该状态的层数。
    /// </summary>
    public static int DonQuixote_LanceReplayCount(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerKnightProtection>(DonQuixote_creature);
    }

    public static bool DonQuixote_LanceHitsAll(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerBloodSweep>(DonQuixote_creature) > 0;
    }

    public static async Task DonQuixote_Forge(
        PlayerChoiceContext DonQuixote_ctx, Player DonQuixote_player, int DonQuixote_amount, CardModel DonQuixote_source)
    {
        await DonQuixote_Grant<DonQuixotePowerLanceForge>(DonQuixote_ctx, DonQuixote_player, DonQuixote_amount, DonQuixote_source);
    }

    public static async Task DonQuixote_Technique(
        PlayerChoiceContext DonQuixote_ctx, Player DonQuixote_player, int DonQuixote_amount, CardModel DonQuixote_source)
    {
        await DonQuixote_Grant<DonQuixotePowerLanceTechnique>(DonQuixote_ctx, DonQuixote_player, DonQuixote_amount, DonQuixote_source);
    }

    public static async Task DonQuixote_Amplify(
        PlayerChoiceContext DonQuixote_ctx, Player DonQuixote_player, int DonQuixote_amount, CardModel DonQuixote_source)
    {
        await DonQuixote_Grant<DonQuixotePowerLanceAmplify>(DonQuixote_ctx, DonQuixote_player, DonQuixote_amount, DonQuixote_source);
    }

    private static async Task DonQuixote_Grant<T>(
        PlayerChoiceContext DonQuixote_ctx, Player DonQuixote_player, int DonQuixote_amount, CardModel DonQuixote_source)
        where T : PowerModel
    {
        if (DonQuixote_player?.Creature == null)
        {
            return;
        }

        // 血铸 / 技巧 / 增幅 三个关键词共享同一个「本场已发长枪」标记。
        bool DonQuixote_first = !DonQuixote_LanceAlreadyGranted(DonQuixote_player)
            || !DonQuixote_HasLance(DonQuixote_player);

        if (DonQuixote_amount > 0)
        {
            await PowerCmd.Apply<T>(
                DonQuixote_ctx, DonQuixote_player.Creature, DonQuixote_amount, DonQuixote_player.Creature, DonQuixote_source, false);
        }

        // 每次触发都播火堆锻造（升级）音效。
        DonQuixote_PlayForgeSfx();

        if (DonQuixote_first)
        {
            DonQuixote_MarkLanceGranted(DonQuixote_player);
            await DonQuixote_EnsureLanceInHand(DonQuixote_player, DonQuixote_source);
        }

        // 让手牌/牌堆里两把长枪的卡面数字同步（放在发牌之后，新牌也能立刻显示正确数字）。
        DonQuixote_RefreshLanceStats(DonQuixote_player);
    }

    private static ulong DonQuixote_LanceGrantedNetId;
    private static MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_LanceGrantedCombat;

    private static bool DonQuixote_LanceAlreadyGranted(Player DonQuixote_player)
    {
        if (DonQuixote_player?.Creature?.CombatState == null)
        {
            return false;
        }

        if (DonQuixote_LanceGrantedNetId == 0 || DonQuixote_LanceGrantedCombat == null)
        {
            return false;
        }

        if (!ReferenceEquals(DonQuixote_LanceGrantedCombat, DonQuixote_player.Creature.CombatState))
        {
            return false;
        }

        return DonQuixote_LanceGrantedNetId == DonQuixote_player.NetId;
    }

    private static void DonQuixote_MarkLanceGranted(Player DonQuixote_player)
    {
        if (DonQuixote_player?.Creature?.CombatState == null)
        {
            return;
        }

        DonQuixote_LanceGrantedNetId = DonQuixote_player.NetId;
        DonQuixote_LanceGrantedCombat = DonQuixote_player.Creature.CombatState;
    }

    /// <summary>
    /// 把「血铸 / 技巧 / 增幅」的累计值写进两把长枪的 DynamicVars（只用于卡面显示。
    /// </summary>
    public static void DonQuixote_RefreshLanceStats(Player DonQuixote_player)
    {
        try
        {
            Creature DonQuixote_owner = DonQuixote_player?.Creature;
            if (DonQuixote_owner == null)
            {
                return;
            }

            int DonQuixote_forge = DonQuixote_ForgeBonus(DonQuixote_owner);
            int DonQuixote_technique = DonQuixote_TechniqueBonus(DonQuixote_owner);
            int DonQuixote_amplify = DonQuixote_AmplifyBonus(DonQuixote_owner);

            PileType[] DonQuixote_piles =
            {
                PileType.Hand, PileType.Draw, PileType.Discard, PileType.Deck,
            };

            foreach (PileType DonQuixote_pile in DonQuixote_piles)
            {
                CardPile DonQuixote_cards = null;
                try
                {
                    DonQuixote_cards = DonQuixote_pile.GetPile(DonQuixote_player);
                }
                catch (Exception)
                {
                    DonQuixote_cards = null;
                }

                if (DonQuixote_cards == null)
                {
                    continue;
                }

                foreach (CardModel DonQuixote_card in DonQuixote_cards.Cards.ToList())
                {
                    if (DonQuixote_card is DonQuixoteKnightLance DonQuixote_knightLance)
                    {
                        DonQuixote_SetDisplay(DonQuixote_card, "Damage", 3m + DonQuixote_forge);
                        DonQuixote_SetDisplay(DonQuixote_card, "Hits", 1m + DonQuixote_technique);
                        DonQuixote_SetDisplay(DonQuixote_card, "Armor", 3m + DonQuixote_amplify);
                        DonQuixote_SetDisplay(DonQuixote_card, "Wavering", 3m + DonQuixote_amplify);

                        // 「重放」只在「如骑士般提供保护」挂着时才有，次数 = 该状态的层数。
                        int DonQuixote_replay = DonQuixote_LanceReplayCount(DonQuixote_owner);
                        if (DonQuixote_knightLance.BaseReplayCount != DonQuixote_replay)
                        {
                            DonQuixote_knightLance.BaseReplayCount = DonQuixote_replay;
                        }
                    }
                    else if (DonQuixote_card is DonQuixoteBloodFeastLance)
                    {
                        DonQuixote_SetDisplay(DonQuixote_card, "Damage", 6m + DonQuixote_forge);
                        DonQuixote_SetDisplay(DonQuixote_card, "Hits", 1m + DonQuixote_technique);
                        DonQuixote_SetDisplay(DonQuixote_card, "Triggers", 1m + DonQuixote_amplify);
                    }
                }
            }
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] refresh lance stats failed: " + DonQuixote_error.Message, 0);
        }
    }

    private static void DonQuixote_SetDisplay(CardModel DonQuixote_card, string DonQuixote_name, decimal DonQuixote_value)
    {
        DynamicVar DonQuixote_var;
        if (!DonQuixote_card.DynamicVars.TryGetValue(DonQuixote_name, out DonQuixote_var) || DonQuixote_var == null)
        {
            return;
        }

        DonQuixote_var.BaseValue = DonQuixote_value;
        DonQuixote_var.PreviewValue = DonQuixote_value;
    }

    /// <summary>
    /// 播放原版火堆锻造（铁匠升级卡牌）音效。
    /// </summary>
    public static void DonQuixote_PlayForgeSfx()
    {
        try
        {
            MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager DonQuixote_audio =
                MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager.Instance;
            if (DonQuixote_audio == null)
            {
                return;
            }

            // 主音：火堆锻造。
            DonQuixote_audio.Play(
                DonQuixote_SmithClip, 1.8f, MegaCrit.Sts2.Core.Audio.Debug.PitchVariance.Small);

            // 再叠一层清脆的金属提示音，作为「长枪被强化了」的额外提示。
            DonQuixote_audio.Play(
                DonQuixote_ChimeClip, 0.7f, MegaCrit.Sts2.Core.Audio.Debug.PitchVariance.Small);
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] forge sfx failed: " + DonQuixote_error.Message, 0);
        }
    }

    /// <summary>
    /// 把这次打出的卡「放回手牌」。
    /// </summary>
    public static void DonQuixote_SetResultPileToHand(CardPlay DonQuixote_play)
    {
        try
        {
            System.Reflection.PropertyInfo DonQuixote_property = typeof(CardPlay).GetProperty("ResultPile");
            DonQuixote_property?.SetValue(DonQuixote_play, PileType.Hand);
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] set result pile failed: " + DonQuixote_error.Message, 0);
        }
    }

    /// <summary>
    /// 战斗中的各个牌堆里是否已经有一把长枪：本体「骑士长枪」或堕落版「饱食鲜血的长枪」。
    /// </summary>
    public static bool DonQuixote_HasLance(Player DonQuixote_player)
    {
        if (DonQuixote_player == null)
        {
            return false;
        }

        PileType[] DonQuixote_piles =
        {
            PileType.Hand, PileType.Draw, PileType.Discard, PileType.Play,
        };

        foreach (PileType DonQuixote_type in DonQuixote_piles)
        {
            CardPile DonQuixote_pile = DonQuixote_type.GetPile(DonQuixote_player);
            if (DonQuixote_pile == null)
            {
                continue;
            }

            foreach (CardModel DonQuixote_card in DonQuixote_pile.Cards)
            {
                if (DonQuixote_card is DonQuixoteKnightLance or DonQuixoteBloodFeastLance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 把「骑士长枪」放进手牌（手牌里已经有一张就不再重复给）。
    /// </summary>
    public static async Task DonQuixote_EnsureLanceInHand(Player DonQuixote_player, CardModel DonQuixote_source)
    {
        if (DonQuixote_player?.Creature == null || DonQuixote_player.Creature.CombatState == null)
        {
            return;
        }

        try
        {
            // 去重：手牌 / 抽牌堆 / 弃牌堆 / 消耗堆 / 打出区 里只要已经有一把长枪 （本体「骑士长枪」或堕落版「饱食鲜血的长枪」）就不再发。
            if (DonQuixote_HasLance(DonQuixote_player))
            {
                return;
            }

            CardPile DonQuixote_hand = PileType.Hand.GetPile(DonQuixote_player);

            // 规范模型本身没有牌局作用域，必须借用一张正在战斗里的卡的 CardScope 派生实例。
            MegaCrit.Sts2.Core.Runs.ICardScope DonQuixote_scope = DonQuixote_source?.CardScope;
            if (DonQuixote_scope == null && DonQuixote_hand != null)
            {
                DonQuixote_scope = DonQuixote_hand.Cards.FirstOrDefault()?.CardScope;
            }

            if (DonQuixote_scope == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] ensure lance in hand skipped: no card scope available", 0);
                return;
            }

            CardModel DonQuixote_canonical = ModelDb.Card<DonQuixoteKnightLance>();
            CardModel DonQuixote_lance = DonQuixote_scope.CreateCard(DonQuixote_canonical, DonQuixote_player);
            if (DonQuixote_lance == null)
            {
                return;
            }

            if (DonQuixote_HasLanceReplay(DonQuixote_player.Creature))
            {
                DonQuixote_lance.BaseReplayCount = DonQuixote_LanceReplayCount(DonQuixote_player.Creature);
            }

            // 消耗堆里如果躺着上一把长枪，新的一把继承它的数值（升级状态 / 卡面数值 / 重放）。
            DonQuixote_InheritExhaustedLance(DonQuixote_player, DonQuixote_lance);

            await CardPileCmd.Add(DonQuixote_lance, PileType.Hand, CardPilePosition.Top, null, false);
            DonQuixote_RefreshLanceStats(DonQuixote_player);
            DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] knight lance added to hand");
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] ensure lance in hand failed: " + DonQuixote_error.Message, 0);
        }
    }

    /// <summary>
    /// 新发的长枪继承消耗堆里上一把长枪的数值：升级状态、各个 DynamicVar 的卡面值、以及重放次数。
    /// </summary>
    private static void DonQuixote_InheritExhaustedLance(Player DonQuixote_player, CardModel DonQuixote_newLance)
    {
        try
        {
            if (DonQuixote_player == null || DonQuixote_newLance == null)
            {
                return;
            }

            CardPile DonQuixote_exhaust = PileType.Exhaust.GetPile(DonQuixote_player);
            if (DonQuixote_exhaust == null)
            {
                return;
            }

            CardModel DonQuixote_old = null;
            foreach (CardModel DonQuixote_card in DonQuixote_exhaust.Cards)
            {
                if (DonQuixote_card is DonQuixoteKnightLance or DonQuixoteBloodFeastLance)
                {
                    DonQuixote_old = DonQuixote_card;
                    break;
                }
            }

            if (DonQuixote_old == null)
            {
                return;
            }

            if (DonQuixote_old.IsUpgraded && !DonQuixote_newLance.IsUpgraded)
            {
                DonQuixote_newLance.UpgradeInternal();
            }

            foreach (KeyValuePair<string, DynamicVar> DonQuixote_pair in DonQuixote_old.DynamicVars)
            {
                if (DonQuixote_pair.Value == null)
                {
                    continue;
                }

                DynamicVar DonQuixote_target;
                if (DonQuixote_newLance.DynamicVars.TryGetValue(DonQuixote_pair.Key, out DonQuixote_target)
                    && DonQuixote_target != null)
                {
                    DonQuixote_target.BaseValue = DonQuixote_pair.Value.BaseValue;
                    DonQuixote_target.PreviewValue = DonQuixote_pair.Value.BaseValue;
                }
            }

            if (DonQuixote_newLance.BaseReplayCount < DonQuixote_old.BaseReplayCount)
            {
                DonQuixote_newLance.BaseReplayCount = DonQuixote_old.BaseReplayCount;
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] lance re-issued inherits exhausted copy: upgraded=" + DonQuixote_old.IsUpgraded
                + " replay=" + DonQuixote_old.BaseReplayCount, 0);
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] inherit exhausted lance failed: " + DonQuixote_error.Message, 0);
        }
    }
}
