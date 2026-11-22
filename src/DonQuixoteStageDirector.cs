using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace DonQuixote;

/// <summary>
/// 战场舞台：把「幸福笑容如鲜花般绽放之地 / 噩梦的拉曼却领」两张能力卡变成真正的场景演出。
///
/// 做法
/// ----
/// ・背景：一张铺满全屏的 <see cref="TextureRect"/>，挂到
///   <c>%CombatSceneContainer</c> 的 1 号位（紧跟原版 BgContainer、排在角色容器之前），
///   位置计算统一交给 <see cref="DonQuixoteSceneOverlay"/>。只换天幕、不动角色和地面逻辑；
///   能力结束时淡出销毁，原版背景一直在下面躺着，天然「复原」。
/// ・帝王蟹（Kaiser Crab）这种「敌怪画在背景里」的场次整段跳过画面（只播音乐），
///   否则覆盖图会把 BOSS 一起盖掉。
/// ・音乐：新开一个 <see cref="AudioStreamPlayer"/> 播 ogg（原版音乐走 FMOD，塞不进自定义曲目），
///   同时把游戏自己的 BGM 音量压到 0（不 StopMusic，避免还原时把 FMOD 事件状态搞乱），
///   演出结束时把音量设回玩家的设置值。
///
/// 多人优先级
/// ----------
/// 两端都只根据「已经同步的能力实例」推算当前该放哪一个：按持有者在大厅里的座位号升序，
/// 同一个人重复打出则后打的优先。主机是 0 号座位，所以谁和主机同时打出都以主机为准，
/// 而且两端算出来的结果一致，不会出现各端各放一套的错位。
/// </summary>
internal static class DonQuixoteStageDirector
{
    public const string DonQuixote_StageHappy = "happy_smile_land";

    public const string DonQuixote_StageNightmare = "nightmare_land";

    private const string DonQuixote_OverlayName = "DonQuixoteStageOverlay";

    private const string DonQuixote_MusicName = "DonQuixoteStageMusic";

    private const float DonQuixote_FadeIn = 0.45f;

    private const float DonQuixote_FadeOut = 0.4f;

    private const double DonQuixote_TickSeconds = 0.4;

    /// <summary>
    /// 覆盖图在原背景层里留的富余（像素）。BgContainer 有 <c>offset_left/right = 23</c>，
    /// 并不正好落在屏幕中心，铺满时四周各多留一圈才不会露边。
    /// </summary>
    private const float DonQuixote_OverlayMargin = 96f;

    private sealed class DonQuixote_StageRequest
    {
        public string DonQuixote_Stage;

        public PowerModel DonQuixote_Power;

        public long DonQuixote_Order;
    }

    private static readonly List<DonQuixote_StageRequest> DonQuixote_Requests = new List<DonQuixote_StageRequest>();

    private static long DonQuixote_Counter;

    /// <summary>当前正在展示的舞台；空串表示没有舞台（原版场景）。</summary>
    private static string DonQuixote_Shown = string.Empty;

    private static Control? DonQuixote_Overlay;

    private static AudioStreamPlayer? DonQuixote_Music;

    private static bool DonQuixote_Ducked;

    private static float DonQuixote_SavedBgmVolume = 1f;

    private static SceneTree? DonQuixote_Tree;

    private static bool DonQuixote_Watching;

    /// <summary>
    /// 当前覆盖层已经存在了多久（秒）。淡入 tween 万一被打断/丢弃，alpha 会卡在中间，
    /// watchdog 靠这个计时把最终状态强行补成完全不透明。
    /// </summary>
    private static double DonQuixote_OverlayAge;

    private static string DonQuixote_LastNote = "none";

    /// <summary>
    /// 这一场「有舞台能力、但故意不画覆盖图」（帝王蟹这种敌怪画在背景里的场次）。
    /// 记下来就不会被 watchdog 每 0.4 秒重新尝试一遍。
    /// </summary>
    private static bool DonQuixote_Suppressed;

    // ------------------------------------------------------------------
    // 能力卡调用的入口
    // ------------------------------------------------------------------

