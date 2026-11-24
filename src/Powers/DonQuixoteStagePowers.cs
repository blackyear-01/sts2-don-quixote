using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 两张舞台能力共用的规则与工具。
/// </summary>
internal static class DonQuixoteStageRules
{
    /// <summary>
    /// 每回合给全体友方单位的血编的荆棘层数。
    /// </summary>
    public const int DonQuixote_ThornsPerTurn = 3;

    /// <summary>
    /// 每回合给全体友方单位的摇曳【血魔】层数。
    /// </summary>
    public const int DonQuixote_WaveringPerTurn = 20;

    /// <summary>
    /// 「效果持续期间摇曳【血魔】不会在回合开始时消失」：由 <see cref="DonQuixotePowerWavering"/> 在减半前查一次。
    /// </summary>
    public static bool DonQuixote_KeepsWavering(Creature DonQuixote_owner)
    {
        if (DonQuixote_owner == null)
        {
            return false;
        }

        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerHappySmileLand>(DonQuixote_owner) > 0
            || DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerNightmareLand>(DonQuixote_owner) > 0;
    }

    public static List<Creature> DonQuixote_LivingAllies(Creature DonQuixote_owner)
    {
        if (DonQuixote_owner?.CombatState == null)
        {
            return new List<Creature>();
        }

        return DonQuixote_owner.CombatState.Allies
            .Where(DonQuixote_creature => DonQuixote_creature != null && !DonQuixote_creature.IsDead)
            .ToList();
    }

    public static List<Creature> DonQuixote_LivingEnemies(Creature DonQuixote_owner)
    {
        if (DonQuixote_owner?.CombatState == null)
        {
            return new List<Creature>();
        }

        return DonQuixote_owner.CombatState.Enemies
            .Where(DonQuixote_creature => DonQuixote_creature != null && !DonQuixote_creature.IsDead)
            .ToList();
    }

    /// <summary>
    /// 「持续清除负面效果」的真实含义：效果持续期间无法被施加负面状态。
    /// </summary>
    public static bool DonQuixote_BlocksDebuff(
        PowerModel DonQuixote_stage,
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        Creature? DonQuixote_owner = DonQuixote_stage?.Owner;
        if (DonQuixote_owner == null || target == null || amount <= 0m)
        {
            return false;
        }

        if (target.Side != DonQuixote_owner.Side)
        {
            return false;
        }

        if (canonicalPower == null || !canonicalPower.IsVisible)
        {
            return false;
        }

        if (canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return false;
        }

        if (DonQuixotePowerBlacklist.DonQuixote_IsBlocked(canonicalPower))
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    /// <summary>
    /// 清除一个单位身上的所有负面状态。
    /// </summary>
    public static async Task DonQuixote_ClearDebuffs(PlayerChoiceContext DonQuixote_ctx, Creature DonQuixote_target)
    {
        if (DonQuixote_target == null)
        {
            return;
        }

        DonQuixotePowerBleed? DonQuixote_bleed = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_target);
        if (DonQuixote_bleed != null)
        {
            DonQuixotePowerBleedLayers? DonQuixote_layers =
                DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleedLayers>(DonQuixote_target);
            if (DonQuixote_layers != null)
            {
                await PowerCmd.Remove(DonQuixote_layers);
            }

            await PowerCmd.Remove(DonQuixote_bleed);
        }

        foreach (PowerModel DonQuixote_power in DonQuixote_target.Powers.ToList())
        {
            if (!DonQuixote_IsClearable(DonQuixote_power, PowerType.Debuff))
            {
                continue;
            }

            if (DonQuixote_power is DonQuixotePowerBleed or DonQuixotePowerBleedLayers)
            {
                continue;
            }

            await PowerCmd.Remove(DonQuixote_power);
        }
    }

    /// <summary>
    /// 清除一个单位身上的所有增益（沙坑等「动了就出错」的状态按黑名单跳过）。
    /// </summary>
    public static async Task DonQuixote_ClearBuffs(PlayerChoiceContext DonQuixote_ctx, Creature DonQuixote_target)
    {
        if (DonQuixote_target == null)
        {
            return;
        }

        foreach (PowerModel DonQuixote_power in DonQuixote_target.Powers.ToList())
        {
            if (!DonQuixote_IsClearable(DonQuixote_power, PowerType.Buff))
            {
                continue;
            }

            await PowerCmd.Remove(DonQuixote_power);
        }
    }

