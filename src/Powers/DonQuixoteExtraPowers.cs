using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 硬血 X：资源型状态，打出需要消耗硬血的卡时扣除。
/// </summary>
public sealed class DonQuixotePowerHardBlood : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/hard_blood.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/hard_blood_big.png";
}

/// <summary>
/// 血铠 X：为下一回合保留至多 X 点护甲，回合结束时层数 -1。
/// </summary>
public sealed class DonQuixotePowerBloodArmor : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_armor.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_armor_big.png";

    /// <summary>
    /// 有血铠时护甲不随回合开始清空。
    /// </summary>
    public override bool ShouldClearBlock(Creature DonQuixote_creature)
    {
        return !ReferenceEquals(DonQuixote_creature, Owner) || Amount <= 0;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Enemy || Owner == null || Owner.IsDead)
        {
            return;
        }

        int DonQuixote_keep = Amount;
        if (DonQuixote_keep <= 0)
        {
            return;
        }

        int DonQuixote_block = Owner.Block;
        if (DonQuixote_block > DonQuixote_keep)
        {
            await CreatureCmd.LoseBlock(
                DonQuixote_choiceContext, Owner, DonQuixote_block - DonQuixote_keep, Owner);
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] blood armor enemy turn end: block_before=" + DonQuixote_block
            + " kept_up_to=" + DonQuixote_keep + " block_after=" + Owner.Block, 0);

        // 「加强防御」升级版：血铠不再减少。
        if (DonQuixotePowerReinforcedDefense.DonQuixote_KeepsBloodArmor(Owner))
        {
            return;
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}

/// <summary>
/// 血编的荆棘 X：受到攻击时，对攻击者施加 5X 级流血强度并触发 X 次流血，然后失去 1 层。
/// </summary>
public sealed class DonQuixotePowerBloodThorns : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_thorns.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_thorns_big.png";

    public override async Task AfterDamageReceived(
        PlayerChoiceContext DonQuixote_choiceContext,
        Creature DonQuixote_target,
        DamageResult DonQuixote_result,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource)
    {
        if (Owner == null || !ReferenceEquals(DonQuixote_target, Owner) || DonQuixote_result == null)
        {
            return;
        }

        if (DonQuixote_result == null || DonQuixote_dealer == null
            || ReferenceEquals(DonQuixote_dealer, Owner) || Amount <= 0)
        {
            return;
        }

        // 文案：受到攻击时，对攻击者施加 5X 级流血强度并触发 X 次流血。
        int DonQuixote_stacks = Amount;
        int DonQuixote_strength = DonQuixote_stacks * 5;
        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext, DonQuixote_dealer, Owner, null, DonQuixote_strength, 0);

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_stacks && !DonQuixote_dealer.IsDead; DonQuixote_i++)
        {
            await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
                DonQuixote_choiceContext, DonQuixote_dealer, false);
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}

/// <summary>
/// 正在凝固的血 X：使目标 X 回合无法行动。
/// </summary>
public sealed class DonQuixotePowerClottingBlood : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/clotting_blood.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/clotting_blood_big.png";

    /// <summary>
    /// 原版「戏剧性刺穿」特效，用作血刺贯穿的视觉表现。
    /// </summary>
    private const string DonQuixote_ClotVfx = "vfx/vfx_dramatic_stab";

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_combatState)
    {
        if (DonQuixote_side != CombatSide.Enemy || Owner == null || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        if (DonQuixote_participants == null
            || !DonQuixote_participants.Any(DonQuixote_creature => ReferenceEquals(DonQuixote_creature, Owner)))
        {
            return;
        }

        try
        {
            // 血刺贯穿的视觉效果：戏剧性刺穿特效落在被凝固的目标身上。
            VfxCmd.PlayOnCreature(Owner, DonQuixote_ClotVfx);
        }
        catch (Exception DonQuixote_vfxError)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] clotting blood vfx failed: " + DonQuixote_vfxError.Message, 0);
        }

        try
        {
            await CreatureCmd.Stun(Owner, null);
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] clotting blood stun failed: " + DonQuixote_error.Message, 0);
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}

/// <summary>
/// 责任感：造成的伤害 +20%，受到的伤害 +20%。
/// </summary>
public sealed class DonQuixotePowerResponsibility : ModPowerTemplate
{
    public const decimal DonQuixote_DamageBonus = 1.2m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/responsibility.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/responsibility_big.png";

    public override decimal ModifyDamageMultiplicative(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null)
        {
            return 1m;
        }

        if (DonQuixote_dealer != null && ReferenceEquals(DonQuixote_dealer, Owner))
        {
            return DonQuixote_DamageBonus;
        }

