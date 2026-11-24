using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 骑士的无畏冲锋：1 费，造成 8 点伤害（升级 12），给予 2 层流血（升级 3）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteKnightCharge : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 80;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteBloodDesire>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteBloodDesire>();

    /// <summary>
    /// 「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteKnightCharge()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Bleed", 3m),
        new DynamicVar("Corruption", 80m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["Bleed"].UpgradeValueBy(2m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        // 「决斗对象」的 +50% 增伤已收进「骑士」词条（DonQuixoteKnightDuelBonus）。
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
            0,
            DynamicVars["Bleed"].IntValue);
    }
}

/// <summary>
/// 腐败版：血之欲望：2 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodDesire : DonQuixoteBloodFeastCard, IDonQuixoteCorruptedCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\血之欲望.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_desire.png";

    public DonQuixoteBloodDesire()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(13m, ValueProp.Move),
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("Impulse", 2m),
        new DynamicVar("BleedTimes", 4m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    // 堕落牌的费用折扣由 Hook.ModifyEnergyCostInCombat 补丁统一处理 （CanonicalEnergyCost 只在卡牌创建时被读一次。

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7m);
        DynamicVars["BloodFeast"].UpgradeValueBy(-5m);
        DynamicVars["Impulse"].UpgradeValueBy(2m);
        DynamicVars["BleedTimes"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DonQuixote_ScaledDamage(DynamicVars.Damage.BaseValue))
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        int DonQuixote_times = DonQuixote_Var("BleedTimes");
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_times; DonQuixote_i++)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, Owner.Creature, this, 0, 1);
        }

        // 血宴 10：条件触发，够了才获得复还的血之冲动。
        int DonQuixote_impulse = DonQuixote_Var("Impulse");
        if (DonQuixote_impulse > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_impulse, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 堂吉诃德派硬血 4 式-剜除：2 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodGouge : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\堂吉诃德派硬血4式-剜除.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_gouge.png";

    public DonQuixoteHardBloodGouge()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(13m, ValueProp.Move),
        new DynamicVar("Impulse", 2m),
        new DynamicVar("BleedStrength", 8m),
        new DynamicVar("BleedLayers", 0m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Impulse"].UpgradeValueBy(1m);
        DynamicVars["BleedLayers"].UpgradeValueBy(2m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Impulse"].IntValue, Owner.Creature, this, false);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            DynamicVars["BleedLayers"].IntValue);
    }
}

/// <summary>
/// 血宴时刻：1 费，血宴 10，恢复 10 点生命，获得 1 层复还的血之冲动、2 层硬血 （升级：15 点生命，2 层冲动，5 层硬血）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteFeastMoment : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\血宴时刻.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/feast_moment.png";

    public DonQuixoteFeastMoment()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 10m),
        new HealVar(10m),
        new DynamicVar("Impulse", 1m),
        new DynamicVar("HardBlood", 2m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars.Heal.UpgradeValueBy(5m);
        DynamicVars["Impulse"].UpgradeValueBy(1m);
        DynamicVars["HardBlood"].UpgradeValueBy(3m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Var("Impulse"), Owner.Creature, this, false);

        int DonQuixote_hardBlood = DonQuixote_Var("HardBlood");
        if (DonQuixote_hardBlood > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_choiceContext,
                Owner.Creature,
                DonQuixote_hardBlood,
                Owner.Creature,
                this,
                false);
        }

        // 血宴 10：条件触发，够了才回血。
        if (await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue, true);
        }
    }
}

