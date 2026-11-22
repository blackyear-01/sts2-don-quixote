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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 血液扩散：3 费攻击，对全体敌人造成 21 点伤害并恢复等量生命，
/// 施加 5 级流血与 5 层流血。升级后改为「1 级流血 6 次 + 1 层流血 6 次」。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodSpread : ModCardTemplate
{
    /// <summary>卡面插画（资源\卡牌\血液扩散.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/blood_spread.png";

    public DonQuixoteBloodSpread()
        : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(21m, ValueProp.Move),
        new DynamicVar("BleedStrength", 5m),
        new DynamicVar("BleedLayers", 5m),
        new DynamicVar("Times", 1m),
    };

    protected override void OnUpgrade()
    {
        // 5 级 / 5 层 → 1 级 / 1 层，但改成各 6 次。
        DynamicVars["BleedStrength"].UpgradeValueBy(-4m);
        DynamicVars["BleedLayers"].UpgradeValueBy(-4m);
        DynamicVars["Times"].UpgradeValueBy(5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        List<Creature> DonQuixote_targets =
            DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature).ToList();

        int DonQuixote_times = Math.Max(1, DynamicVars["Times"].IntValue);
        int DonQuixote_strength = DynamicVars["BleedStrength"].IntValue;
        int DonQuixote_layers = DynamicVars["BleedLayers"].IntValue;

        decimal DonQuixote_dealt = 0m;
        foreach (Creature DonQuixote_target in DonQuixote_targets)
        {
            if (DonQuixote_target == null || DonQuixote_target.IsDead)
            {
                continue;
            }

            decimal DonQuixote_before = DonQuixote_target.CurrentHp;
            await CreatureCmd.Damage(
                DonQuixote_choiceContext,
                DonQuixote_target,
                DynamicVars.Damage.BaseValue,
                ValueProp.Move,
                Owner.Creature,
                this,
                DonQuixote_cardPlay);
            DonQuixote_dealt += Math.Max(0m, DonQuixote_before - DonQuixote_target.CurrentHp);

            for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_times; DonQuixote_i++)
            {
                if (DonQuixote_target.IsDead)
                {
                    break;
                }

                await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                    DonQuixote_choiceContext,
                    DonQuixote_target,
                    Owner.Creature,
                    this,
                    DonQuixote_strength,
                    DonQuixote_layers);
            }
        }

        int DonQuixote_heal = (int)Math.Ceiling(DonQuixote_dealt);
        if (DonQuixote_heal > 0 && Owner.Creature.CurrentHp < Owner.Creature.MaxHp)
        {
            await CreatureCmd.Heal(Owner.Creature, DonQuixote_heal, true);
        }
    }
}

