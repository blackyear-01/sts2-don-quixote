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
/// 血铸累计：每场战斗累计的「血铸X」总量，只影响「骑士长枪」的伤害，
/// 本身不在状态栏显示（真正的关键词显示在卡面的描述文本里）。
/// </summary>
public sealed class DonQuixotePowerLanceForge : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>技巧累计：为「骑士长枪」与「饱食鲜血的长枪」增加攻击段数。</summary>
public sealed class DonQuixotePowerLanceTechnique : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>增幅累计：为「骑士长枪」加血铠与摇曳，为「饱食鲜血的长枪」加流血触发次数。</summary>
public sealed class DonQuixotePowerLanceAmplify : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>「如骑士般提供保护」提供的状态：让「骑士长枪」获得重放。</summary>
public sealed class DonQuixotePowerKnightProtection : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>状态被清掉（战斗结束 / 被驱散）时把长枪上的重放同步回去。</summary>
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

/// <summary>「用鲜血荡涤前路」提供的状态：让「饱食鲜血的长枪」打到全体敌人。</summary>
public sealed class DonQuixotePowerBloodSweep : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 伤口破坏 X：持续 X 回合，对方回合结束时减一层。
/// 拥有该状态的单位受到的流血伤害 ×1.5（不增加血宴获取量）。
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
/// 鲜血重铸：每消耗 30 点血宴获得 3 点硬血（升级后固有）。
///
/// 中立状态（<see cref="PowerType.None"/>）：层数恒为 1，只是「机制开关」，
/// 翻倍没有意义，被「清除增益」清掉则会永久丢掉这条转换通道。
/// </summary>
public sealed class DonQuixotePowerBloodRecast : ModPowerTemplate
{
    private const int DonQuixote_FeastStep = 30;

    private const int DonQuixote_HardBloodGain = 3;

    private int DonQuixote_Paid;

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
        if (Owner == null || DonQuixote_power is not DonQuixotePowerFeastTotal)
        {
            return;
        }

        int DonQuixote_total = DonQuixoteBloodMagic.DonQuixote_FeastTotal(Owner);
        int DonQuixote_should = DonQuixote_total / DonQuixote_FeastStep * DonQuixote_HardBloodGain;
        int DonQuixote_delta = DonQuixote_should - DonQuixote_Paid;
        if (DonQuixote_delta <= 0)
        {
            return;
        }

        DonQuixote_Paid = DonQuixote_should;
        await PowerCmd.Apply<DonQuixotePowerHardBlood>(
            DonQuixote_choiceContext, Owner, DonQuixote_delta, Owner, DonQuixote_cardSource, true);
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
