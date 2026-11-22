using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace DonQuixote.Acts;

/// <summary>
/// 折射轨道线的线性地图：先古 → 宝箱 → 商店 → 休息 → Boss。
///
/// 这一层不刷普通怪和精英（文档要求），而原版 <see cref="StandardActMap"/> 会强制
/// 在地图第一行放怪物节点、其余未分配节点也退化成怪物，所以这里不用标准地图，
/// 照 <c>GoldenPathActMap</c> 的做法手搓一条单列地图。
/// </summary>
public sealed class DonQuixoteRefractionRailMap : ActMap
{
    private const int DonQuixote_Width = 7;

    private const int DonQuixote_Middle = 3;

    /// <summary>除先古与 Boss 外的房间：宝箱、商店、休息。</summary>
    private static readonly MapPointType[] DonQuixote_Rooms =
    {
        MapPointType.Treasure,
        MapPointType.Shop,
        MapPointType.RestSite,
    };

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    protected override MapPoint?[,] Grid { get; }

    public DonQuixoteRefractionRailMap(IRunState runState)
    {
        Grid = new MapPoint[DonQuixote_Width, DonQuixote_Rooms.Length + 1];

        BossMapPoint = new MapPoint(DonQuixote_Width / 2, GetRowCount())
        {
            PointType = MapPointType.Boss,
        };

        StartingMapPoint = new MapPoint(DonQuixote_Width / 2, 0)
        {
            PointType = MapPointType.Ancient,
        };

        for (int DonQuixote_index = 0; DonQuixote_index < DonQuixote_Rooms.Length; DonQuixote_index++)
        {
            MapPoint DonQuixote_point = new MapPoint(DonQuixote_Middle, DonQuixote_index + 1)
            {
                PointType = DonQuixote_Rooms[DonQuixote_index],
            };

            Grid[DonQuixote_Middle, DonQuixote_index + 1] = DonQuixote_point;

            if (DonQuixote_index == 0)
            {
                StartingMapPoint.AddChildPoint(DonQuixote_point);
                startMapPoints.Add(DonQuixote_point);
            }
            else
            {
                Grid[DonQuixote_Middle, DonQuixote_index]?.AddChildPoint(DonQuixote_point);
            }
        }

        Grid[DonQuixote_Middle, GetRowCount() - 1]?.AddChildPoint(BossMapPoint);
    }
}

/// <summary>
/// 折射轨道线：第三层「荣耀」结束、进入建筑师之前多出来的一层。
///
/// ・美术全部复用「荣耀」：靠 <see cref="DonQuixoteRailActPathPatch"/> 把
///   <c>ActModel.FilePathIdentifier</c> 改成 glory，休息室背景 / 地图背景 / 战场背景
///   就都指向荣耀的原始资源，不用额外塞一套图。
/// ・房间只有宝箱 / 商店 / 休息 + Boss（白月骑士-巴里），先古是参孙。
/// </summary>
public sealed class DonQuixoteRefractionRail : ActModel
{
    /// <summary>进建筑师的最后一层之后就是它，所以固定排在荣耀（Index 2）后面。</summary>
    public override int Index => 3;

    public override bool IsDefault => true;

    protected override int BaseNumberOfRooms => 3;

    public override Color MapTraveledColor => new Color("1D1E2F");

    public override Color MapUntraveledColor => new Color("60717C");

    public override Color MapBgColor => new Color("819A97");

    // 音乐、环境音、宝箱全部沿用第三层。
    public override string[] BgMusicOptions => new[] { "event:/music/act3_a1_v1", "event:/music/act3_a2_v1" };

    public override string[] MusicBankPaths => new[]
    {
        "res://banks/desktop/act3_a1.bank",
        "res://banks/desktop/act3_a2.bank",
    };

    public override string AmbientSfx => "event:/sfx/ambience/act3_ambience";

    public override string ChestSpineResourcePath =>
        "res://animations/backgrounds/treasure_room/chest_room_act_3_skel_data.tres";

    public override string ChestSpineSkinNameNormal => "act3";

    public override string ChestSpineSkinNameStroke => "act3_stroke";

    public override string ChestOpenSfx => "event:/sfx/ui/treasure/treasure_act3";

    public override IEnumerable<EncounterModel> BossDiscoveryOrder =>
        new List<EncounterModel> { ModelDb.Encounter<Encounters.DonQuixoteWhiteMoonKnightEncounter>() };

    public override IEnumerable<AncientEventModel> AllAncients =>
        new List<AncientEventModel> { ModelDb.AncientEvent<Events.DonQuixoteAncientSamson>() };

    public override IEnumerable<EventModel> AllEvents => new List<EventModel>();

    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
        new List<EncounterModel> { ModelDb.Encounter<Encounters.DonQuixoteWhiteMoonKnightEncounter>() };

    public override IEnumerable<AncientEventModel> GetUnlockedAncients(UnlockState unlockState) =>
        AllAncients.ToList();

    protected override void ApplyActDiscoveryOrderModifications(UnlockState unlockState)
    {
    }

    public override bool IsUnlocked(UnlockState unlockState)
    {
        return true;
    }

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng)
    {
        // 自定义地图不会用到这些计数，但 ActModel 是抽象要求实现。
        return new MapPointTypeCounts(0, 3)
        {
            NumOfElites = 0,
        };
    }
}
