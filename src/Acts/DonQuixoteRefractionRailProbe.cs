using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote.Acts;

/// <summary>
/// 启动自检：确认「折射轨道线」真的挂进了原版的章节列表，
/// 并且它的 Boss 遭遇、先古、自定义地图都能取到。
/// 日志里搜 <c>selftest rail</c> 即可。
/// </summary>
internal static class DonQuixoteRefractionRailProbe
{
    public static string DonQuixote_Run()
    {
        StringBuilder DonQuixote_sb = new StringBuilder();

        try
        {
            ActModel DonQuixote_rail = ModelDb.Act<DonQuixoteRefractionRail>();
            DonQuixote_sb.Append("act=").Append(DonQuixote_rail != null ? DonQuixote_rail.Id.Entry : "missing");

            var DonQuixote_acts = ModelDb.Acts.ToList();
            DonQuixote_sb.Append(" acts=").Append(DonQuixote_acts.Count);

            // 我们**刻意不**出现在 ModelDb.Acts 里：那张表是全局的，塞进去会让
            // 每一局都变 4 幕，其它第四幕 mod（goety 等）就会认为位置被占而卡死。
            // 只在本局抽签结果（ActModel.GetRandomList）末尾追加，所以这里应为 clean。
            bool DonQuixote_present = DonQuixote_acts.Any(DonQuixote_a =>
                ReferenceEquals(DonQuixote_a, DonQuixote_rail));
            DonQuixote_sb.Append(" globalList=").Append(DonQuixote_present ? "polluted" : "clean");

            var DonQuixote_byIndex = ModelDb.ActsByIndex;
            DonQuixote_sb.Append(" byIndex=").Append(DonQuixote_byIndex.Count);

            if (DonQuixote_rail != null)
            {
                DonQuixote_sb.Append(" index=").Append(DonQuixote_rail.Index);
                DonQuixote_sb.Append(" fileId=").Append(DonQuixote_rail.Id.Entry.ToLowerInvariant());
                DonQuixote_sb.Append(" boss=").Append(DonQuixote_rail.AllBossEncounters.Count());
                DonQuixote_sb.Append(" ancients=").Append(DonQuixote_rail.AllAncients.Count());
                DonQuixote_sb.Append(" monsters=").Append(DonQuixote_rail.AllMonsters.Count());
            }

            // 荣耀的先古池里应该能看到巴里。
            bool DonQuixote_barry = ModelDb.AncientEvent<Events.DonQuixoteAncientBarry>() != null;
            DonQuixote_sb.Append(" barry=").Append(DonQuixote_barry ? "ok" : "missing");

            // 先古事件背景：EventModel.CreateBackgroundScene 走
            // res://scenes/events/background_scenes/<entry>.tscn，场景里再引贴图。
            // 这两处曾经缺过文件、也引错过图（引成白月骑士的战斗背景），所以每次启动都核一遍。
            DonQuixote_sb.Append(" ancientbg=").Append(DonQuixote_AncientBg());

            // 白月骑士：两个白月状态是否注册 + 逐帧贴图是否齐全。
            DonQuixote_sb.Append(" whitemoon=").Append(DonQuixote_WhiteMoon());
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_sb.Append(" failed=").Append(DonQuixote_Exception.Message);
        }

        // 第四层是 index 3，原版 TreasureRoom 的构造函数硬校验 actIndex<=2，
        // 曾经导致点宝箱节点直接黑屏。这里每次启动都试构造一次，确认放行补丁生效。
        DonQuixote_sb.Append(" treasure3=").Append(DonQuixote_TreasureRoomOk() ? "ok" : "blocked");

        return DonQuixote_sb.ToString();
    }

