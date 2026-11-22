using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// Potion pool that owns every Don Quixote potion (empty for now).
/// </summary>
public sealed class DonQuixotePotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "orange";

    public override Color LabOutlineColor => new Color("#d58b2f");

    protected override IEnumerable<Type> PotionTypes => Array.Empty<Type>();
}
