using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 展现洒落在战场上的潜伏之血：3 费能力，血宴获取量变为 1.5 倍（升级 2 倍）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteLatentBlood : ModCardTemplate
{
    public DonQuixoteLatentBlood()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        // 存百分比：50 = +50%（1.5 倍），升级后 100 = +100%（2 倍）。
        new DynamicVar("Bonus", 50m),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerLatentBlood>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Bonus"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Bonus"].UpgradeValueBy(50m);
    }
}

/// <summary>
/// 好点子形态：3 费能力，每消耗 20 点血宴获得 1 点能量。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteGoodIdeaForm : ModCardTemplate
{
    public DonQuixoteGoodIdeaForm()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("FeastCost", 20m),
    };

    /// <summary>
    /// 卡面插画（资源\卡牌\好点子形态.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/good_idea_form.png";

    private int DonQuixote_Threshold => DynamicVars["FeastCost"].IntValue;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        await PowerCmd.Apply<DonQuixotePowerGoodIdeaForm>(
            DonQuixote_choiceContext, Owner.Creature, 1m, Owner.Creature, this, false);

        DonQuixotePowerGoodIdeaForm DonQuixote_power =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerGoodIdeaForm>(Owner.Creature);
        DonQuixote_power?.DonQuixote_Initialize(
            DonQuixote_Threshold,
            DonQuixoteBloodMagic.DonQuixote_FeastTotal(Owner.Creature));
    }

    protected override void OnUpgrade()
    {
        // 20 -> 15：消耗更少的血宴就能攒出 1 点能量。
        DynamicVars["FeastCost"].UpgradeValueBy(-5m);

        // 升级后固有。关键词是实例上缓存的。
        AddKeyword(CardKeyword.Innate);
    }
}