/// <summary>
/// 堂吉诃德派硬血奥义-球：5 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodOrb : DonQuixoteHardBloodCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\堂吉诃德派硬血奥义-球.png，先古卡 250x351）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_orb.png";

    public DonQuixoteHardBloodOrb()
        : base(5, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    public override int DonQuixote_HardBloodCost => DonQuixote_Var("HardBlood");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(50m, ValueProp.Move),
        new DynamicVar("HardBlood", 10m),
        new DynamicVar("ImpulseBonus", 1m),
        new DynamicVar("Vulnerable", 2m),
        new DynamicVar("FeastMultiplier", 2m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
        DynamicVars["HardBlood"].UpgradeValueBy(5m);
        DynamicVars["ImpulseBonus"].UpgradeValueBy(1m);
        DynamicVars["FeastMultiplier"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        int DonQuixote_consumed = await DonQuixote_PayHardBlood(DonQuixote_choiceContext);

        List<Creature> DonQuixote_targets = DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature).ToList();
        if (DonQuixote_targets.Count == 0)
        {
            return;
        }

        int DonQuixote_impulse = DonQuixoteBloodMagic.DonQuixote_BloodImpulse(Owner.Creature);

        // 硬血：每消耗 1 点增加 2 点基础伤害（与「桩」「鞭」一致）。
        decimal DonQuixote_damage = DynamicVars.Damage.BaseValue
            + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_consumed)
            + (DonQuixote_impulse * DynamicVars["ImpulseBonus"].IntValue);

        // 堕落值那部分增伤交给全局钩子（DonQuixotePowerCorruption.ModifyDamageMultiplicative）。

        // 「消耗血宴总数」：每 1 点 +1%（无上限）。
        int DonQuixote_total = DonQuixoteBloodMagic.DonQuixote_FeastTotal(Owner.Creature);

        int DonQuixote_multiplier = Math.Max(1, DynamicVars["FeastMultiplier"].IntValue);
        decimal DonQuixote_feastMultiplier = 1m + (DonQuixote_total * 0.01m * DonQuixote_multiplier);

        DonQuixote_damage *= DonQuixote_feastMultiplier;

        // 描述：伤害不受「难以杀灭」「滑溜」「无实体」影响 → 逐个目标走无视减免的伤害。
        foreach (Creature DonQuixote_hitTarget in DonQuixote_targets)
        {
            await DonQuixoteBloodMagic.DonQuixote_DamageIgnoringLimits(
                DonQuixote_choiceContext,
                DonQuixote_hitTarget,
                DonQuixote_damage,
                Owner.Creature,
                this,
                DonQuixote_cardPlay,
                ValueProp.Move | ValueProp.Unpowered);
        }

        foreach (Creature DonQuixote_target in DonQuixote_targets)
        {
            if (DonQuixote_target == null || DonQuixote_target.IsDead)
            {
                continue;
            }

            await PowerCmd.Apply<VulnerablePower>(
                DonQuixote_choiceContext, DonQuixote_target, DynamicVars["Vulnerable"].IntValue, Owner.Creature, this, false);

            int DonQuixote_layers = DonQuixoteBloodMagic.DonQuixote_BleedLayers(DonQuixote_target);

            // 新描述：「触发全体敌方单位身上全部流血层数的效果」→ 每个敌人按其流血层数 逐次触发流血（每次触发造成强度点伤害并给堂吉诃德等量血宴。
            for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_layers; DonQuixote_i++)
            {
                if (DonQuixote_target.IsDead
                    || DonQuixoteBloodMagic.DonQuixote_BleedStrength(DonQuixote_target) <= 0)
                {
                    break;
                }

                await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(DonQuixote_choiceContext, DonQuixote_target);
            }

            // 「去除所有敌方流血效果」：触发完把残留的强度/层数一并清掉。
            DonQuixotePowerBleed DonQuixote_bleed = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_target);
            if (DonQuixote_bleed != null)
            {
                await PowerCmd.Remove(DonQuixote_bleed);
            }

            DonQuixotePowerBleedLayers DonQuixote_layerPower =
                DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleedLayers>(DonQuixote_target);
            if (DonQuixote_layerPower != null)
            {
                await PowerCmd.Remove(DonQuixote_layerPower);
            }
        }
    }
}

/// <summary>
/// 血腥旋转木马：1 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodCarousel : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\血腥旋转木马.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_carousel.png";

    public DonQuixoteBloodCarousel()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Ethereal, CardKeyword.Exhaust };

    protected override void OnUpgrade()
    {
        // 升级后去掉「消耗」，只保留虚无。
        RemoveKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        for (int DonQuixote_i = 0; DonQuixote_i < 3; DonQuixote_i++)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, Owner.Creature, this, 0, 1);
        }

        for (int DonQuixote_j = 0; DonQuixote_j < 3; DonQuixote_j++)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, Owner.Creature, this, 2, 0);
        }
    }
}

/// <summary>
/// 向着那巨人冲锋！
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteGiantCharge : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteJustAWindmill>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteJustAWindmill>();

    public DonQuixoteGiantCharge()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Vigor", 10m),
        new DynamicVar("HpLoss", 5m),
        new DynamicVar("Corruption", 40m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Vigor"].UpgradeValueBy(5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<VigorPower>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Vigor"].BaseValue,
            Owner.Creature,
            this,
            false);

        // 失去生命：无视格挡与任何伤害修正，直接扣血。
        await CreatureCmd.Damage(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            this,
            DonQuixote_cardPlay);
    }
}

