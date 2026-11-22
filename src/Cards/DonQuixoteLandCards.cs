using System.Collections.Generic;
using System.Threading.Tasks;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Cards;

/// <summary>
/// 幸福笑容如鲜花般绽放之地：4 费能力（稀有，堂吉诃德卡池但不可通过选卡获得）。
/// 堕落 80、保留。
///
/// 打出后战场换成拉曼却乐园的游乐园夜景并奏响《欢迎来到拉曼查乐园》；能力持续
/// <see cref="DonQuixote_Turns"/> 回合，每个回合开始时全体友方单位获得 3 层
/// 血编的荆棘、20 层摇曳【血魔】并清除负面效果，且持续期间摇曳不会在回合开始时消失
/// （见 <see cref="DonQuixotePowerHappySmileLand"/>）。
///
/// 升级：能耗 -1、持续回合 +1（文档「升级后：减少1点能耗……后4回合」）。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteHappySmileLand : ModCardTemplate, IDonQuixoteCorruptible, IDonQuixoteUnselectableCard
{
    public const int DonQuixote_CorruptionThreshold = 80;

    private const decimal DonQuixote_Turns = 3m;

    public int DonQuixote_CorruptAt => DonQuixote_CorruptionThreshold;

    public CardModel DonQuixote_CorruptedCanonical => ModelDb.Card<DonQuixoteNightmareLand>();

    public CardModel DonQuixote_CorruptedVersion(Player DonQuixote_owner)
    {
        return CardScope.CreateCard(ModelDb.Card<DonQuixoteNightmareLand>(), DonQuixote_owner);
    }

    public DonQuixoteHappySmileLand()
        : base(4, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Corruption", DonQuixote_CorruptionThreshold),
        new DynamicVar("Turns", DonQuixote_Turns),
        new DynamicVar("Thorns", DonQuixoteStageRules.DonQuixote_ThornsPerTurn),
        new DynamicVar("Wavering", DonQuixoteStageRules.DonQuixote_WaveringPerTurn),
    };

    /// <summary>卡面插画（资源\卡牌\幸福笑容如鲜花般绽放之地.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/happy_smile_land.png";

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 层数 = 剩余回合数，由能力自己在每回合开始时递减。
        await PowerCmd.Apply<DonQuixotePowerHappySmileLand>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Turns"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["Turns"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// 噩梦的拉曼却领：4 费能力（稀有，堂吉诃德卡池但不可通过选卡获得），保留。
/// 「幸福笑容如鲜花般绽放之地」的堕落版本，也由「血魔长老的本能」直接发放。
///
/// 持续 <see cref="DonQuixote_Turns"/> 回合：每个回合开始时全体友方单位翻倍身上的增益
/// 并清除所有负面效果，所有敌方单位的负面效果翻倍并清除所有增益（沙坑除外），
/// 且持续期间摇曳【血魔】不会在回合开始时消失。
///
/// 升级：能耗 -1、持续回合 +1。
/// </summary>
[RegisterCard(typeof(DonQuixoteCardPool))]
public sealed class DonQuixoteNightmareLand : ModCardTemplate, IDonQuixoteCorruptedCard
{
    public DonQuixoteNightmareLand()
        : base(4, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Turns", DonQuixote_Turns),
    };

    private const decimal DonQuixote_Turns = 3m;

    /// <summary>卡面插画（资源\卡牌\噩梦的拉曼却领.webp）。</summary>
    public override string CustomPortraitPath => "res://DonQuixote/images/cards/nightmare_land.png";

    protected override async Task OnPlay(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerNightmareLand>(
            DonQuixote_choiceContext,
            Owner.Creature,
            DynamicVars["Turns"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["Turns"].UpgradeValueBy(1m);
    }
}
