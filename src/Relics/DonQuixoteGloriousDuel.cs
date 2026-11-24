using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Relics;

/// <summary>
/// 光荣的决斗（先古遗物，白月骑士-巴里给的堂吉诃德专属）。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteGloriousDuel : ModRelicTemplate
{
    /// <summary>
    /// 本场战斗是否已经免过一次硬血消耗。
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DonQuixote_Used { get; set; }

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string CustomIconPath => "res://DonQuixote/images/relics/glorious_duel_icon.png";

    public override string CustomIconOutlinePath => "res://DonQuixote/images/relics/glorious_duel_outline.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/relics/glorious_duel_big.png";

    public override Task BeforeCombatStart()
    {
        // 每场战斗重新给一次「第一次血铸免费」。
        DonQuixote_Used = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 尝试消耗这次硬血。
    /// </summary>
    public static bool DonQuixote_TrySpare(Creature DonQuixote_owner, int DonQuixote_amount)
    {
        if (DonQuixote_owner?.Player == null || DonQuixote_amount <= 0)
        {
            return false;
        }

        foreach (RelicModel DonQuixote_relic in DonQuixote_owner.Player.Relics)
        {
            if (DonQuixote_relic is not DonQuixoteGloriousDuel DonQuixote_duel || DonQuixote_duel.DonQuixote_Used)
            {
                continue;
            }

            DonQuixote_duel.DonQuixote_Used = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] glorious duel: first forge spends no hard blood", 0);
            return true;
        }

        return false;
    }
}
