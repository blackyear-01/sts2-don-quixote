using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Cards;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// 二代眷属-桑丘：每两回合获得 2 层摇曳【血魔】。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteSanchoFollower : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/sancho_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/sancho_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/sancho_big.png";

    // 联机不同步修复（关键）：
    // 这里原来用「遗物实例自己的 private int 计数器」判断第几回合，并且挂在 AfterPlayerTurnStart 上。
    // 联机开战时游戏会用主机发来的序列化玩家数据重建「其它玩家」的遗物（自家遗物不重建），
    // 这个计数器不在存档/同步数据里 → 远端那份被清零，自家那份还留着上局累计值
    // → 两端对「现在是不是该给摇曳」的判断不一致 → 各端只给自己加 2 层 → 开战校验和不同 → 掉线。
    // 现在改用 CombatState.RoundNumber（两端一致且参与校验和）判定，并在
    // AfterSideTurnStart（原版对「回合开始加能力」的推荐钩子，见 AbstractModel 注释）里结算。
    public override async Task AfterSideTurnStart(
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != CombatSide.Player)
        {
            return;
        }

        Creature DonQuixote_owner = Owner?.Creature;
        if (DonQuixote_owner == null || DonQuixote_owner.IsDead)
        {
            return;
        }

        // 额外回合时其它玩家可能不在参与者里，此时不结算。
        if (DonQuixote_participants == null || !DonQuixote_participants.Contains(DonQuixote_owner))
        {
            return;
        }

        int DonQuixote_round = DonQuixote_state?.RoundNumber ?? 0;
        if (DonQuixote_round < 2 || DonQuixote_round % 2 != 0)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_GainWavering(
            new ThrowingPlayerChoiceContext(), DonQuixote_owner, 2, null);

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] sancho wavering round=" + DonQuixote_round
            + " player=" + Owner.NetId + " -> +2",
            0);
    }
}

/// <summary>
/// 二代眷属-杜尔西内亚：战斗开始时获得 15 点血编的荆棘。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteDulcinea : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/dulcinea_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/dulcinea_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/dulcinea_big.png";

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerBloodThorns>(
            new BlockingPlayerChoiceContext(), Owner.Creature, 15m, Owner.Creature, null, false);
    }
}

/// <summary>
/// 三代眷属-古良布洛：战斗开始时获得 50 点血宴。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteGullinbursti : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/gullinbursti_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/gullinbursti_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/gullinbursti_big.png";

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_GainBloodFeast(
            new BlockingPlayerChoiceContext(), Owner.Creature, 50, null);
    }
}

/// <summary>
/// 三代眷属-尼古莉娜：每消耗 30 点血宴获得 1 点力量。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteNikolina : ModRelicTemplate
{
    /// <summary>
    /// 已经换算成力量的「消耗血宴总数」额度。同样是遗物上的可变状态，
    /// 必须序列化，否则联机开战同步会把客机这份重置成 0，导致重复加力量（状态不同步）。
    /// </summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastConsumed { get; set; }

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/nikolina_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/nikolina_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/nikolina_big.png";

    /// <summary>
    /// 「消耗血宴总数」是每场战斗的临时能力，战斗结束就没了；
    /// 这个换算额度如果跟着跨战斗留着，第二场战斗就永远凑不满新的 30 点。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        DonQuixote_FeastConsumed = 0;
        return Task.CompletedTask;
    }

    // 每有 30 点「消耗血宴总数」获得 1 点力量
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext DonQuixote_choiceContext,
        PowerModel DonQuixote_power,
        decimal DonQuixote_amount,
        Creature DonQuixote_applier,
        CardModel DonQuixote_cardSource)
    {
        if (Owner?.Creature == null || DonQuixote_power is not DonQuixotePowerFeastTotal)
        {
            return;
        }

        int DonQuixote_total = DonQuixoteBloodMagic.DonQuixote_FeastTotal(Owner.Creature);
        int DonQuixote_should = DonQuixote_total / 30;
        if (DonQuixote_should <= DonQuixote_FeastConsumed)
        {
            return;
        }

        int DonQuixote_gain = DonQuixote_should - DonQuixote_FeastConsumed;
        DonQuixote_FeastConsumed = DonQuixote_should;
        await PowerCmd.Apply<StrengthPower>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_gain, Owner.Creature, null, false);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] nikolina strength +" + DonQuixote_gain + " (feast total " + DonQuixote_total + ")", 0);
    }
}

