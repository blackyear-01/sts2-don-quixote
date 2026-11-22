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
/// Shared helpers for the 血魔 (blood demon) keyword family:
/// 血宴 / 堕落值 / 流血 / 复还的血之冲动.
/// </summary>
internal static class DonQuixoteBloodMagic
{
    public const int DonQuixote_CorruptionMax = 100;

    /// <summary>每 10 点堕落值使消耗血宴的卡伤害 +5%。</summary>
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

    /// <summary>本模组统一规则：所有需要取整的地方一律向上取整。</summary>
    /// <summary>为 true 时，护甲结算跳过所有修正（摇曳【血魔】专用）。</summary>
    public static bool DonQuixote_RawBlockGain;
    public static int DonQuixote_CeilHalf(int DonQuixote_value)
    {
        return (DonQuixote_value + 1) / 2;
    }

    /// <summary>按百分比计算并向上取整。</summary>
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

    /// <summary>消耗血宴总数：每 1 点让施加流血效果的卡最终伤害 +1%（无上限）。</summary>
    public static int DonQuixote_FeastTotal(Creature DonQuixote_creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerFeastTotal>(DonQuixote_creature);
    }

    /// <summary>
    /// 消耗血宴总数带来的伤害倍率：每 1 点 +1%，<b>不设上限</b>
    /// （1.00 起，消耗多少就涨多少）。
    /// </summary>
    public static decimal DonQuixote_FeastTotalMultiplier(Creature DonQuixote_creature)
    {
        int DonQuixote_total = DonQuixote_FeastTotal(DonQuixote_creature);
        return 1m + (DonQuixote_total * 0.01m);
    }

    /// <summary>累计消耗血宴总数（血宴卡结算时调用）。</summary>
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
    /// 施加流血 X/Y：X 为强度，Y 为层数。目标没有层数时先 +1 层。
    /// 复还的血之冲动会额外提高强度与层数，但「目标就是施加者自己」时不吃这个加成。
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

        // 复还的血之冲动只强化「施加给敌人」的流血；
        // 给自己或队友上的流血（凝血为铠、令血凝固…这类代价/增益）不吃这个加成。
        bool DonQuixote_onEnemy = DonQuixote_target.IsEnemy;

        int DonQuixote_impulse = DonQuixote_onEnemy
            ? DonQuixote_BloodImpulse(DonQuixote_applier ?? DonQuixote_target)
            : 0;

        if (DonQuixote_impulse > 0)
        {
            DonQuixote_strength += DonQuixote_impulse;
            DonQuixote_layers += DonQuixote_impulse / 5;
        }

        // 蜜拉卡：施加流血强度时额外 +1 级。这 1 级是「施加完再加」，
        // 所以不会被复还的血之冲动再乘一遍（文案里特意标了这一点）。
        // 同样只对敌人生效，免得把「给自己上流血」的代价也放大。
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