        if (DonQuixote_target != null && ReferenceEquals(DonQuixote_target, Owner))
        {
            return DonQuixote_DamageBonus;
        }

        return 1m;
    }
}

/// <summary>
/// 眷属：每使用 1 张卡自身失去 5 点生命、堂吉诃德获得 5 点血宴。
/// </summary>
public sealed class DonQuixotePowerFollower : ModPowerTemplate
{
    public const int DonQuixote_HpPerCard = 5;

    public const int DonQuixote_FeastPerCard = 5;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/follower.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/follower_big.png";

    public override async Task AfterCardPlayed(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null || Owner.IsDead || DonQuixote_cardPlay?.Player == null
            || Owner.Player == null || !ReferenceEquals(DonQuixote_cardPlay.Player, Owner.Player))
        {
            return;
        }

        await CreatureCmd.Damage(
            DonQuixote_choiceContext, Owner, DonQuixote_HpPerCard, ValueProp.Unblockable,
            Owner, (CardModel)null, (CardPlay)null);

        // 血宴给「施加眷属的人」（谁转化的谁拿），取不到才回退到场上那名堂吉诃德。
        Creature DonQuixote_receiver = Applier
            ?? DonQuixoteBloodMagic.DonQuixote_PlayerCreature(Owner);
        if (DonQuixote_receiver != null && !DonQuixote_receiver.IsDead)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(
                DonQuixote_choiceContext, DonQuixote_receiver, DonQuixote_FeastPerCard, null);
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext DonQuixote_choiceContext,
        Creature DonQuixote_dealer,
        DamageResult DonQuixote_result,
        ValueProp DonQuixote_props,
        Creature DonQuixote_target,
        CardModel DonQuixote_cardSource)
    {
        if (Owner == null || Owner.IsDead || DonQuixote_dealer == null
            || !ReferenceEquals(DonQuixote_dealer, Owner) || DonQuixote_result == null)
        {
            return;
        }

        decimal DonQuixote_heal = DonQuixoteBloodMagic.DonQuixote_CeilPercent(DonQuixote_result.TotalDamage, 0.10m);
        if (DonQuixote_heal > 0m)
        {
            await CreatureCmd.Heal(Owner, DonQuixote_heal, true);
        }
    }
}

/// <summary>
/// 血袋：造成伤害时回复伤害量 5% 的生命；堂吉诃德血宴不足时可以用生命代替。
/// </summary>
public sealed class DonQuixotePowerBloodBag : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_bag.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_bag_big.png";

    public override async Task AfterDamageGiven(
        PlayerChoiceContext DonQuixote_choiceContext,
        Creature DonQuixote_dealer,
        DamageResult DonQuixote_result,
        ValueProp DonQuixote_props,
        Creature DonQuixote_target,
        CardModel DonQuixote_cardSource)
    {
        if (Owner == null || Owner.IsDead || DonQuixote_dealer == null
            || !ReferenceEquals(DonQuixote_dealer, Owner) || DonQuixote_result == null)
        {
            return;
        }

        decimal DonQuixote_heal = DonQuixoteBloodMagic.DonQuixote_CeilPercent(DonQuixote_result.TotalDamage, 0.05m);
        if (DonQuixote_heal > 0m)
        {
            await CreatureCmd.Heal(Owner, DonQuixote_heal, true);
        }
    }
}

/// <summary>
/// 消耗血宴总数：记录本场战斗里「血宴 X」与「消耗至多 X 点血宴」累计支付的数值。
/// </summary>
public sealed class DonQuixotePowerFeastTotal : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/feast_total.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/feast_total_big.png";

    /// <summary>
    /// 本场战斗已经发放过几次舞台卡（消耗血宴总数每满 325 发一次）。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_RewardSteps { get; set; }

    /// <summary>
    /// 每 1 点让「施加流血」的卡最终伤害 +1%（无上限）。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (!ReferenceEquals(DonQuixote_dealer, Owner) || DonQuixote_cardSource == null)
        {
            return 1m;
        }

        if (!DonQuixote_cardSource.DynamicVars.ContainsKey("Bleed")
            && !DonQuixote_cardSource.DynamicVars.ContainsKey("BleedStrength")
            && !DonQuixote_cardSource.DynamicVars.ContainsKey("BleedLayers")
            && !DonQuixote_cardSource.DynamicVars.ContainsKey("BleedTimes"))
        {
            return 1m;
        }

        int DonQuixote_total = Amount;
        return 1m + (DonQuixote_total * 0.01m);
    }
}
