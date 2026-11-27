using System;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Relics;

/// <summary>
/// 血魔的本能：每次攻击回复造成伤害的 15%。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
[RegisterCharacterStarterRelic(typeof(DonQuixoteCharacter), Order = 0)]
[RegisterTouchOfOrobasRefinement(typeof(DonQuixoteBloodElderInstinct))]
public sealed class DonQuixoteBloodInstinct : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string CustomIconPath => "res://DonQuixote/images/relics/blood_instinct_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/blood_instinct_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/blood_instinct_big.png";

    /// <summary>
    /// 已经发放过几次舞台卡（按「消耗血宴总数 / 325」计）。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastRewardSteps { get; set; }

    /// <summary>
    /// 「消耗血宴总数」是战斗内的临时能力。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        DonQuixote_FeastRewardSteps = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (Owner?.Creature == null || dealer != Owner.Creature || cardSource == null || result == null)
        {
            return;
        }

        if (cardSource.Type != MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
        {
            return;
        }

        decimal DonQuixote_heal = DonQuixoteBloodMagic.DonQuixote_CeilPercent(result.TotalDamage, 0.15m);
        if (DonQuixote_heal <= 0m)
        {
            return;
        }

        await CreatureCmd.Heal(Owner.Creature, DonQuixote_heal, true);
    }

    // 与桑丘同理：会改状态的回合开始效果走 AfterSideTurnStart。
    public override async Task AfterSideTurnStart(
        MegaCrit.Sts2.Core.Combat.CombatSide DonQuixote_side,
        System.Collections.Generic.IReadOnlyList<Creature> DonQuixote_participants,
        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }

        Creature DonQuixote_owner = Owner?.Creature;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead)
        {
            return;
        }

        if (DonQuixote_participants == null || !DonQuixote_participants.Contains(DonQuixote_owner))
        {
            return;
        }

        // 兜底补发：正常在「消耗血宴总数」涨上去的那一刻就发了。
        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_owner);

        // 「第二回合开始后」才检查血宴：第一回合是给玩家铺血宴的时间，不该被直接惩罚。
        int DonQuixote_round = DonQuixote_state?.RoundNumber ?? 0;
        if (DonQuixote_round < 2)
        {
            return;
        }

        if (DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_owner) > 0)
        {
            return;
        }

        PlayerChoiceContext DonQuixote_context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WeakPower>(DonQuixote_context, DonQuixote_owner, 1m, DonQuixote_owner, null, false);
        await PowerCmd.Apply<VulnerablePower>(DonQuixote_context, DonQuixote_owner, 1m, DonQuixote_owner, null, false);
    }
}

/// <summary>
/// 血魔长老的本能：欧洛巴斯之触把「血魔的本能」精炼成的初始遗物。
/// 已经是最终形态，再被欧洛巴斯精炼时仍映射回自身，避免游戏本体把它回落成原版遗物。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
[RegisterTouchOfOrobasRefinement(typeof(DonQuixoteBloodElderInstinct))]
public sealed class DonQuixoteBloodElderInstinct : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string CustomIconPath => "res://DonQuixote/images/relics/elder_instinct_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/elder_instinct_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/elder_instinct_big.png";

    /// <summary>
    /// 精炼后发的是升级版「幸福笑容如鲜花般绽放之地」；同样按消耗血宴总数发货，所以另记一个计数。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastRewardSteps { get; set; }

    /// <summary>
    /// 与「血魔的本能」同理：「消耗血宴总数」每场战斗归零。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        DonQuixote_FeastRewardSteps = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (Owner?.Creature == null || dealer != Owner.Creature || cardSource == null || result == null)
        {
            return;
        }

        if (cardSource.Type != MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
        {
            return;
        }

        decimal DonQuixote_heal = DonQuixoteBloodMagic.DonQuixote_CeilPercent(result.TotalDamage, 0.20m);
        if (DonQuixote_heal <= 0m)
        {
            return;
        }

        await CreatureCmd.Heal(Owner.Creature, DonQuixote_heal, true);
    }

    /// <summary>
    /// 每打出一张攻击牌获得 2 点血宴。
    /// </summary>
    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Cards.CardPlay cardPlay)
    {
        if (Owner?.Creature == null || cardPlay?.Card == null)
        {
            return;
        }

        if (cardPlay.Card.Type != MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
        {
            return;
        }

        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(choiceContext, Owner.Creature, 2, cardPlay.Card);
    }

    // 与血魔的本能一致：改状态的回合开始效果走 AfterSideTurnStart。
    public override async Task AfterSideTurnStart(
        MegaCrit.Sts2.Core.Combat.CombatSide DonQuixote_side,
        System.Collections.Generic.IReadOnlyList<Creature> DonQuixote_participants,
        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }

        Creature DonQuixote_owner = Owner?.Creature;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead)
        {
            return;
        }

        if (DonQuixote_participants == null || !DonQuixote_participants.Contains(DonQuixote_owner))
        {
            return;
        }

        // 兜底补发：正常在「消耗血宴总数」涨上去的那一刻就发了。
        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_owner);

        // 「第二回合开始后」才检查血宴：第一回合是给玩家铺血宴的时间，不该被直接惩罚。
        int DonQuixote_round = DonQuixote_state?.RoundNumber ?? 0;
        if (DonQuixote_round < 2)
        {
            return;
        }

        if (DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_owner) > 0)
        {
            return;
        }

        PlayerChoiceContext DonQuixote_context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WeakPower>(DonQuixote_context, DonQuixote_owner, 1m, DonQuixote_owner, null, false);
        await PowerCmd.Apply<VulnerablePower>(DonQuixote_context, DonQuixote_owner, 1m, DonQuixote_owner, null, false);
    }
}

