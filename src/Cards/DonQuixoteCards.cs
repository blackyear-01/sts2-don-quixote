using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// 堂吉诃德派硬血14式斗篷：1 费，血宴5，获得 8 层摇曳【血魔】（升级15）
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodCape14 : DonQuixoteBloodFeastCard
{
    /// <summary>卡面插画（资源\卡牌\堂吉诃德派硬血14式-斗篷.png）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_cape14.png";

    public DonQuixoteHardBloodCape14()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 5m),
        new DynamicVar("Wavering", 8m),
        new DynamicVar("ExtraWavering", 10m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    private int DonQuixote_Wavering => DonQuixote_Var("Wavering");

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Wavering, this);

        // 血宴 5：条件触发，够了才额外再给一层同样规模的摇曳。
        int DonQuixote_extra = DonQuixote_Var("ExtraWavering");
        if (DonQuixote_extra > 0 && await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await DonQuixoteBloodMagic.DonQuixote_GainWavering(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_extra, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Wavering"].UpgradeValueBy(7m);
        DynamicVars["ExtraWavering"].UpgradeValueBy(7m);
    }
}

/// <summary>
/// 桑丘啊！我有一个点子：0 费，恢复 1 点能量并抽 1 张卡（升级抽 2 张）
/// 堕落值达到 40 时会被腐化替换成「化脓腐烂的怠惰之梦」
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteSanchoIdea : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>卡面插画（资源\卡牌\桑丘啊！我有个好点子.png）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/sancho_idea.png";

    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteRottenLazyDream>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteRottenLazyDream>();

    public DonQuixoteSanchoIdea()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Cards", 1m),
        new DynamicVar("Corruption", 40m),
    };

    private int DonQuixote_Draw => DynamicVars["Cards"].IntValue;

    private int DonQuixote_CorruptionGain => DynamicVars["Corruption"].IntValue;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(DonQuixote_choiceContext, DonQuixote_Draw, Owner, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Cards"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 腐败版：化脓腐烂的怠惰之梦：0 费，恢复 1 点能量并抽 1 张卡（升级恢复 2 点能量）
/// 只能由「桑丘啊！我有一个点子」在堕落值达到阈值时转化而来，不进卡池。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteRottenLazyDream : ModCardTemplate, IDonQuixoteCorruptedCard
{
    /// <summary>卡面插画（资源\卡牌\化脓腐烂的怠惰之梦.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/rotten_lazy_dream.png";

    public DonQuixoteRottenLazyDream()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }
    // 堕落牌的费用折扣由 Hook.ModifyEnergyCostInCombat 补丁统一处理
    // （CanonicalEnergyCost 只在卡牌创建时被读一次，运行时改它不会生效）。


    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Energy", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);
        await CardPileCmd.Draw(DonQuixote_choiceContext, 1m, Owner, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Energy"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 决斗宣告：2 费，堕落 80，给予目标 1 层决斗对象，本回合获得 2 层临时强壮，消耗（升级 5 层）。
/// 堕落值达到 80 时会被替换成「令伤口撕裂，令鲜血绽放」。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteDuelDeclaration : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 80;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteWoundBloom>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteWoundBloom>(), DonQuixote_owner);
    }

    public DonQuixoteDuelDeclaration()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight, CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 80m),
        new DynamicVar("Strength", 5m),
    };

    private int DonQuixote_Strength => DynamicVars["Strength"].IntValue;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        // 决斗对象最大只有 1 层，已经有这个状态的敌人不再叠加。
        if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelTarget>(DonQuixote_cardPlay.Target) == null)
        {
            await PowerCmd.Apply<DonQuixotePowerDuelTarget>(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Target, 1m, Owner.Creature, this, false);
        }
        await PowerCmd.Apply<FlexPotionPower>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_Strength, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Strength"].UpgradeValueBy(5m);
    }
}

