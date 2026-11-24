using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace DonQuixote;

/// <summary>
/// Shared helpers for the 血魔 (blood demon) keyword family: 血宴 / 堕落值 / 流血 / 复还的血之冲动.
/// </summary>
internal static class DonQuixoteBloodMagic
{
    public const int DonQuixote_CorruptionMax = 100;

    /// <summary>
    /// 每 10 点堕落值使消耗血宴的卡伤害 +5%。
    /// </summary>
    public const decimal DonQuixote_CorruptionDamageStep = 0.05m;

    public static T DonQuixote_Find<T>(Creature creature) where T : PowerModel
    {
        if (creature == null)
        {
            return null;
        }

        return creature.Powers.OfType<T>().FirstOrDefault();
    }

    public static int DonQuixote_AmountOf<T>(Creature creature) where T : PowerModel
    {
        return DonQuixote_Find<T>(creature)?.Amount ?? 0;
    }

    public static int DonQuixote_BloodFeast(Creature creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerBloodFeast>(creature);
    }

    public static int DonQuixote_Corruption(Creature creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerCorruption>(creature);
    }

    /// <summary>
    /// 为 true 时，护甲结算跳过所有修正（摇曳【血魔】专用）。
    /// </summary>
    public static bool DonQuixote_RawBlockGain;

    /// <summary>
    /// 本模组统一规则：所有需要取整的地方一律向上取整。
    /// </summary>
    public static int DonQuixote_CeilHalf(int DonQuixote_value)
    {
        return (DonQuixote_value + 1) / 2;
    }

    /// <summary>
    /// 按百分比计算并向上取整。
    /// </summary>
    public static decimal DonQuixote_CeilPercent(decimal DonQuixote_value, decimal DonQuixote_rate)
    {
        return Math.Ceiling(DonQuixote_value * DonQuixote_rate);
    }

    public static int DonQuixote_BleedLayers(Creature creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerBleedLayers>(creature);
    }

    public static int DonQuixote_BleedStrength(Creature creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerBleed>(creature);
    }

    public static int DonQuixote_BloodImpulse(Creature creature)
    {
        int DonQuixote_amount = DonQuixote_AmountOf<DonQuixotePowerBloodImpulse>(creature);
        return DonQuixote_amount > 30 ? 30 : DonQuixote_amount;
    }

    /// <summary>
    /// 消耗血宴总数：每 1 点让施加流血效果的卡最终伤害 +1%（无上限）。
    /// </summary>
    public static int DonQuixote_FeastTotal(Creature DonQuixote_creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerFeastTotal>(DonQuixote_creature);
    }

    /// <summary>
    /// 消耗血宴总数带来的伤害倍率：每 1 点 +1%。
    /// </summary>
    public static decimal DonQuixote_FeastTotalMultiplier(Creature DonQuixote_creature)
    {
        int DonQuixote_total = DonQuixote_FeastTotal(DonQuixote_creature);
        return 1m + (DonQuixote_total * 0.01m);
    }

