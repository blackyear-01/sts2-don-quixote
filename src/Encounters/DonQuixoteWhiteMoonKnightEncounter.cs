using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace DonQuixote.Encounters;

/// <summary>
/// 白月骑士-巴里的 Boss 遭遇（只在「折射轨道线」章节出现）。
///
/// 背景不走原版 <c>BackgroundAssets</c> 的目录约定，而是由怪物自己
/// （<see cref="Monsters.DonQuixoteWhiteMoonKnight.AfterAddedToRoom"/>）挂一层全屏贴图，
/// 所以这里 <see cref="EncounterModel.HasCustomBackground"/> 保持 false，避免去读不存在的层目录。
/// </summary>
public sealed class DonQuixoteWhiteMoonKnightEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => true;

    public override string CustomBgm => "event:/music/act3_boss_queen";

    public override string BossNodePath =>
        "res://images/map/placeholder/don_quixote_white_moon_knight_encounter_icon";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new List<MonsterModel> { ModelDb.Monster<Monsters.DonQuixoteWhiteMoonKnight>() };

    public override float GetCameraScaling()
    {
        return 0.95f;
    }

    public override Vector2 GetCameraOffset()
    {
        return Vector2.Down * 40f;
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        List<(MonsterModel, string?)> DonQuixote_list = new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Monsters.DonQuixoteWhiteMoonKnight>().ToMutable(), null),
        };

        return DonQuixote_list;
    }
}