    /// <summary>某个舞台能力被挂上了：登记并刷新当前该展示的舞台。</summary>
    public static void DonQuixote_Enter(string DonQuixote_Stage, PowerModel DonQuixote_Power)
    {
        try
        {
            DonQuixote_Requests.RemoveAll(r => ReferenceEquals(r.DonQuixote_Power, DonQuixote_Power));
            DonQuixote_Requests.Add(new DonQuixote_StageRequest
            {
                DonQuixote_Stage = DonQuixote_Stage,
                DonQuixote_Power = DonQuixote_Power,
                DonQuixote_Order = ++DonQuixote_Counter,
            });

            DonQuixote_StartWatchdog(DonQuixote_Overlay);
            DonQuixote_Refresh();
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage enter failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>某个舞台能力消失了（战斗结束、被驱散）：注销并刷新。</summary>
    public static void DonQuixote_Exit(PowerModel DonQuixote_Power)
    {
        try
        {
            if (DonQuixote_Requests.RemoveAll(r => ReferenceEquals(r.DonQuixote_Power, DonQuixote_Power)) > 0)
            {
                DonQuixote_Refresh();
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage exit failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>离开战斗：清空登记、淡出舞台、把 BGM 音量还回去。</summary>
    public static void DonQuixote_Reset()
    {
        DonQuixote_Requests.Clear();
        DonQuixote_Shown = string.Empty;
        DonQuixote_Watching = false;
        DonQuixote_OverlayAge = 0.0;
        DonQuixote_Suppressed = false;
        DonQuixote_ClearVisuals();
        DonQuixote_Unduck();
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Resume();
    }

    /// <summary>自检探针。</summary>
    public static string DonQuixote_Probe()
    {
        return $"stages={DonQuixote_Requests.Count} shown={(string.IsNullOrEmpty(DonQuixote_Shown) ? "none" : DonQuixote_Shown)} "
            + $"overlay={GodotObject.IsInstanceValid(DonQuixote_Overlay)} music={GodotObject.IsInstanceValid(DonQuixote_Music)} "
            + $"ducked={DonQuixote_Ducked} last={DonQuixote_LastNote}";
    }

    /// <summary>启动自检：两张背景、两首曲子、两张卡必须都能取到。</summary>
    public static string DonQuixote_ContentProbe()
    {
        string DonQuixote_happyBg = DonQuixote_HasBackground(DonQuixote_StageHappy);
        string DonQuixote_nightmareBg = DonQuixote_HasBackground(DonQuixote_StageNightmare);
        string DonQuixote_happyMusic = DonQuixote_HasMusic(DonQuixote_StageHappy);
        string DonQuixote_nightmareMusic = DonQuixote_HasMusic(DonQuixote_StageNightmare);
        string DonQuixote_cards = DonQuixote_HasCards();

        return $"bg={DonQuixote_happyBg}/{DonQuixote_nightmareBg} "
            + $"music={DonQuixote_happyMusic}/{DonQuixote_nightmareMusic} cards={DonQuixote_cards}";
    }

    private static string DonQuixote_HasBackground(string DonQuixote_Stage)
    {
        try
        {
            return ResourceLoader.Load<Texture2D>(
                DonQuixote_BackgroundPath(DonQuixote_Stage), null, ResourceLoader.CacheMode.Reuse) != null
                ? "ok"
                : "missing";
        }
        catch (Exception)
        {
            return "missing";
        }
    }

    private static string DonQuixote_HasMusic(string DonQuixote_Stage)
    {
        try
        {
            return DonQuixote_LoadOgg(DonQuixote_MusicPath(DonQuixote_Stage)) != null ? "ok" : "missing";
        }
        catch (Exception)
        {
            return "missing";
        }
    }

    /// <summary>
    /// 读 PCK 里的原始 ogg。先试 LoadFromFile（直接读文件），失败再退回
    /// FileAccess + LoadFromBuffer —— 不同 Godot 小版本对 res:// 内的原始音频文件宽容度不一样。
    /// </summary>
    private static AudioStreamOggVorbis? DonQuixote_LoadOgg(string DonQuixote_Path)
    {
        try
        {
            AudioStreamOggVorbis? DonQuixote_Stream = AudioStreamOggVorbis.LoadFromFile(DonQuixote_Path);
            if (DonQuixote_Stream != null)
            {
                return DonQuixote_Stream;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            byte[] DonQuixote_Bytes = FileAccess.GetFileAsBytes(DonQuixote_Path);
            if (DonQuixote_Bytes != null && DonQuixote_Bytes.Length > 0)
            {
                return AudioStreamOggVorbis.LoadFromBuffer(DonQuixote_Bytes);
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    /// <summary>两张舞台卡必须取得到，顺便把费用/稀有度打进日志便于对照文档核对。</summary>
    private static string DonQuixote_HasCards()
    {
        try
        {
            Cards.DonQuixoteHappySmileLand DonQuixote_happy = ModelDb.Card<Cards.DonQuixoteHappySmileLand>();
            Cards.DonQuixoteNightmareLand DonQuixote_nightmare = ModelDb.Card<Cards.DonQuixoteNightmareLand>();
            if (DonQuixote_happy == null || DonQuixote_nightmare == null)
            {
                return "missing";
            }

            return $"ok(happy={DonQuixote_happy.EnergyCost.Canonical}/{DonQuixote_happy.Rarity} "
                + $"nightmare={DonQuixote_nightmare.EnergyCost.Canonical}/{DonQuixote_nightmare.Rarity})";
        }
        catch (Exception)
        {
            return "missing";
        }
    }

    // ------------------------------------------------------------------
    // 选择该展示哪一幕
    // ------------------------------------------------------------------

    private static void DonQuixote_Refresh()
    {
        DonQuixote_Prune();

        string DonQuixote_Wanted = DonQuixote_Pick();

        if (!string.IsNullOrEmpty(DonQuixote_Wanted) && DonQuixote_Suppressed)
        {
            // 已经判断过「这场不该盖背景」（帝王蟹），不用每轮重试。
            return;
        }

        if (string.Equals(DonQuixote_Wanted, DonQuixote_Shown, StringComparison.Ordinal)
            && (string.IsNullOrEmpty(DonQuixote_Wanted) || DonQuixote_VisualsAlive()))
        {
            return;
        }

        DonQuixote_Show(DonQuixote_Wanted);
    }

    /// <summary>把已经不在了的能力（被驱散、持有者换场）从登记表里剔掉。</summary>
    private static void DonQuixote_Prune()
    {
        DonQuixote_Requests.RemoveAll(DonQuixote_IsGone);
    }

    private static bool DonQuixote_IsGone(DonQuixote_StageRequest DonQuixote_Request)
    {
        PowerModel DonQuixote_Power = DonQuixote_Request?.DonQuixote_Power;
        if (DonQuixote_Power == null)
        {
            return true;
        }

        try
        {
            MegaCrit.Sts2.Core.Entities.Creatures.Creature DonQuixote_Owner = DonQuixote_Power.Owner;
            if (DonQuixote_Owner == null)
            {
                return true;
            }

            foreach (PowerModel DonQuixote_Power2 in DonQuixote_Owner.Powers)
            {
                if (ReferenceEquals(DonQuixote_Power2, DonQuixote_Power))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// 谁说了算：座位号小的优先（主机是 0 号），同一个人打出的后一次优先。
    /// </summary>
    private static string DonQuixote_Pick()
    {
        string DonQuixote_BestStage = string.Empty;
        int DonQuixote_BestSeat = int.MaxValue;
        long DonQuixote_BestOrder = long.MinValue;

        foreach (DonQuixote_StageRequest DonQuixote_Request in DonQuixote_Requests)
        {
            int DonQuixote_Seat = DonQuixote_SeatOf(DonQuixote_Request.DonQuixote_Power);
            if (DonQuixote_Seat < DonQuixote_BestSeat
                || (DonQuixote_Seat == DonQuixote_BestSeat && DonQuixote_Request.DonQuixote_Order > DonQuixote_BestOrder))
            {
                DonQuixote_BestSeat = DonQuixote_Seat;
                DonQuixote_BestOrder = DonQuixote_Request.DonQuixote_Order;
                DonQuixote_BestStage = DonQuixote_Request.DonQuixote_Stage;
            }
        }

        return DonQuixote_BestStage ?? string.Empty;
    }

    private static int DonQuixote_SeatOf(PowerModel DonQuixote_Power)
    {
        try
        {
            Player DonQuixote_Player = DonQuixote_Power?.Owner?.Player;
            if (DonQuixote_Player == null)
            {
                return int.MaxValue;
            }

            RunState DonQuixote_State = RunManager.Instance?.DebugOnlyGetState();
            IReadOnlyList<Player> DonQuixote_Players = DonQuixote_State?.Players;
            if (DonQuixote_Players == null)
            {
                return int.MaxValue;
            }

            for (int DonQuixote_Index = 0; DonQuixote_Index < DonQuixote_Players.Count; DonQuixote_Index++)
            {
                if (ReferenceEquals(DonQuixote_Players[DonQuixote_Index], DonQuixote_Player))
                {
                    return DonQuixote_Index;
                }
            }
        }
        catch (Exception)
        {
        }

        return int.MaxValue;
    }

    // ------------------------------------------------------------------
    // 换场
    // ------------------------------------------------------------------

    private static bool DonQuixote_VisualsAlive()
    {
        return GodotObject.IsInstanceValid(DonQuixote_Overlay)
            && DonQuixote_Overlay != null
            && DonQuixote_Overlay.IsInsideTree();
    }

    /// <summary>
    /// 兜底：淡入 tween 被打断（节点被换掉、引擎丢 tween 等）时 alpha 会停在中间，
    /// 看着就像「背景没了」。这里在超过一个淡入时长后直接把不透明度设满。
    /// </summary>
    private static void DonQuixote_EnsureOverlayOpaque()
    {
        if (string.IsNullOrEmpty(DonQuixote_Shown))
        {
            DonQuixote_OverlayAge = 0.0;
            return;
        }

        if (!DonQuixote_VisualsAlive() || DonQuixote_Overlay == null)
        {
            DonQuixote_OverlayAge = 0.0;
            return;
        }

        DonQuixote_OverlayAge += DonQuixote_TickSeconds;

        // 原版背景层偶尔会在战斗中被重建（换 Encounter / 重新 SetUpBackground），
        // 节点也可能被换掉父级 —— 每轮重新贴一次位置，自愈。
        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        if (DonQuixote_Room != null)
        {
            DonQuixoteSceneOverlay.DonQuixote_Attach(
                DonQuixote_Overlay, DonQuixote_Room, DonQuixoteSceneOverlay.DonQuixote_OrderStage);
            DonQuixoteSceneOverlay.DonQuixote_Fill(DonQuixote_Overlay, DonQuixote_Room, DonQuixote_OverlayMargin);
        }

        if (DonQuixote_OverlayAge < DonQuixote_FadeIn * 2.0)
        {
            return;
        }

        if (DonQuixote_Overlay.Modulate.A < 0.99f)
        {
            DonQuixote_Overlay.Modulate = new Color(1f, 1f, 1f, 1f);
            DonQuixote_LastNote = "fade forced " + DonQuixote_Shown;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage overlay fade stuck, forced opaque: {DonQuixote_Shown}", 0);
        }
    }

    private static void DonQuixote_Show(string DonQuixote_Stage)
    {
        if (string.IsNullOrEmpty(DonQuixote_Stage))
        {
            DonQuixote_Shown = string.Empty;
            DonQuixote_Suppressed = false;
            DonQuixote_ClearVisuals();
            DonQuixote_Unduck();

            // 舞台收场：巴里战的白月背景重新露出来（还在巴里战才有效，其他战斗是空操作）。
            DonQuixoteWhiteMoonBackdrop.DonQuixote_Resume();
            return;
        }

        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        Control? DonQuixote_Bg = DonQuixote_Room?.GetNodeOrNull<Control>("%BgContainer");
        if (DonQuixote_Bg == null || DonQuixote_Room == null)
        {
            // 还没进战斗房间（或者已经离开了）：保持想要的状态，等 watchdog 重试。
            DonQuixote_Shown = string.Empty;
            DonQuixote_LastNote = "no bg container";
            return;
        }

        // 先撤掉上一张，再决定这一场画不画。
        DonQuixote_ClearVisuals();
        DonQuixote_Shown = DonQuixote_Stage;
        DonQuixote_OverlayAge = 0.0;

        // 帝王蟹这类「敌怪画在背景场景里」的场次：盖住背景等于盖住 BOSS，
        // 所以整段画面跳过，只保留音乐。
        if (DonQuixoteSceneOverlay.DonQuixote_BackgroundOwnsEnemies(DonQuixote_Room))
        {
            DonQuixote_Suppressed = true;
            DonQuixote_PlayMusic(DonQuixote_Stage, DonQuixote_Room);
            DonQuixote_Duck();
            DonQuixote_LastNote = "visual suppressed " + DonQuixote_Stage;
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] stage visual suppressed (boss owns background): {DonQuixote_Stage}", 0);
            return;
        }

        DonQuixote_Suppressed = false;

        Texture2D? DonQuixote_Texture = ResourceLoader.Load<Texture2D>(
            DonQuixote_BackgroundPath(DonQuixote_Stage), null, ResourceLoader.CacheMode.Reuse);
        if (DonQuixote_Texture == null)
        {
            // 缺图是永久性的，标成 suppressed 免得 watchdog 每 0.4 秒刷一遍日志。
            DonQuixote_Suppressed = true;
            DonQuixote_LastNote = "bg texture missing";
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage background missing: {DonQuixote_BackgroundPath(DonQuixote_Stage)}", 0);
            return;
        }

        // 挂到 %CombatSceneContainer 的 1 号位（紧跟原版背景层、排在角色容器之前），
        // 位置由 DonQuixoteSceneOverlay 统一计算 —— 客机/主机、背景缩放多少都不会压到角色。
        // **不去改原版背景的 Visible**：原背景一直躺着，最坏情况只是没换成舞台图。
        TextureRect DonQuixote_Frame = new TextureRect
        {
            Name = DonQuixote_OverlayName,
            Texture = DonQuixote_Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZAsRelative = true,
            ZIndex = 0,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };

        DonQuixoteSceneOverlay.DonQuixote_Attach(
            DonQuixote_Frame, DonQuixote_Room, DonQuixoteSceneOverlay.DonQuixote_OrderStage);
        DonQuixoteSceneOverlay.DonQuixote_Fill(DonQuixote_Frame, DonQuixote_Room, DonQuixote_OverlayMargin);

        // 巴里战里白月骑士自己的背景也挂在同一层：舞台卡是完整的一张天幕，
        // 让它暂时让位，否则两张全屏图叠着、谁在上面全看谁后挂。
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Suspend();

        Tween DonQuixote_Tween = DonQuixote_Frame.CreateTween();
        DonQuixote_Tween.TweenProperty(DonQuixote_Frame, "modulate:a", 1.0f, DonQuixote_FadeIn);

        DonQuixote_Overlay = DonQuixote_Frame;

        DonQuixote_PlayMusic(DonQuixote_Stage, DonQuixote_Room);
        DonQuixote_Duck();

        DonQuixote_LastNote = "shown " + DonQuixote_Stage;
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            $"[DonQuixote] stage shown: {DonQuixote_Stage} room={DonQuixote_Room.Size} "
            + DonQuixoteSceneOverlay.DonQuixote_Describe(DonQuixote_Frame),
            0);
    }

    private static void DonQuixote_ClearVisuals()
    {
        Control? DonQuixote_Frame = DonQuixote_Overlay;
        DonQuixote_Overlay = null;

        if (DonQuixote_Frame != null && GodotObject.IsInstanceValid(DonQuixote_Frame))
        {
            Tween DonQuixote_Tween = DonQuixote_Frame.CreateTween();
            DonQuixote_Tween.TweenProperty(DonQuixote_Frame, "modulate:a", 0.0f, DonQuixote_FadeOut);
            DonQuixote_Tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(DonQuixote_Frame))
                {
                    DonQuixote_Frame.QueueFree();
                }
            }));
        }

        AudioStreamPlayer? DonQuixote_Player = DonQuixote_Music;
        DonQuixote_Music = null;

        if (DonQuixote_Player != null && GodotObject.IsInstanceValid(DonQuixote_Player))
        {
            // 音乐用一整个 AudioStreamPlayer 换掉，淡出后直接销毁，不做暂停续播 ——
            // 两张卡互相顶替时更干净，也不会出现两条曲子叠在一起。
            Tween DonQuixote_Tween = DonQuixote_Player.CreateTween();
            DonQuixote_Tween.TweenProperty(DonQuixote_Player, "volume_db", -42.0f, DonQuixote_FadeOut);
            DonQuixote_Tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(DonQuixote_Player))
                {
                    DonQuixote_Player.Stop();
                    DonQuixote_Player.QueueFree();
                }
            }));
        }
    }

    private static void DonQuixote_PlayMusic(string DonQuixote_Stage, NCombatRoom DonQuixote_Room)
    {
        try
        {
            string DonQuixote_Path = DonQuixote_MusicPath(DonQuixote_Stage);
            if (string.IsNullOrEmpty(DonQuixote_Path))
            {
                return;
            }

            // 自定义曲子不进 Godot 的导入管线，直接读 PCK 里的原始 ogg 文件。
            AudioStreamOggVorbis? DonQuixote_Stream = DonQuixote_LoadOgg(DonQuixote_Path);
            if (DonQuixote_Stream == null)
            {
                DonQuixote_LastNote = "music missing";
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] stage music missing: {DonQuixote_Path}", 0);
                return;
            }

            DonQuixote_Stream.Loop = true;

            AudioStreamPlayer DonQuixote_Player = new AudioStreamPlayer
            {
                Name = DonQuixote_MusicName,
                Stream = DonQuixote_Stream,
                VolumeDb = DonQuixote_MusicVolumeDb(),
            };

            // 挂在房间根节点上：换房间时随场景树一起释放，不会留一条野曲子。
            DonQuixote_Room.AddChild(DonQuixote_Player);
            DonQuixote_Player.Play();
            DonQuixote_Music = DonQuixote_Player;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_LastNote = "music failed";
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage music failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    private static float DonQuixote_MusicVolumeDb()
    {
        float DonQuixote_Volume = DonQuixote_BgmVolume();
        return DonQuixote_Volume <= 0.001f ? -60f : Mathf.LinearToDb(DonQuixote_Volume);
    }

    private static float DonQuixote_BgmVolume()
    {
        try
        {
            return SaveManager.Instance.SettingsSave.VolumeBgm;
        }
        catch (Exception)
        {
            return 1f;
        }
    }

    /// <summary>把游戏自己的 BGM 压低到 0（不是停播）：还原时只要把音量写回去就行。</summary>
    private static void DonQuixote_Duck()
    {
        if (DonQuixote_Ducked)
        {
            return;
        }

        DonQuixote_SavedBgmVolume = DonQuixote_BgmVolume();
        try
        {
            NAudioManager.Instance?.SetBgmVol(0f);
            DonQuixote_Ducked = true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage bgm duck failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    private static void DonQuixote_Unduck()
    {
        if (!DonQuixote_Ducked)
        {
            return;
        }

        try
        {
            NAudioManager.Instance?.SetBgmVol(DonQuixote_SavedBgmVolume <= 0f ? 1f : DonQuixote_SavedBgmVolume);
        }
        catch (Exception)
        {
        }

        DonQuixote_Ducked = false;
    }

    private static string DonQuixote_BackgroundPath(string DonQuixote_Stage)
    {
        return DonQuixote_Stage == DonQuixote_StageNightmare
            ? "res://DonQuixote/images/stage/nightmare_land.png"
            : "res://DonQuixote/images/stage/happy_smile_land.png";
    }

    private static string DonQuixote_MusicPath(string DonQuixote_Stage)
    {
        return DonQuixote_Stage == DonQuixote_StageNightmare
            ? "res://DonQuixote/audio/hero_theme.ogg"
            : "res://DonQuixote/audio/lamancha_carnival.ogg";
    }

    // ------------------------------------------------------------------
    // watchdog：能力没被正常移除、或者换了房间时，兜底复原
    // ------------------------------------------------------------------

    private static void DonQuixote_StartWatchdog(Node? DonQuixote_Context)
    {
        if (DonQuixote_Watching)
        {
            return;
        }

        SceneTree? DonQuixote_Loop = DonQuixote_Context?.GetTree();
        if (DonQuixote_Loop == null)
        {
            try
            {
                DonQuixote_Loop = Engine.GetMainLoop() as SceneTree;
            }
            catch (Exception)
            {
            }
        }

        if (DonQuixote_Loop == null)
        {
            return;
        }

        DonQuixote_Tree = DonQuixote_Loop;
        DonQuixote_Watching = true;
        DonQuixote_Tick();
    }

    private static void DonQuixote_Tick()
    {
        if (!DonQuixote_Watching)
        {
            return;
        }

        try
        {
            if (NCombatRoom.Instance == null)
            {
                // 已经离开战斗房间：场景和音量都必须还回去。
                DonQuixote_Reset();
                return;
            }

            DonQuixote_Refresh();
            DonQuixote_EnsureOverlayOpaque();

            if (DonQuixote_Requests.Count == 0 && string.IsNullOrEmpty(DonQuixote_Shown))
            {
                DonQuixote_Watching = false;
                return;
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage watchdog failed: {DonQuixote_Exception.Message}", 0);
            DonQuixote_Watching = false;
            return;
        }

        try
        {
            SceneTreeTimer? DonQuixote_Timer = DonQuixote_Tree?.CreateTimer(DonQuixote_TickSeconds);
            if (DonQuixote_Timer == null)
            {
                DonQuixote_Watching = false;
                return;
            }

            DonQuixote_Timer.Timeout += DonQuixote_Tick;
        }
        catch (Exception)
        {
            DonQuixote_Watching = false;
        }
    }
}

/// <summary>
/// 遗物开局给一张「舞台卡」：从规范模型派生一张本局实例，塞进起手手牌。
/// </summary>
internal static class DonQuixoteStageCards
{
    /// <summary>
    /// 派生一张卡塞进手牌。<paramref name="DonQuixote_Upgrade"/> 为真时直接发升级版
    /// （遗物的「升级版」写法就是靠它）。返回值表示是否真的发到手，
    /// 调用方要按结果推进「已经发过几次」的计数，不要先记数后发货。
    /// </summary>
    public static async Task<bool> DonQuixote_Grant<T>(Player? DonQuixote_Player, bool DonQuixote_Upgrade = false)
        where T : CardModel
    {
        try
        {
            if (DonQuixote_Player?.Creature == null)
            {
                return false;
            }

            ICombatState DonQuixote_Combat = DonQuixote_Player.Creature.CombatState;
            if (DonQuixote_Combat is not ICardScope DonQuixote_Scope)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] stage card grant skipped: no card scope", 0);
                return false;
            }

            CardModel DonQuixote_Canonical = ModelDb.Card<T>();
            if (DonQuixote_Canonical == null)
            {
                return false;
            }

            CardModel DonQuixote_Card = DonQuixote_Scope.CreateCard(DonQuixote_Canonical, DonQuixote_Player);
            if (DonQuixote_Card == null)
            {
                return false;
            }

            if (DonQuixote_Upgrade)
            {
                // 和原版「直接给升级版」一样：先升，再收尾（Finalize 会清掉用于
                // 显示升级前后差异的临时状态）。
                DonQuixote_Card.UpgradeInternal();
                DonQuixote_Card.FinalizeUpgradeInternal();
            }

            await CardPileCmd.Add(DonQuixote_Card, PileType.Hand, CardPilePosition.Top, null, false);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] stage card granted: " + typeof(T).Name
                + (DonQuixote_Upgrade ? " (upgraded)" : string.Empty), 0);
            return true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] stage card grant failed: {DonQuixote_Exception.Message}", 0);
            return false;
        }
    }
}

/// <summary>
/// 血宴奖励：**消耗血宴总数**每满 325 点，发一张对应的舞台能力卡。
/// ・血魔的本能      → 幸福笑容如鲜花般绽放之地
/// ・血魔长老的本能  → 噩梦的拉曼却领（长老优先，两件都在只发长老那张）
/// 发放次数记在遗物自己的 [SavedProperty] 上：这是会被存档 + 联机同步的字段，
/// 不会出现「客机重开又发一遍」的情况。
/// </summary>
internal static class DonQuixoteFeastReward
{
    /// <summary>消耗血宴总数的发放门槛（遗物.docx：325 点）。</summary>
    public const int DonQuixote_Threshold = 325;

