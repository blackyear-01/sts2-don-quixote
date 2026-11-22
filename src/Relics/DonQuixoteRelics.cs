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
/// 血魔的本能：每次攻击回复造成伤害的 15%；
/// 消耗血宴总数每满 325 点，把「幸福笑容如鲜花般绽放之地」加入手牌；
/// 第二回合开始后若血宴为 0，获得 1 层虚弱与 1 层易伤。
/// 拿到欧洛巴斯之触后精炼成「血魔长老的本能」。
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

    /// <summary>已经发放过几次舞台卡（按「消耗血宴总数 / 325」计）。</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastRewardSteps { get; set; }

    /// <summary>
    /// 「消耗血宴总数」是战斗内的临时能力，战斗一结束就归零，
    /// 所以「已经发过几张」也必须跟着每场战斗复位 —— 否则第一场把 325/650 用掉之后，
    /// 后面每场从头攒的额度永远追不上整局累计的计数，舞台卡就再也不发了。
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

    // 与桑丘同理：会改状态的回合开始效果走 AfterSideTurnStart，避免落在
    // AfterPlayerTurnStart（每个玩家各自的回合准备，联机时可能被暂停/交错）里。
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

        // 兜底补发：正常在「消耗血宴总数」涨上去的那一刻就发了；
        // 万一那次没发成（手牌已满、时点不对），回合开始再补一次（按遗物里的已发次数去重）。
        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_owner);

        // 「第二回合开始后」才检查血宴：第一回合是给玩家铺血宴的时间，不该被直接惩罚。
        // 用 CombatState.RoundNumber 判定（参与校验和、两端一致），不要自己数回合。
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
/// 每次攻击回复造成伤害的 20%，每打出一张攻击牌获得 2 点血宴；
/// 消耗血宴总数每满 325 点，把升级版「幸福笑容如鲜花般绽放之地」加入手牌；
/// 第二回合开始后若血宴为 0，获得 1 层虚弱与 1 层易伤。
/// 不进随机遗物池，只能通过精炼获得。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteBloodElderInstinct : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override string CustomIconPath => "res://DonQuixote/images/relics/elder_instinct_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/elder_instinct_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/elder_instinct_big.png";

    /// <summary>精炼后发的是升级版「幸福笑容如鲜花般绽放之地」；同样按消耗血宴总数发货，所以另记一个计数。</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastRewardSteps { get; set; }

    /// <summary>
    /// 与「血魔的本能」同理：「消耗血宴总数」每场战斗归零，
    /// 这里的发放计数也必须在开战前复位，否则整局只会发得出前几张。
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

    /// <summary>每打出一张攻击牌获得 2 点血宴。</summary>
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

    // 与血魔的本能一致：改状态的回合开始效果走 AfterSideTurnStart，避免联机时机不一致。
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

        // 兜底补发：正常在「消耗血宴总数」涨上去的那一刻就发了；
        // 万一那次没发成（手牌已满、时点不对），回合开始再补一次（按遗物里的已发次数去重）。
        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_owner);

        // 「第二回合开始后」才检查血宴：第一回合是给玩家铺血宴的时间，不该被直接惩罚。
        // 用 CombatState.RoundNumber 判定（参与校验和、两端一致），不要自己数回合。
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
/// 计数默认 0，可通过控制台命令 dq_corruption_rate 调整。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
[RegisterCharacterStarterRelic(typeof(DonQuixoteCharacter), Order = 10)]
public sealed class DonQuixoteKnightOfInstinct : ModRelicTemplate
{
    /// <summary>
    /// 堕落计数：战斗开始时向自身施加等量的堕落值。
    /// 必须走 [SavedProperty] 才会写进遗物存档数据：联机开战时游戏会用主机的序列化玩家数据
    /// 覆盖各端状态，未序列化的字段会被重置成默认值（表现为客机开战没有堕落值 → 状态不同步）。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_DegenerateCounter { get; set; }

    /// <summary>旧命令 dq_corruption_rate 的别名，指向同一个计数。</summary>
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

    // 补一道保险：玩家回合结束前，把「不可摧毁卡牌」附魔 + 虚无 的手牌提前自动打出并送进弃牌堆，
    // 这样原版的虚无结算（ResolveTurnEndCardEffects）就找不到这张牌了。
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
/// 血液结晶（稀有）：回合结束时，按全体敌方单位的流血回血
/// —— 总流血强度 × 总层数 × 0.01（向上取整）。
/// 旧版是「自身流血强度减半 + 按自身强度×层数回血」，新版不再动自身流血。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteBloodCrystal : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string CustomIconPath => "res://DonQuixote/images/relics/blood_crystal_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/blood_crystal_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/blood_crystal_big.png";

    /// <summary>总强度 × 总层数的换算比例。</summary>
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

        // Owner?.Creature 正常都有；极端情况下（遗物挂在还没绑定生物的单位上）退回 participants 里的玩家。
        Creature DonQuixote_me = Owner?.Creature
            ?? System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.Where(participants ?? System.Linq.Enumerable.Empty<Creature>(),
                    DonQuixote_c => DonQuixote_c != null && DonQuixote_c.IsPlayer));
        if (DonQuixote_me == null)
        {
            return;
        }

        int DonQuixote_totalStrength = 0;
        int DonQuixote_totalLayers = 0;

        foreach (Creature DonQuixote_enemy in DonQuixoteStageRules.DonQuixote_LivingEnemies(DonQuixote_me))
        {
            DonQuixote_totalStrength += DonQuixoteBloodMagic.DonQuixote_BleedStrength(DonQuixote_enemy);
            DonQuixote_totalLayers += DonQuixoteBloodMagic.DonQuixote_BleedLayers(DonQuixote_enemy);
        }

        if (DonQuixote_totalStrength <= 0 || DonQuixote_totalLayers <= 0)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] blood crystal: no bleed (strength=" + DonQuixote_totalStrength
                + " layers=" + DonQuixote_totalLayers + ")", 0);
            return;
        }

        // 总强度 × 总层数 × 0.01，结果向上取整（本模组统一规则）。
        decimal DonQuixote_heal = Math.Ceiling(
            DonQuixote_totalStrength * DonQuixote_totalLayers * DonQuixote_HealRate);

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] blood crystal: strength=" + DonQuixote_totalStrength
            + " layers=" + DonQuixote_totalLayers
            + " raw=" + (DonQuixote_totalStrength * DonQuixote_totalLayers * DonQuixote_HealRate)
            + " heal=" + DonQuixote_heal
            + " hp=" + DonQuixote_me.CurrentHp + "/" + DonQuixote_me.MaxHp, 0);

        if (DonQuixote_heal <= 0m)
        {
            return;
        }

        await CreatureCmd.Heal(DonQuixote_me, DonQuixote_heal, true);
    }
}
