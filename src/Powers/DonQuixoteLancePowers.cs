using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 血铸累计：每场战斗累计的「血铸X」总量。
/// </summary>
public sealed class DonQuixotePowerLanceForge : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 技巧累计：为「骑士长枪」与「饱食鲜血的长枪」增加攻击段数。
/// </summary>
public sealed class DonQuixotePowerLanceTechnique : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 增幅累计：为「骑士长枪」加血铠与摇曳，为「饱食鲜血的长枪」加流血触发次数。
/// </summary>
public sealed class DonQuixotePowerLanceAmplify : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 「如骑士般提供保护」提供的状态：让「骑士长枪」获得重放。
/// </summary>
public sealed class DonQuixotePowerKnightProtection : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 状态被清掉（战斗结束 / 被驱散）时把长枪上的重放同步回去。
    /// </summary>
    public override Task AfterRemoved(Creature DonQuixote_oldOwner)
    {
        Player DonQuixote_player = DonQuixote_oldOwner?.Player;
        if (DonQuixote_player != null)
        {
            DonQuixoteLance.DonQuixote_RefreshLanceStats(DonQuixote_player);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 「用鲜血荡涤前路」提供的状态：让「饱食鲜血的长枪」打到全体敌人。
/// </summary>
public sealed class DonQuixotePowerBloodSweep : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 伤口破坏 X：持续 X 回合，对方回合结束时减一层。
/// </summary>
public sealed class DonQuixotePowerWoundDestruction : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/wound_destruction.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/wound_destruction_big.png";

    public override async Task BeforeSideTurnEndVeryEarly(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Enemy || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// 鲜血重铸：每消耗 6 点硬血，血铸 1 次（升级后再增幅 1）。
/// </summary>
public sealed class DonQuixotePowerBloodRecast : ModPowerTemplate
{
    private const int DonQuixote_HardBloodStep = 6;

    /// <summary>
    /// 本次战斗中已经统计过的硬血消耗量。
    /// </summary>
    private int DonQuixote_Consumed;

    /// <summary>
    /// 已经结算过几次「血铸」。
    /// </summary>
    private int DonQuixote_Forges;

    /// <summary>
    /// 升级版：每次血铸同时增幅 1。
    /// </summary>
    public bool DonQuixote_Amplified;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_recast.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_recast_big.png";

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext DonQuixote_choiceContext,
        PowerModel DonQuixote_power,
        decimal DonQuixote_amount,
        Creature DonQuixote_applier,
        CardModel DonQuixote_cardSource)
    {
        if (Owner?.Player == null || DonQuixote_power is not DonQuixotePowerHardBlood)
        {
            return;
        }

        // 只统计硬血被消耗（层数减少）。
        if (DonQuixote_amount >= 0m)
        {
            return;
        }

        DonQuixote_Consumed += (int)(-DonQuixote_amount);
        int DonQuixote_should = DonQuixote_Consumed / DonQuixote_HardBloodStep;
        int DonQuixote_delta = DonQuixote_should - DonQuixote_Forges;
        if (DonQuixote_delta <= 0)
        {
            return;
        }

        DonQuixote_Forges = DonQuixote_should;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_delta; DonQuixote_i++)
        {
            await DonQuixoteLance.DonQuixote_Forge(
                DonQuixote_choiceContext, Owner.Player, 1, DonQuixote_cardSource);

            if (DonQuixote_Amplified)
            {
                await DonQuixoteLance.DonQuixote_Amplify(
                    DonQuixote_choiceContext, Owner.Player, 1, DonQuixote_cardSource);
            }
        }
    }
}

/// <summary>
/// 无法摆脱的可怖的疾病：回合开始时给予全场敌人 X 层流血。
/// </summary>
public sealed class DonQuixotePowerDreadfulDisease : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/dreadful_disease.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/dreadful_disease_big.png";

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_combatState)
    {
        if (DonQuixote_side != CombatSide.Player
            || Owner == null
            || Owner.CombatState == null
            || Amount <= 0)
        {
            return;
        }

        foreach (Creature DonQuixote_target in DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner).ToList())
        {
            if (DonQuixote_target.IsDead)
            {
                continue;
            }

            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_target, Owner, null, 0, Amount);
        }
    }
}