/// <summary>
/// 骑士的梦：2 费能力，每使用 1 张技能卡获得 1 点格挡（升级后固有）
/// 堕落值达到 80 时会被腐化替换成「不负责任的梦」
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteKnightDream : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>卡面插画（资源\卡牌\骑士的梦.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/knight_dream.png";

    public const int DonQuixote_CorruptionThreshold = 80;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteIrresponsibleDream>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteIrresponsibleDream>();

    /// <summary>「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteKnightDream()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 80m),
        new DynamicVar("Defense", 3m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Defense"].UpgradeValueBy(4m);

        // 升级后固有。CardModel 的关键词只在第一次取用时按 CanonicalKeywords 缓存一次
        // （见 CardModel.LocalKeywords），升级不会重算，所以这里必须显式加。
        AddKeyword(CardKeyword.Innate);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerKnightDream>(
            DonQuixote_choiceContext, Owner.Creature, DynamicVars["Defense"].IntValue, Owner.Creature, this, false);
    }
}

/// <summary>
/// 腐败版：不负责任的梦：2 费能力，腐化卡有 10% 概率重放 1 次（升级后固有）
/// 只能由「骑士的梦」在堕落值达到阈值时转化而来，不进卡池。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteIrresponsibleDream : ModCardTemplate, IDonQuixoteCorruptedCard
{
    /// <summary>卡面插画（资源\卡牌\不负责任的梦.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/irresponsible_dream.png";

    public DonQuixoteIrresponsibleDream()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }
    // 堕落牌的费用折扣由 Hook.ModifyEnergyCostInCombat 补丁统一处理
    // （CanonicalEnergyCost 只在卡牌创建时被读一次，运行时改它不会生效）。


    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerIrresponsibleDream>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);
    }
}

/// <summary>
/// 灼烧的饥渴：古老牙齿转化后的先古卡。血宴3 只对「获得复还的血之冲动」生效
/// （血宴不够时不给冲动，伤害与流血照常结算），造成 24 点伤害并施加 7 级流血 2 次
/// （升级：6 层冲动、34 点伤害，10 级流血 + 5 层流血 4 次）
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteBurningHunger : DonQuixoteBloodFeastCard
{
    /// <summary>卡面插画（资源\卡牌\灼烧的饥渴.png，先古卡 250x351）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/burning_hunger.png";

    public DonQuixoteBurningHunger()
        : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(24m, ValueProp.Move),
        new DynamicVar("BloodFeast", 3m),
        new DynamicVar("Impulse", 4m),
        new DynamicVar("BleedStrength", 7m),
        new DynamicVar("BleedLayers", 0m),
        new DynamicVar("Times", 2m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_cardPlay.Target, "cardPlay.Target");

        // 「血宴 3：」只覆盖「获得复还的血之冲动」这一段 —— 血宴不够时不拿冲动，
        // 伤害和流血照常结算（卡面把这两句拆成独立的两行）。
        if (await DonQuixote_TriggerBloodFeast(DonQuixote_choiceContext))
        {
            await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
                DonQuixote_choiceContext, Owner.Creature, DonQuixote_Var("Impulse"), Owner.Creature, this, false);
        }

        await DamageCmd.Attack(DonQuixote_ScaledDamage(DynamicVars.Damage.BaseValue))
            .FromCard(this, DonQuixote_cardPlay)
            .Targeting(DonQuixote_cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_choiceContext);

        // 先施加两次流血强度，再把升级附加的层数单独加一次
        // （若两次调用都带层数，层数会被算成两倍）。
        int DonQuixote_times = Math.Max(1, DynamicVars["Times"].IntValue);
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_times; DonQuixote_i++)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext,
                DonQuixote_cardPlay.Target,
                Owner.Creature,
                this,
                DynamicVars["BleedStrength"].IntValue,
                0);
        }

        int DonQuixote_layers = DynamicVars["BleedLayers"].IntValue;
        if (DonQuixote_layers > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext,
                DonQuixote_cardPlay.Target,
                Owner.Creature,
                this,
                0,
                DonQuixote_layers);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10m);
        DynamicVars["Impulse"].UpgradeValueBy(2m);
        DynamicVars["BleedStrength"].UpgradeValueBy(3m);
        DynamicVars["BleedLayers"].UpgradeValueBy(5m);
        DynamicVars["Times"].UpgradeValueBy(2m);
    }
}
