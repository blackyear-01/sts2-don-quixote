using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 楔形硬血：1 费技能，保留，施加 2 级流血与 2 层流血。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodWedge : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\楔形硬血.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_wedge.png";

    public DonQuixoteHardBloodWedge()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BleedStrength", 2m),
        new DynamicVar("BleedLayers", 2m),
        new DynamicVar("Impulse", 0m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        int DonQuixote_impulse = DynamicVars["Impulse"].IntValue;
        if (DonQuixote_impulse > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
                DonQuixote_choiceContext,
                Owner.Creature,
                DonQuixote_impulse,
                Owner.Creature,
                this,
                false);
        }

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            DonQuixote_cardPlay.Target,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            DynamicVars["BleedLayers"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Impulse"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 破碎四散的硬血：1 费技能，本回合每抽一张牌对全体敌人施加 1 级流血。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteShatteredHardBlood : ModCardTemplate
{
    public DonQuixoteShatteredHardBlood()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerShatteredHardBlood>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        // 升级后保留。关键词是实例上缓存的集合，升级不会重算 CanonicalKeywords。
        AddKeyword(CardKeyword.Retain);
    }
}

/// <summary>
/// 空虚而安逸之梦：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteEmptyComfortDream : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 80;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteSelfishWishOfFamily>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteSelfishWishOfFamily>();

    public DonQuixoteEmptyComfortDream()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 80m),
        new DynamicVar("Feast", 30m),
        new DynamicVar("FeastPerWavering", 5m),
    };

    private int DonQuixote_FeastPerWavering => DynamicVars["FeastPerWavering"].IntValue;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Feast"].IntValue, this);

        // 先加血宴再结算，取「当前血宴 / N」。
        int DonQuixote_per = DonQuixote_FeastPerWavering;
        int DonQuixote_feast = DonQuixoteBloodMagic.DonQuixote_BloodFeast(Owner.Creature);
        int DonQuixote_wavering = DonQuixote_per <= 0 ? 0 : DonQuixote_feast / DonQuixote_per;
        if (DonQuixote_wavering > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainWavering(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_wavering, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 5 -> 3：同样的血宴换到更多摇曳。
        DynamicVars["FeastPerWavering"].UpgradeValueBy(-2m);
    }
}

/// <summary>
/// 堕落版：牺牲了家族的自私愿望：3 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteSelfishWishOfFamily : DonQuixoteHardBloodCard, IDonQuixoteCorruptedCard
{
    public DonQuixoteSelfishWishOfFamily()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    public override int DonQuixote_HardBloodCost => DonQuixote_Var("HardBlood");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("HardBlood", 3m),
        new DynamicVar("Impulse", 5m),
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("Hits", 3m),
        new DynamicVar("BleedLayers", 8m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        int DonQuixote_consumed = await DonQuixote_PayHardBlood(DonQuixote_choiceContext);

        await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Impulse"].IntValue,
            Owner.Creature,
            this,
            false);

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
                0,
                DonQuixote_Var("BleedLayers"));
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
        AddKeyword(CardKeyword.Retain);
    }
}

/// <summary>
/// 摧垮：1 费攻击，血宴 5（升级 2），获得 4 层硬血（升级 7 层），造成 7 点伤害。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteCrush : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/crush.png";

    public DonQuixoteCrush()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.RandomEnemy)
    {
    }

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("HardBlood", 4m),
        new DamageVar(7m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        // 目标类型是「随机敌人」时 cardPlay.Target 是空的。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_cardPlay)
            .TargetingRandomOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        // 血宴 5：条件触发，够了才获得硬血。
        int DonQuixote_hardBlood = DynamicVars["HardBlood"].IntValue;
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

    protected override void OnUpgrade()
    {
        // 卡牌.doc：升级只把硬血 4 -> 7，血宴仍然是 5。
        DynamicVars["HardBlood"].UpgradeValueBy(3m);
    }
}

