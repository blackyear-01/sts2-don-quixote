using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 需要消耗硬血的卡：硬血不足时仍可打出，按实际消耗结算。
/// </summary>
public abstract class DonQuixoteHardBloodCard : ModCardTemplate
{
    protected DonQuixoteHardBloodCard(int DonQuixote_cost, CardType DonQuixote_type, CardRarity DonQuixote_rarity, TargetType DonQuixote_target)
        : base(DonQuixote_cost, DonQuixote_type, DonQuixote_rarity, DonQuixote_target)
    {
    }

    public abstract int DonQuixote_HardBloodCost { get; }

    protected int DonQuixote_Var(string DonQuixote_name)
    {
        DynamicVar DonQuixote_var;
        return DynamicVars.TryGetValue(DonQuixote_name, out DonQuixote_var) ? DonQuixote_var.IntValue : 0;
    }

    protected Task<int> DonQuixote_PayHardBlood(PlayerChoiceContext DonQuixote_ctx)
    {
        return DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_ctx, Owner?.Creature, DonQuixote_HardBloodCost);
    }
}

/// <summary>
/// 由血所铸就：1 费，血宴 10，获得 2 层硬血（升级：血宴 15，5 层硬血）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteForgedOfBlood : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\由血所铸就）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/forged_of_blood.png";

    public DonQuixoteForgedOfBlood()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("HardBlood", 2m),
        new DynamicVar("GateHardBlood", 5m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        // 卡牌.doc：升级只提高两条硬血的数量，血宴仍然是 10。
        DynamicVars["HardBlood"].UpgradeValueBy(3m);
        DynamicVars["GateHardBlood"].UpgradeValueBy(5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerHardBlood>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Var("HardBlood"), Owner.Creature, this, false);

        // 血宴 10：条件触发，够了才额外再给 5 层硬血。
        int DonQuixote_gate = DonQuixote_Var("GateHardBlood");
        if (DonQuixote_gate > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_gate, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 凝血为铠：1 费，血宴 2，对自身施加 1 级流血，获得 5 层血铠，消耗（升级：2 级流血，8 层血铠）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteClotIntoArmor : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/clot_into_armor.png";

    public DonQuixoteClotIntoArmor()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("Bleed", 1m),
        new DynamicVar("Plating", 3m),
        new DynamicVar("BloodArmor", 5m),
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        // 升级：流血 1→2 级、覆甲 3→6 层、血铠 5→8 层，血宴需求 5→2。
        DynamicVars["Bleed"].UpgradeValueBy(1m);
        DynamicVars["Plating"].UpgradeValueBy(3m);
        DynamicVars["BloodArmor"].UpgradeValueBy(3m);
        DynamicVars["BloodFeast"].UpgradeValueBy(-3m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        // 对自己的流血不吃「复还的血之冲动」加成（那是给敌人的）。
        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext, Owner.Creature, Owner.Creature, this, DonQuixote_Var("Bleed"), 0);

        // 覆甲用原版的 PlatingPower（回合结束获得格挡、回合开始减 1 层）。
        int DonQuixote_plating = DonQuixote_Var("Plating");
        if (DonQuixote_plating > 0)
        {
            await PowerCmd.Apply<PlatingPower>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_plating, Owner.Creature, this, false);
        }

        // 血宴 5：条件触发，够了才给血铠。
        int DonQuixote_bloodArmor = DonQuixote_Var("BloodArmor");
        if (DonQuixote_bloodArmor > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerBloodArmor>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_bloodArmor, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 令血凝固，缠绕于你我身周（联机）：2 费，血宴 10，全体友方获得 3 级流血与 8 层血铠，自身额外获得 6 层硬血（升级：血宴 15，5 级流血，15 层血铠，自身额外 10 层硬血）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteFreezeBloodAroundUs : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/freeze_blood_around_us.png";

    public DonQuixoteFreezeBloodAroundUs()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
    {
    }

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("Bleed", 3m),
        new DynamicVar("BloodArmor", 8m),
        new DynamicVar("HardBlood", 6m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars["BloodFeast"].UpgradeValueBy(5m);
        DynamicVars["Bleed"].UpgradeValueBy(2m);
        DynamicVars["BloodArmor"].UpgradeValueBy(7m);
        DynamicVars["HardBlood"].UpgradeValueBy(4m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        List<Creature> DonQuixote_allies = Owner.Creature.CombatState.PlayerCreatures.ToList();
        foreach (Creature DonQuixote_ally in DonQuixote_allies)
        {
            if (DonQuixote_ally == null || DonQuixote_ally.IsDead)
            {
                continue;
            }

            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_ally, Owner.Creature, this, DonQuixote_Var("Bleed"), 0);
            await PowerCmd.Apply<DonQuixotePowerBloodArmor>(
                DonQuixote_choiceContext, DonQuixote_ally, DonQuixote_Var("BloodArmor"), Owner.Creature, this, false);
        }

        // 血宴 10：条件触发，够了自身才额外获得硬血。
        int DonQuixote_hardBlood = DonQuixote_Var("HardBlood");
        if (DonQuixote_hardBlood > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_hardBlood, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 堂吉诃德派硬血 2 式-桩：2 费，硬血 5，造成 14 点伤害并施加 4 级流血。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodPile : DonQuixoteHardBloodCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\堂吉诃德派硬血2式-桩.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_pile.png";

    public DonQuixoteHardBloodPile()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override int DonQuixote_HardBloodCost => DonQuixote_Var("HardBlood");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("Bleed", 4m),
        new DynamicVar("Triggers", 0m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Triggers"].UpgradeValueBy(3m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        int DonQuixote_consumed = await DonQuixote_PayHardBlood(DonQuixote_choiceContext);
        decimal DonQuixote_damage =
            DynamicVars.Damage.BaseValue + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_consumed);

        await DamageCmd.Attack(DonQuixote_damage)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext, DonQuixote_cardPlay.Target, Owner.Creature, this, DonQuixote_Var("Bleed"), 0);

        // 升级后：触发对方 3 次流血效果，并因此扣掉 3 层（卡面明确写了「减少 3 层」）。
        int DonQuixote_triggers = DonQuixote_Var("Triggers");
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_triggers; DonQuixote_i++)
        {
            if (DonQuixote_cardPlay.Target == null || DonQuixote_cardPlay.Target.IsDead)
            {
                break;
            }

            await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, true);
        }
    }
}

