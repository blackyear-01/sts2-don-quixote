using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 血魔之力：每次攻击恢复 2 点生命，每回合开始时获得 2 点临时强壮。
/// </summary>
public sealed class DonQuixotePowerBloodfiendMight : ModPowerTemplate
{
    private const int DonQuixote_HealPerAttack = 2;

    /// <summary>和卡面的 {Strength} 一致：每回合开始给 2 层临时强壮。</summary>
    private const int DonQuixote_TempStrengthPerTurn = 2;

    /// <summary>自检用：确认每回合实际给出去的临时强壮层数。</summary>
    public static string DonQuixote_Probe()
    {
        return "tempStrength=" + DonQuixote_TempStrengthPerTurn
            + " heal=" + DonQuixote_HealPerAttack;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/bloodfiend_might.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/bloodfiend_might_big.png";

    /// <summary>每次攻击（不是每段伤害）恢复 2 点生命。</summary>
    public override async Task AfterAttack(
        PlayerChoiceContext DonQuixote_choiceContext,
        MegaCrit.Sts2.Core.Commands.Builders.AttackCommand DonQuixote_command)
    {
        Creature DonQuixote_owner = Owner;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead || DonQuixote_command == null)
        {
            return;
        }

        if (DonQuixote_command.Attacker == null || !ReferenceEquals(DonQuixote_command.Attacker, DonQuixote_owner))
        {
            return;
        }

        if (DonQuixote_owner.CurrentHp >= DonQuixote_owner.MaxHp)
        {
            return;
        }

        await CreatureCmd.Heal(DonQuixote_owner, DonQuixote_HealPerAttack, true);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != CombatSide.Player)
        {
            return;
        }

        Creature DonQuixote_owner = Owner;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead || DonQuixote_owner.Player == null)
        {
            return;
        }

        await PowerCmd.Apply<FlexPotionPower>(
            DonQuixote_choiceContext,
            DonQuixote_owner,
            DonQuixote_TempStrengthPerTurn,
            DonQuixote_owner,
            (CardModel)null,
            false);
    }
}

/// <summary>
/// 永恒饥渴：每回合开始时消耗至多 30 点血宴，每消耗 X 点恢复 1 点生命，
/// 然后触发全体敌方单位 1 次流血并减少 1 层。
/// </summary>
public sealed class DonQuixotePowerEternalThirst : ModPowerTemplate
{
    private const int DonQuixote_FeastCostPerTurn = 30;

    /// <summary>每多少点血宴换 1 点生命（升级 15 → 10）。</summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_HealPer { get; set; } = 15;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/eternal_thirst.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/eternal_thirst_big.png";

    public void DonQuixote_Initialize(int DonQuixote_healPer)
    {
        if (DonQuixote_healPer > 0)
        {
            DonQuixote_HealPer = DonQuixote_healPer;
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != CombatSide.Player)
        {
            return;
        }

        Creature DonQuixote_owner = Owner;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead || DonQuixote_owner.CombatState == null)
        {
            return;
        }

        // 这里的「消耗」不属于卡面的「血宴 X / 至多消耗 X 点血宴」，
        // 所以只扣血宴、不累计「消耗血宴总数」。
        int DonQuixote_have = DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_owner);
        int DonQuixote_spend = DonQuixote_have > DonQuixote_FeastCostPerTurn
            ? DonQuixote_FeastCostPerTurn
            : DonQuixote_have;

        if (DonQuixote_spend > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_ConsumeBloodFeast(
                DonQuixote_choiceContext, DonQuixote_owner, DonQuixote_spend, (CardModel)null);
        }

        int DonQuixote_healPer = DonQuixote_HealPer > 0 ? DonQuixote_HealPer : 15;
        int DonQuixote_heal = DonQuixote_spend / DonQuixote_healPer;
        if (DonQuixote_heal > 0 && DonQuixote_owner.CurrentHp < DonQuixote_owner.MaxHp)
        {
            await CreatureCmd.Heal(DonQuixote_owner, DonQuixote_heal, true);
        }

        foreach (Creature DonQuixote_enemy in DonQuixoteBloodMagic.DonQuixote_EnemiesOf(DonQuixote_owner).ToList())
        {
            if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
            {
                continue;
            }

            await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
                DonQuixote_choiceContext, DonQuixote_enemy, true);
        }
    }
}

/// <summary>
/// 血宴封锁：本回合不能再打出消耗血宴的牌，玩家回合结束时自动移除。
/// </summary>
public sealed class DonQuixotePowerBloodFeastLock : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_feast_lock.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_feast_lock_big.png";

    public override async Task BeforeSideTurnEndVeryEarly(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Player || Owner == null)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