/// <summary>
/// 此去永相别：失去桑丘，战斗开始时获得 1 层决斗的荣耀。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteFarewell : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/farewell_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/farewell_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/farewell_big.png";

    public override async Task BeforeCombatStart()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerDuelGlory>(
            new BlockingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null, false);
    }
}

/// <summary>
/// 飞天若奔月：每张堕落牌首次打出后，本场战斗内「这张牌」费用 -1。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteMoonFlight : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string CustomIconPath => "res://DonQuixote/images/relics/moon_flight_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/moon_flight_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/moon_flight_big.png";

    /// <summary>
    /// 每张堕落牌首次被打出后，只给「打出的这张牌」记上折扣（见
    /// <see cref="DonQuixoteMoonFlightState"/>）。折扣本身由
    /// <see cref="DonQuixoteCorruptedCostPatch"/> 在费用钩子里减，这里只负责记账，
    /// 所以没打出过的堕落牌价格不受影响。
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        CardModel DonQuixote_card = DonQuixote_cardPlay?.Card;
        if (Owner?.Creature == null
            || DonQuixote_card?.Owner?.Creature == null
            || !ReferenceEquals(DonQuixote_card.Owner.Creature, Owner.Creature))
        {
            return Task.CompletedTask;
        }

        if (DonQuixote_card is not IDonQuixoteCorruptedCard
            || DonQuixoteMoonFlightState.DonQuixote_WasPlayedThisCombat(DonQuixote_card))
        {
            return Task.CompletedTask;
        }

        DonQuixoteMoonFlightState.DonQuixote_MarkPlayed(DonQuixote_card);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 胜利之证（罕见）：击杀流血强度大于 10 的敌人后，下回合获得 2 层力量。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteProofOfVictory : ModRelicTemplate
{
    /// <summary>
    /// 待发放的力量。必须是 [SavedProperty]：联机开战时远端玩家的遗物会被
    /// 主机发来的序列化数据重建，未序列化的字段会被清零（两端不一致 → 不同步）。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DonQuixote_pendingStrength { get; set; }

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override string CustomIconPath => "res://DonQuixote/images/relics/victory_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/victory_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/victory_big.png";

    public override async Task AfterDamageGiven(
        PlayerChoiceContext DonQuixote_choiceContext,
        Creature DonQuixote_dealer,
        DamageResult DonQuixote_result,
        ValueProp DonQuixote_props,
        Creature DonQuixote_target,
        CardModel DonQuixote_cardSource)
    {
        await Task.CompletedTask;

        if (Owner?.Creature == null || DonQuixote_result == null || DonQuixote_target == null
            || !DonQuixote_result.WasTargetKilled)
        {
            return;
        }

        int DonQuixote_strength = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_target)?.Amount ?? 0;
        if (DonQuixote_strength > 10)
        {
            DonQuixote_pendingStrength = true;
        }
    }

    // 同桑丘：挂在 AfterPlayerTurnStart 上会落在「每个玩家各自的回合准备」里，
    // 联机时一端的准备被暂停就会让结算时机/次数不一致。改为 AfterSideTurnStart。
    public override async Task AfterSideTurnStart(
        CombatSide DonQuixote_side,
        IReadOnlyList<Creature> DonQuixote_participants,
        ICombatState DonQuixote_state)
    {
        if (DonQuixote_side != CombatSide.Player || !DonQuixote_pendingStrength)
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

        DonQuixote_pendingStrength = false;
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), DonQuixote_owner, 2m, DonQuixote_owner, null, false);
    }
}
