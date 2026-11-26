using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 令伤口撕裂，令鲜血绽放（决斗宣告的堕落版）：2 费攻击，造成 13 点伤害并施加 2 层伤口破坏，消耗。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteWoundBloom : ModCardTemplate, IDonQuixoteCorruptedCard
{
    public DonQuixoteWoundBloom()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(13m, ValueProp.Move),
        new DynamicVar("Wounds", 2m),
    };

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (DonQuixote_target == null || Owner?.Creature == null)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_play)
            .Targeting(DonQuixote_target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_ctx);

        if (DonQuixote_target.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerWoundDestruction>(
            DonQuixote_ctx,
            DonQuixote_target,
            DynamicVars["Wounds"].IntValue,
            Owner.Creature,
            this,
            false);
    }
}

/// <summary>
/// 侧耳倾听的故事与激荡的心情：2 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteListenToStories : ModCardTemplate, IDonQuixoteCorruptible
{
    /// <summary>
    /// 卡面插画（资源\卡牌\侧耳倾听的故事与激荡的心情.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/listen_to_stories.png";

    public const int DonQuixote_CorruptionThreshold = 40;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteAdventureOver>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteAdventureOver>(), DonQuixote_owner);
    }

    public DonQuixoteListenToStories()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    // 卡面文案里没有「消耗」。
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", 40m),
        new DynamicVar("Strength", 5m),
        new DynamicVar("Energy", 3m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Strength"].UpgradeValueBy(5m);
        DynamicVars["Energy"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<FlexPotionPower>(
            DonQuixote_ctx,
            Owner.Creature,
            DynamicVars["Strength"].IntValue,
            Owner.Creature,
            this,
            false);

        await PlayerCmd.GainEnergy(DynamicVars["Energy"].IntValue, Owner);

        // 消耗手牌中所有状态牌，每消耗 1 张获得 1 层摇曳【血魔】。
        CardPile DonQuixote_hand = PileType.Hand.GetPile(Owner);
        if (DonQuixote_hand == null)
        {
            return;
        }

        int DonQuixote_exhausted = 0;
        foreach (CardModel DonQuixote_card in DonQuixote_hand.Cards.ToList())
        {
            if (DonQuixote_card == null || DonQuixote_card.Type != CardType.Status)
            {
                continue;
            }

            await CardCmd.Exhaust(DonQuixote_ctx, DonQuixote_card, false, false);
            DonQuixote_exhausted++;
        }

        if (DonQuixote_exhausted > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainWavering(
                DonQuixote_ctx, Owner.Creature, DonQuixote_exhausted, this);
        }
    }
}