/// <summary>
/// 腐败版：那只是个风车而已：1 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteJustAWindmill : DonQuixoteBloodFeastCard, IDonQuixoteCorruptedCard
{
    public DonQuixoteJustAWindmill()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("Vulnerable", 2m),
        new DynamicVar("HardBlood", 5m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    // 堕落牌的费用折扣由 Hook.ModifyEnergyCostInCombat 补丁统一处理 （CanonicalEnergyCost 只在卡牌创建时被读一次。

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["HardBlood"].UpgradeValueBy(5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        await PowerCmd.Apply<VulnerablePower>(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            DynamicVars["Vulnerable"].IntValue,
            Owner.Creature,
            this,
            false);

        await DamageCmd.Attack(DonQuixote_ScaledDamage(DynamicVars.Damage.BaseValue))
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        // 血宴 5：条件触发，够了才获得硬血。
        int DonQuixote_hardBlood = DonQuixote_Var("HardBlood");
        if (DonQuixote_hardBlood > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_choiceContext,
                Owner.Creature,
                DonQuixote_hardBlood,
                Owner.Creature,
                this,
                false);
        }
    }
}

/// <summary>
/// 堂吉诃德派硬血 2 式-硬血：1 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodForm2 : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\堂吉诃德派硬血2式-硬血.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_form2.png";

    public DonQuixoteHardBloodForm2()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 3m),
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("BleedLayers", 2m),
        new DynamicVar("BleedStrength", 3m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars["HardBlood"].UpgradeValueBy(10m);
        DynamicVars["BleedStrength"].UpgradeValueBy(3m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            Owner.Creature,
            Owner.Creature,
            this,
            DonQuixote_Var("BleedStrength"),
            DonQuixote_Var("BleedLayers"));

        // 血宴 3：条件触发，够了才获得硬血。
        int DonQuixote_hardBlood = DonQuixote_Var("HardBlood");
        if (DonQuixote_hardBlood > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_choiceContext,
                Owner.Creature,
                DonQuixote_hardBlood,
                Owner.Creature,
                this,
                false);
        }
    }
}

/// <summary>
/// 堂吉诃德派硬血 10 式-鞭：1 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodWhip10 : DonQuixoteHardBloodCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\堂吉诃德派硬血10式-鞭.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_whip10.png";

    public DonQuixoteHardBloodWhip10()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    public override int DonQuixote_HardBloodCost => DonQuixote_Var("HardBlood");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar("HardBlood", 2m),
        new DynamicVar("Hits", 2m),
        new DynamicVar("BleedStrength", 3m),
        new DynamicVar("BleedTimes", 2m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["HardBlood"].UpgradeValueBy(3m);
        DynamicVars["Hits"].UpgradeValueBy(1m);
        DynamicVars["BleedTimes"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        int DonQuixote_consumed = await DonQuixote_PayHardBlood(DonQuixote_choiceContext);

        // 硬血：每消耗 1 点增加 2 点基础伤害（与「桩」「球」一致）。
        decimal DonQuixote_damage =
            DynamicVars.Damage.BaseValue + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_consumed);

        int DonQuixote_hits = DonQuixote_Var("Hits");
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            await DamageCmd.Attack(DonQuixote_damage)
                .FromCard(this, DonQuixote_cardPlay)
                .TargetingAllOpponents(Owner.Creature.CombatState)
                .WithHitFx("vfx/vfx_attack_slash", null, null)
                .Execute(DonQuixote_choiceContext);
        }

        int DonQuixote_bleedTimes = Math.Max(1, DonQuixote_Var("BleedTimes"));
        for (int DonQuixote_k = 0; DonQuixote_k < DonQuixote_bleedTimes; DonQuixote_k++)
        {
            foreach (Creature DonQuixote_enemy in DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature).ToList())
            {
                if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
                {
                    continue;
                }

                await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                    DonQuixote_choiceContext,
                    DonQuixote_enemy,
                    Owner.Creature,
                    this,
                    DonQuixote_Var("BleedStrength"),
                    0);
            }
        }
    }
}