/// <summary>
/// 血魔之力：2 费能力，每次攻击恢复 2 点生命，每回合开始时获得 2 点临时强壮（升级后固有）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBloodfiendMight : ModCardTemplate
{
    public DonQuixoteBloodfiendMight()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Heal", 2m),
        new DynamicVar("Strength", 2m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerBloodfiendMight>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 永恒饥渴：2 费能力，每回合开始时消耗至多 30 点血宴，每 15 点（升级 10 点）恢复 1 点生命，
/// 并触发全体敌方单位 1 次流血（减少 1 层）。升级后固有。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteEternalThirst : ModCardTemplate
{
    /// <summary>卡面插画（资源\卡牌\永恒饥渴.png）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/eternal_thirst.png";

    public DonQuixoteEternalThirst()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Feast", 30m),
        new DynamicVar("HealPer", 10m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerEternalThirst>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);

        DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerEternalThirst>(Owner.Creature)?
            .DonQuixote_Initialize(DynamicVars["HealPer"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HealPer"].UpgradeValueBy(-5m);
        AddKeyword(CardKeyword.Innate);
    }
}

/// <summary>
/// 武器维护：1 费技能，血铸 2，下回合获得 1 点能量（升级 2 点）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteWeaponMaintenance : ModCardTemplate, IDonQuixoteBloodForgeCard
{
    /// <summary>卡面插画（资源\卡牌\武器维护.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/weapon_maintenance.png";

    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public DonQuixoteWeaponMaintenance()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Forge", 3m),
        new DynamicVar("Energy", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int DonQuixote_forgeSpent = await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Forge"].IntValue, true);
        await DonQuixoteLance.DonQuixote_Forge(
            DonQuixote_choiceContext, Owner, DonQuixote_forgeSpent, this);

        await PowerCmd.Apply<EnergyNextTurnPower>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Energy"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Energy"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 请帮助我们吧！堂吉诃德大人：1 费技能，堕落 40，抽 1 张、丢弃 2 张，下回合开始时额外抽 1 张。
/// 升级后抽 2 张、下回合额外抽 2 张。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixotePleaseHelp : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>卡面插画（资源\卡牌\请帮助我们吧！堂吉诃德大人.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/please_help.png";

    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteLookBackFather>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteLookBackFather>(), DonQuixote_owner);
    }

    /// <summary>「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixotePleaseHelp()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 40m),
        new DynamicVar("Draw", 1m),
        new DynamicVar("Discard", 2m),
        new DynamicVar("NextDraw", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null)
        {
            return;
        }

        await CardPileCmd.Draw(DonQuixote_choiceContext, DynamicVars["Draw"].IntValue, Owner, false);

        int DonQuixote_discard = DynamicVars["Discard"].IntValue;
        if (DonQuixote_discard > 0)
        {
            // 走原版的「丢弃」选择：提示语用 CardSelectorPrefs.DiscardSelectionPrompt（公共文案），
            // 之前自己拼的 cards 表键没对上，选牌时会显示成缺键的错误文本。
            IEnumerable<CardModel> DonQuixote_picked = await CardSelectCmd.FromHandForDiscard(
                DonQuixote_choiceContext,
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DonQuixote_discard),
                null,
                this);

            await CardCmd.Discard(DonQuixote_choiceContext, DonQuixote_picked.ToList());
        }

        await PowerCmd.Apply<DrawCardsNextTurnPower>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["NextDraw"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1m);
        DynamicVars["NextDraw"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 请将视线移回我们吧！父亲大人（请帮助我们吧的堕落版）：1 费技能，
/// 抽 2 张、丢弃 1 张，在抽牌堆和弃牌堆各加入 1 张「手足们在呼唤我」，下回合开始时恢复 1 点能量。
/// 升级后只在抽牌堆加入 1 张，下回合恢复 2 点能量。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteLookBackFather : ModCardTemplate, IDonQuixoteCorruptedCard
{
    /// <summary>卡面插画（资源\卡牌\请将视线移回我们吧！父亲大人.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/look_back_father.png";

    public DonQuixoteLookBackFather()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Draw", 2m),
        new DynamicVar("Discard", 1m),
        new DynamicVar("AddToDraw", 1m),
        new DynamicVar("AddToDiscard", 1m),
        new DynamicVar("NextEnergy", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CardPileCmd.Draw(DonQuixote_choiceContext, DynamicVars["Draw"].IntValue, Owner, false);

        int DonQuixote_discard = DynamicVars["Discard"].IntValue;
        if (DonQuixote_discard > 0)
        {
            IEnumerable<CardModel> DonQuixote_picked = await CardSelectCmd.FromHandForDiscard(
                DonQuixote_choiceContext,
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DonQuixote_discard),
                null,
                this);

            await CardCmd.Discard(DonQuixote_choiceContext, DonQuixote_picked.ToList());
        }

        int DonQuixote_toDraw = DynamicVars["AddToDraw"].IntValue;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_toDraw; DonQuixote_i++)
        {
            CardModel DonQuixote_curse = CardScope.CreateCard(ModelDb.Card<DonQuixoteCallOfKin>(), Owner);
            if (DonQuixote_curse != null)
            {
                await CardPileCmd.Add(DonQuixote_curse, PileType.Draw, CardPilePosition.Random, null, false);
            }
        }

        int DonQuixote_toDiscard = DynamicVars["AddToDiscard"].IntValue;
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_toDiscard; DonQuixote_i++)
        {
            CardModel DonQuixote_curse = CardScope.CreateCard(ModelDb.Card<DonQuixoteCallOfKin>(), Owner);
            if (DonQuixote_curse != null)
            {
                await CardPileCmd.Add(DonQuixote_curse, PileType.Discard, CardPilePosition.Random, null, false);
            }
        }

        await PowerCmd.Apply<EnergyNextTurnPower>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["NextEnergy"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["AddToDiscard"].UpgradeValueBy(-1m);
        DynamicVars["NextEnergy"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 为了共存必须舍弃：1 费技能，本回合不能再打出消耗血宴的牌，获得 4 点能量。
/// 升级后下回合额外获得 2 点能量。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteMustAbandon : ModCardTemplate
{
    public DonQuixoteMustAbandon()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Energy", 4m),
        new DynamicVar("NextEnergy", 0m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerBloodFeastLock>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);

        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);

        int DonQuixote_next = DynamicVars["NextEnergy"].IntValue;
        if (DonQuixote_next > 0)
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_next, Owner.Creature, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["NextEnergy"].UpgradeValueBy(2m);
    }
}

/// <summary>
/// 持续了三天的决斗：2 费技能，堕落 80，施加 1 层决斗对象，并在之后两回合继续盯住同一目标，消耗。
/// 升级后减 1 点耗能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteThreeDayDuel : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 80;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteEndFoolishFight>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteEndFoolishFight>(), DonQuixote_owner);
    }

    public DonQuixoteThreeDayDuel()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 80m),
        new DynamicVar("Renew", 2m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        Creature DonQuixote_target = DonQuixote_cardPlay.Target;
        if (Owner?.Creature == null || DonQuixote_target == null)
        {
            return;
        }

        // 决斗对象最大 1 层，已经有这个状态的敌人不再叠加。
        if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelTarget>(DonQuixote_target) == null)
        {
            await PowerCmd.Apply<DonQuixotePowerDuelTarget>(
                DonQuixote_choiceContext, DonQuixote_target, 1m, Owner.Creature, this, false);
        }

        // 「下两回合对同一目标施加决斗对象」：用续期层数，决斗对象到期时先消耗续期而不是消失。
        int DonQuixote_renew = DynamicVars["Renew"].IntValue;
        if (DonQuixote_renew > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerDuelRenewal>(
                DonQuixote_choiceContext, DonQuixote_target, DonQuixote_renew, Owner.Creature, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

/// <summary>
/// 结束这场愚蠢的战斗（持续了三天的决斗的堕落版）：2 费技能，
/// 对全体敌人施加 2 层易损，技巧 2、增幅 1，消耗。升级后下回合开始时获得 1 点能量。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteEndFoolishFight : ModCardTemplate, IDonQuixoteCorruptedCard
{
    public DonQuixoteEndFoolishFight()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust,
        DonQuixoteKeywords.DonQuixote_Technique,
        DonQuixoteKeywords.DonQuixote_Amplify,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Vulnerable", 2m),
        new DynamicVar("Technique", 2m),
        new DynamicVar("Amplify", 1m),
        new DynamicVar("NextEnergy", 0m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        foreach (Creature DonQuixote_enemy in DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature).ToList())
        {
            if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
            {
                continue;
            }

            await PowerCmd.Apply<VulnerablePower>(
                DonQuixote_choiceContext,
                DonQuixote_enemy,
                DynamicVars["Vulnerable"].IntValue,
                Owner.Creature,
                this,
                false);
        }

        await DonQuixoteLance.DonQuixote_Technique(
            DonQuixote_choiceContext, Owner, DynamicVars["Technique"].IntValue, this);
        await DonQuixoteLance.DonQuixote_Amplify(
            DonQuixote_choiceContext, Owner, DynamicVars["Amplify"].IntValue, this);

        int DonQuixote_next = DynamicVars["NextEnergy"].IntValue;
        if (DonQuixote_next > 0)
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_next, Owner.Creature, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["NextEnergy"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 为了不再动摇：1 费技能，获得 7 层摇曳【血魔】，血铸 5。
/// 升级后摇曳 7 → 15，血铸 5 → 7。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteNoLongerWaver : ModCardTemplate, IDonQuixoteBloodForgeCard
{
    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public DonQuixoteNoLongerWaver()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Wavering", 7m),
        new DynamicVar("Forge", 5m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Wavering"].IntValue, this);

        int DonQuixote_forgeSpent = await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Forge"].IntValue, true);
        await DonQuixoteLance.DonQuixote_Forge(
            DonQuixote_choiceContext, Owner, DonQuixote_forgeSpent, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Wavering"].UpgradeValueBy(8m);
        DynamicVars["Forge"].UpgradeValueBy(2m);
    }
}
