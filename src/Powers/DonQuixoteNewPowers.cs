using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Enchantments;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 心-勇敢的骑士：受到的伤害 -25%，拥有「骑士」词条的卡造成的基础伤害 +5%、
/// 费用 -1（费用在 DonQuixoteCorruptedCostPatch 里统一减）。不可叠加。
/// </summary>
public sealed class DonQuixotePowerBraveHeart : ModPowerTemplate
{
    public const decimal DonQuixote_DamageTakenMultiplier = 0.75m;

    public const decimal DonQuixote_KnightDamageMultiplier = 1.05m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string CustomIconPath => "res://DonQuixote/images/powers/brave_heart.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/brave_heart_big.png";

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

        if (DonQuixote_target != null && ReferenceEquals(DonQuixote_target, Owner))
        {
            return DonQuixote_DamageTakenMultiplier;
        }

        if (DonQuixote_dealer != null
            && ReferenceEquals(DonQuixote_dealer, Owner)
            && DonQuixoteKeywords.DonQuixote_IsKnightCard(DonQuixote_cardSource))
        {
            return DonQuixote_KnightDamageMultiplier;
        }

        return 1m;
    }
}

/// <summary>
/// 望：下一张**自己**打出的攻击牌造成的伤害 +25%（每层 +25%，可叠加），那张牌打完就消失。
///
/// 绑定方式和原版「活力」（VigorPower）一致：BeforeAttack 时记录这一次攻击，
/// 伤害结算只认这条被记录的攻击，AfterAttack 再消耗掉。
/// 不能用 AfterCardPlayed —— 多人模式里队友出牌也会广播给所有玩家的能力，
/// 那样堂吉诃德的「望」会被队友的攻击牌白白吃掉。
/// </summary>
public sealed class DonQuixotePowerHope : ModPowerTemplate
{
    public const decimal DonQuixote_DamagePerStack = 0.25m;

    /// <summary>这一次「望」认领的攻击（和 VigorPower 的 internal data 同一套做法）。</summary>
    private sealed class DonQuixoteHopeBinding
    {
        public AttackCommand DonQuixote_command;
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/hope.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/hope_big.png";

    private DonQuixoteHopeBinding DonQuixote_Binding => GetInternalData<DonQuixoteHopeBinding>();

    /// <summary>自检用：确认「望」已经改成绑定自己的攻击，而不是 AfterCardPlayed。</summary>
    public static string DonQuixote_Probe()
    {
        System.Type DonQuixote_type = typeof(DonQuixotePowerHope);
        System.Reflection.MethodInfo DonQuixote_before = DonQuixote_type.GetMethod(nameof(BeforeAttack));
        System.Reflection.MethodInfo DonQuixote_after = DonQuixote_type.GetMethod(nameof(AfterAttack));
        System.Reflection.MethodInfo DonQuixote_cardPlayed = DonQuixote_type.GetMethod("AfterCardPlayed");

        return "beforeAttack=" + (DonQuixote_before != null && DonQuixote_before.DeclaringType == DonQuixote_type)
            + " afterAttack=" + (DonQuixote_after != null && DonQuixote_after.DeclaringType == DonQuixote_type)
            + " afterCardPlayed=" + (DonQuixote_cardPlayed != null && DonQuixote_cardPlayed.DeclaringType == DonQuixote_type);
    }

    protected override object InitInternalData()
    {
        return new DonQuixoteHopeBinding();
    }