/// <summary>
/// 致伤：1 费攻击，造成 4 点伤害并施加 8 级流血（升级 14 级）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteWound : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/wound.png";

    public DonQuixoteWound()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("BleedStrength", 10m),
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
        DynamicVars["BleedStrength"].UpgradeValueBy(7m);
    }
}

/// <summary>
/// 能量灌注（联机）：1 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteEnergyInfusion : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\能量灌注.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/energy_infusion.png";

    public DonQuixoteEnergyInfusion()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
    {
    }

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("Strength", 3m),
        new DynamicVar("BloodArmor", 5m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        // 血宴 10：条件触发，判定一次，触发后所有友方都拿到血铠。
        bool DonQuixote_boosted = await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext);

        List<Creature> DonQuixote_allies = DonQuixoteBloodMagic.DonQuixote_PlayerCreatures(Owner.Creature);
        foreach (Creature DonQuixote_ally in DonQuixote_allies)
        {
            await PowerCmd.Apply<StrengthPower>(
                DonQuixote_choiceContext,
                DonQuixote_ally,
                DynamicVars["Strength"].IntValue,
                Owner.Creature,
                this,
                false);

            if (!DonQuixote_boosted)
            {
                continue;
            }

            await PowerCmd.Apply<DonQuixotePowerBloodArmor>(
                DonQuixote_choiceContext,
                DonQuixote_ally,
                DynamicVars["BloodArmor"].IntValue,
                Owner.Creature,
                this,
                false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Strength"].UpgradeValueBy(2m);
        DynamicVars["BloodArmor"].UpgradeValueBy(5m);
    }
}

/// <summary>
/// 由此踏上冒险之途：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteSetOutOnAdventure : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>
    /// 卡面插画（资源\卡牌\由此踏上冒险之途.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/set_out_on_adventure.png";

    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteBetrayalOfKin>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteBetrayalOfKin>();

    /// <summary>
    /// 「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteSetOutOnAdventure()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 40m),
        new DynamicVar("Wavering", 10m),
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("BleedStrength", 5m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Wavering"].IntValue, this);

        await PowerCmd.Apply<DonQuixotePowerHardBlood>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["HardBlood"].IntValue,
            Owner.Creature,
            this,
            false);

        // 目标是自身：自己给自己上流血不吃复还的血之冲动。
        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            Owner.Creature,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            0);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Wavering"].UpgradeValueBy(5m);
        DynamicVars["HardBlood"].UpgradeValueBy(4m);
        DynamicVars["BleedStrength"].UpgradeValueBy(-2m);
    }
}

/// <summary>
/// 堕落版：来自亲族的背叛：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBetrayalOfKin : DonQuixoteBloodFeastCard, IDonQuixoteCorruptedCard
{
    public DonQuixoteBetrayalOfKin()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("Impulse", 5m),
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("BleedStrength", 10m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerHardBlood>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["HardBlood"].IntValue,
            Owner.Creature,
            this,
            false);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_choiceContext,
            Owner.Creature,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            0);

        // 血宴 10：条件触发，够了才获得复还的血之冲动。
        int DonQuixote_impulse = DynamicVars["Impulse"].IntValue;
        if (DonQuixote_impulse > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
                DonQuixote_choiceContext,
                Owner.Creature,
                DonQuixote_impulse,
                Owner.Creature,
                this,
                false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BleedStrength"].UpgradeValueBy(-4m);
        DynamicVars["BloodFeast"].UpgradeValueBy(-5m);
    }
}

/// <summary>
/// 血之欢喜：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodJoy : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_joy.png";

    public DonQuixoteBloodJoy()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BleedStrength", 10m),
        new DynamicVar("Triggers", 2m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        List<Creature> DonQuixote_enemies = DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature).ToList();

        foreach (Creature DonQuixote_enemy in DonQuixote_enemies)
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
                DynamicVars["BleedStrength"].IntValue,
                0);
        }

        int DonQuixote_triggers = DynamicVars["Triggers"].IntValue;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_triggers; DonQuixote_i++)
        {
            foreach (Creature DonQuixote_enemy in DonQuixote_enemies)
            {
                if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
                {
                    continue;
                }

                // 卡牌写的「触发 X 次流血效果」只是额外结算一次伤害，不消耗层数。
                await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
                    DonQuixote_choiceContext, DonQuixote_enemy, false);
            }
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}

