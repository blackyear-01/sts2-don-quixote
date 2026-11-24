using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 白月骑士：白月骑士-巴里战斗开始时给自己挂上的身份标记（1 层）。
/// </summary>
public sealed class DonQuixotePowerWhiteMoonKnight : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string CustomIconPath => "res://DonQuixote/images/powers/white_moon_knight.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/white_moon_knight_big.png";
}

/// <summary>
/// 白月骑士-决斗对象：白月骑士点名的目标标记。
/// </summary>
public sealed class DonQuixotePowerWhiteMoonDuel : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 头顶（状态图标）直接用和「决斗对象」一样的特写大图。
    public override string CustomIconPath => "res://DonQuixote/images/powers/duel_target_big.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/duel_target_big.png";

    public override async Task BeforeSideTurnEndVeryEarly(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 状态挂在玩家身上，但要等白月骑士（敌方）的回合走完才减层，和玩家侧「决斗对象」同一套时机。
        if (side != CombatSide.Enemy || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// 白月骑士-决斗的荣耀：白月骑士侧的「决斗的荣耀」，和玩家侧同名状态共用图标与头顶标记。
/// </summary>
public sealed class DonQuixotePowerWhiteMoonGlory : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/duel_glory_big.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/duel_glory_big.png";
}