    /// <summary>认领自己打出的下一张攻击牌（队友的攻击不算）。</summary>
    public override Task BeforeAttack(AttackCommand DonQuixote_command)
    {
        if (DonQuixote_command == null
            || Owner == null
            || Owner.IsDead
            || Amount <= 0)
        {
            return Task.CompletedTask;
        }

        if (DonQuixote_command.Attacker == null
            || !ReferenceEquals(DonQuixote_command.Attacker, Owner))
        {
            return Task.CompletedTask;
        }

        if (DonQuixote_command.ModelSource is not CardModel DonQuixote_card
            || DonQuixote_card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }

        DonQuixoteHopeBinding DonQuixote_binding = DonQuixote_Binding;

        // 已经认领过一次了，等它结算完再说。
        if (DonQuixote_binding.DonQuixote_command != null)
        {
            return Task.CompletedTask;
        }

        DonQuixote_binding.DonQuixote_command = DonQuixote_command;
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null
            || Amount <= 0
            || DonQuixote_dealer == null
            || !ReferenceEquals(DonQuixote_dealer, Owner)
            || DonQuixote_cardSource == null
            || DonQuixote_cardSource.Type != CardType.Attack)
        {
            return 1m;
        }

        // 只加在「被认领的那一次攻击」上：同一次攻击的每一段都吃得到，
        // 没被认领的其它伤害（遗物、能力、队友）一律不加。
        AttackCommand DonQuixote_claimed = DonQuixote_Binding.DonQuixote_command;
        if (DonQuixote_claimed == null
            || !ReferenceEquals(DonQuixote_claimed.ModelSource, DonQuixote_cardSource))
        {
            return 1m;
        }

        return 1m + (DonQuixote_DamagePerStack * Amount);
    }