    /// <summary>
    /// 把身上所有同类型状态的层数翻倍（增益 / 负面各用一次）。
    /// </summary>
    public static async Task DonQuixote_DoubleAmounts(
        PlayerChoiceContext DonQuixote_ctx,
        Creature DonQuixote_target,
        PowerType DonQuixote_type)
    {
        if (DonQuixote_target == null)
        {
            return;
        }

        foreach (PowerModel DonQuixote_power in DonQuixote_target.Powers.ToList())
        {
            // 只处理「层数就是强度」的增益 / 减益：- PowerType.None 是中立（资源、计数器、标记）。
            if (DonQuixote_power == null
                || DonQuixote_power.Type != DonQuixote_type
                || DonQuixote_power.StackType != PowerStackType.Counter
                || !DonQuixote_power.IsVisible
                || DonQuixote_power.Amount <= 0
                || DonQuixote_power.InstanceType != PowerInstanceType.None)
            {
                continue;
            }

            // 这几条不能翻倍：舞台能力自己（翻倍会把剩余回合越滚越多）、堕落值（上限 100）、决斗对象（上限 1）。
            if (DonQuixote_power is DonQuixotePowerHappySmileLand
                or DonQuixotePowerNightmareLand
                or DonQuixotePowerCorruption
                or DonQuixotePowerDuelTarget
                or DonQuixotePowerBloodFeast
                or DonQuixotePowerFeastTotal)
            {
                continue;
            }

            int DonQuixote_amount = DonQuixote_power.Amount;
            await PowerCmd.ModifyAmount(
                DonQuixote_ctx, DonQuixote_power, DonQuixote_amount, DonQuixote_target, null, true);

            if (DonQuixote_power is DonQuixotePowerBleedLayers)
            {
                // 层数存在这条状态上，图标右侧的主数字是另一个能力画的，得手动刷新。
                DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_target)
                    ?.DonQuixote_RefreshAmountDisplay();
            }
        }
    }

    private static bool DonQuixote_IsClearable(PowerModel DonQuixote_power, PowerType DonQuixote_type)
    {
        // Type 不等于目标类型就直接跳过。
        if (DonQuixote_power == null
            || DonQuixote_power.Type != DonQuixote_type
            || !DonQuixote_power.IsVisible
            || DonQuixote_power.Amount <= 0)
        {
            return false;
        }

        // 多实例 / 与施加者绑定的状态、以及原版强绑定的状态（沙坑等）都不能动。
        if (DonQuixote_power.InstanceType != PowerInstanceType.None)
        {
            return false;
        }

        return !DonQuixotePowerBlacklist.DonQuixote_IsBlocked(DonQuixote_power);
    }

    /// <summary>
    /// 回合开始的收尾：层数（剩余回合）大于 1 就减 1，只剩 1 层时标记「本回合结束后移除」。
    /// </summary>
    public static async Task DonQuixote_TickTurns(PowerModel DonQuixote_power)
    {
        if (DonQuixote_power == null || DonQuixote_power.Amount <= 0)
        {
            return;
        }

        if (DonQuixote_power.Amount > 1)
        {
            await PowerCmd.Decrement(DonQuixote_power);
        }
    }
}

