using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 骑士长枪：只能由「血铸 / 技巧 / 增幅」放进手牌。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteKnightLance : ModCardTemplate, IDonQuixoteCorruptible, IDonQuixoteTokenCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\骑士长枪.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/knight_lance.png";

    public DonQuixoteKnightLance()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight, CardKeyword.Retain };

    public int DonQuixote_CorruptAt => DonQuixoteLance.DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteBloodFeastLance>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteBloodFeastLance>(), DonQuixote_owner);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Corruption", 40m),
        new DynamicVar("Hits", 1m),
        new DynamicVar("Armor", 3m),
        new DynamicVar("Wavering", 3m),
    };

    /// <summary>
    /// 打出后回到手牌（照抄原版「粒子墙」ParticleWall 的写法）。
    /// </summary>
    protected override CardLocation GetResultLocationForCardPlay()
    {
        CardLocation DonQuixote_location = base.GetResultLocationForCardPlay();
        if (DonQuixote_location.pileType == PileType.Discard)
        {
            DonQuixote_location.pileType = PileType.Hand;
        }

        return DonQuixote_location;
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        Creature DonQuixote_owner = Owner.Creature;
        // 打出时闪一下「举枪突刺」的立绘（资源\人物\长枪.png）。
        DonQuixoteLancePose.DonQuixote_Play(DonQuixote_owner);
        int DonQuixote_bonus = DonQuixoteLance.DonQuixote_ForgeBonus(DonQuixote_owner);
        int DonQuixote_hits = 1 + DonQuixoteLance.DonQuixote_TechniqueBonus(DonQuixote_owner);
        // 基础值用常量：DynamicVars 里的数字只负责卡面显示，避免重复加成。
        decimal DonQuixote_damage = 3m + DonQuixote_bonus;

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            if (DonQuixote_play.Target == null || DonQuixote_play.Target.IsDead)
            {
                break;
            }

            await CreatureCmd.Damage(
                DonQuixote_ctx, DonQuixote_play.Target, DonQuixote_damage, ValueProp.Move, DonQuixote_owner, this, DonQuixote_play);
        }

        int DonQuixote_amplify = DonQuixoteLance.DonQuixote_AmplifyBonus(DonQuixote_owner);
        int DonQuixote_armor = 3 + DonQuixote_amplify;
        int DonQuixote_wavering = 3 + DonQuixote_amplify;

        foreach (Creature DonQuixote_ally in DonQuixoteBloodMagic.DonQuixote_PlayerCreatures(DonQuixote_owner).ToList())
        {
            if (DonQuixote_ally.IsDead)
            {
                continue;
            }

            await PowerCmd.Apply<DonQuixotePowerBloodArmor>(
                DonQuixote_ctx, DonQuixote_ally, DonQuixote_armor, DonQuixote_owner, this, false);

            // 文档：为全体队友施加 3 层摇曳【血魔】（增幅再加）。
            await DonQuixoteBloodMagic.DonQuixote_GainWavering(
                DonQuixote_ctx, DonQuixote_ally, DonQuixote_wavering, this);
        }
    }
}

