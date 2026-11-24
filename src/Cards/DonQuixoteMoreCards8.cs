using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 不被允许的期待：0 费技能，消耗手里 2 张牌，抽 1 张。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteUnallowedExpectation : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\不被允许的期待.png）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/unallowed_expectation.png";

    /// <summary>
    /// 要消耗的手牌数量。
    /// </summary>
    private const int DonQuixote_ExhaustCount = 2;

    public DonQuixoteUnallowedExpectation()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        CardPile DonQuixote_hand = PileType.Hand.GetPile(Owner);
        int DonQuixote_count = Math.Min(DonQuixote_ExhaustCount, DonQuixote_hand?.Cards.Count ?? 0);

        if (DonQuixote_count > 0)
        {
            CardSelectorPrefs DonQuixote_prefs = new CardSelectorPrefs(
                new LocString("cards", "DON_QUIXOTE_CARD_DON_QUIXOTE_UNALLOWED_EXPECTATION.selectionScreenPrompt"),
                DonQuixote_count);

            List<CardModel> DonQuixote_selected =
                (await CardSelectCmd.FromHand(DonQuixote_ctx, Owner, DonQuixote_prefs, null, this)).ToList();

            foreach (CardModel DonQuixote_card in DonQuixote_selected)
            {
                if (DonQuixote_card == null)
                {
                    continue;
                }

                await CardCmd.Exhaust(DonQuixote_ctx, DonQuixote_card, false, false);
            }
        }

        if (!IsUpgraded)
        {
            await CardPileCmd.Draw(DonQuixote_ctx, 1, Owner);
            return;
        }

        // 升级版：从抽牌堆挑 1 张，直接进手牌。
        CardPile DonQuixote_draw = PileType.Draw.GetPile(Owner);
        if (DonQuixote_draw == null || DonQuixote_draw.Cards.Count == 0)
        {
            return;
        }

        CardSelectorPrefs DonQuixote_drawPrefs = new CardSelectorPrefs(
            new LocString("cards", "DON_QUIXOTE_CARD_DON_QUIXOTE_UNALLOWED_EXPECTATION.drawSelectionPrompt"),
            1);

        List<CardModel> DonQuixote_picked =
            (await CardSelectCmd.FromCombatPile(DonQuixote_ctx, DonQuixote_draw, Owner, DonQuixote_drawPrefs)).ToList();

        foreach (CardModel DonQuixote_card in DonQuixote_picked)
        {
            if (DonQuixote_card == null)
            {
                continue;
            }

            await CardPileCmd.Add(DonQuixote_card, PileType.Hand, CardPilePosition.Top, this, false);
        }
    }
}

/// <summary>
/// 所做皆为徒劳：0 费攻击，虚无 + 消耗，造成 13 点伤害（升级 20）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteAllInVain : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\所做皆为徒劳.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/all_in_vain.png";

    public DonQuixoteAllInVain()
        : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Ethereal,
        CardKeyword.Exhaust,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(13m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        ArgumentNullException.ThrowIfNull(DonQuixote_play.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, DonQuixote_play)
            .Targeting(DonQuixote_play.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, null)
            .Execute(DonQuixote_ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(7m);
    }
}

/// <summary>
/// 陌生的晚宴：X 费技能。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteStrangeBanquet : ModCardTemplate
{
    /// <summary>
    /// 卡面插画（资源\卡牌\陌生的晚宴.webp）。
    /// </summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/strange_banquet.png";

    public DonQuixoteStrangeBanquet()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    /// <summary>
    /// X 费：能量全部投入，X = 本回合消耗的能量。
    /// </summary>
    protected override bool HasEnergyCostX => true;

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_ctx, CardPlay DonQuixote_play)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int DonQuixote_x = ResolveEnergyXValue();
        if (DonQuixote_x <= 0)
        {
            return;
        }

        // 升级版：这一段结算里，流血给的血宴翻倍。
        int DonQuixote_previousMultiplier = DonQuixoteBloodMagic.DonQuixote_FeastGainMultiplier;
        DonQuixoteBloodMagic.DonQuixote_FeastGainMultiplier = IsUpgraded ? 2 : 1;
        try
        {
            foreach (Creature DonQuixote_enemy in DonQuixoteStageRules.DonQuixote_LivingEnemies(Owner.Creature).ToList())
            {
                for (int DonQuixote_hit = 0; DonQuixote_hit < DonQuixote_x; DonQuixote_hit++)
                {
                    if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
                    {
                        break;
                    }

                    // consumeLayers: true.
                    await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(DonQuixote_ctx, DonQuixote_enemy, true);
                }
            }
        }
        finally
        {
            DonQuixoteBloodMagic.DonQuixote_FeastGainMultiplier = DonQuixote_previousMultiplier;
        }

        // 减少自身 2X 点生命（无视格挡与增伤，本质是失血）。
        await CreatureCmd.Damage(
            DonQuixote_ctx,
            Owner.Creature,
            DonQuixote_x * 2,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null,
            (CardPlay)null);
    }
}