/// <summary>
/// 无止尽的饥饿：3 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteEndlessHunger : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\无止尽的饥饿.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/endless_hunger.png";

    public DonQuixoteEndlessHunger()
        : base(4, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("Hits", 1m),
        new DynamicVar("FeastPerHit", 10m),
        new DynamicVar("BleedLayers", 3m),
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("MaxFeast", 300m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        int DonQuixote_hardBlood = await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["HardBlood"].IntValue);

        // 硬血：每消耗 1 点增加 2 点基础伤害。
        decimal DonQuixote_hitDamage =
            DynamicVars.Damage.BaseValue + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_hardBlood);

        // 消耗至多 MaxFeast 点血宴：按实际消耗的点数决定追加攻击次数。
        int DonQuixote_spend = await DonQuixoteBloodMagic.DonQuixote_PayBloodFeastUpTo(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["MaxFeast"].IntValue, this);
        int DonQuixote_per = Math.Max(1, DynamicVars["FeastPerHit"].IntValue);
        int DonQuixote_hits = DynamicVars["Hits"].IntValue + (DonQuixote_spend / DonQuixote_per);

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            await DamageCmd.Attack(DonQuixote_hitDamage)
                .FromCard(this, DonQuixote_cardPlay)
                .TargetingAllOpponents(Owner.Creature.CombatState)
                .WithHitFx("vfx/vfx_attack_slash", null, null)
                .Execute(DonQuixote_choiceContext);
        }

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
                0,
                DynamicVars["BleedLayers"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FeastPerHit"].UpgradeValueBy(-5m);
    }
}


/// <summary>
/// 血喜：1 费攻击，血宴 10，获得 3 层硬血（升级 8 层），造成 10 点伤害，目标每有 1 级流血便 +1 点伤害。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodCheer : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_cheer.png";

    public DonQuixoteBloodCheer()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("HardBlood", 3m),
        new DamageVar(10m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        int DonQuixote_bleed = DonQuixoteBloodMagic.DonQuixote_BleedStrength(DonQuixote_cardPlay.Target);
        decimal DonQuixote_damage = DynamicVars.Damage.BaseValue + (DonQuixote_bleed > 0 ? DonQuixote_bleed : 0);

        await DamageCmd.Attack(DonQuixote_damage)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        // 血宴 5：条件触发，够了才获得硬血。
        int DonQuixote_hardBlood = DynamicVars["HardBlood"].IntValue;
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

    protected override void OnUpgrade()
    {
        DynamicVars["HardBlood"].UpgradeValueBy(5m);
    }
}