/// <summary>
/// 饱食鲜血的长枪（骑士长枪的堕落版）：保留、血宴 1、打出后返回手牌、造成 6 点伤害、触发 1 次流血效果。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodFeastLance : DonQuixoteBloodFeastCard, IDonQuixoteCorruptedCard, IDonQuixoteTokenCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\饱食鲜血的长枪.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_feast_lance.png";

    public DonQuixoteBloodFeastLance()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight, CardKeyword.Retain };

    private int DonQuixote_Var(string DonQuixote_name)
    {
        DynamicVar DonQuixote_var;
        return DynamicVars.TryGetValue(DonQuixote_name, out DonQuixote_var) ? DonQuixote_var.IntValue : 0;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("BloodFeast", 1m),
        new DynamicVar("Hits", 1m),
        new DynamicVar("Triggers", 1m),
        new DynamicVar("Bleed", 1m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    /// <summary>
    /// 打出后回到手牌（照抄原版「粒子墙」ParticleWall 的写法）。
    /// </summary>
    protected override CardLocation GetResultLocationForCardPlay()
    {
        CardLocation DonQuixote_location = base.GetResultLocationForCardPlay();
        if (DonQuixote_location.pileType == PileType.Discard)
        {
            DonQuixote_location.pileType = PileType.Hand;
        }

        return DonQuixote_location;
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        // 血宴 1：条件触发。
        bool DonQuixote_feastPaid = await DonQuixote_TriggerBloodFeast(DonQuixote_ctx);

        Creature DonQuixote_owner = Owner.Creature;
        // 打出时闪一下「举枪突刺」的立绘（资源\人物\长枪.png）。
        DonQuixoteLancePose.DonQuixote_Play(DonQuixote_owner);
        int DonQuixote_bonus = DonQuixoteLance.DonQuixote_ForgeBonus(DonQuixote_owner);
        int DonQuixote_hits = 1 + DonQuixoteLance.DonQuixote_TechniqueBonus(DonQuixote_owner);
        // 基础值用常量：DynamicVars 里的数字只负责卡面显示，避免重复加成。
        decimal DonQuixote_damage = 6m + DonQuixote_bonus;
        bool DonQuixote_all = DonQuixoteLance.DonQuixote_LanceHitsAll(DonQuixote_owner);

        List<Creature> DonQuixote_targets = new List<Creature>();
        if (DonQuixote_all)
        {
            DonQuixote_targets.AddRange(DonQuixoteBloodMagic.DonQuixote_EnemiesOf(DonQuixote_owner));
        }
        else if (DonQuixote_play.Target != null)
        {
            DonQuixote_targets.Add(DonQuixote_play.Target);
        }

        int DonQuixote_triggers = 1 + DonQuixoteLance.DonQuixote_AmplifyBonus(DonQuixote_owner);
        // 基础 1 级流血；技巧、增幅各再加 X 级。
        int DonQuixote_bleed =
            1 + DonQuixoteLance.DonQuixote_TechniqueBonus(DonQuixote_owner)
              + DonQuixoteLance.DonQuixote_AmplifyBonus(DonQuixote_owner);

        foreach (Creature DonQuixote_target in DonQuixote_targets)
        {
            if (DonQuixote_target == null || DonQuixote_target.IsDead)
            {
                continue;
            }

            for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
            {
                if (DonQuixote_target.IsDead)
                {
                    break;
                }

                await CreatureCmd.Damage(
                    DonQuixote_ctx, DonQuixote_target, DonQuixote_damage, ValueProp.Move, DonQuixote_owner, this, DonQuixote_play);
            }

            if (!DonQuixote_target.IsDead && DonQuixote_bleed > 0)
            {
                await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                    DonQuixote_ctx, DonQuixote_target, DonQuixote_owner, this, DonQuixote_bleed, 0);
            }

            for (int DonQuixote_t = 0; DonQuixote_t < DonQuixote_triggers && DonQuixote_feastPaid; DonQuixote_t++)
            {
                if (DonQuixote_target.IsDead)
                {
                    break;
                }

                await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(DonQuixote_ctx, DonQuixote_target, false);
            }
        }
    }
}

/// <summary>
/// 凝聚鲜血：1 费，对自身施加 6 级流血，血铸 3（升级 6）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteCondenseBlood : ModCardTemplate, IDonQuixoteBloodForgeCard
{
    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public DonQuixoteCondenseBlood()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Bleed", 8m),
        new DynamicVar("Forge", 2m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Forge"].UpgradeValueBy(2m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_ctx, Owner.Creature, Owner.Creature, this, DynamicVars["Bleed"].IntValue, 0);

        await DonQuixoteLance.DonQuixote_ForgeFromCard(
            DonQuixote_ctx, Owner, DynamicVars["Forge"].IntValue, this);
    }
}

