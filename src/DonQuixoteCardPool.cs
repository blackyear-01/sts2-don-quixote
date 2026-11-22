using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// Card pool that owns every Don Quixote card.
///
/// 卡牌来源只有一处：每张卡上的 [RegisterCard(typeof(DonQuixoteCardPool))] 特性，
/// RitsuLib 会用 ModHelper.AddModelToPool 把卡注入本池。
/// 不要再额外覆写 TypeListCardPoolModel.CardTypes —— RitsuLib 已把它标记为 Obsolete，
/// 原因正是「在这里列出的类型会和 ModHelper 注入重复」，导致 AllCards 里同一张卡出现两次
/// （卡牌奖励里同一张卡权重翻倍、甚至一次奖励里给出两张相同的卡）。
/// </summary>
public sealed class DonQuixoteCardPool : TypeListCardPoolModel
{
    public override string Title => "DonQuixote";

    // 必须是原版已有的能量颜色（ironclad/silent/defect/watcher/necrobinder/regent/colorless），
    // 否则游戏会去找 res://images/packed/sprite_fonts/<名字>_energy_icon.png 并报错。
    public override string EnergyColorName => "ironclad";

    public override string CardFrameMaterialPath => "card_frame_orange";

    public override Color DeckEntryCardColor => new Color("#d58b2f");

    public override bool IsColorless => false;
}