    /// <summary>
    /// 累计消耗血宴总数（血宴卡结算时调用）。
    /// </summary>
    public static async Task DonQuixote_AccumulateFeastTotal(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_creature,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_creature == null || DonQuixote_amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerFeastTotal>(
            DonQuixote_ctx, DonQuixote_creature, DonQuixote_amount, DonQuixote_creature, DonQuixote_source, false);

        // 消耗血宴总数是遗物发货的唯一口径：每满 325 点发一张对应的舞台卡。
        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_creature);
    }

    /// <summary>
    /// 堕落值每满 10 点，消耗血宴的卡造成的伤害 +5%。
    /// </summary>
    public static decimal DonQuixote_BloodFeastDamageMultiplier(Creature creature)
    {
        int DonQuixote_steps = DonQuixote_Corruption(creature) / 10;
        return 1m + (DonQuixote_CorruptionDamageStep * DonQuixote_steps);
    }

    /// <summary>
    /// 施加流血 X/Y：X 为强度，Y 为层数。
    /// </summary>
    public static async Task DonQuixote_ApplyBleed(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        Creature DonQuixote_applier,
        CardModel DonQuixote_source,
        int DonQuixote_strength,
        int DonQuixote_layers)
    {
        if (DonQuixote_target == null || (DonQuixote_strength <= 0 && DonQuixote_layers <= 0))
        {
            return;
        }

        // 复还的血之冲动只强化「施加给敌人」的流血。
        bool DonQuixote_onEnemy = DonQuixote_target.IsEnemy;

        int DonQuixote_impulse = DonQuixote_onEnemy
            ? DonQuixote_BloodImpulse(DonQuixote_applier ?? DonQuixote_target)
            : 0;

        if (DonQuixote_impulse > 0)
        {
            DonQuixote_strength += DonQuixote_impulse;
            DonQuixote_layers += DonQuixote_impulse / 5;
        }

        // 蜜拉卡：施加流血强度时额外 +1 级。
        if (DonQuixote_onEnemy
            && DonQuixote_strength > 0
            && DonQuixote_HasRelic<Relics.DonQuixoteMiraka>(DonQuixote_applier ?? DonQuixote_target))
        {
            DonQuixote_strength += 1;
        }

        if (DonQuixote_layers > 0)
        {
            // 施加层数时若当前强度为 0，先 +1 强度再增加层数。
            if (DonQuixote_strength <= 0 && DonQuixote_BleedStrength(DonQuixote_target) <= 0)
            {
                DonQuixote_strength = 1;
            }

            await PowerCmd.Apply<DonQuixotePowerBleedLayers>(
                DonQuixote_ctx, DonQuixote_target, DonQuixote_layers, DonQuixote_applier, DonQuixote_source, false);
        }
        else if (DonQuixote_BleedLayers(DonQuixote_target) <= 0)
        {
            await PowerCmd.Apply<DonQuixotePowerBleedLayers>(
                DonQuixote_ctx, DonQuixote_target, 1m, DonQuixote_applier, DonQuixote_source, false);
        }

        if (DonQuixote_strength > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerBleed>(
                DonQuixote_ctx, DonQuixote_target, DonQuixote_strength, DonQuixote_applier, DonQuixote_source, false);
        }

        DonQuixote_RefreshBleedDisplay(DonQuixote_target);
    }

    /// <summary>
    /// 层数变化后让流血图标上的层数立即刷新。
    /// </summary>
    public static void DonQuixote_RefreshBleedDisplay(Creature DonQuixote_creature)
    {
        DonQuixotePowerBleed DonQuixote_bleed = DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_creature);
        if (DonQuixote_bleed != null)
        {
            DonQuixote_bleed.DonQuixote_RefreshAmountDisplay();
        }
    }

    /// <summary>
    /// 血宴获取量修正：潜伏之血（展现洒落在战场上的潜伏之血）把获得量放大。
    /// </summary>
    public static int DonQuixote_ApplyBloodFeastGainBonus(Creature DonQuixote_target, int DonQuixote_amount)
    {
        if (DonQuixote_target == null || DonQuixote_amount <= 0)
        {
            return DonQuixote_amount;
        }

        int DonQuixote_bonus = DonQuixote_AmountOf<DonQuixotePowerLatentBlood>(DonQuixote_target);
        if (DonQuixote_bonus <= 0)
        {
            return DonQuixote_amount;
        }

        return (int)Math.Ceiling(DonQuixote_amount * (1m + (DonQuixote_bonus / 100m)));
    }

    public static async Task DonQuixote_GainBloodFeast(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_target == null || DonQuixote_amount <= 0)
        {
            return;
        }

        DonQuixote_amount = DonQuixote_ApplyBloodFeastGainBonus(DonQuixote_target, DonQuixote_amount);

        await PowerCmd.Apply<DonQuixotePowerBloodFeast>(
            DonQuixote_ctx, DonQuixote_target, DonQuixote_amount, DonQuixote_target, DonQuixote_source, false);
    }

    public static async Task DonQuixote_ConsumeBloodFeast(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_target == null || DonQuixote_amount <= 0)
        {
            return;
        }

        DonQuixotePowerBloodFeast DonQuixote_feast = DonQuixote_Find<DonQuixotePowerBloodFeast>(DonQuixote_target);
        if (DonQuixote_feast == null)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            DonQuixote_ctx, DonQuixote_feast, -DonQuixote_amount, DonQuixote_target, DonQuixote_source, false);
    }

    /// <summary>
    /// 当前硬血层数。
    /// </summary>
    public static int DonQuixote_HardBlood(Creature DonQuixote_creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerHardBlood>(DonQuixote_creature);
    }

    /// <summary>
    /// 消耗硬血，返回实际消耗掉的层数（硬血不足时按剩余量结算）。
    /// </summary>
    public static async Task<int> DonQuixote_ConsumeHardBlood(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        int DonQuixote_amount,
        bool DonQuixote_forge = false)
    {
        if (DonQuixote_target == null || DonQuixote_amount <= 0)
        {
            return 0;
        }

        DonQuixotePowerHardBlood DonQuixote_hard = DonQuixote_Find<DonQuixotePowerHardBlood>(DonQuixote_target);
        if (DonQuixote_hard == null)
        {
            return 0;
        }

        int DonQuixote_have = DonQuixote_hard.Amount;
        if (DonQuixote_have <= 0)
        {
            return 0;
        }

        int DonQuixote_paid = DonQuixote_have > DonQuixote_amount ? DonQuixote_amount : DonQuixote_have;

        // 光荣的决斗：每场第一次血铸不真的扣硬血，但按全额结算（伤害加成照给）。
        if (DonQuixote_forge && Relics.DonQuixoteGloriousDuel.DonQuixote_TrySpare(DonQuixote_target, DonQuixote_amount))
        {
            return DonQuixote_paid;
        }

        await PowerCmd.ModifyAmount(
            DonQuixote_ctx, DonQuixote_hard, -DonQuixote_paid, DonQuixote_target, (CardModel)null, false);

        return DonQuixote_paid;
    }

    /// <summary>
    /// 每消耗 1 点硬血，消耗硬血的牌基础伤害 +2。
    /// </summary>
    public const decimal DonQuixote_HardBloodDamagePerPoint = 2m;

    /// <summary>
    /// 硬血增伤：「消耗硬血的卡，每消耗1点硬血增加2点基础伤害」。
    /// </summary>
    public static decimal DonQuixote_HardBloodDamageBonus(int DonQuixote_consumed)
    {
        return DonQuixote_consumed <= 0
            ? 0m
            : DonQuixote_consumed * DonQuixote_HardBloodDamagePerPoint;
    }

    /// <summary>
    /// 堂吉诃德是否在场上有血袋（每名血袋可代付不足的血宴）。
    /// </summary>
    public static int DonQuixote_BloodBagCount(Creature DonQuixote_donQuixote)
    {
        if (DonQuixote_donQuixote?.CombatState?.PlayerCreatures == null)
        {
            return 0;
        }

        int DonQuixote_count = 0;
        foreach (Creature DonQuixote_creature in DonQuixote_donQuixote.CombatState.PlayerCreatures)
        {
            if (DonQuixote_creature != null && DonQuixote_Find<DonQuixotePowerBloodBag>(DonQuixote_creature) != null)
            {
                DonQuixote_count++;
            }
        }

        return DonQuixote_count;
    }

    /// <summary>
    /// 支付血宴：先扣血宴，不足的部分若有血袋则用生命代替；返回是否支付成功。
    /// </summary>
    public static async Task<bool> DonQuixote_PayBloodFeast(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_donQuixote,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_donQuixote == null || DonQuixote_amount <= 0)
        {
            return true;
        }

        int DonQuixote_have = DonQuixote_BloodFeast(DonQuixote_donQuixote);
        int DonQuixote_fromFeast = DonQuixote_have > DonQuixote_amount ? DonQuixote_amount : DonQuixote_have;
        int DonQuixote_shortfall = DonQuixote_amount - DonQuixote_fromFeast;

        if (DonQuixote_fromFeast > 0)
        {
            await DonQuixote_ConsumeBloodFeast(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_fromFeast, DonQuixote_source);
        }

        await DonQuixote_AccumulateFeastTotal(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_amount, DonQuixote_source);

        if (DonQuixote_shortfall <= 0)
        {
            return true;
        }

        // 血袋：堂吉诃德自己带血袋时用生命代替；否则找场上有血袋的队友代付。
        if (DonQuixote_BloodBagCount(DonQuixote_donQuixote) > 0)
        {
            await MegaCrit.Sts2.Core.Commands.CreatureCmd.Damage(
                DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_shortfall,
                MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable, DonQuixote_donQuixote, DonQuixote_source, (CardPlay)null);
            return true;
        }

        Creature DonQuixote_bagHolder = DonQuixote_BloodBagHolder(DonQuixote_donQuixote);
        if (DonQuixote_bagHolder == null)
        {
            return false;
        }

        await MegaCrit.Sts2.Core.Commands.CreatureCmd.Damage(
            DonQuixote_ctx, DonQuixote_bagHolder, DonQuixote_shortfall,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable, DonQuixote_donQuixote, DonQuixote_source, (CardPlay)null);
        return true;
    }

    /// <summary>
    /// 结算卡面上写的「消耗至多 X 点血宴」：只扣当前真正拥有的数量。
    /// </summary>
    public static async Task<int> DonQuixote_PayBloodFeastUpTo(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_donQuixote,
        int DonQuixote_max,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_donQuixote == null || DonQuixote_max <= 0)
        {
            return 0;
        }

        int DonQuixote_have = DonQuixote_BloodFeast(DonQuixote_donQuixote);
        if (DonQuixote_have <= 0)
        {
            return 0;
        }

        int DonQuixote_spend = DonQuixote_have > DonQuixote_max ? DonQuixote_max : DonQuixote_have;
        await DonQuixote_ConsumeBloodFeast(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_spend, DonQuixote_source);
        await DonQuixote_AccumulateFeastTotal(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_spend, DonQuixote_source);
        return DonQuixote_spend;
    }

    /// <summary>
    /// 「血宴 X：」条件触发：血宴达到 X 点时扣掉 X 并返回 true。
    /// </summary>
    public static async Task<bool> DonQuixote_TryConsumeBloodFeast(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_donQuixote,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_donQuixote == null || DonQuixote_amount <= 0)
        {
            return true;
        }

        int DonQuixote_have = DonQuixote_BloodFeast(DonQuixote_donQuixote);
        if (DonQuixote_have >= DonQuixote_amount)
        {
            await DonQuixote_ConsumeBloodFeast(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_amount, DonQuixote_source);
            await DonQuixote_AccumulateFeastTotal(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_amount, DonQuixote_source);
            return true;
        }

        // 血宴不够：场上有血袋时用生命补足，仍然算「满足条件」。
        if (DonQuixote_BloodBagCount(DonQuixote_donQuixote) > 0)
        {
            return await DonQuixote_PayBloodFeast(DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_amount, DonQuixote_source);
        }

        return false;
    }

    /// <summary>
    /// 场上第一个带「血袋」的队友（不含自己）。
    /// </summary>
    public static Creature DonQuixote_BloodBagHolder(Creature DonQuixote_donQuixote)
    {
        if (DonQuixote_donQuixote?.CombatState == null)
        {
            return null;
        }

        foreach (Creature DonQuixote_creature in DonQuixote_donQuixote.CombatState.PlayerCreatures)
        {
            if (DonQuixote_creature == null
                || ReferenceEquals(DonQuixote_creature, DonQuixote_donQuixote)
                || DonQuixote_creature.IsDead
                || DonQuixote_creature.Player == null)
            {
                continue;
            }

            if (DonQuixote_BloodBagCount(DonQuixote_creature) > 0)
            {
                return DonQuixote_creature;
            }
        }

        return null;
    }

    /// <summary>
    /// 场上所有眷属（不含堂吉诃德本人）。
    /// </summary>
    public static List<Creature> DonQuixote_Followers(Creature DonQuixote_donQuixote)
    {
        List<Creature> DonQuixote_list = new List<Creature>();
        if (DonQuixote_donQuixote?.CombatState?.PlayerCreatures == null)
        {
            return DonQuixote_list;
        }

        foreach (Creature DonQuixote_creature in DonQuixote_donQuixote.CombatState.PlayerCreatures)
        {
            if (DonQuixote_creature == null || ReferenceEquals(DonQuixote_creature, DonQuixote_donQuixote) || DonQuixote_creature.IsDead)
            {
                continue;
            }

            if (DonQuixote_Find<DonQuixotePowerFollower>(DonQuixote_creature) != null)
            {
                DonQuixote_list.Add(DonQuixote_creature);
            }
        }

        return DonQuixote_list;
    }

    /// <summary>
    /// 获得摇曳[血魔]。
    /// </summary>
    public static async Task DonQuixote_GainWavering(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_donQuixote,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_donQuixote == null || DonQuixote_amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerWavering>(
            DonQuixote_ctx, DonQuixote_donQuixote, DonQuixote_amount, DonQuixote_donQuixote, DonQuixote_source, false);

        if (DonQuixote_Find<DonQuixotePowerResponsibility>(DonQuixote_donQuixote) == null)
        {
            return;
        }

        int DonQuixote_share = DonQuixote_CeilHalf(DonQuixote_amount);
        List<Creature> DonQuixote_followers = DonQuixote_Followers(DonQuixote_donQuixote);
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_followers.Count; DonQuixote_i++)
        {
            await PowerCmd.Apply<DonQuixotePowerWavering>(
                DonQuixote_ctx,
                DonQuixote_followers[DonQuixote_i],
                DonQuixote_share,
                DonQuixote_donQuixote,
                DonQuixote_source,
                false);
        }

        if (DonQuixote_followers.Count > 0)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] responsibility: followers=" + DonQuixote_followers.Count
                + " wavering_share=" + DonQuixote_share + " total=" + DonQuixote_amount,
                0);
        }
    }

    public static async Task DonQuixote_ApplyCorruption(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        int DonQuixote_amount,
        CardModel DonQuixote_source)
    {
        if (DonQuixote_target == null || DonQuixote_amount <= 0)
        {
            return;
        }

        int DonQuixote_current = DonQuixote_Corruption(DonQuixote_target);
        int DonQuixote_room = DonQuixote_CorruptionMax - DonQuixote_current;
        if (DonQuixote_room <= 0)
        {
            return;
        }

        int DonQuixote_apply = DonQuixote_amount > DonQuixote_room ? DonQuixote_room : DonQuixote_amount;
        await PowerCmd.Apply<DonQuixotePowerCorruption>(
            DonQuixote_ctx, DonQuixote_target, DonQuixote_apply, DonQuixote_target, DonQuixote_source, false);
    }

    /// <summary>
    /// 最近一次战斗内的选择上下文，供没有 ctx 参数的钩子（如获得格挡）复用。
    /// </summary>
    public static PlayerChoiceContext DonQuixote_LastContext { get; set; }

    /// <summary>
    /// 找到堂吉诃德玩家的生物（用于把血宴算给她）。
    /// </summary>
    public static Creature DonQuixote_PlayerCreature(Creature DonQuixote_contextCreature)
    {
        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state = DonQuixote_contextCreature?.CombatState;
        if (DonQuixote_state == null)
        {
            return null;
        }

        return DonQuixote_state.PlayerCreatures
            .FirstOrDefault(DonQuixote_creature =>
                DonQuixote_creature?.Player != null
                && DonQuixote_creature.Player.Character is DonQuixoteCharacter);
    }

    /// <summary>
    /// 场上所有玩家角色（活着的），用于把血宴发给每个玩家。
    /// </summary>
    public static List<Creature> DonQuixote_PlayerCreatures(Creature DonQuixote_contextCreature)
    {
        List<Creature> DonQuixote_result = new List<Creature>();
        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state = DonQuixote_contextCreature?.CombatState;
        if (DonQuixote_state == null)
        {
            return DonQuixote_result;
        }

        foreach (Creature DonQuixote_creature in DonQuixote_state.PlayerCreatures)
        {
            if (DonQuixote_creature != null && !DonQuixote_creature.IsDead)
            {
                DonQuixote_result.Add(DonQuixote_creature);
            }
        }

        return DonQuixote_result;
    }

    /// <summary>
    /// 触发一次流血：造成强度等量伤害、给堂吉诃德等量血宴，默认再扣 1 层。
    /// </summary>
    public static async Task DonQuixote_TriggerBleed(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_bleeding,
        bool DonQuixote_consumeLayers = true)
    {
        if (DonQuixote_ctx == null || DonQuixote_bleeding == null || DonQuixote_bleeding.IsDead)
        {
            return;
        }

        // 防重入：流血伤害 / 血宴 本身还会引发钩子，避免自我递归叠加。
        if (DonQuixote_Triggering)
        {
            return;
        }

        DonQuixote_Triggering = true;
        try
        {
            await DonQuixote_TriggerBleedCore(DonQuixote_ctx, DonQuixote_bleeding, DonQuixote_consumeLayers);
        }
        finally
        {
            DonQuixote_Triggering = false;
        }
    }

    public static bool DonQuixote_Triggering { get; private set; }

    /// <summary>
    /// 流血触发时「获得的血宴」倍率。
    /// </summary>
    public static int DonQuixote_FeastGainMultiplier { get; set; } = 1;

    /// <summary>
    /// 为 true 时，本次伤害无视「难以杀灭」之类的伤害上限修正（流血专用）。
    /// </summary>
    public static bool DonQuixote_IgnoreDamageCap { get; set; }

    /// <summary>
    /// 为 true 时，本次伤害结算跳过「虚弱」的减伤（被刺穿吧的百分比伤害专用）。
    /// </summary>
    public static bool DonQuixote_SkipWeakReduction { get; set; }

    /// <summary>
    /// 本次跳过虚弱减伤的伤害来源，用来只屏蔽该角色身上的虚弱。
    /// </summary>
    public static Creature DonQuixote_SkipWeakDealer { get; set; }

    /// <summary>
    /// 把算出来的数值当作「基础攻击伤害」结算：照常吃力量、易损、堕落等增伤修正，但不受虚弱影响。
    /// </summary>
    public static async Task DonQuixote_DamageAsBaseAttack(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_source,
        CardPlay DonQuixote_cardPlay)
    {
        if (DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return;
        }

        // 手算伤害虽然不走原版卡牌伤害管线。
        bool DonQuixote_previousSkip = DonQuixote_SkipWeakReduction;
        Creature DonQuixote_previousDealer = DonQuixote_SkipWeakDealer;
        DonQuixote_SkipWeakReduction = true;
        DonQuixote_SkipWeakDealer = DonQuixote_dealer;
        try
        {
            await CreatureCmd.Damage(
                DonQuixote_ctx,
                DonQuixote_target,
                DonQuixote_amount,
                ValueProp.Move,
                DonQuixote_dealer,
                DonQuixote_source,
                DonQuixote_cardPlay);
        }
        finally
        {
            DonQuixote_SkipWeakReduction = DonQuixote_previousSkip;
            DonQuixote_SkipWeakDealer = DonQuixote_previousDealer;
        }
    }

    /// <summary>
    /// 造成「无视一切减免」的伤害：不改动对方身上的任何状态（以前是摘掉再装回。
    /// </summary>
    public static async Task DonQuixote_DamageIgnoringLimits(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_source,
        CardPlay DonQuixote_cardPlay,
        ValueProp DonQuixote_props = ValueProp.Unblockable)
    {
        if (DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return;
        }

        Creature DonQuixote_previousHpLossTarget = DonQuixoteRawHpLossState.DonQuixote_Target;
        DonQuixoteRawHpLossState.DonQuixote_Target = DonQuixote_target;
        DonQuixote_IgnoreDamageCap = true;
        try
        {
            await CreatureCmd.Damage(
                DonQuixote_ctx,
                DonQuixote_target,
                DonQuixote_amount,
                DonQuixote_props,
                DonQuixote_dealer,
                DonQuixote_source,
                DonQuixote_cardPlay);
        }
        finally
        {
            DonQuixote_IgnoreDamageCap = false;
            DonQuixoteRawHpLossState.DonQuixote_Target = DonQuixote_previousHpLossTarget;
        }
    }

    private static async Task DonQuixote_TriggerBleedCore(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_bleeding,
        bool DonQuixote_consumeLayers)
    {
        DonQuixotePowerBleed DonQuixote_bleed = DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_bleeding);
        if (DonQuixote_bleed == null)
        {
            return;
        }

        int DonQuixote_strength = DonQuixote_bleed.Amount;
        if (DonQuixote_strength <= 0)
        {
            return;
        }

        int DonQuixote_layers = DonQuixote_BleedLayers(DonQuixote_bleeding);

        // 只有「会消耗层数」的触发才受层数限制：层数为 0 说明这段流血已经结算完了，顺手把强度清掉。
        if (DonQuixote_consumeLayers && DonQuixote_layers <= 0)
        {
            await PowerCmd.ModifyAmount(
                DonQuixote_ctx, DonQuixote_bleed, -DonQuixote_strength, DonQuixote_bleeding, null, true);
            return;
        }

        // 所有凝固的血：自身受到的流血伤害按百分比减少。
        int DonQuixote_perHit = DonQuixote_BleedHitDamage(DonQuixote_bleeding, DonQuixote_strength);
        if (DonQuixote_perHit <= 0)
        {
            return;
        }

        // 撕裂的血袋：敌方触发流血时，每回合前 3 次额外触发 1 次（不额外扣层）。
        int DonQuixote_hits = 1;
        if (DonQuixote_bleeding.Player == null)
        {
            DonQuixote_hits += DonQuixote_ConsumeTornBagCharges(DonQuixote_bleeding);
        }

        // 血宴的接收者必须在「造成伤害之前」取好：敌方被流血打死时。
        List<Creature> DonQuixote_feastTargets = DonQuixote_PlayerCreatures(DonQuixote_bleeding);

        // 流血是「生命伤害」：无视防御、难以杀灭、无实体、滑溜等一切伤害修正。
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            if (DonQuixote_bleeding.IsDead)
            {
                break;
            }

            int DonQuixote_hpBefore = DonQuixote_bleeding.CurrentHp;
            await DonQuixote_DamageIgnoringLimits(
                DonQuixote_ctx,
                DonQuixote_bleeding,
                DonQuixote_perHit,
                DonQuixote_bleed.Applier ?? DonQuixote_bleeding,
                (CardModel)null,
                (CardPlay)null);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] bleed hp loss " + DonQuixote_perHit
                + " -> hp " + DonQuixote_hpBefore + " => " + DonQuixote_bleeding.CurrentHp
                + " dead=" + DonQuixote_bleeding.IsDead, 0);

            // 血宴给场上所有玩家角色（联机时队友也能用「血宴 X」的卡）。
            foreach (Creature DonQuixote_ally in DonQuixote_feastTargets)
            {
                if (DonQuixote_ally == null || DonQuixote_ally.IsDead)
                {
                    continue;
                }

                await DonQuixote_GainBloodFeast(
                    DonQuixote_ctx,
                    DonQuixote_ally,
                    DonQuixote_strength * (DonQuixote_FeastGainMultiplier < 1 ? 1 : DonQuixote_FeastGainMultiplier),
                    null);
            }
        }

        // 已经死掉的单位不需要再结算层数与图标（死亡时它的能力已被全部移除）。
        if (DonQuixote_bleeding.IsDead)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] bleed kill on dead creature skipped layer handling", 0);
            return;
        }

        if (DonQuixote_consumeLayers)
        {
            DonQuixotePowerBleedLayers DonQuixote_layersPower =
                DonQuixote_Find<DonQuixotePowerBleedLayers>(DonQuixote_bleeding);
            if (DonQuixote_layers <= 1)
            {
                if (DonQuixote_layersPower != null)
                {
                    await PowerCmd.Remove(DonQuixote_layersPower);
                }

                await PowerCmd.ModifyAmount(
                    DonQuixote_ctx, DonQuixote_bleed, -DonQuixote_strength, DonQuixote_bleeding, null, true);
            }
            else if (DonQuixote_layersPower != null)
            {
                await PowerCmd.ModifyAmount(
                    DonQuixote_ctx, DonQuixote_layersPower, -1, DonQuixote_bleeding, null, true);
            }
        }

        DonQuixote_bleed.DonQuixote_RefreshAmountDisplay();

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] bleed triggered strength=" + DonQuixote_strength
            + " layers_before=" + DonQuixote_layers
            + " layers_after=" + DonQuixote_BleedLayers(DonQuixote_bleeding)
            + " hits=" + DonQuixote_hits
            + " consume_layers=" + DonQuixote_consumeLayers
            + " move=" + (DonQuixote_bleeding.Player == null
                ? Powers.DonQuixotePowerBleed.DonQuixote_CurrentMoveId ?? "?"
                : "-"),
            0);
    }

    /// <summary>
    /// 单次流血的实际伤害：所有凝固的血按百分比削减（向上取整），没有该状态时就是强度本身。
    /// </summary>
    public static int DonQuixote_BleedHitDamage(Creature DonQuixote_bleeding, int DonQuixote_strength)
    {
        if (DonQuixote_bleeding == null || DonQuixote_strength <= 0)
        {
            return 0;
        }

        // 伤口破坏：受到流血的单位。
        decimal DonQuixote_amount = DonQuixote_strength;
        if (DonQuixote_AmountOf<Powers.DonQuixotePowerWoundDestruction>(DonQuixote_bleeding) > 0)
        {
            DonQuixote_amount *= 1.5m;
        }

        int DonQuixote_reduction = DonQuixote_AmountOf<DonQuixotePowerAllClottedBlood>(DonQuixote_bleeding);
        if (DonQuixote_reduction > 0)
        {
            if (DonQuixote_reduction > 100)
            {
                DonQuixote_reduction = 100;
            }

            DonQuixote_amount *= (100 - DonQuixote_reduction) / 100m;
        }

        // 白月骑士：受到的流血伤害 -75%（文档「白月骑士」状态第三条）。
        if (DonQuixote_AmountOf<Powers.DonQuixotePowerWhiteMoonKnight>(DonQuixote_bleeding) > 0)
        {
            DonQuixote_amount *= 0.25m;
        }

        return (int)Math.Ceiling(DonQuixote_amount);
    }

    /// <summary>
    /// 撕裂的血袋：敌方每触发一次流血，持有者每回合的前 3 次可以让它额外触发 1 次。
    /// </summary>
    private static int DonQuixote_ConsumeTornBagCharges(Creature DonQuixote_bleeding)
    {
        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state = DonQuixote_bleeding?.CombatState;
        if (DonQuixote_state == null || DonQuixote_state.PlayerCreatures == null)
        {
            return 0;
        }

        int DonQuixote_round = DonQuixote_state.RoundNumber;
        int DonQuixote_extra = 0;

        foreach (Creature DonQuixote_ally in DonQuixote_state.PlayerCreatures)
        {
            MegaCrit.Sts2.Core.Entities.Players.Player DonQuixote_player = DonQuixote_ally?.Player;
            if (DonQuixote_player?.Relics == null)
            {
                continue;
            }

            foreach (RelicModel DonQuixote_relic in DonQuixote_player.Relics)
            {
                if (DonQuixote_relic is Relics.DonQuixoteTornBloodBag DonQuixote_bag
                    && DonQuixote_bag.DonQuixote_TryTakeCharge(DonQuixote_round))
                {
                    DonQuixote_extra++;
                }
            }
        }

        return DonQuixote_extra;
    }

    /// <summary>
    /// 这只生物身上是否有指定类型的遗物（按持有者判定）。
    /// </summary>
    public static bool DonQuixote_HasRelic<T>(Creature DonQuixote_creature) where T : RelicModel
    {
        MegaCrit.Sts2.Core.Entities.Players.Player DonQuixote_player = DonQuixote_creature?.Player;
        if (DonQuixote_player?.Relics == null)
        {
            return false;
        }

        foreach (RelicModel DonQuixote_relic in DonQuixote_player.Relics)
        {
            if (DonQuixote_relic is T)
            {
                return true;
            }
        }

        return false;
    }

    // 飞天而奔月给堕落牌的折扣不再是「角色身上的某个数值」。

    /// <summary>
    /// 腐化替换：堕落值达到阈值时，卡牌被替换成腐化版本。
    /// </summary>
    public static bool DonQuixote_IsCorrupted(Creature DonQuixote_owner, int DonQuixote_threshold)
    {
        return DonQuixote_Corruption(DonQuixote_owner) >= DonQuixote_threshold;
    }

    public static IEnumerable<Creature> DonQuixote_EnemiesOf(Creature DonQuixote_owner)
    {
        if (DonQuixote_owner?.CombatState == null)
        {
            return Enumerable.Empty<Creature>();
        }

        return DonQuixote_owner.CombatState.Enemies;
    }
}
