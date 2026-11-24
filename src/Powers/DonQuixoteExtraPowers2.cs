using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 破碎四散的硬血：本回合每抽一张牌，对全体敌人施加 1 级流血。
/// </summary>
public sealed class DonQuixotePowerShatteredHardBlood : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/shattered_hard_blood.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/shattered_hard_blood_big.png";

    public override async Task AfterCardDrawn(
        PlayerChoiceContext DonQuixote_choiceContext,
        CardModel DonQuixote_card,
        bool DonQuixote_fromHandDraw)
    {
        if (Owner == null || Owner.IsDead || DonQuixote_card == null)
        {
            return;
        }

        if (Owner.Player == null || DonQuixote_card.Owner != Owner.Player)
        {
            return;
        }

        foreach (Creature DonQuixote_enemy in DonQuixoteBloodMagic.DonQuixote_EnemiesOf(Owner).ToList())
        {
            if (DonQuixote_enemy == null || DonQuixote_enemy.IsDead)
            {
                continue;
            }

            await DonQuixoteBloodMagic.DonQuixote_ApplyBleed(
                DonQuixote_choiceContext, DonQuixote_enemy, Owner, null, 1, 0);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext DonQuixote_choiceContext,
        CombatSide DonQuixote_side,
        IEnumerable<Creature> DonQuixote_participants)
    {
        if (DonQuixote_side != CombatSide.Player || Owner == null)
        {
            return;
        }

        if (DonQuixote_participants == null || !DonQuixote_participants.Contains(Owner))
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 所有凝固的血：自身受到的流血伤害降低 50%（升级 75%）。
/// </summary>
public sealed class DonQuixotePowerAllClottedBlood : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    // 数值（50 / 75）存在 Amount 上。
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/all_clotted_blood.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/all_clotted_blood_big.png";
}
