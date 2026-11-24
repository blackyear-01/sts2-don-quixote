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
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 过人的毅力：1 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteExtraordinaryGrit : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\过人的毅力.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/extraordinary_grit.png";

    public DonQuixoteExtraordinaryGrit()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Reduction", 75m),
        new DynamicVar("Cap", 2m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (IsUpgraded)
        {
            // 升级版是「每次最高受 2 点」，换成上限型 power。
            await PowerCmd.Apply<DonQuixotePowerGritCap>(
                DonQuixote_ctx, Owner.Creature, DynamicVars["Cap"].IntValue, Owner.Creature, this, false);
        }
        else
        {
            await PowerCmd.Apply<DonQuixotePowerGrit>(
                DonQuixote_ctx, Owner.Creature, DynamicVars["Reduction"].IntValue, Owner.Creature, this, false);
        }

        CardPile DonQuixote_hand = PileType.Hand.GetPile(Owner);
        if (DonQuixote_hand == null)
        {
            return;
        }

        foreach (CardModel DonQuixote_card in DonQuixote_hand.Cards.ToList())
        {
            if (DonQuixote_card == null
                || DonQuixote_card.Type == CardType.Curse
                || DonQuixote_card.Type == CardType.Status
                || DonQuixote_card.Enchantment != null)
            {
                continue;
            }

            // 临时附魔：本回合结束（敌方回合结束时）由 power 回收。
            CardCmd.Enchant<Enchantments.DonQuixoteIndestructibleCard>(
                DonQuixote_card, Enchantments.DonQuixoteIndestructibleState.DonQuixote_TemporaryAmount);
        }
    }
}

/// <summary>
/// 援护反击：2 费技能，获得 1 层援护防御，消耗（升级后 1 费）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteGuardRiposte : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\援护反击.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/guard_riposte.png";

    public DonQuixoteGuardRiposte()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    /// <summary>
    /// 只进联机卡池（<see cref="MegaCrit.Sts2.Core.Models.CardPoolModel.GetUnlockedCards"/> 按它过滤）。
    /// </summary>
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 最大 1 层：已经有就不再叠。
        if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerGuardAssist>(Owner.Creature) == null)
        {
            await PowerCmd.Apply<DonQuixotePowerGuardAssist>(
                DonQuixote_ctx, Owner.Creature, 1m, Owner.Creature, this, false);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

/// <summary>
/// 援护反击（衍生攻击）：0 费无色攻击，造成 6 点伤害并施加 2 级流血。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteGuardRiposteStrike : ModCardTemplate
{
    /// <summary>
    /// 援护防御判定用的固定伤害（不吃任何加成，只用来比较大小）。
    /// </summary>
    public const int DonQuixote_BaseDamage = 6;

    public DonQuixoteGuardRiposteStrike()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
    {
    }

    /// <summary>
    /// 衍生攻击同样只属于联机（跟着援护反击走）。
    /// </summary>
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("BleedStrength", 2m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (Owner?.Creature == null || DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_play)
            .Targeting(DonQuixote_target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_ctx);

        await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
            DonQuixote_ctx,
            DonQuixote_target,
            Owner.Creature,
            this,
            DynamicVars["BleedStrength"].IntValue,
            0);
    }
}

/// <summary>
/// 勇敢骑士的决心：2 费能力，堕落 40 / 骑士词条，获得「心-勇敢的骑士」。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteResoluteHeart : ModCardTemplate, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteHope>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteHope>();

    /// <summary>
    /// 「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteResoluteHeart()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 40m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerBraveHeart>(
            DonQuixote_ctx, Owner.Creature, 1m, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

