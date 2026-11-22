using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// Relic pool that owns every Don Quixote relic.
///
/// 遗物来源只有一处：每个遗物类上的 [RegisterRelic(typeof(DonQuixoteRelicPool))] 特性，
/// RitsuLib 会用 ModHelper.AddModelToPool 注入本池。覆写 TypeListRelicPoolModel.RelicTypes
/// 会被 RitsuLib 判为 Obsolete，因为它和注入重复 —— 同一件遗物在 AllRelics 里出现两次，
/// 会让随机遗物奖励的权重翻倍、甚至一次奖励里给出两件相同的遗物。
/// </summary>
public sealed class DonQuixoteRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "ironclad";

    public override Color LabOutlineColor => new Color("#d58b2f");
}