    /// <summary>第四层（actIndex=3）的宝箱房现在能否构造。false 说明放行补丁没挂上。</summary>
    private static bool DonQuixote_TreasureRoomOk()
    {
        try
        {
            _ = new MegaCrit.Sts2.Core.Rooms.TreasureRoom(3);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>两个先古的背景场景 + 贴图是否都在（2/2 表示场景与图都存在）。</summary>
    private static string DonQuixote_AncientBg()    {
        var DonQuixote_pairs = new[]
        {
            ("barry",
                "res://scenes/events/background_scenes/don_quixote_ancient_barry.tscn",
                "res://DonQuixote/images/events/bg_barry.png"),
            ("samson",
                "res://scenes/events/background_scenes/don_quixote_ancient_samson.tscn",
                "res://DonQuixote/images/events/bg_samson.png"),
        };

        StringBuilder DonQuixote_sb = new StringBuilder();
        foreach (var DonQuixote_pair in DonQuixote_pairs)
        {
            if (DonQuixote_sb.Length > 0)
            {
                DonQuixote_sb.Append(',');
            }

            int DonQuixote_hit = 0;
            if (ResourceLoader.Exists(DonQuixote_pair.Item2))
            {
                DonQuixote_hit++;
            }

            if (ResourceLoader.Exists(DonQuixote_pair.Item3))
            {
                DonQuixote_hit++;
            }

            DonQuixote_sb.Append(DonQuixote_pair.Item1).Append(':')
                .Append(DonQuixote_hit).Append("/2");
        }

        return DonQuixote_sb.ToString();
    }

    /// <summary>
    /// 白月骑士的战斗资源自检：
    /// <c>duel=ok glory=ok frames=20/20</c> 表示两个白月状态注册成功、
    /// 逐帧贴图（闲置/受伤/防御/4 帧挥砍/16 帧拉弓）全部在 PCK 里。
    /// </summary>
    private static string DonQuixote_WhiteMoon()
    {
        StringBuilder DonQuixote_sb = new StringBuilder();

        DonQuixote_sb.Append("duel=")
            .Append(ModelDb.Power<Powers.DonQuixotePowerWhiteMoonDuel>() != null ? "ok" : "missing");
        DonQuixote_sb.Append(" glory=")
            .Append(ModelDb.Power<Powers.DonQuixotePowerWhiteMoonGlory>() != null ? "ok" : "missing");

        var DonQuixote_paths = new List<string>
        {
            "res://DonQuixote/images/creatures/barry_idle.png",
            "res://DonQuixote/images/creatures/barry_hurt.png",
            "res://DonQuixote/images/creatures/barry_guard.png",
        };
        for (int DonQuixote_i = 0; DonQuixote_i < 4; DonQuixote_i++)
        {
            DonQuixote_paths.Add($"res://DonQuixote/images/creatures/barry_atk_f{DonQuixote_i}.png");
        }

        for (int DonQuixote_i = 0; DonQuixote_i < 16; DonQuixote_i++)
        {
            DonQuixote_paths.Add($"res://DonQuixote/images/creatures/barry_oath_f{DonQuixote_i}.png");
        }

        int DonQuixote_ok = 0;
        foreach (string DonQuixote_path in DonQuixote_paths)
        {
            if (ResourceLoader.Exists(DonQuixote_path))
            {
                DonQuixote_ok++;
            }
        }

        DonQuixote_sb.Append(" frames=").Append(DonQuixote_ok).Append('/').Append(DonQuixote_paths.Count);

        // 骨架（死亡图 / 全部姿势已经走 spine，帧图只在骨架加载失败时兜底）。
        // 只查存在性、不实例化：Vulkan 下实例化 Spine 会申请大块缓冲。
        var DonQuixote_spine = new List<string>
        {
            "res://DonQuixote/animations/barry_skel_data.tres",
            "res://DonQuixote/animations/barry.spjson",
            "res://DonQuixote/animations/barry.spatlas",
            "res://DonQuixote/scenes/creature_visuals/white_moon_knight_spine.tscn",
        };
        int DonQuixote_spineOk = 0;
        foreach (string DonQuixote_path in DonQuixote_spine)
        {
            if (ResourceLoader.Exists(DonQuixote_path))
            {
                DonQuixote_spineOk++;
            }
        }

        DonQuixote_sb.Append(" spine=").Append(DonQuixote_spineOk).Append('/').Append(DonQuixote_spine.Count);
        return DonQuixote_sb.ToString();
    }
}