/// <summary>
/// 望：2 费技能，获得 1 层「望」，消耗（升级后获得保留）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHope : ModCardTemplate, IDonQuixoteCorruptedCard
{
    public DonQuixoteHope()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Hope", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerHope>(
            DonQuixote_ctx, Owner.Creature, DynamicVars["Hope"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}

/// <summary>
/// 舍弃身后之物踏上冒险：2 费技能，消耗所有手牌。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteAbandonAll : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\舍弃身后之物踏上冒险.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/abandon_all.png";

    public DonQuixoteAbandonAll()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Wavering", 1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Wavering"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        CardPile DonQuixote_hand = PileType.Hand.GetPile(Owner);
        if (DonQuixote_hand == null)
        {
            return;
        }

        List<CardModel> DonQuixote_cards = DonQuixote_hand.Cards
            .Where(DonQuixote_card => DonQuixote_card != null && !ReferenceEquals(DonQuixote_card, this))
            .ToList();

        int DonQuixote_exhausted = 0;
        foreach (CardModel DonQuixote_card in DonQuixote_cards)
        {
            await CardCmd.Exhaust(DonQuixote_ctx, DonQuixote_card, false, false);
            DonQuixote_exhausted++;
        }

        if (DonQuixote_exhausted <= 0)
        {
            return;
        }

        await CardPileCmd.Draw(DonQuixote_ctx, DonQuixote_exhausted, Owner, false);
        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_ctx,
            Owner.Creature,
            DonQuixote_exhausted * DynamicVars["Wavering"].IntValue,
            this);
    }
}

/// <summary>
/// 硬血传授：1 费技能，需消耗 5 点硬血才能打出（硬血不足时这张牌直接不能打出）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodTeaching : ModCardTemplate, IDonQuixoteHardBloodCost
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_teaching.png";

    public int DonQuixote_HardBloodCost => DynamicVars["HardBlood"].IntValue;

    public DonQuixoteHardBloodTeaching()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("HardBlood", 5m),
        new DynamicVar("Cards", 1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Cards"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 抽牌堆空的时候不白扣硬血，直接按空过处理。
        CardPile DonQuixote_draw = PileType.Draw.GetPile(Owner);
        if (DonQuixote_draw == null || DonQuixote_draw.Cards.Count == 0)
        {
            return;
        }

        // 出牌前已被 CanPlay 拦过，这里硬血一定够。
        await DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood(
            DonQuixote_ctx, Owner.Creature, DynamicVars["HardBlood"].IntValue);

        int DonQuixote_count = Math.Min(DynamicVars["Cards"].IntValue, DonQuixote_draw.Cards.Count);
        CardSelectorPrefs DonQuixote_prefs = new CardSelectorPrefs(SelectionScreenPrompt, DonQuixote_count);
        List<CardModel> DonQuixote_selected = (await CardSelectCmd.FromCombatPile(
            DonQuixote_ctx, DonQuixote_draw, Owner, DonQuixote_prefs)).ToList();

        foreach (CardModel DonQuixote_card in DonQuixote_selected)
        {
            if (DonQuixote_card == null)
            {
                continue;
            }

            if (DonQuixote_card.Enchantment == null)
            {
                // 永久附魔：不会在回合结束时被回收。
                CardCmd.Enchant<Enchantments.DonQuixoteIndestructibleCard>(
                    DonQuixote_card, Enchantments.DonQuixoteIndestructibleState.DonQuixote_PermanentAmount);
            }

            await CardPileCmd.Add(DonQuixote_card, PileType.Hand, CardPilePosition.Top, this, false);
        }
    }
}

/// <summary>
/// 冒险的见闻：2 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteAdventureSight : DonQuixoteBloodFeastCard, IDonQuixoteCorruptible
{
    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteHazyMemory>(), DonQuixote_owner);
    }

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteHazyMemory>();

    /// <summary>
    /// 「骑士」词条：对有[gold]决斗对象[/gold]的敌人伤害 +50%。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { DonQuixoteKeywords.DonQuixote_Knight };

    public DonQuixoteAdventureSight()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BloodFeast", 10m),
        new DynamicVar("Vulnerable", 2m),
        new DynamicVar("Amplify", 2m),
        new DynamicVar("FeastGain", 20m),
        new DynamicVar("DamageAt", 15m),
        new DynamicVar("Corruption", 40m),
    };

    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["Amplify"].UpgradeValueBy(3m);
        DynamicVars["FeastGain"].UpgradeValueBy(10m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (Owner?.Creature == null || DonQuixote_target == null)
        {
            return;
        }

        decimal DonQuixote_before = DonQuixote_target.CurrentHp;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_play)
            .Targeting(DonQuixote_target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_ctx);
        decimal DonQuixote_dealt = Math.Max(0m, DonQuixote_before - DonQuixote_target.CurrentHp);

        if (!DonQuixote_target.IsDead)
        {
            await PowerCmd.Apply<VulnerablePower>(
                DonQuixote_ctx,
                DonQuixote_target,
                DynamicVars["Vulnerable"].IntValue,
                Owner.Creature,
                this,
                false);
        }

        // 血宴 10：条件触发，够了才打出增幅。
        if (await DonQuixote_TriggerBloodFeast(DonQuixote_ctx))
        {
            await DonQuixoteLance.DonQuixote_Amplify(
                DonQuixote_ctx, Owner, DynamicVars["Amplify"].IntValue, this);
        }

        if (DonQuixote_dealt > DynamicVars["DamageAt"].BaseValue)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(
                DonQuixote_ctx, Owner.Creature, DynamicVars["FeastGain"].IntValue, this);
        }
    }
}