/// <summary>
/// 战术技巧：1 费，技巧 1、消耗 1 张牌、丢弃 1 张牌（升级获得保留）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteTacticalTechnique : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\战术技巧.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/tactical_technique.png";

    public DonQuixoteTacticalTechnique()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_Technique,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Technique", 1m),
    };

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteLance.DonQuixote_Technique(DonQuixote_ctx, Owner, DynamicVars["Technique"].IntValue, this);

        CardSelectorPrefs DonQuixote_exhaustPrefs = new CardSelectorPrefs(
            new LocString("cards", "DON_QUIXOTE_CARD_DON_QUIXOTE_TACTICAL_TECHNIQUE.selectionScreenPrompt"), 1);
        CardModel DonQuixote_exhaust = (await CardSelectCmd.FromHand(DonQuixote_ctx, Owner, DonQuixote_exhaustPrefs, null, this))
            .FirstOrDefault();
        if (DonQuixote_exhaust != null)
        {
            await CardCmd.Exhaust(DonQuixote_ctx, DonQuixote_exhaust, false, false);
        }

        CardModel DonQuixote_discard = (await CardSelectCmd.FromHand(
                DonQuixote_ctx,
                Owner,
                new CardSelectorPrefs(
                    new LocString("cards", "DON_QUIXOTE_CARD_DON_QUIXOTE_TACTICAL_TECHNIQUE.discardSelectionPrompt"), 1),
                null,
                this))
            .FirstOrDefault();
        if (DonQuixote_discard != null)
        {
            await CardCmd.Discard(DonQuixote_ctx, DonQuixote_discard);
        }
    }
}

/// <summary>
/// 如骑士般提供保护：2 费能力。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteKnightlyProtection : ModCardTemplate, IDonQuixoteCorruptible, IDonQuixoteBloodForgeCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/knightly_protection.png";

    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public DonQuixoteKnightlyProtection()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_Knight,
        DonQuixoteKeywords.DonQuixote_BloodForge,
        DonQuixoteKeywords.DonQuixote_Technique,
        DonQuixoteKeywords.DonQuixote_Amplify,
    };

    public int DonQuixote_CorruptAt => DonQuixoteLance.DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteBloodSweep>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteBloodSweep>(), DonQuixote_owner);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Forge", 2m),
        new DynamicVar("Technique", 2m),
        new DynamicVar("Amplify", 5m),
        new DynamicVar("Corruption", 40m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Forge"].UpgradeValueBy(1m);
        DynamicVars["Amplify"].UpgradeValueBy(5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 「重放 1」必须先挂上。
        await PowerCmd.Apply<DonQuixotePowerKnightProtection>(DonQuixote_ctx, Owner.Creature, 1m, Owner.Creature, this, false);

        await DonQuixoteLance.DonQuixote_ForgeFromCard(
            DonQuixote_ctx, Owner, DynamicVars["Forge"].IntValue, this);
        await DonQuixoteLance.DonQuixote_Technique(DonQuixote_ctx, Owner, DynamicVars["Technique"].IntValue, this);
        await DonQuixoteLance.DonQuixote_Amplify(DonQuixote_ctx, Owner, DynamicVars["Amplify"].IntValue, this);

        // 长枪可能早就躺在手里/牌堆里了（之前用血铸拿过），这里统一把重放同步上去。
        DonQuixoteLance.DonQuixote_RefreshLanceStats(Owner);
    }
}

