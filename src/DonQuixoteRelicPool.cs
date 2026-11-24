using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// Relic pool that owns every Don Quixote relic. 遗物来源只有一处：每个遗物类上的 [RegisterRelic(typeof(DonQuixoteRelicPool))] 特性。
/// </summary>
public sealed class DonQuixoteRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "ironclad";

    public override Color LabOutlineColor => new Color("#d58b2f");
}