/// <summary>
/// 朦胧的回忆（勇敢骑士的决心的堕落版）：2 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHazyMemory : ModCardTemplate, IDonQuixoteCorruptedCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\朦胧的回忆.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hazy_memory.png";

    public DonQuixoteHazyMemory()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Impulse", 3m),
        new DynamicVar("FeastLow", 10m),
        new DynamicVar("FeastHigh", 20m),
        new DynamicVar("Technique", 2m),
        new DynamicVar("Amplify", 1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["FeastHigh"].UpgradeValueBy(-5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (Owner?.Creature == null || DonQuixote_target == null)
        {
            return;
        }

        // 先按最高档结算血宴，够哪一档就走哪一档。
        int DonQuixote_have = DonQuixoteBloodMagic.DonQuixote_BloodFeast(Owner.Creature);
        int DonQuixote_high = DynamicVars["FeastHigh"].IntValue;
        int DonQuixote_low = DynamicVars["FeastLow"].IntValue;

        bool DonQuixote_top = DonQuixote_have >= DonQuixote_high;
        bool DonQuixote_mid = !DonQuixote_top && DonQuixote_have >= DonQuixote_low;

        if (DonQuixote_top || DonQuixote_mid)
        {
            int DonQuixote_spend = DonQuixote_top ? DonQuixote_high : DonQuixote_low;
            await DonQuixoteBloodMagic.DonQuixote_PayBloodFeastUpTo(
                DonQuixote_ctx, Owner.Creature, DonQuixote_spend, this);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_play)
            .Targeting(DonQuixote_target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_ctx);

        // 从消耗牌堆里自选 1 张加入手牌（原来是从弃牌堆随机拿 1 张）。
        CardPile DonQuixote_exhaust = PileType.Exhaust.GetPile(Owner);
        if (DonQuixote_exhaust != null && DonQuixote_exhaust.Cards.Count > 0)
        {
            List<CardModel> DonQuixote_choices = DonQuixote_exhaust.Cards.ToList();

            // 提示文本必须有对应的 .selectionScreenPrompt 键。
            CardSelectorPrefs DonQuixote_pickPrefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            List<CardModel> DonQuixote_picked =
                (await CardSelectCmd.FromSimpleGrid(DonQuixote_ctx, DonQuixote_choices, Owner, DonQuixote_pickPrefs))
                .ToList();

            foreach (CardModel DonQuixote_card in DonQuixote_picked)
            {
                if (DonQuixote_card == null || DonQuixote_card.Pile?.Type != PileType.Exhaust)
                {
                    continue;
                }

                await CardPileCmd.Add(DonQuixote_card, PileType.Hand, CardPilePosition.Top, this, false);
            }
        }

        if (DonQuixote_top || DonQuixote_mid)
        {
            await PowerCmd.Apply<DonQuixotePowerBloodImpulse>(
                DonQuixote_ctx, Owner.Creature, DynamicVars["Impulse"].IntValue, Owner.Creature, this, false);
        }

        if (DonQuixote_top)
        {
            await DonQuixoteLance.DonQuixote_Technique(
                DonQuixote_ctx, Owner, DynamicVars["Technique"].IntValue, this);
            await DonQuixoteLance.DonQuixote_Amplify(
                DonQuixote_ctx, Owner, DynamicVars["Amplify"].IntValue, this);
        }
    }
}

/// <summary>
/// 加强防御：3 费能力，摇曳【血魔】持续多 1 回合后减半；升级后血铠不再减少。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteReinforceDefense : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\援护防御.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/reinforce_defense.png";

    public DonQuixoteReinforceDefense()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 层数 1 = 基础版，2 = 升级版（血铠也不再减少）。
        await PowerCmd.Apply<DonQuixotePowerReinforcedDefense>(
            DonQuixote_ctx, Owner.Creature, IsUpgraded ? 2m : 1m, Owner.Creature, this, false);
    }
}
