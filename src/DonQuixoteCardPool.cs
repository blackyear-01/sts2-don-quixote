using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// Card pool that owns every Don Quixote card. 卡牌来源只有一处：每张卡上的 [RegisterCard(typeof(DonQuixoteCardPool))] 特性。
/// </summary>
public sealed class DonQuixoteCardPool : TypeListCardPoolModel
{
    public override string Title => "DonQuixote";

    // 必须是原版已有的能量颜色（ironclad/silent/defect/watcher/necrobinder/regent/colorless）。
    public override string EnergyColorName => "ironclad";

    public override string CardFrameMaterialPath => "card_frame_orange";

    public override Color DeckEntryCardColor => new Color("#d58b2f");

    public override bool IsColorless => false;
}