/// <summary>
/// 向本能堕落的骑士：回合开始时向自身施加等量计数的堕落值。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
[RegisterCharacterStarterRelic(typeof(DonQuixoteCharacter), Order = 10)]
public sealed class DonQuixoteKnightOfInstinct : ModRelicTemplate
{
    /// <summary>
    /// 堕落计数：战斗开始时向自身施加等量的堕落值。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_DegenerateCounter { get; set; }

    /// <summary>
    /// 旧命令 dq_corruption_rate 的别名，指向同一个计数。
    /// </summary>
    public int DonQuixote_CorruptionPerTurn
    {
        get => DonQuixote_DegenerateCounter;
        set => DonQuixote_DegenerateCounter = value;
    }

    public override bool ShowCounter => true;

    public override int DisplayAmount => DonQuixote_DegenerateCounter;

    public void DonQuixote_RefreshCounter()
    {
        InvokeDisplayAmountChanged();
    }

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string CustomIconPath => "res://DonQuixote/images/relics/knight_instinct_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/knight_instinct_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/knight_instinct_big.png";

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null || DonQuixote_DegenerateCounter <= 0)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_ApplyCorruption(
            new BlockingPlayerChoiceContext(), Owner.Creature, DonQuixote_DegenerateCounter, null);
    }

    // 补一道保险：玩家回合结束前。
    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext DonQuixote_context,
        MegaCrit.Sts2.Core.Combat.CombatSide DonQuixote_side,
        System.Collections.Generic.IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || Owner == null)
        {
            return;
        }

        Player DonQuixote_player = Owner;
        System.Collections.Generic.List<CardModel> DonQuixote_hand =
            MegaCrit.Sts2.Core.Entities.Cards.CardPile
                .Get(MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, DonQuixote_player)
                .Cards
                .Where(DonQuixote_card => DonQuixote_card != null)
                .ToList();

        foreach (CardModel DonQuixote_card in DonQuixote_hand)
        {
            if (!DonQuixote_card.Keywords.Contains(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword.Ethereal))
            {
                continue;
            }

            if (!Enchantments.DonQuixoteIndestructibleState.DonQuixote_HasEnchantment(DonQuixote_card))
            {
                continue;
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] ethereal enchant pre-handled at turn end: " + DonQuixote_card.GetType().Name, 0);

            await Enchantments.DonQuixoteIndestructibleState.DonQuixote_ExhaustToDiscard(
                DonQuixote_context, DonQuixote_card);
        }
    }
}

/// <summary>
/// 血液结晶（稀有）：回合结束时，按全体敌方单位的流血回血。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteBloodCrystal : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string CustomIconPath => "res://DonQuixote/images/relics/blood_crystal_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/blood_crystal_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/blood_crystal_big.png";

    /// <summary>
    /// 总强度 × 总层数的换算比例。
    /// </summary>
    private const decimal DonQuixote_HealRate = 0.01m;

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        System.Collections.Generic.IEnumerable<Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }

        // Owner?.Creature 正常都有。
        Creature DonQuixote_me = Owner?.Creature
            ?? System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.Where(participants ?? System.Linq.Enumerable.Empty<Creature>(),
                    DonQuixote_c => DonQuixote_c != null && DonQuixote_c.IsPlayer));
        if (DonQuixote_me == null)
        {
            return;
        }

        // 逐个敌人结算：只要该敌人身上还有流血，就按下式回复并向上取整，最少 1 点。
        int DonQuixote_bleedingEnemies = 0;
        decimal DonQuixote_heal = 0m;
        System.Text.StringBuilder DonQuixote_detail = new System.Text.StringBuilder();

        foreach (Creature DonQuixote_enemy in DonQuixoteStageRules.DonQuixote_LivingEnemies(DonQuixote_me))
        {
            int DonQuixote_strength = DonQuixoteBloodMagic.DonQuixote_BleedStrength(DonQuixote_enemy);
            int DonQuixote_layers = DonQuixoteBloodMagic.DonQuixote_BleedLayers(DonQuixote_enemy);
            if (DonQuixote_strength <= 0 || DonQuixote_layers <= 0)
            {
                continue;
            }

            DonQuixote_bleedingEnemies++;

            decimal DonQuixote_raw = DonQuixote_strength * DonQuixote_layers * DonQuixote_HealRate;
            decimal DonQuixote_part = Math.Ceiling(DonQuixote_raw);
            if (DonQuixote_part < 1m)
            {
                DonQuixote_part = 1m;
            }

            DonQuixote_heal += DonQuixote_part;

            DonQuixote_detail.Append(" [").Append(DonQuixote_strength).Append('x')
                .Append(DonQuixote_layers).Append(" raw=").Append(DonQuixote_raw)
                .Append(" heal=").Append(DonQuixote_part).Append(']');
        }

        if (DonQuixote_bleedingEnemies == 0 || DonQuixote_heal <= 0m)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] blood crystal: no bleeding enemy", 0);
            return;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] blood crystal: enemies=" + DonQuixote_bleedingEnemies
            + " heal=" + DonQuixote_heal + DonQuixote_detail
            + " hp=" + DonQuixote_me.CurrentHp + "/" + DonQuixote_me.MaxHp, 0);

        await CreatureCmd.Heal(DonQuixote_me, DonQuixote_heal, true);
    }
}