/// <summary>
/// 被刺穿吧：3 费，血宴 20，虚无，获得 1 层复还的血之冲动（升级 2 层），造成目标最大生命值 20% 的伤害，消耗。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBePierced : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\被刺穿吧.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/be_pierced.png";

    public DonQuixoteBePierced()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 20m),
        new DynamicVar("Impulse", 1m),
        new DynamicVar("Percent", 20m),
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Ethereal, CardKeyword.Exhaust };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars["Impulse"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Var("Impulse"), Owner.Creature, this, false);

        // 百分比伤害：先按目标最大生命值向上取整算出基础值，再乘堕落值增伤（每 10 点 +5%）。
        decimal DonQuixote_baseDamage = decimal.Ceiling(
            DonQuixote_cardPlay.Target.MaxHp * DynamicVars["Percent"].BaseValue / 100m);
        decimal DonQuixote_damage = decimal.Ceiling(DonQuixote_ScaledDamage(DonQuixote_baseDamage));

        await DonQuixoteBloodMagic.DonQuixote_DamageAsBaseAttack(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            DonQuixote_damage,
            Owner.Creature,
            this,
            DonQuixote_cardPlay);

        // 血宴 20：条件触发，够了再打第二段（升级后卡面才有这一行）。
        if (IsUpgraded && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await DonQuixoteBloodMagic.DonQuixote_DamageAsBaseAttack(
                DonQuixote_choiceContext,
                DonQuixote_cardPlay.Target,
                DonQuixote_damage,
                Owner.Creature,
                this,
                DonQuixote_cardPlay);
        }
    }
}