/// <summary>
/// 幸福笑容如鲜花般绽放之地：把战场换成拉曼却乐园的游乐园夜景，并奏响嘉年华的曲子。
/// </summary>
public sealed class DonQuixotePowerHappySmileLand : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/happy_smile_land.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/happy_smile_land_big.png";

    /// <summary>
    /// 本回合结束后就要移除（最后一层时置位）。
    /// </summary>
    private bool DonQuixote_Expiring;

    /// <summary>
    /// 持续期间无法被施加负面状态。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        return DonQuixoteStageRules.DonQuixote_BlocksDebuff(this, canonicalPower, target, amount, out modifiedAmount);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        DonQuixoteStageDirector.DonQuixote_Enter(DonQuixoteStageDirector.DonQuixote_StageHappy, this);

        // 打出当回合就先把身上已经有的负面清掉，不用等下一个回合开始。
        await DonQuixoteStageRules.DonQuixote_ClearDebuffs(new ThrowingPlayerChoiceContext(), Owner);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        DonQuixoteStageDirector.DonQuixote_Exit(this);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        // 只在持有者自己这一侧开始时结算，避免多人局里被别人的回合重复触发。
        if (participants == null || !participants.Contains(Owner))
        {
            return;
        }

        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        foreach (Creature DonQuixote_ally in DonQuixoteStageRules.DonQuixote_LivingAllies(Owner))
        {
            await PowerCmd.Apply<DonQuixotePowerBloodThorns>(
                DonQuixote_ctx,
                DonQuixote_ally,
                DonQuixoteStageRules.DonQuixote_ThornsPerTurn,
                Owner,
                null,
                false);

            await PowerCmd.Apply<DonQuixotePowerWavering>(
                DonQuixote_ctx,
                DonQuixote_ally,
                DonQuixoteStageRules.DonQuixote_WaveringPerTurn,
                Owner,
                null,
                false);

            await DonQuixoteStageRules.DonQuixote_ClearDebuffs(DonQuixote_ctx, DonQuixote_ally);
        }

        if (Amount <= 1)
        {
            DonQuixote_Expiring = true;
        }

        await DonQuixoteStageRules.DonQuixote_TickTurns(this);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!DonQuixote_Expiring || side != CombatSide.Player || Owner == null)
        {
            return;
        }

        if (participants == null || !participants.Contains(Owner))
        {
            return;
        }

        DonQuixote_Expiring = false;
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 噩梦的拉曼却领：把战场换成噩梦中的拉曼却领，并奏响英雄的主题曲。
/// </summary>
public sealed class DonQuixotePowerNightmareLand : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/nightmare_land.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/nightmare_land_big.png";

    /// <summary>
    /// 本回合结束后就要移除（最后一层时置位）。
    /// </summary>
    private bool DonQuixote_Expiring;

    /// <summary>
    /// 持续期间无法被施加负面状态。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        return DonQuixoteStageRules.DonQuixote_BlocksDebuff(this, canonicalPower, target, amount, out modifiedAmount);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        DonQuixoteStageDirector.DonQuixote_Enter(DonQuixoteStageDirector.DonQuixote_StageNightmare, this);

        // 打出当回合就先把身上已经有的负面清掉。
        await DonQuixoteStageRules.DonQuixote_ClearDebuffs(new ThrowingPlayerChoiceContext(), Owner);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        DonQuixoteStageDirector.DonQuixote_Exit(this);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (participants == null || !participants.Contains(Owner))
        {
            return;
        }

        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        // 友方：先翻倍增益，再清负面（顺序反过来的话，本回合刚翻倍的增益不受影响，但清负面会把「决斗对象」这类不能动的状态排除在外）。
        foreach (Creature DonQuixote_ally in DonQuixoteStageRules.DonQuixote_LivingAllies(Owner))
        {
            await DonQuixoteStageRules.DonQuixote_DoubleAmounts(DonQuixote_ctx, DonQuixote_ally, PowerType.Buff);
            await DonQuixoteStageRules.DonQuixote_ClearDebuffs(DonQuixote_ctx, DonQuixote_ally);
        }

        // 敌方：负面翻倍、增益清空（沙坑等黑名单状态跳过）。
        foreach (Creature DonQuixote_enemy in DonQuixoteStageRules.DonQuixote_LivingEnemies(Owner))
        {
            await DonQuixoteStageRules.DonQuixote_DoubleAmounts(DonQuixote_ctx, DonQuixote_enemy, PowerType.Debuff);
            await DonQuixoteStageRules.DonQuixote_ClearBuffs(DonQuixote_ctx, DonQuixote_enemy);
        }

        if (Amount <= 1)
        {
            DonQuixote_Expiring = true;
        }

        await DonQuixoteStageRules.DonQuixote_TickTurns(this);
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!DonQuixote_Expiring || side != CombatSide.Player || Owner == null)
        {
            return;
        }

        if (participants == null || !participants.Contains(Owner))
        {
            return;
        }

        DonQuixote_Expiring = false;
        await PowerCmd.Remove(this);
    }
}