/// <summary>
/// 冒险结束了……（侧耳倾听的堕落版）：3 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteAdventureOver : ModCardTemplate, IDonQuixoteCorruptedCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌\冒险结束了....webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/adventure_over.png";

    public DonQuixoteAdventureOver()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(26m, ValueProp.Move),
        new DynamicVar("Hits", 1m),
        new DynamicVar("MaxFeast", 200m),
        new DynamicVar("HardBloodRate", 0.1m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 1) 消耗至多 200 点血宴：每点让基础伤害 +0.5%。
        int DonQuixote_feast = DonQuixoteBloodMagic.DonQuixote_BloodFeast(Owner.Creature);
        int DonQuixote_spend = await DonQuixoteBloodMagic.DonQuixote_PayBloodFeastUpTo(
            DonQuixote_ctx, Owner.Creature, DynamicVars["MaxFeast"].IntValue, this);

        // 2) 剩余血宴的 1/2 转化为摇曳【血魔】。
        int DonQuixote_remain = DonQuixote_feast - DonQuixote_spend;
        int DonQuixote_wavering = DonQuixoteBloodMagic.DonQuixote_CeilHalf(DonQuixote_remain);
        if (DonQuixote_wavering > 0)
        {
            await DonQuixoteBloodMagic.DonQuixote_GainWavering(
                DonQuixote_ctx, Owner.Creature, DonQuixote_wavering, this);
        }

        // 3) 伤害与硬血。
        decimal DonQuixote_damage =
            DynamicVars.Damage.BaseValue * (1m + 0.005m * DonQuixote_spend);
        int DonQuixote_hits = DynamicVars["Hits"].IntValue;
        decimal DonQuixote_dealt = 0m;

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_hits; DonQuixote_i++)
        {
            Creature DonQuixote_target = DonQuixote_play.Target;
            if (DonQuixote_target == null || DonQuixote_target.IsDead)
            {
                break;
            }

            decimal DonQuixote_before = DonQuixote_target.CurrentHp;
            await CreatureCmd.Damage(
                DonQuixote_ctx,
                DonQuixote_target,
                DonQuixote_damage,
                ValueProp.Move,
                Owner.Creature,
                this,
                DonQuixote_play);
            DonQuixote_dealt += Math.Max(0m, DonQuixote_before - DonQuixote_target.CurrentHp);
        }

        decimal DonQuixote_rate = DynamicVars["HardBloodRate"].BaseValue;
        int DonQuixote_hardBlood = (int)Math.Floor(DonQuixote_dealt * DonQuixote_rate);
        if (DonQuixote_hardBlood > 0)
        {
            await PowerCmd.Apply<DonQuixotePowerHardBlood>(
                DonQuixote_ctx,
                Owner.Creature,
                DonQuixote_hardBlood,
                Owner.Creature,
                this,
                false);
        }

        // 4) 把所有状态牌/诅咒牌变成「手足们在呼唤我」。
        await DonQuixote_TransformStatusesAndCurses(DonQuixote_ctx);
    }

    private async Task DonQuixote_TransformStatusesAndCurses(PlayerChoiceContext DonQuixote_ctx)
    {
        var DonQuixote_playerState = Owner?.PlayerCombatState;
        MegaCrit.Sts2.Core.Runs.ICardScope DonQuixote_scope = CardScope;
        if (DonQuixote_playerState == null || DonQuixote_scope == null)
        {
            return;
        }

        CardModel DonQuixote_canonical = ModelDb.Card<DonQuixoteCallOfKin>();

        foreach (CardPile DonQuixote_pile in DonQuixote_playerState.AllPiles.ToList())
        {
            if (DonQuixote_pile == null || DonQuixote_pile.Type == PileType.Play)
            {
                continue;
            }

            foreach (CardModel DonQuixote_card in DonQuixote_pile.Cards.ToList())
            {
                if (DonQuixote_card == null || DonQuixote_card is DonQuixoteCallOfKin)
                {
                    continue;
                }

                if (DonQuixote_card.Type != CardType.Status && DonQuixote_card.Type != CardType.Curse)
                {
                    continue;
                }

                CardModel DonQuixote_replacement = DonQuixote_scope.CreateCard(DonQuixote_canonical, Owner);
                if (DonQuixote_replacement == null)
                {
                    continue;
                }

                await CardCmd.Transform(DonQuixote_card, DonQuixote_replacement, CardPreviewStyle.None);
            }
        }
    }
}

/// <summary>
/// 手足们在呼唤我（诅咒）：无法主动打出，回合结束时自动打出。
/// </summary>
[RegisterCard(typeof(CurseCardPool))]
public sealed class DonQuixoteCallOfKin : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\手足们在呼唤我.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/call_of_kin.png";

    public DonQuixoteCallOfKin()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.Self)
    {
    }

    // 「不能被打出」+「虚无」：游戏在回合结束时会先把这些牌移进打出区（有出牌动画）。
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Unplayable,
        CardKeyword.Ethereal,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("HpLoss", 5m),
        new DynamicVar("Feast", 10m),
    };

    /// <summary>
    /// 开启「回合结束时若在手牌中则触发」的原版机制。
    /// </summary>
    public override bool HasTurnEndInHandEffect => true;

    /// <summary>
    /// 由 CombatManager 在回合结束、把牌移进打出区之后调用。
    /// </summary>
    protected override async Task OnTurnEndInHand(PlayerChoiceContext DonQuixote_ctx)
    {
        await DonQuixote_Fire(DonQuixote_ctx);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        await DonQuixote_Fire(DonQuixote_ctx);
    }

    private async Task DonQuixote_Fire(PlayerChoiceContext DonQuixote_ctx)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CreatureCmd.Damage(
            DonQuixote_ctx,
            Owner.Creature,
            DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null,
            null);

        if (Owner.Creature.IsDead)
        {
            return;
        }

        int DonQuixote_feast = DynamicVars["Feast"].IntValue;
        foreach (Creature DonQuixote_ally in DonQuixoteBloodMagic.DonQuixote_PlayerCreatures(Owner.Creature).ToList())
        {
            if (DonQuixote_ally == null || DonQuixote_ally.IsDead)
            {
                continue;
            }

            await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(
                DonQuixote_ctx, DonQuixote_ally, DonQuixote_feast, this);
        }
    }
}