/// <summary>
/// 用鲜血荡涤前路（如骑士般提供保护的堕落版）：1 费能力，饱食鲜血的长枪攻击全体敌人。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodSweep : ModCardTemplate, IDonQuixoteCorruptedCard, IDonQuixoteBloodForgeCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_sweep.png";

    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public DonQuixoteBloodSweep()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
        DonQuixoteKeywords.DonQuixote_Technique,
        DonQuixoteKeywords.DonQuixote_Amplify,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Forge", 1m),
        new DynamicVar("Technique", 3m),
        new DynamicVar("Amplify", 1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Forge"].UpgradeValueBy(2m);
        DynamicVars["Amplify"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteLance.DonQuixote_ForgeFromCard(
            DonQuixote_ctx, Owner, DynamicVars["Forge"].IntValue, this);
        await DonQuixoteLance.DonQuixote_Technique(DonQuixote_ctx, Owner, DynamicVars["Technique"].IntValue, this);
        await DonQuixoteLance.DonQuixote_Amplify(DonQuixote_ctx, Owner, DynamicVars["Amplify"].IntValue, this);
        await PowerCmd.Apply<DonQuixotePowerBloodSweep>(DonQuixote_ctx, Owner.Creature, 1m, Owner.Creature, this, false);
    }
}

/// <summary>
/// 鲜血重铸：2 费能力，每消耗 6 点硬血血铸 1 次（升级减 1 费，并额外增幅 1）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodRecast : ModCardTemplate
{
    public DonQuixoteBloodRecast()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerBloodRecast>(DonQuixote_ctx, Owner.Creature, 1m, Owner.Creature, this, false);

        if (IsUpgraded)
        {
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBloodRecast>(Owner.Creature)
                ?.DonQuixote_Amplified = true;
        }
    }
}

/// <summary>
/// 无法摆脱的可怖的疾病：2 费能力，回合开始时给予全场 2 层流血（升级 3 层并获得保留）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteDreadfulDisease : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\无法摆脱的可怖疾病.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/dreadful_disease.png";

    public DonQuixoteDreadfulDisease()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Layers", 2m),
    };

    protected override void OnUpgrade()
    {
        // 升级后：给予 3 层流血，并获得「保留」。
        DynamicVars["Layers"].UpgradeValueBy(1m);
        AddKeyword(CardKeyword.Retain);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerDreadfulDisease>(
            DonQuixote_ctx, Owner.Creature, DynamicVars["Layers"].IntValue, Owner.Creature, this, false);
    }
}

/// <summary>
/// 硬血11式-贯穿：3 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodPierce : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_pierce.png";

    public DonQuixoteHardBloodPierce()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("Hits", 3m),
        new DynamicVar("Bleed", 4m),
        new DynamicVar("HardBlood", 5m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["Bleed"].UpgradeValueBy(2m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null || DonQuixote_play.Target == null)
        {
            return;
        }

        // 硬血：每消耗 1 点增加 2 点基础伤害。
        int DonQuixote_consumed = await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_ctx, Owner.Creature, DynamicVars["HardBlood"].IntValue);
        decimal DonQuixote_damage =
            DynamicVars.Damage.BaseValue + DonQuixoteBloodMagic.DonQuixote_HardBloodDamageBonus(DonQuixote_consumed);

        Creature DonQuixote_target = DonQuixote_play.Target;
        int DonQuixote_hits = DynamicVars["Hits"].IntValue;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            if (DonQuixote_target.IsDead)
            {
                return;
            }

            await CreatureCmd.Damage(
                DonQuixote_ctx, DonQuixote_target, DonQuixote_damage, ValueProp.Move, Owner.Creature, this, DonQuixote_play);
        }

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_ctx, DonQuixote_target, Owner.Creature, this, 0, DynamicVars["Bleed"].IntValue);

        int DonQuixote_layers = DonQuixoteBloodMagic.DonQuixote_BleedLayers(DonQuixote_target);
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_layers && !DonQuixote_target.IsDead; DonQuixote_i++)
        {
            // 不扣层：次数要等于层数，扣层会在中途把流血打光（层数用完流血就被移除了）。
            await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(DonQuixote_ctx, DonQuixote_target, false);
        }

        if (DonQuixote_target.IsDead)
        {
            return;
        }

        // 然后把层数压到 1 层（保留强度），和卡面的「把层数压到1层」对上。
        DonQuixotePowerBleedLayers DonQuixote_layersPower =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleedLayers>(DonQuixote_target);
        if (DonQuixote_layersPower != null && DonQuixote_layersPower.Amount > DonQuixote_KeepLayers)
        {
            await PowerCmd.ModifyAmount(
                DonQuixote_ctx,
                DonQuixote_layersPower,
                -(DonQuixote_layersPower.Amount - DonQuixote_KeepLayers),
                Owner.Creature,
                this,
                true);
        }
    }

    /// <summary>
    /// 贯穿结算后留下的流血层数（卡面写的是 1 层）。
    /// </summary>
    private const int DonQuixote_KeepLayers = 1;
}

