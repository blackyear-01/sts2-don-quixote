using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 血宴卡：消耗血宴的卡，堕落值每 10 点使伤害 +5%。
/// </summary>
public abstract class DonQuixoteBloodFeastCard : ModCardTemplate
{
    protected DonQuixoteBloodFeastCard(int DonQuixote_cost, CardType DonQuixote_type, CardRarity DonQuixote_rarity, TargetType DonQuixote_target)
        : base(DonQuixote_cost, DonQuixote_type, DonQuixote_rarity, DonQuixote_target)
    {
    }

    /// <summary>
    /// 需要消耗的血宴数量（由 DynamicVars 提供，升级时同步变化）。
    /// </summary>
    public abstract int DonQuixote_BloodFeastCost { get; }

    /// <summary>
    /// 是否必须凑够血宴才能打出。
    /// </summary>
    public virtual bool DonQuixote_BloodFeastMustPay => false;

    /// <summary>
    /// 取一个动态数值；缺失时返回 0，避免升级/预览路径抛异常。
    /// </summary>
    protected int DonQuixote_Var(string DonQuixote_name)
    {
        DynamicVar DonQuixote_var;
        return DynamicVars.TryGetValue(DonQuixote_name, out DonQuixote_var) ? DonQuixote_var.IntValue : 0;
    }

    protected decimal DonQuixote_ScaledDamage(decimal DonQuixote_baseValue)
    {
        // 堕落值的增伤已经改成全局伤害钩子（DonQuixotePowerCorruption.ModifyDamageMultiplicative）。
        return DonQuixote_baseValue;
    }

    protected Task<bool> DonQuixote_PayBloodFeast(PlayerChoiceContext DonQuixote_ctx)
    {
        return DonQuixoteBloodMagic.DonQuixote_PayBloodFeast(
            DonQuixote_ctx, Owner?.Creature, DonQuixote_BloodFeastCost, this);
    }

    /// <summary>
    /// 「血宴 X：」条件触发。
    /// </summary>
    protected Task<bool> DonQuixote_TriggerBloodFeast(PlayerChoiceContext DonQuixote_ctx)
    {
        return DonQuixoteBloodMagic.DonQuixote_TryConsumeBloodFeast(
            DonQuixote_ctx, Owner?.Creature, DonQuixote_BloodFeastCost, this);
    }
}

/// <summary>
/// 硬血斩击：1 费，造成 5 点伤害并施加 1 级流血（升级 6 点伤害、3 级流血）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
[RegisterCharacterStarterCard(typeof(DonQuixoteCharacter), 4, Order = 10)]
public sealed class DonQuixoteHardBloodSlash : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\硬血打击.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_slash.png";

    public DonQuixoteHardBloodSlash()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    // CardModel.IsBasicStrikeOrDefend 就是靠「Basic + Strike/Defend 标签」判定的，遗物（LeafyPoultice、NeowsTalisman、LargeCapsule、NutritiousSoup）直接查 Tags。
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag>
    {
        CardTag.Strike,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar("BleedStrength", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            0);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["BleedStrength"].UpgradeValueBy(2m);
    }
}

/// <summary>
/// 硬血防御：1 费，获得 5 层摇曳[血魔]（升级 7）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
[RegisterCharacterStarterCard(typeof(DonQuixoteCharacter), 5, Order = 20)]
public sealed class DonQuixoteHardBloodGuard : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\硬血防御.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_guard.png";

    public DonQuixoteHardBloodGuard()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag>
    {
        CardTag.Defend,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Wavering", 5m),
    };

    private int DonQuixote_Wavering => DynamicVars["Wavering"].IntValue;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Wavering, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Wavering"].UpgradeValueBy(2m);
    }
}

/// <summary>
/// 抑制的血之渴望：2 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
[RegisterCharacterStarterCard(typeof(DonQuixoteCharacter), 1, Order = 30)]
public sealed class DonQuixoteBloodHunger : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\抑制的血之渴望.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_hunger.png";

    public DonQuixoteBloodHunger()
        : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(11m, ValueProp.Move),
        new DynamicVar("BleedStrength", 5m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            0);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["BleedStrength"].UpgradeValueBy(9m);
    }
}

/// <summary>
/// 脆弱，脆弱不堪：0 费，施加 1 层虚弱与 2 层流血（升级 2 层虚弱与 4 层流血）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
[RegisterCharacterStarterCard(typeof(DonQuixoteCharacter), 1, Order = 40)]
public sealed class DonQuixoteFragileFragility : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\脆弱，脆弱不堪.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/fragile_fragility.png";

    public DonQuixoteFragileFragility()
        : base(0, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Weak", 1m),
        new DynamicVar("BleedLayers", 2m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await PowerCmd.Apply<WeakPower>(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            DynamicVars["Weak"].IntValue,
            Owner.Creature,
            this,
            false);
        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            Owner.Creature,
            this,
            0,
            DynamicVars["BleedLayers"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Weak"].UpgradeValueBy(1m);
        DynamicVars["BleedLayers"].UpgradeValueBy(2m);
    }
}