/// <summary>
/// 被贯穿吧：1 费，血宴 10，造成 5 点伤害，施加 1 层「正在凝固的血」，消耗（升级后不再消耗）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBeRunThrough : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\被贯穿吧.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/be_run_through.png";

    public DonQuixoteBeRunThrough()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 35m),
        new DynamicVar("Damage", 5m),
        new DynamicVar("Clot", 1m),
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        // 升级后不再消耗。
        RemoveKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await CreatureCmd.Damage(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            DynamicVars["Damage"].BaseValue,
            ValueProp.Move,
            Owner.Creature,
            this,
            DonQuixote_cardPlay);

        int DonQuixote_clot = DonQuixote_Var("Clot");

        // 升级后卡面上多一层「正在凝固的血」是白给的。
        if (IsUpgraded && DonQuixote_clot > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerClottingBlood>(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, DonQuixote_clot, Owner.Creature, this, false);
        }

        // 血宴 35：条件触发，够了再额外施加一层。
        if (DonQuixote_clot > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerClottingBlood>(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, DonQuixote_clot, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 鲜血逆流：1 费，血宴 3，抽 1 张卡，并从弃牌堆取回 1 张卡（升级后 2 张）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodBackflow : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_backflow.png";

    public DonQuixoteBloodBackflow()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("Cards", 1m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars["Cards"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await CardPileCmd.Draw(DonQuixote_choiceContext, 1m, Owner, false);

        // 血宴 5：条件触发，够了才能从弃牌堆拿牌。
        if (!await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            return;
        }

        int DonQuixote_count = DonQuixote_Var("Cards");
        CardPile DonQuixote_discard = PileType.Discard.GetPile(Owner);
        if (DonQuixote_discard == null || DonQuixote_discard.Cards.Count == 0)
        {
            return;
        }

        CardSelectorPrefs DonQuixote_prefs = new CardSelectorPrefs(SelectionScreenPrompt, DonQuixote_count);
        List<CardModel> DonQuixote_selected = (await CardSelectCmd.FromCombatPile(
            DonQuixote_choiceContext, DonQuixote_discard, Owner, DonQuixote_prefs)).ToList();

        foreach (CardModel DonQuixote_picked in DonQuixote_selected)
        {
            if (DonQuixote_picked != null)
            {
                await CardPileCmd.Add(DonQuixote_picked, PileType.Hand, CardPilePosition.Top, this, false);
            }
        }
    }
}

/// <summary>
/// 眷属转化（联机）：3 费，对一名友方施加 1 层眷属，消耗（升级后不再消耗）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteFollowerConversion : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\眷属转化.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/follower_conversion.png";

    public DonQuixoteFollowerConversion()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
    {
    }

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 按「转化先后」记录场上的眷属，用来判定「第三名眷属进场时把最后转化的那名变成血袋」。
    /// </summary>
    private static readonly List<Creature> DonQuixote_FollowerOrder = new List<Creature>();

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        Creature DonQuixote_target = DonQuixote_cardPlay.Target;
        bool DonQuixote_alreadyFollower =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerFollower>(DonQuixote_target) != null;

        // 场上最多两名眷属；已经有第三名时，把「最后转化」的那名变成血袋。
        if (!DonQuixote_alreadyFollower && DonQuixoteBloodMagic.DonQuixote_Followers(Owner.Creature).Count >= 2)
        {
            Creature DonQuixote_last = null;
            for (int DonQuixote_i = DonQuixote_FollowerOrder.Count - 1; DonQuixote_i >= 0; DonQuixote_i--)
            {
                Creature DonQuixote_candidate = DonQuixote_FollowerOrder[DonQuixote_i];
                if (DonQuixote_candidate != null && !DonQuixote_candidate.IsDead
                    && DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerFollower>(DonQuixote_candidate) != null)
                {
                    DonQuixote_last = DonQuixote_candidate;
                    break;
                }
            }

            if (DonQuixote_last != null)
            {
                DonQuixotePowerFollower DonQuixote_power =
                    DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerFollower>(DonQuixote_last);
                if (DonQuixote_power != null)
                {
                    await PowerCmd.Remove(DonQuixote_power);
                }

                DonQuixote_FollowerOrder.Remove(DonQuixote_last);
                await PowerCmd.Apply<DonQuixotePowerBloodBag>(
                    DonQuixote_choiceContext, DonQuixote_last, 1m, Owner.Creature, this, false);
            }
        }

        // 眷属层数最高 1：已经拥有眷属的目标不再叠加。
        if (DonQuixote_alreadyFollower)
        {
            return;
        }

        DonQuixote_FollowerOrder.Add(DonQuixote_target);
        await PowerCmd.Apply<DonQuixotePowerFollower>(
            DonQuixote_choiceContext, DonQuixote_target, 1m, Owner.Creature, this, false);
    }
}

/// <summary>
/// 家族的责任（联机）：3 费能力，获得 1 层责任感（升级：费用 2，固有）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteFamilyResponsibility : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\家族的责任.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/family_responsibility.png";

    public DonQuixoteFamilyResponsibility()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        AddKeyword(CardKeyword.Innate);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerResponsibility>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);
    }
}