/// <summary>
/// 爹死亡（无色/商店）：1 费技能，固有，获得 16 层摇曳【血魔】，消耗；升级后不再消耗。
/// </summary>
[RegisterCard(typeof(ColorlessCardPool))]
public sealed class DonQuixoteFatherDeath : ModCardTemplate
{
    public DonQuixoteFatherDeath()
        : base(1, CardType.Skill, CardRarity.None, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Innate,
        CardKeyword.Exhaust,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Wavering", 16m),
    };

    protected override void OnUpgrade()
    {
        // 升级后不消耗：「固有 / 获得 16 层摇曳【血魔】」。
        RemoveKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            DonQuixote_ctx, Owner.Creature, DynamicVars["Wavering"].IntValue, this);
    }
}

/// <summary>
/// 于镜中无法呈现倒影之人：2 费技能，获得 1 层无实体（升级减 1 点耗能）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteNoReflection : ModCardTemplate
{
    public DonQuixoteNoReflection()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Intangible", 1m),
    };

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

        await PowerCmd.Apply<IntangiblePower>(
            DonQuixote_ctx,
            Owner.Creature,
            DynamicVars["Intangible"].IntValue,
            Owner.Creature,
            this,
            false);
    }
}

/// <summary>
/// 桑丘之血-仿制：2 费技能，血铸 5、增幅 1，消耗（升级增幅 2）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteSanchoBloodImitation : ModCardTemplate, IDonQuixoteBloodForgeCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/sancho_blood_imitation.png";

    public DonQuixoteSanchoBloodImitation()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
        DonQuixoteKeywords.DonQuixote_Amplify,
        CardKeyword.Exhaust,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Forge", 3m),
        new DynamicVar("Amplify", 1m),
    };

    protected override void OnUpgrade()
    {
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
        await DonQuixoteLance.DonQuixote_Amplify(DonQuixote_ctx, Owner, DynamicVars["Amplify"].IntValue, this);
    }
}

/// <summary>
/// 让我们以枪决斗吧：2 费技能，血铸 8，并给予目标 1 层决斗对象。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteDuelWithLance : ModCardTemplate, IDonQuixoteBloodForgeCard
{
    public DonQuixoteDuelWithLance()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public int DonQuixote_ForgeCost => DynamicVars["Forge"].IntValue;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        DonQuixoteKeywords.DonQuixote_BloodForge,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Forge", 4m),
        new DynamicVar("Duel", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteLance.DonQuixote_ForgeFromCard(
            DonQuixote_ctx, Owner, DynamicVars["Forge"].IntValue, this);

        Creature DonQuixote_target = DonQuixote_play.Target;
        if (DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return;
        }

        // 决斗对象最大只有 1 层，已经有这个状态的敌人不再叠加。
        if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelTarget>(DonQuixote_target) == null)
        {
            await PowerCmd.Apply<DonQuixotePowerDuelTarget>(
                DonQuixote_ctx, DonQuixote_target, 1m, Owner.Creature, this, false);
        }
    }
}