/// <summary>
/// 硬血3式-突袭：2 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodRaid : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_raid.png";

    public DonQuixoteHardBloodRaid()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("MaxFeast", 50m),
        new DynamicVar("FeastPerPoint", 10m),
        new DynamicVar("Bleed", 3m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["FeastPerPoint"].UpgradeValueBy(-5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null || DonQuixote_play.Target == null)
        {
            return;
        }

        int DonQuixote_spend = await DonQuixoteBloodMagic.DonQuixote_PayBloodFeastUpTo(
            DonQuixote_ctx, Owner.Creature, DynamicVars["MaxFeast"].IntValue, this);
        int DonQuixote_per = Math.Max(1, DynamicVars["FeastPerPoint"].IntValue);

        int DonQuixote_hardBlood = DonQuixote_spend / DonQuixote_per;
        if (DonQuixote_hardBlood > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_ctx, Owner.Creature, DonQuixote_hardBlood, Owner.Creature, this, false);
        }

        decimal DonQuixote_before = DonQuixote_play.Target.CurrentHp;
        await CreatureCmd.Damage(
            DonQuixote_ctx, DonQuixote_play.Target, DynamicVars.Damage.BaseValue, ValueProp.Move, Owner.Creature, this, DonQuixote_play);
        decimal DonQuixote_dealt = DonQuixote_before - DonQuixote_play.Target.CurrentHp;

        int DonQuixote_gain = (int)Math.Ceiling(Math.Max(0m, DonQuixote_dealt));
        if (DonQuixote_gain > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(DonQuixote_ctx, Owner.Creature, DonQuixote_gain, this);
        }

        if (!DonQuixote_play.Target.IsDead)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_ctx, DonQuixote_play.Target, Owner.Creature, this, 0, DynamicVars["Bleed"].IntValue);
        }
    }
}

/// <summary>
/// 伤口破坏（公平的决斗的堕落版）：2 费。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteWoundDestruction : ModCardTemplate, IDonQuixoteCorruptedCard
{
    public DonQuixoteWoundDestruction()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Remove", 1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Remove"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null || DonQuixote_play.Target == null)
        {
            return;
        }

        // 与「公平的决斗」共用同一份黑名单：沙坑（无厌沙虫的吞人计数）、多实例状态、本模组状态等都不能解。
        List<PowerModel> DonQuixote_buffs = DonQuixote_play.Target.Powers
            .Where(DonQuixote_power => DonQuixote_power != null
                && DonQuixote_power.Type == PowerType.Buff
                && DonQuixote_power.Amount > 0
                && !DonQuixotePowerBlacklist.DonQuixote_IsBlocked(DonQuixote_power))
            .OrderBy(DonQuixote_power => DonQuixote_power.Id.Entry, StringComparer.Ordinal)
            .ToList();

        int DonQuixote_remove = DynamicVars["Remove"].IntValue;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_remove && DonQuixote_i < DonQuixote_buffs.Count; DonQuixote_i++)
        {
            await PowerCmd.Remove(DonQuixote_buffs[DonQuixote_i]);
        }

        await PowerCmd.Apply<DonQuixotePowerWoundDestruction>(
            DonQuixote_ctx, DonQuixote_play.Target, 1m, Owner.Creature, this, false);
    }
}
