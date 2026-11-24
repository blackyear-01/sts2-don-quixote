using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Relics;

/// <summary>
/// 蜜拉卡（罕见）：施加流血强度时额外 +1 级，且这 1 级不受复还的血之冲动影响。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteMiraka : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override string CustomIconPath => "res://DonQuixote/images/relics/miraka_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/miraka_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/miraka_big.png";
}

/// <summary>
/// 硬血长柄刀（稀有）：每消耗 15 点血宴，获得 1 层硬血。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteHardBloodHalberd : ModRelicTemplate
{
    private const int DonQuixote_FeastPerStack = 15;

    /// <summary>
    /// 已经换算成硬血的「消耗血宴总数」额度。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_FeastConsumed { get; set; }

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string CustomIconPath => "res://DonQuixote/images/relics/hard_blood_halberd_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/hard_blood_halberd_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/hard_blood_halberd_big.png";

    public override Task BeforeCombatStart()
    {
        DonQuixote_FeastConsumed = 0;
        return Task.CompletedTask;
    }

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
        int DonQuixote_should = DonQuixote_total / DonQuixote_FeastPerStack;
        if (DonQuixote_should <= DonQuixote_FeastConsumed)
        {
            return;
        }

        int DonQuixote_gain = DonQuixote_should - DonQuixote_FeastConsumed;
        DonQuixote_FeastConsumed = DonQuixote_should;

        await PowerCmd.Apply<DonQuixotePowerHardBlood>(
            DonQuixote_choiceContext, Owner.Creature, DonQuixote_gain, Owner.Creature, null, false);

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] hard blood halberd +" + DonQuixote_gain
            + " (feast total " + DonQuixote_total + ")", 0);
    }
}

/// <summary>
/// 撕裂的血袋（稀有）：每回合前 3 次敌方触发流血效果时，额外触发 1 次。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteTornBloodBag : ModRelicTemplate
{
    private const int DonQuixote_ChargesPerRound = 3;

    /// <summary>
    /// 本回合已经用掉的次数。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_ChargesUsed { get; set; }

    /// <summary>
    /// 这份计数属于第几回合（回合号变了就清零）。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_ChargeRound { get; set; }

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string CustomIconPath => "res://DonQuixote/images/relics/torn_blood_bag_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/torn_blood_bag_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/torn_blood_bag_big.png";

    public override Task BeforeCombatStart()
    {
        DonQuixote_ChargesUsed = 0;
        DonQuixote_ChargeRound = 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 取走一次「额外触发」额度；本回合 3 次用完后返回 false。
    /// </summary>
    public bool DonQuixote_TryTakeCharge(int DonQuixote_round)
    {
        if (DonQuixote_ChargeRound != DonQuixote_round)
        {
            DonQuixote_ChargeRound = DonQuixote_round;
            DonQuixote_ChargesUsed = 0;
        }

        if (DonQuixote_ChargesUsed >= DonQuixote_ChargesPerRound)
        {
            return false;
        }

        DonQuixote_ChargesUsed++;
        InvokeDisplayAmountChanged();
        return true;
    }

    public override bool ShowCounter => true;

    public override int DisplayAmount =>
        DonQuixote_ChargesPerRound - DonQuixote_ChargesUsed;
}