    /// <summary>层数变化后让流血图标上的层数立即刷新。</summary>
    public static void DonQuixote_RefreshBleedDisplay(Creature DonQuixote_creature)
    {
        DonQuixotePowerBleed DonQuixote_bleed = DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_creature);
        if (DonQuixote_bleed != null)
        {
            DonQuixote_bleed.DonQuixote_RefreshAmountDisplay();
        }
    }

    /// <summary>
    /// 血宴获取量修正：潜伏之血（展现洒落在战场上的潜伏之血）把获得量放大，
    /// 能力上存的是百分比（50 = 1.5 倍），一律向上取整。
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

    /// <summary>当前硬血层数。</summary>
    public static int DonQuixote_HardBlood(Creature DonQuixote_creature)
    {
        return DonQuixote_AmountOf<DonQuixotePowerHardBlood>(DonQuixote_creature);
    }

    /// <summary>消耗硬血，返回实际消耗掉的层数（硬血不足时按剩余量结算）。</summary>
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

    /// <summary>每消耗 1 点硬血，消耗硬血的牌基础伤害 +2。</summary>
    public const decimal DonQuixote_HardBloodDamagePerPoint = 2m;

    /// <summary>
    /// 硬血增伤：「消耗硬血的卡，每消耗1点硬血增加2点基础伤害」。
    /// 加在卡牌自己的基础伤害上（先加、再吃堕落值/血宴总数那类倍率），
    /// 所以每张消耗硬血的攻击牌都要在自己的伤害值上调用它。
    /// </summary>
    public static decimal DonQuixote_HardBloodDamageBonus(int DonQuixote_consumed)
    {
        return DonQuixote_consumed <= 0
            ? 0m
            : DonQuixote_consumed * DonQuixote_HardBloodDamagePerPoint;
    }

    /// <summary>堂吉诃德是否在场上有血袋（每名血袋可代付不足的血宴）。</summary>
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
    /// 结算卡面上写的「消耗至多 X 点血宴」：只扣当前真正拥有的数量，
    /// 并按【实际消耗】的点数累计「消耗血宴总数」（PayBloodFeast 是「必须付满 X 点」的语义，
    /// 两者的扣费与累计口径不同，所以单独一支）。
    /// 返回实际消耗的点数，供卡牌按「这一次到底消耗了多少」继续结算后续效果。
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
    /// 「血宴 X：」条件触发：血宴达到 X 点时扣掉 X 并返回 true，不足时有血袋也可以（血袋代付），
    /// 仍然不足则返回 false，卡牌继续结算其余无条件效果。
    /// 注意与 <see cref="DonQuixote_PayBloodFeastUpTo"/> 的区别：那是「至多消耗」，这里是「必须凑满 X」。
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

    /// <summary>场上第一个带「血袋」的队友（不含自己）。</summary>
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

    /// <summary>场上所有眷属（不含堂吉诃德本人）。</summary>
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
    /// 获得摇曳[血魔]。若堂吉诃德拥有「责任感」，场上每个眷属额外获得一半（向上取整）的摇曳。
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

    /// <summary>最近一次战斗内的选择上下文，供没有 ctx 参数的钩子（如获得格挡）复用。</summary>
    public static PlayerChoiceContext DonQuixote_LastContext { get; set; }

    /// <summary>找到堂吉诃德玩家的生物（用于把血宴算给她）。</summary>
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

    /// <summary>场上所有玩家角色（活着的），用于把血宴发给每个玩家。</summary>
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
    /// <paramref name="DonQuixote_consumeLayers"/> 为 false 时不扣层——卡牌上写的
    /// 「触发对方 X 次流血效果」都属于这种「额外结算一次，但不消耗层数」的触发，
    /// 只有明确写了「消耗层数」的来源（敌方行动、打出/消耗卡牌）才扣。
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
    /// 流血触发时「获得的血宴」倍率。默认 1；陌生的晚宴（升级版）在结算期间把它设成 2。
    /// 只影响流血给的这部分血宴，不影响「血宴 X」这类主动获得。
    /// </summary>
    public static int DonQuixote_FeastGainMultiplier { get; set; } = 1;

    /// <summary>为 true 时，本次伤害无视「难以杀灭」之类的伤害上限修正（流血专用）。</summary>
    public static bool DonQuixote_IgnoreDamageCap { get; set; }

    /// <summary>为 true 时，本次伤害结算跳过「虚弱」的减伤（被刺穿吧的百分比伤害专用）。</summary>
    public static bool DonQuixote_SkipWeakReduction { get; set; }

    /// <summary>本次跳过虚弱减伤的伤害来源，用来只屏蔽该角色身上的虚弱。</summary>
    public static Creature DonQuixote_SkipWeakDealer { get; set; }

    /// <summary>
    /// 把算出来的数值当作「基础攻击伤害」结算：
    /// 照常吃力量、易损、堕落等增伤修正，但不受虚弱影响。
    /// 用于「被刺穿吧」这类按目标最大生命值换算出来的伤害。
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

        // 手算伤害虽然不走原版卡牌伤害管线，但 CreatureCmd.Damage 内部仍会经过
        // Hook.ModifyDamage，而那里已经会调用卡牌附魔的 EnchantDamageAdditive /
        // EnchantDamageMultiplicative —— 所以这里「不能」再手动应用一次附魔修正，
        // 否则「不可摧毁卡牌」的 -80% 会被乘两次（表现为伤害异常）。
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
    /// 造成「无视一切减免」的伤害：不改动对方身上的任何状态（以前是摘掉再装回，
    /// 但同一实例再 Apply 会把层数叠加成两倍），改成结算期间打开两个开关，
    /// 由 <c>Hook.ModifyDamage</c> / <c>Hook.ModifyHpLost</c> 的后置补丁把数值还原成原始值。
    /// 伤害仍走 CreatureCmd.Damage，所以死亡动画与特效正常。
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

        // 只有「会消耗层数」的触发才受层数限制：层数为 0 说明这段流血已经结算完了，
        // 顺手把强度清掉。卡牌主动触发的「额外结算一次」不看层数。
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

        // 血宴的接收者必须在「造成伤害之前」取好：
        // 敌方被流血打死时，CreatureCmd.Kill 会把这只怪从战斗里移除，
        // CombatState.RemoveCreature 顺手把 creature.CombatState 置空，
        // 之后再通过 DonQuixote_bleeding.CombatState 找玩家就找不到了 ——
        // 这正是「卡牌主动触发流血打死敌人时拿不到血宴」的原因
        // （敌方行动中死亡时 IsPerformingMove 为 true，不会移除，所以看起来只有卡牌触发才出问题）。
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
            // 用上面提前取好的列表，死亡后 CombatState 被清空也不影响。
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
            + " consume_layers=" + DonQuixote_consumeLayers,
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

        // 伤口破坏：受到流血的单位，其流血伤害 x1.5（不会增加血宴的获取量，
        // 因为血宴只在 DonQuixote_TriggerBleedCore 里按「强度」结算，与这里无关）。
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
        // 放在最后乘，和「所有凝固的血」的百分比减免是叠乘关系；
        // 只在流血数值上扣，血宴仍然按「强度」结算，所以不吃这一刀。
        if (DonQuixote_AmountOf<Powers.DonQuixotePowerWhiteMoonKnight>(DonQuixote_bleeding) > 0)
        {
            DonQuixote_amount *= 0.25m;
        }

        return (int)Math.Ceiling(DonQuixote_amount);
    }

    /// <summary>
    /// 撕裂的血袋：敌方每触发一次流血，持有者每回合的前 3 次可以让它额外触发 1 次。
    /// 返回额外触发的次数（多名队友各带一件就叠加）。
    ///
    /// 计数写在遗物的 [SavedProperty] 上、按 CombatState.RoundNumber 复位：
    /// 两端跑的是同一段确定性代码，回合号也参与校验和，所以不会出现「一端算过、一端没算」。
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

    /// <summary>这只生物身上是否有指定类型的遗物（按持有者判定）。</summary>
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

    // 飞天而奔月给堕落牌的折扣不再是「角色身上的某个数值」，而是按卡牌实例记账：
    // 每张堕落牌首次被打出后，本场战斗内这张牌的费用 -1。记账见 DonQuixoteMoonFlightState，
    // 费用由 DonQuixoteCorruptedCostPatch 在费用钩子里减。

    /// <summary>腐化替换：堕落值达到阈值时，卡牌被替换成腐化版本。</summary>
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