    /// <summary>认领的那次攻击结算完，层数清空（打出后消失）。</summary>
    public override async Task AfterAttack(
        PlayerChoiceContext DonQuixote_choiceContext,
        AttackCommand DonQuixote_command)
    {
        if (Owner == null || DonQuixote_command == null)
        {
            return;
        }

        DonQuixoteHopeBinding DonQuixote_binding = DonQuixote_Binding;
        if (DonQuixote_binding.DonQuixote_command == null
            || !ReferenceEquals(DonQuixote_binding.DonQuixote_command, DonQuixote_command))
        {
            return;
        }

        DonQuixote_binding.DonQuixote_command = null;
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 援护防御：最大 1 层。敌方攻击意图会给友方造成失去生命的伤害时，
/// 若「援护反击」的伤害高于对方这一段伤害，就打断这次攻击
/// （把整次攻击的段数清零，动画与后续命中一并跳过），
/// 并按对方原攻击段数重复打出「援护反击」。触发后 -1 层，回合开始也 -1 层。
/// </summary>
public sealed class DonQuixotePowerGuardAssist : ModPowerTemplate
{
    /// <summary>AttackCommand._hitCount：这一击的段数。</summary>
    private static readonly System.Reflection.FieldInfo DonQuixote_HitCountField =
        typeof(AttackCommand).GetField("_hitCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    /// <summary>AttackCommand._damagePerHit：固定伤害类的每段伤害。</summary>
    private static readonly System.Reflection.FieldInfo DonQuixote_DamagePerHitField =
        typeof(AttackCommand).GetField("_damagePerHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    /// <summary>AttackCommand._calculatedDamageVar：按目标计算的每段伤害（优先于 _damagePerHit）。</summary>
    private static readonly System.Reflection.FieldInfo DonQuixote_CalculatedDamageField =
        typeof(AttackCommand).GetField("_calculatedDamageVar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    /// <summary>AttackCommand._singleTarget：单体攻击的目标（全体攻击为 null）。</summary>
    private static readonly System.Reflection.FieldInfo DonQuixote_SingleTargetField =
        typeof(AttackCommand).GetField("_singleTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string CustomIconPath => "res://DonQuixote/images/powers/guard_assist.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/guard_assist_big.png";

    /// <summary>
    /// 打断实现：AttackCommand.Execute 的顺序是
    /// BeforeAttack → attackCount = Hook.ModifyAttackHitCount(_hitCount) → for(i &lt; attackCount)。
    /// 所以在 BeforeAttack 里把 hitCount 改成 0，整次攻击就会被完整跳过（不出手、不结算）。
    /// </summary>
    public override async Task BeforeAttack(AttackCommand DonQuixote_command)
    {
        if (DonQuixote_command == null || Owner == null || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        try
        {
            Creature DonQuixote_attacker = DonQuixote_command.Attacker;
            if (DonQuixote_attacker == null || DonQuixote_attacker.IsDead || DonQuixote_attacker.Player != null)
            {
                return;
            }

            // 只挡「打到友方」的攻击；TargetSide 是这一击的目标阵营（单体/全体都会被设置）。
            if (DonQuixote_command.TargetSide != CombatSide.Player)
            {
                return;
            }

            Creature DonQuixote_target = DonQuixote_SingleTargetOf(DonQuixote_command);
            if (DonQuixote_target == null || DonQuixote_target.IsDead || DonQuixote_target.Player == null)
            {
                return;
            }

            int DonQuixote_hits = DonQuixote_HitCountOf(DonQuixote_command);
            decimal DonQuixote_perHit = DonQuixote_PerHitDamageOf(DonQuixote_command, DonQuixote_target);

            // 判定用的是「援护反击这张卡此刻真能打出多少」：卡面基础伤害再走一遍
            // Hook.ModifyDamage（力量、增伤之类的增减益全算上），不是卡面上的裸值。
            // 真正结算时 CreatureCmd.Damage 内部会跑同一条管线，所以那边仍然传裸值，
            // 否则力量会被算两次。
            decimal DonQuixote_riposteBase = DonQuixote_RiposteBaseDamage();
            decimal DonQuixote_riposte = DonQuixote_RiposteFinalDamage(DonQuixote_target, DonQuixote_riposteBase);

            // 只有「援护反击」的伤害严格高于对方单段伤害才算挡得住。
            if (DonQuixote_hits <= 0 || DonQuixote_perHit <= 0m || DonQuixote_riposte <= DonQuixote_perHit)
            {
                return;
            }

            PlayerChoiceContext DonQuixote_ctx = DonQuixoteBloodMagic.DonQuixote_LastContext;
            if (DonQuixote_ctx == null)
            {
                return;
            }

            // 1) 打断整次攻击。
            DonQuixote_command.WithHitCount(0);

            // 2) 触发后 -1 层（只有 1 层，于是整条消失）。
            await PowerCmd.Remove(this);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] guard assist interrupt: hits=" + DonQuixote_hits
                + " perHit=" + DonQuixote_perHit
                + " riposte=" + DonQuixote_riposte
                + " (base " + DonQuixote_riposteBase + ")"
                + " attacker=" + DonQuixote_attacker.Name,
                0);

            // 3) 按原攻击段数重复打出「援护反击」（每段 6 伤 + 2 级流血）。
            for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
            {
                if (DonQuixote_attacker.IsDead)
                {
                    break;
                }

                await CreatureCmd.Damage(
                    DonQuixote_ctx,
                    DonQuixote_attacker,
                    DonQuixote_riposteBase,
                    ValueProp.Move,
                    Owner,
                    (CardModel)null,
                    (CardPlay)null);

                if (DonQuixote_attacker.IsDead)
                {
                    break;
                }

                await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                    DonQuixote_ctx, DonQuixote_attacker, Owner, null, 2, 0);
            }
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] guard assist riposte failed: " + DonQuixote_ex.Message, 0);
        }
    }

    /// <summary>「援护反击」卡面上的基础伤害（从规范卡取，改了卡面数值这里自动跟着变）。</summary>
    private static decimal DonQuixote_RiposteBaseDamage()
    {
        try
        {
            Cards.DonQuixoteGuardRiposteStrike DonQuixote_card =
                ModelDb.Card<Cards.DonQuixoteGuardRiposteStrike>();
            if (DonQuixote_card != null)
            {
                return DonQuixote_card.DynamicVars.Damage.BaseValue;
            }
        }
        catch (Exception)
        {
        }

        return Cards.DonQuixoteGuardRiposteStrike.DonQuixote_BaseDamage;
    }

    /// <summary>
    /// 「援护反击」此刻实际能打出的每段伤害：基础伤害过一遍 <c>Hook.ModifyDamage</c>，
    /// 力量、增伤、减伤、上限等修正全部计入 —— 判定要和真正结算出来的数字一致。
    /// 结算侧仍传基础伤害（<see cref="CreatureCmd.Damage"/> 内部会再跑同一条管线）。
    /// </summary>
    private decimal DonQuixote_RiposteFinalDamage(Creature DonQuixote_target, decimal DonQuixote_baseDamage)
    {
        try
        {
            MegaCrit.Sts2.Core.Entities.Players.Player DonQuixote_player = Owner?.Player;
            ICombatState DonQuixote_combat = Owner?.CombatState;
            if (DonQuixote_player == null || DonQuixote_combat == null || DonQuixote_target == null)
            {
                return DonQuixote_baseDamage;
            }

            return MegaCrit.Sts2.Core.Hooks.Hook.ModifyDamage(
                DonQuixote_player.RunState,
                DonQuixote_combat,
                DonQuixote_target,
                Owner,
                DonQuixote_baseDamage,
                ValueProp.Move,
                null,
                null,
                MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _);
        }
        catch (Exception)
        {
            return DonQuixote_baseDamage;
        }
    }

    /// <summary>读 AttackCommand 的原始段数（拿不到就按 1 段算）。</summary>
    private static int DonQuixote_HitCountOf(AttackCommand DonQuixote_command)
    {
        try
        {
            object DonQuixote_raw = DonQuixote_HitCountField?.GetValue(DonQuixote_command);
            if (DonQuixote_raw is int DonQuixote_hits)
            {
                return DonQuixote_hits < 1 ? 1 : DonQuixote_hits;
            }
        }
        catch (Exception)
        {
        }

        return 1;
    }

    /// <summary>自检用：确认反射字段都找到了，且 BeforeAttack 确实被本类重写。</summary>
    public static string DonQuixote_Probe()
    {
        System.Reflection.MethodInfo DonQuixote_method =
            typeof(DonQuixotePowerGuardAssist).GetMethod(nameof(BeforeAttack));
        bool DonQuixote_overridden = DonQuixote_method != null
            && DonQuixote_method.DeclaringType == typeof(DonQuixotePowerGuardAssist);

        return "hitCount=" + (DonQuixote_HitCountField != null)
            + " perHit=" + (DonQuixote_DamagePerHitField != null)
            + " calc=" + (DonQuixote_CalculatedDamageField != null)
            + " single=" + (DonQuixote_SingleTargetField != null)
            + " override=" + DonQuixote_overridden;
    }

    /// <summary>读单体攻击的目标；全体攻击（_singleTarget 为空）时取第一个活着的玩家单位来估值。</summary>
    private static Creature DonQuixote_SingleTargetOf(AttackCommand DonQuixote_command)
    {
        try
        {
            object DonQuixote_raw = DonQuixote_SingleTargetField?.GetValue(DonQuixote_command);
            if (DonQuixote_raw is Creature DonQuixote_single && !DonQuixote_single.IsDead)
            {
                return DonQuixote_single;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            ICombatState DonQuixote_combat = DonQuixote_command.Attacker?.CombatState;
            if (DonQuixote_combat?.PlayerCreatures != null)
            {
                foreach (Creature DonQuixote_creature in DonQuixote_combat.PlayerCreatures)
                {
                    if (DonQuixote_creature != null && !DonQuixote_creature.IsDead)
                    {
                        return DonQuixote_creature;
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    /// <summary>读这一击的每段伤害（和 AttackCommand.Execute 内部取值顺序一致）。</summary>
    private static decimal DonQuixote_PerHitDamageOf(AttackCommand DonQuixote_command, Creature DonQuixote_target)
    {
        try
        {
            object DonQuixote_calc = DonQuixote_CalculatedDamageField?.GetValue(DonQuixote_command);
            if (DonQuixote_calc is MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar DonQuixote_var)
            {
                return DonQuixote_var.Calculate(DonQuixote_target);
            }

            object DonQuixote_flat = DonQuixote_DamagePerHitField?.GetValue(DonQuixote_command);
            if (DonQuixote_flat is decimal DonQuixote_value)
            {
                return DonQuixote_value;
            }
        }
        catch (Exception)
        {
        }

        return 0m;
    }

    public override async Task AfterSideTurnStart(
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_combatState)
    {
        if (DonQuixote_side != CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (DonQuixote_participants == null || !DonQuixote_participants.Contains(Owner))
        {
            return;
        }

        if (Amount <= 0)
        {
            return;
        }

        // 回合开始 -1 层（1 层就整条消失）。
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 加强防御：摇曳【血魔】持续多 1 回合后减半（隔回合才减半）。
/// 层数为 2（升级版）时，血铠回合结束不再减少。
/// </summary>
public sealed class DonQuixotePowerReinforcedDefense : ModPowerTemplate
{
    public static bool DonQuixote_IsUpgradedAmount(Creature DonQuixote_creature)
    {
        return DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerReinforcedDefense>(DonQuixote_creature) >= 2;
    }

    public static bool DonQuixote_KeepsBloodArmor(Creature DonQuixote_creature)
    {
        return DonQuixote_IsUpgradedAmount(DonQuixote_creature);
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_armor.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_armor_big.png";
}

/// <summary>
/// 过人的毅力（未升级）：本回合受到的伤害按层数百分比降低。
/// 「本回合」指到**敌方回合结束**为止 —— 敌人在它自己的回合里攻击，
/// 所以不能在玩家回合结束时移除，否则减伤完全吃不到。
/// </summary>
public sealed class DonQuixotePowerGrit : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/grit.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/grit_big.png";

    public override decimal ModifyDamageMultiplicative(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null || DonQuixote_target == null || !ReferenceEquals(DonQuixote_target, Owner))
        {
            return 1m;
        }

        decimal DonQuixote_percent = Amount;
        if (DonQuixote_percent <= 0)
        {
            return 1m;
        }

        if (DonQuixote_percent > 100)
        {
            DonQuixote_percent = 100;
        }

        return (100m - DonQuixote_percent) / 100m;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Enemy || Owner == null)
        {
            return;
        }

        // 同一个「本回合」的结尾，把临时挂上的「不可摧毁卡牌」附魔一起收回。
        DonQuixoteIndestructibleState.DonQuixote_ReclaimTemporary(Owner);
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 过人的毅力（升级版）：本回合每次受到的伤害固定最高 X 点（卡面是 2）。
/// 和 <see cref="DonQuixotePowerGrit"/> 一样，持续到敌方回合结束。
/// 用无实体的那套「伤害上限」钩子（ModifyDamageCap 管格挡/预览，ModifyHpLostAfterOsty 管实际掉血）。
/// </summary>
public sealed class DonQuixotePowerGritCap : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/grit.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/grit_big.png";

    public override decimal ModifyDamageCap(
        Creature DonQuixote_target,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null || DonQuixote_target == null || !ReferenceEquals(DonQuixote_target, Owner))
        {
            return decimal.MaxValue;
        }

        return Amount > 0 ? Amount : decimal.MaxValue;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource)
    {
        if (Owner == null || DonQuixote_target == null || !ReferenceEquals(DonQuixote_target, Owner))
        {
            return DonQuixote_amount;
        }

        if (Amount <= 0 || DonQuixote_amount <= 0)
        {
            return DonQuixote_amount;
        }

        return DonQuixote_amount > Amount ? Amount : DonQuixote_amount;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Enemy || Owner == null)
        {
            return;
        }

        DonQuixoteIndestructibleState.DonQuixote_ReclaimTemporary(Owner);
        await PowerCmd.Remove(this);
    }
}