    public static async Task DonQuixote_Check(Creature DonQuixote_creature)
    {
        try
        {
            Player? DonQuixote_player = DonQuixote_creature?.Player;
            if (DonQuixote_player == null)
            {
                return;
            }

            Relics.DonQuixoteBloodElderInstinct? DonQuixote_elder = null;
            Relics.DonQuixoteBloodInstinct? DonQuixote_basic = null;

            foreach (RelicModel DonQuixote_relic in DonQuixote_player.Relics)
            {
                if (DonQuixote_relic is Relics.DonQuixoteBloodElderInstinct DonQuixote_foundElder)
                {
                    DonQuixote_elder = DonQuixote_foundElder;
                }
                else if (DonQuixote_relic is Relics.DonQuixoteBloodInstinct DonQuixote_foundBasic)
                {
                    DonQuixote_basic = DonQuixote_foundBasic;
                }
            }

            if (DonQuixote_elder == null && DonQuixote_basic == null)
            {
                return;
            }

            int DonQuixote_total = DonQuixoteBloodMagic.DonQuixote_FeastTotal(DonQuixote_creature);
            int DonQuixote_steps = DonQuixote_total / DonQuixote_Threshold;
            if (DonQuixote_steps <= 0)
            {
                return;
            }

            if (DonQuixote_elder != null)
            {
                // 长老优先：两件都在时按长老结算，普通那件不再单独发货。
                // 长老发的仍是「幸福笑容如鲜花般绽放之地」，只是升级版（不是噩梦的拉曼却领）。
                await DonQuixote_GrantUpTo(
                    DonQuixote_player,
                    DonQuixote_steps,
                    DonQuixote_elder.DonQuixote_FeastRewardSteps,
                    DonQuixote_value => DonQuixote_elder.DonQuixote_FeastRewardSteps = DonQuixote_value,
                    DonQuixote_total,
                    false,
                    true);
                return;
            }

            await DonQuixote_GrantUpTo(
                DonQuixote_player,
                DonQuixote_steps,
                DonQuixote_basic.DonQuixote_FeastRewardSteps,
                DonQuixote_value => DonQuixote_basic.DonQuixote_FeastRewardSteps = DonQuixote_value,
                DonQuixote_total,
                false,
                false);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] feast reward failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>
    /// 补发到第 steps 次。先发货、成功之后才把「已发几次」写进遗物 ——
    /// 反过来会在发货失败（手牌已满、不在战斗里等）时把次数白吃掉，卡就永远不发了。
    /// </summary>
    private static async Task DonQuixote_GrantUpTo(
        Player DonQuixote_player,
        int DonQuixote_steps,
        int DonQuixote_granted,
        Action<int> DonQuixote_write,
        int DonQuixote_total,
        bool DonQuixote_nightmare,
        bool DonQuixote_upgrade)
    {
        if (DonQuixote_steps <= DonQuixote_granted)
        {
            return;
        }

        // 一次最多补 6 张，异常数据（比如一次性灌进几百点）也不会把手牌刷爆。
        int DonQuixote_pending = DonQuixote_steps - DonQuixote_granted;
        if (DonQuixote_pending > 6)
        {
            DonQuixote_pending = 6;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] feast reward: total=" + DonQuixote_total
            + " steps=" + DonQuixote_steps
            + " granted=" + DonQuixote_granted
            + " pending=" + DonQuixote_pending
            + (DonQuixote_nightmare ? " card=nightmare" : " card=happy")
            + (DonQuixote_upgrade ? "+" : ""),
            0);

        for (int DonQuixote_index = 0; DonQuixote_index < DonQuixote_pending; DonQuixote_index++)
        {
            bool DonQuixote_ok = DonQuixote_nightmare
                ? await DonQuixoteStageCards.DonQuixote_Grant<Cards.DonQuixoteNightmareLand>(
                    DonQuixote_player, DonQuixote_upgrade)
                : await DonQuixoteStageCards.DonQuixote_Grant<Cards.DonQuixoteHappySmileLand>(
                    DonQuixote_player, DonQuixote_upgrade);

            if (!DonQuixote_ok)
            {
                // 这次没发出去：不推进计数，等下一次检查（回合开始 / 下次消耗）重试。
                break;
            }

            DonQuixote_granted++;
            DonQuixote_write(DonQuixote_granted);
        }
    }
}