/// <summary>
/// 堂吉诃德式硬血 5 式-追踪：1 费攻击。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHardBloodTrack : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/hard_blood_track.png";

    public DonQuixoteHardBloodTrack()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(15m, ValueProp.Move),
        new DynamicVar("MaxFeast", 30m),
        new DynamicVar("FeastPerPoint", 5m),
        new DynamicVar("Bleed", 8m),
        new DynamicVar("EnergyAt", 25m),
        new DynamicVar("DrawAt", 35m),
    };

    protected override void OnUpgrade()
    {
        DynamicVars["EnergyAt"].UpgradeValueBy(-5m);
        DynamicVars["DrawAt"].UpgradeValueBy(-5m);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (Owner?.Creature == null || DonQuixote_target == null)
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

        decimal DonQuixote_dealt = await DonQuixote_Hit(DonQuixote_ctx, DonQuixote_play, DonQuixote_target);

        if (!DonQuixote_target.IsDead)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_ctx,
                DonQuixote_target,
                Owner.Creature,
                this,
                0,
                DynamicVars["Bleed"].IntValue);
        }

        if (DonQuixote_dealt > DynamicVars["EnergyAt"].BaseValue)
        {
            await PlayerCmd.GainEnergy(1m, Owner);
        }

        if (DonQuixote_dealt > DynamicVars["DrawAt"].BaseValue)
        {
            await CardPileCmd.Draw(DonQuixote_ctx, 1m, Owner, false);
        }

        // 击杀目标：对另一名敌人重放 1 次（同样的伤害与流血结算）。
        if (!DonQuixote_target.IsDead)
        {
            return;
        }

        List<Creature> DonQuixote_others = DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner.Creature)
            .Where(DonQuixote_enemy => DonQuixote_enemy != null && !DonQuixote_enemy.IsDead)
            .ToList();
        if (DonQuixote_others.Count == 0)
        {
            return;
        }

        Creature DonQuixote_next = DonQuixote_others[0];
        await DonQuixote_Hit(DonQuixote_ctx, DonQuixote_play, DonQuixote_next);
        if (!DonQuixote_next.IsDead)
        {
            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_ctx,
                DonQuixote_next,
                Owner.Creature,
                this,
                0,
                DynamicVars["Bleed"].IntValue);
        }
    }

    private async Task<decimal> DonQuixote_Hit(
        PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play, Creature DonQuixote_target)
    {
        if (DonQuixote_target == null || DonQuixote_target.IsDead)
        {
            return 0m;
        }

        decimal DonQuixote_before = DonQuixote_target.CurrentHp;
        await CreatureCmd.Damage(
            DonQuixote_ctx,
            DonQuixote_target,
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            Owner.Creature,
            this,
            DonQuixote_play);
        return Math.Max(0m, DonQuixote_before - DonQuixote_target.CurrentHp);
    }
}

/// <summary>
/// 堂吉诃德派硬血奥义-La Sangre：5 费技能，血宴 100，保留。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteLaSangre : DonQuixoteBloodFeastCard
{
    /// <summary>
    /// 卡面插画（资源\卡牌 对应 png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/la_sangre.png";

    public DonQuixoteLaSangre()
        : base(5, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("BloodFeast", 100m),
    };

    // 卡面写明「需消耗 100 点血宴才能打出」。
    public override int DonQuixote_BloodFeastCost => DonQuixote_Var("BloodFeast");

    public override bool DonQuixote_BloodFeastMustPay => true;

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        Creature DonQuixote_target = DonQuixote_play.Target;
        if (DonQuixote_target == null)
        {
            return;
        }

        // 需要 100 点血宴；出牌已被 CanPlay 拦下，这里只是把血宴扣掉。
        await DonQuixote_TriggerBloodFeast(DonQuixote_ctx);

        // 参考控制台 "win" 的做法：先把目标身上的能力全部清掉，再走一次真正的 Kill。
        if (!DonQuixote_target.IsDead)
        {
            DonQuixote_target.RemoveAllPowersInternalExcept();
            await CreatureCmd.Kill(DonQuixote_target, true);
        }

        // Kill 内部走的是 KillWithoutCheckingWinCondition.
        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }

        await DonQuixote_RemoveFromDeck();
    }

    /// <summary>
    /// 使用后从牌组中永久移除（参考原版 DeprecatedCard 的 DeckVersion 写法）。
    /// </summary>
    private async Task DonQuixote_RemoveFromDeck()
    {
        try
        {
            // 战斗中打出的这张是克隆体，牌组里的「本体」挂在 DeckVersion 上。
            CardModel DonQuixote_deckVersion = DeckVersion;
            if (DonQuixote_deckVersion != null && DonQuixote_deckVersion.Pile?.Type == PileType.Deck)
            {
                await CardPileCmd.RemoveFromDeck(DonQuixote_deckVersion);
                DeckVersion = null;
                return;
            }

            // 兜底：DeckVersion 没挂上时，按 Id 在牌组里找同一张牌。
            if (Owner == null)
            {
                return;
            }

            CardPile DonQuixote_deck = PileType.Deck.GetPile(Owner);
            if (DonQuixote_deck == null)
            {
                return;
            }

            CardModel DonQuixote_found = DonQuixote_deck.Cards
                .FirstOrDefault(DonQuixote_card => DonQuixote_card != null
                    && !DonQuixote_card.HasBeenRemovedFromState
                    && DonQuixote_card.Id.Equals(Id));
            if (DonQuixote_found == null)
            {
                return;
            }

            await CardPileCmd.RemoveFromDeck(DonQuixote_found);
        }
        catch (Exception DonQuixote_error)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] La Sangre remove from deck failed: " + DonQuixote_error.Message, 0);
        }
    }
}