/// <summary>
/// 所有凝固的血：1 费能力，自身受到的流血伤害 -50%（升级后固有，并且 -75%）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteAllClottedBlood : ModCardTemplate
{
    public DonQuixoteAllClottedBlood()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Reduction", 50m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        // 能力是 Single（层数固定 1、数值就存在 Amount 上）。
        DonQuixotePowerAllClottedBlood DonQuixote_existing =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerAllClottedBlood>(Owner.Creature);
        if (DonQuixote_existing != null)
        {
            await PowerCmd.Remove(DonQuixote_existing);
        }

        await PowerCmd.Apply<DonQuixotePowerAllClottedBlood>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Reduction"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Reduction"].UpgradeValueBy(25m);
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 披上鲜血，听从血液的驱使：2 费技能，获得 10 层血铠并抽 3 张牌。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteCladInBlood : ModCardTemplate
{
    public DonQuixoteCladInBlood()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodArmor", 10m),
        new DynamicVar("Cards", 3m),
        new DynamicVar("Energy", 0m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerBloodArmor>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["BloodArmor"].IntValue,
            Owner.Creature,
            this,
            false);

        await CardPileCmd.Draw(DonQuixote_choiceContext, DynamicVars["Cards"].IntValue, Owner, false);

        int DonQuixote_energy = DynamicVars["Energy"].IntValue;
        if (DonQuixote_energy > 0)
        {
            await PlayerCmd.GainEnergy(DonQuixote_energy, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BloodArmor"].UpgradeValueBy(5m);
        DynamicVars["Energy"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 猩红之爪：2 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteCrimsonClaw : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\猩红之爪.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/crimson_claw.png";

    public DonQuixoteCrimsonClaw()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Hits", 3m),
        new DynamicVar("HardBlood", 3m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        // 硬血：每消耗 1 点增加 2 点基础伤害。
        int DonQuixote_consumed = await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["HardBlood"].IntValue);
        decimal DonQuixote_damage =
            DynamicVars.Damage.BaseValue + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_consumed);

        AttackCommand DonQuixote_attack = await DamageCmd.Attack(DonQuixote_damage)
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        int DonQuixote_total = DonQuixote_attack.Results
            .SelectMany(DonQuixote_results => DonQuixote_results)
            .Sum(DonQuixote_result => DonQuixote_result.TotalDamage);

        if (DonQuixote_total > 0)
        {
            await CreatureCmd.Heal(Owner.Creature, DonQuixote_total, true);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 鲜血吸收：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodAbsorb : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\鲜血吸收.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_absorb.png";

    public DonQuixoteBloodAbsorb()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Remove", 1m),
        new DynamicVar("Heal", 6m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        int DonQuixote_remove = DynamicVars["Remove"].IntValue;

        // 「随机」必须用同步随机源：Random.Shared 每端都不一样，一用就会数据不同步。
        Rng DonQuixote_rng = Owner.RunState?.Rng?.CombatCardSelection;

        // 注意用 Amount != 0 而不是 > 0：像「缩小」(ShrinkPower) 这种没有回合数、用负数（Amount < 0）表示「永久持续」的负面状态。
        List<PowerModel> DonQuixote_debuffs = Owner.Creature.Powers
            .Where(DonQuixote_power => DonQuixote_power != null
                && DonQuixote_power.Type == PowerType.Debuff
                && DonQuixote_power.IsVisible
                && DonQuixote_power.Amount != 0)
            .ToList();

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_remove && DonQuixote_debuffs.Count > 0; DonQuixote_i++)
        {
            int DonQuixote_index = DonQuixote_rng != null
                ? DonQuixote_rng.NextInt(DonQuixote_debuffs.Count)
                : 0;

            PowerModel DonQuixote_target = DonQuixote_debuffs[DonQuixote_index];
            DonQuixote_debuffs.RemoveAt(DonQuixote_index);
            await PowerCmd.Remove(DonQuixote_target);
        }

        decimal DonQuixote_heal = DynamicVars["Heal"].BaseValue;
        if (DonQuixote_heal > 0m)
        {
            await CreatureCmd.Heal(Owner.Creature, DonQuixote_heal, true);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Remove"].UpgradeValueBy(1m);
        DynamicVars["Heal"].UpgradeValueBy(4m);
    }
}

/// <summary>
/// 公平的决斗：2 费技能（升级 1 费），目标为任意敌人。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteFairDuel : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>
    /// 优先复制的增益（按卡面顺序）。
    /// </summary>
    private static readonly Type[] DonQuixote_PriorityBuffs =
    {
        typeof(BarricadePower),
        typeof(RitualPower),
        typeof(SlipperyPower),
        typeof(ArtifactPower),
        typeof(HardToKillPower),
        typeof(EnragePower),
        typeof(DoomPower),
        typeof(TaintedPower),
    };

    /// <summary>
    /// 「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteFairDuel()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public int DonQuixote_CorruptAt => 80;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteWoundDestruction>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteWoundDestruction>(), DonQuixote_owner);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 80m),
        new DynamicVar("Duel", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        Creature DonQuixote_target = DonQuixote_cardPlay.Target;
        if (DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return;
        }

        Rng DonQuixote_rng = Owner.RunState?.Rng?.CombatCardSelection;

        // 优先从选中的目标身上复制。
        List<PowerModel> DonQuixote_buffs = DonQuixote_BuffsOf(DonQuixote_target);
        if (DonQuixote_buffs.Count == 0)
        {
            List<Creature> DonQuixote_donors = DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature)
                .Where(DonQuixote_enemy => DonQuixote_enemy != null
                    && !DonQuixote_enemy.IsDead
                    && DonQuixote_BuffsOf(DonQuixote_enemy).Count > 0)
                .ToList();

            if (DonQuixote_donors.Count > 0)
            {
                int DonQuixote_donorIndex = DonQuixote_rng != null
                    ? DonQuixote_rng.NextInt(DonQuixote_donors.Count)
                    : 0;
                DonQuixote_buffs = DonQuixote_BuffsOf(DonQuixote_donors[DonQuixote_donorIndex]);
            }
        }

        if (DonQuixote_buffs.Count > 0)
        {
            PowerModel DonQuixote_chosen = null;
            foreach (Type DonQuixote_type in DonQuixote_PriorityBuffs)
            {
                DonQuixote_chosen = DonQuixote_buffs.FirstOrDefault(
                    DonQuixote_buff => DonQuixote_buff.GetType() == DonQuixote_type);
                if (DonQuixote_chosen != null)
                {
                    break;
                }
            }

            if (DonQuixote_chosen == null)
            {
                int DonQuixote_buffIndex = DonQuixote_rng != null
                    ? DonQuixote_rng.NextInt(DonQuixote_buffs.Count)
                    : 0;
                DonQuixote_chosen = DonQuixote_buffs[DonQuixote_buffIndex];
            }

            await DonQuixote_CopyPower(DonQuixote_choiceContext, DonQuixote_chosen);
        }

        // 决斗对象最大只有 1 层，重复施加不会叠到 2 层。
        if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelTarget>(DonQuixote_target) == null)
        {
            await PowerCmd.Apply<DonQuixotePowerDuelTarget>(
                DonQuixote_choiceContext,
                DonQuixote_target,
                1m,
                Owner.Creature,
                this,
                false);
        }
    }

    /// <summary>
    /// 复制黑名单统一放在 <see cref="DonQuixotePowerBlacklist"/>。
    /// </summary>
    private static bool DonQuixote_IsCopyBlacklisted(PowerModel DonQuixote_power)
    {
        return DonQuixotePowerBlacklist.DonQuixote_IsBlocked(DonQuixote_power);
    }

    private static List<PowerModel> DonQuixote_BuffsOf(Creature DonQuixote_creature)
    {
        return DonQuixote_creature.Powers
            .Where(DonQuixote_power => DonQuixote_power != null
                && DonQuixote_power.Type == PowerType.Buff
                && DonQuixote_power.IsVisible
                && DonQuixote_power.Amount > 0
                && !DonQuixote_IsCopyBlacklisted(DonQuixote_power))
            .ToList();
    }

    /// <summary>
    /// 复制一份能力给自己：从模型库里取规范实例再克隆。
    /// </summary>
    private async Task DonQuixote_CopyPower(PlayerChoiceContext DonQuixote_ctx, PowerModel DonQuixote_source)
    {
        if (DonQuixote_source == null || Owner?.Creature == null)
        {
            return;
        }

        PowerModel DonQuixote_canonical = null;
        try
        {
            DonQuixote_canonical = ModelDb.DebugPower(DonQuixote_source.GetType());
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] fair duel copy failed: " + DonQuixote_ex.Message, 0);
        }

        if (DonQuixote_canonical == null)
        {
            return;
        }

        int DonQuixote_amount = DonQuixote_source.Amount;

        // ToMutable(initialAmount) 内部会调用 SetAmount。
        PowerModel DonQuixote_copy = DonQuixote_canonical.ToMutable(0);

        await PowerCmd.Apply(
            DonQuixote_ctx,
            DonQuixote_copy,
            Owner.Creature,
            DonQuixote_amount,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
