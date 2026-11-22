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
///
/// 遗物.docx：战斗开始时第一张带有血铸关键词的卡牌打出时，
/// 血铸将不会导致硬血消耗带来全额影响
/// —— 即「第一次血铸不真的扣硬血，但效果照常按全额结算」。
///
/// 实现挂在 <see cref="DonQuixoteBloodMagic.DonQuixote_ConsumeHardBlood"/> 上：
/// 每场战斗第一次消耗时直接返回想要消耗的数额（视为消耗成功、伤害加成照给），
/// 但不动硬血的层数。用 <see cref="DonQuixote_Used"/> 记录，进战斗时重置。
/// </summary>
[RegisterRelic(typeof(DonQuixoteRelicPool))]
public sealed class DonQuixoteGloriousDuel : ModRelicTemplate
{
    /// <summary>本场战斗是否已经免过一次硬血消耗。必须序列化，联机两端要一致。</summary>
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
    /// 尝试消耗这次硬血。返回 true 表示「这次免单，但按全额结算」。
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
