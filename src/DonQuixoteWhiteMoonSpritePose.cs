using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 白月骑士-巴里的「逐帧贴图」兜底实现（只在骨架加载失败时生效）。
/// </summary>
internal static class DonQuixoteWhiteMoonSpritePose
{
    private const string DonQuixote_IdlePath = "res://DonQuixote/images/creatures/barry_idle.png";

    private const string DonQuixote_GuardPath = "res://DonQuixote/images/creatures/barry_guard.png";

    private const string DonQuixote_HurtPath = "res://DonQuixote/images/creatures/barry_hurt.png";

    private const string DonQuixote_AtkFormat = "res://DonQuixote/images/creatures/barry_atk_f{0}.png";

    private const string DonQuixote_OathFormat = "res://DonQuixote/images/creatures/barry_oath_f{0}.png";

    /// <summary>
    /// 意图 1 / 4 的挥砍帧（4 帧）。
    /// </summary>
    private static readonly int[] DonQuixote_AtkMs = { 200, 160, 160, 520 };

    /// <summary>
    /// 意图 3 的帧时长，直接照抄 GIF：200,160x5,200,160x3 | 160,160,200,160,160,560。
    /// </summary>
    private static readonly int[] DonQuixote_OathMs =
    {
        200, 160, 160, 160, 160, 160, 200, 160, 160, 160,
        160, 160, 200, 160, 160, 560,
    };

    /// <summary>
    /// 拉弓瞄准是第 10 帧之前的最后一帧（0..9 播放完停在这里）。
    /// </summary>
    public const int DonQuixote_OathWindupFrames = 10;

    /// <summary>
    /// 停住不动的那一帧：拉满弓、瞄准。
    /// </summary>
    private static readonly string DonQuixote_AimPath = string.Format(DonQuixote_OathFormat, 9);

    /// <summary>
    /// 受伤 / 防御是单帧，短暂停留后回闲置。
    /// </summary>
    private const double DonQuixote_HurtHold = 0.45;

    private const double DonQuixote_GuardHold = 0.5;

    /// <summary>
    /// 诊断日志开关：排查「姿态没换上去」时临时打开。
    /// </summary>
    private static readonly bool DonQuixote_Debug = true;

    private static readonly Dictionary<ulong, int> DonQuixote_Token = new Dictionary<ulong, int>();

    /// <summary>
    /// 每个贴图节点当前显示的是哪张图（用来判断「现在是不是端着弓」）。
    /// </summary>
    private static readonly Dictionary<ulong, string> DonQuixote_CurrentPath = new Dictionary<ulong, string>();

    /// <summary>
    /// 已经为这次 OATH 意图起过手了，避免意图刷新时重复播起手。
    /// </summary>
    private static readonly HashSet<ulong> DonQuixote_AimArmed = new HashSet<ulong>();

    /// <summary>
    /// 本轮「意图3」是否已经完整起手过（离开 OATH 时清掉）。
    /// </summary>
    private static readonly HashSet<ulong> DonQuixote_OathCycle = new HashSet<ulong>();
    private static readonly Dictionary<string, Texture2D> DonQuixote_Cache = new Dictionary<string, Texture2D>();

    private static int DonQuixote_Serial;

    /// <summary>
    /// 换回闲置贴图（不打断正在播放的序列）。
    /// </summary>
    public static void DonQuixote_PlayIdle(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return;
        }

        DonQuixote_Bump(DonQuixote_Sprite);
        DonQuixote_AimArmed.Remove(DonQuixote_Sprite.GetInstanceId());
        DonQuixote_OathCycle.Remove(DonQuixote_Sprite.GetInstanceId());
        DonQuixote_SetTexture(DonQuixote_Sprite, DonQuixote_IdlePath);
    }

    /// <summary>
    /// 意图出现时起手：把 0..9 帧放一遍。
    /// </summary>
    public static void DonQuixote_HoldBowAim(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Sprite.GetInstanceId();
        if (!DonQuixote_OathCycle.Add(DonQuixote_Key))
        {
            // 这一轮已经起过手了：还端着就什么都不做，被打断过就直接端回来。
            if (!DonQuixote_IsAiming(DonQuixote_Creature))
            {
                DonQuixote_SnapBowAim(DonQuixote_Creature);
            }

            return;
        }

        DonQuixote_AimArmed.Add(DonQuixote_Key);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] white moon oath intent shown -> drawing bow", 0);
        _ = DonQuixote_RunAsync(DonQuixote_Creature, DonQuixote_OathFormat, 0,
            DonQuixote_OathWindupFrames, DonQuixote_OathMs, false);
    }

    /// <summary>
    /// 离开「意图3」这个循环：下一个 OATH 会重新完整起手一次。
    /// </summary>
    public static void DonQuixote_ResetOathCycle(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite != null)
        {
            DonQuixote_OathCycle.Remove(DonQuixote_Sprite.GetInstanceId());
        }
    }

    /// <summary>
    /// 兜底：直接跳到「拉弓瞄准」那一帧并端起弓（不重播 1.7 秒的起手）。
    /// </summary>
    public static void DonQuixote_SnapBowAim(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return;
        }

        DonQuixote_Bump(DonQuixote_Sprite);
        DonQuixote_AimArmed.Add(DonQuixote_Sprite.GetInstanceId());
        DonQuixote_SetTexture(DonQuixote_Sprite, DonQuixote_AimPath);
    }

    /// <summary>
    /// 现在是不是「端着弓」的状态（起手途中或已停在瞄准帧）。
    /// </summary>
    public static bool DonQuixote_IsAiming(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return false;
        }

        ulong DonQuixote_Key = DonQuixote_Sprite.GetInstanceId();
        if (DonQuixote_AimArmed.Contains(DonQuixote_Key))
        {
            return true;
        }

        return DonQuixote_CurrentPath.TryGetValue(DonQuixote_Key, out string DonQuixote_Path)
            && DonQuixote_Path == DonQuixote_AimPath;
    }

    /// <summary>
    /// 受到伤害且真的掉了血：换成「受伤」贴图。
    /// </summary>
    public static void DonQuixote_PlayHurt(Creature DonQuixote_Creature)
    {
        DonQuixote_PlayStill(DonQuixote_Creature, DonQuixote_HurtPath, DonQuixote_HurtHold);
    }

    /// <summary>
    /// 受到伤害但被格挡吃干净：换成「防御」贴图。
    /// </summary>
    public static void DonQuixote_PlayGuard(Creature DonQuixote_Creature)
    {
        DonQuixote_PlayStill(DonQuixote_Creature, DonQuixote_GuardPath, DonQuixote_GuardHold);
    }

    /// <summary>
    /// 意图 1 / 4：整套挥砍帧，播完回闲置。
    /// </summary>
    public static void DonQuixote_PlayAttack(Creature DonQuixote_Creature)
    {
        _ = DonQuixote_RunAsync(DonQuixote_Creature, DonQuixote_AtkFormat, 0, DonQuixote_AtkMs.Length,
            DonQuixote_AtkMs, true);
    }

    /// <summary>
    /// 意图 3 后半段：松弦→收势，播完回闲置。
    /// </summary>
    public static Task DonQuixote_PlayBowRelease(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite != null)
        {
            ulong DonQuixote_Key = DonQuixote_Sprite.GetInstanceId();
            DonQuixote_AimArmed.Remove(DonQuixote_Key);
            DonQuixote_OathCycle.Remove(DonQuixote_Key);
        }

        return DonQuixote_RunAsync(DonQuixote_Creature, DonQuixote_OathFormat, DonQuixote_OathWindupFrames,
            DonQuixote_OathMs.Length - DonQuixote_OathWindupFrames, DonQuixote_OathMs, true);
    }

    /// <summary>
    /// 死亡时清掉残留的帧序列（死亡淡出由原版负责）。
    /// </summary>
    public static void DonQuixote_Stop(Creature DonQuixote_Creature)
    {
        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite != null)
        {
            DonQuixote_AimArmed.Remove(DonQuixote_Sprite.GetInstanceId());
            DonQuixote_Bump(DonQuixote_Sprite);
        }
    }

    private static void DonQuixote_PlayStill(Creature DonQuixote_Creature, string DonQuixote_Path, double DonQuixote_Hold)
    {
        // 兜底：AfterAddedToRoom 里战斗房间可能还没完全就绪，第一次动作时必定在了。
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Show();

        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return;
        }

        if (DonQuixote_Debug)
        {
            DonQuixote_Note("still " + DonQuixote_ShortName(DonQuixote_Path)
                + " hold=" + DonQuixote_Hold.ToString("0.00") + "s sprite=" + DonQuixote_Sprite.GetInstanceId());
        }

        int DonQuixote_Mine = DonQuixote_Bump(DonQuixote_Sprite);
        // 挨打会打断拉弓姿态，下一次 OATH 再重新起手。
        DonQuixote_AimArmed.Remove(DonQuixote_Sprite.GetInstanceId());
        DonQuixote_SetTexture(DonQuixote_Sprite, DonQuixote_Path);
        _ = DonQuixote_RestoreIdleLater(DonQuixote_Creature, DonQuixote_Sprite, DonQuixote_Mine, DonQuixote_Hold);
    }

    /// <summary>
    /// 单帧姿态停留结束：回闲置。
    /// </summary>
    private static async Task DonQuixote_RestoreIdleLater(
        Creature DonQuixote_Creature, Sprite2D DonQuixote_Sprite, int DonQuixote_Mine, double DonQuixote_Delay)
    {
        try
        {
            SceneTree? DonQuixote_Tree = DonQuixote_Sprite.GetTree();
            if (DonQuixote_Tree == null)
            {
                return;
            }

            await DonQuixote_Sprite.ToSignal(DonQuixote_Tree.CreateTimer(DonQuixote_Delay), SceneTreeTimer.SignalName.Timeout);

            if (!GodotObject.IsInstanceValid(DonQuixote_Sprite) || !DonQuixote_IsCurrent(DonQuixote_Sprite, DonQuixote_Mine))
            {
                return;
            }

            if (DonQuixote_IsOathPending(DonQuixote_Creature)
                && DonQuixote_OathCycle.Contains(DonQuixote_Sprite.GetInstanceId()))
            {
                DonQuixote_SnapBowAim(DonQuixote_Creature);
                return;
            }

            DonQuixote_SetTexture(DonQuixote_Sprite, DonQuixote_IdlePath);
        }
        catch (Exception)
        {
            // 节点被销毁时 ToSignal 会抛，忽略。
        }
    }

    /// <summary>
    /// 这一手是不是「意图3」，且还没结算完。
    /// </summary>
    private static bool DonQuixote_IsOathPending(Creature DonQuixote_Creature)
    {
        return DonQuixote_Creature?.Monster is Monsters.DonQuixoteWhiteMoonKnight DonQuixote_Knight
            && DonQuixote_Knight.NextMove?.StateId == "WHITE_MOON_OATH";
    }

    /// <summary>
    /// 逐帧播放。返回的 Task 在整段播完（或被打断）时完成。
    /// </summary>
    private static async Task DonQuixote_RunAsync(
        Creature DonQuixote_Creature,
        string DonQuixote_Format,
        int DonQuixote_Start,
        int DonQuixote_Count,
        int[] DonQuixote_Timing,
        bool DonQuixote_RestoreIdleAtEnd)
    {
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Show();

        Sprite2D? DonQuixote_Sprite = DonQuixote_FindSprite(DonQuixote_Creature);
        if (DonQuixote_Sprite == null)
        {
            return;
        }

        int DonQuixote_Mine = DonQuixote_Bump(DonQuixote_Sprite);

        for (int DonQuixote_I = 0; DonQuixote_I < DonQuixote_Count; DonQuixote_I++)
        {
            int DonQuixote_Index = DonQuixote_Start + DonQuixote_I;
            if (!GodotObject.IsInstanceValid(DonQuixote_Sprite)
                || !DonQuixote_IsCurrent(DonQuixote_Sprite, DonQuixote_Mine))
            {
                return;
            }

            DonQuixote_SetTexture(DonQuixote_Sprite, string.Format(DonQuixote_Format, DonQuixote_Index));

            try
            {
                SceneTree? DonQuixote_Tree = DonQuixote_Sprite.GetTree();
                if (DonQuixote_Tree == null)
                {
                    return;
                }

                double DonQuixote_Ms = DonQuixote_Index < DonQuixote_Timing.Length
                    ? DonQuixote_Timing[DonQuixote_Index]
                    : 160;
                await DonQuixote_Sprite.ToSignal(
                    DonQuixote_Tree.CreateTimer(DonQuixote_Ms / 1000.0), SceneTreeTimer.SignalName.Timeout);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (DonQuixote_RestoreIdleAtEnd
            && GodotObject.IsInstanceValid(DonQuixote_Sprite)
            && DonQuixote_IsCurrent(DonQuixote_Sprite, DonQuixote_Mine))
        {
            DonQuixote_SetTexture(DonQuixote_Sprite, DonQuixote_IdlePath);
        }
    }

    private static int DonQuixote_Bump(Sprite2D DonQuixote_Sprite)
    {
        DonQuixote_Serial++;
        DonQuixote_Token[DonQuixote_Sprite.GetInstanceId()] = DonQuixote_Serial;
        return DonQuixote_Serial;
    }

    private static bool DonQuixote_IsCurrent(Sprite2D DonQuixote_Sprite, int DonQuixote_Mine)
    {
        return DonQuixote_Token.TryGetValue(DonQuixote_Sprite.GetInstanceId(), out int DonQuixote_Current)
            && DonQuixote_Current == DonQuixote_Mine;
    }

    private static void DonQuixote_SetTexture(Sprite2D DonQuixote_Sprite, string DonQuixote_Path)
    {
        if (!GodotObject.IsInstanceValid(DonQuixote_Sprite))
        {
            return;
        }

        Texture2D? DonQuixote_Texture = DonQuixote_Load(DonQuixote_Path);
        if (DonQuixote_Texture == null)
        {
            DonQuixote_Note("frame load failed: " + DonQuixote_Path);
            return;
        }

        DonQuixote_Sprite.Texture = DonQuixote_Texture;
        // 换完再读回来，确认引擎那边真的接收了（排查「设了但没显示」）。
        bool DonQuixote_Applied = ReferenceEquals(
            DonQuixote_Sprite.Texture?.ResourcePath, DonQuixote_Texture.ResourcePath)
            || DonQuixote_Sprite.Texture == DonQuixote_Texture;
        DonQuixote_CurrentPath[DonQuixote_Sprite.GetInstanceId()] = DonQuixote_Path;
        if (DonQuixote_Debug)
        {
            DonQuixote_Note("pose -> " + DonQuixote_ShortName(DonQuixote_Path)
                + (DonQuixote_Applied ? "" : " (NOT APPLIED)"));
        }
    }

    private static string DonQuixote_ShortName(string DonQuixote_Path)
    {
        int DonQuixote_Slash = DonQuixote_Path.LastIndexOf('/');
        return DonQuixote_Slash >= 0 ? DonQuixote_Path.Substring(DonQuixote_Slash + 1) : DonQuixote_Path;
    }

    private static void DonQuixote_Note(string DonQuixote_Message)
    {
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] white moon " + DonQuixote_Message, 0);
    }

    private static Texture2D? DonQuixote_Load(string DonQuixote_Path)
    {
        if (DonQuixote_Cache.TryGetValue(DonQuixote_Path, out Texture2D? DonQuixote_Hit))
        {
            return DonQuixote_Hit;
        }

        try
        {
            if (!ResourceLoader.Exists(DonQuixote_Path))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] white moon frame missing: " + DonQuixote_Path, 0);
                DonQuixote_Cache[DonQuixote_Path] = null;
                return null;
            }

            Texture2D? DonQuixote_Texture = ResourceLoader.Load<Texture2D>(
                DonQuixote_Path, null, ResourceLoader.CacheMode.Reuse);
            DonQuixote_Cache[DonQuixote_Path] = DonQuixote_Texture;
            return DonQuixote_Texture;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] white moon frame load failed: " + DonQuixote_Exception.Message, 0);
            return null;
        }
    }

    private static Sprite2D? DonQuixote_FindSprite(Creature DonQuixote_Creature)
    {
        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        if (DonQuixote_Room == null || DonQuixote_Creature == null)
        {
            DonQuixote_Note("sprite lookup skipped: no combat room");
            return null;
        }

        NCreature? DonQuixote_Node = DonQuixote_Room.GetCreatureNode(DonQuixote_Creature);
        if (DonQuixote_Node == null)
        {
            foreach (NCreature DonQuixote_Candidate in DonQuixote_Room.CreatureNodes)
            {
                if (ReferenceEquals(DonQuixote_Candidate.Entity, DonQuixote_Creature))
                {
                    DonQuixote_Node = DonQuixote_Candidate;
                    break;
                }
            }
        }

        if (DonQuixote_Node == null)
        {
            DonQuixote_Note("sprite lookup failed: creature node not in room");
            return null;
        }

        Node2D? DonQuixote_Body = DonQuixote_Node.Visuals?.Body;
        if (DonQuixote_Body is Sprite2D DonQuixote_Sprite && GodotObject.IsInstanceValid(DonQuixote_Sprite))
        {
            return DonQuixote_Sprite;
        }

        // 兜底：%Visuals 万一不是 Sprite2D（手改场景出错时）。
        Sprite2D? DonQuixote_Found = DonQuixote_FindSpriteDeep(DonQuixote_Node);
        DonQuixote_Note(DonQuixote_Found == null
            ? "sprite lookup failed: body=" + (DonQuixote_Body?.GetType().Name ?? "null")
            : "sprite lookup fallback: body=" + (DonQuixote_Body?.GetType().Name ?? "null"));
        return DonQuixote_Found;
    }

    private static Sprite2D? DonQuixote_FindSpriteDeep(Node DonQuixote_Root)
    {
        foreach (Node DonQuixote_Child in DonQuixote_Root.GetChildren())
        {
            if (DonQuixote_Child is Sprite2D DonQuixote_Sprite && GodotObject.IsInstanceValid(DonQuixote_Sprite))
            {
                return DonQuixote_Sprite;
            }

            Sprite2D? DonQuixote_Nested = DonQuixote_FindSpriteDeep(DonQuixote_Child);
            if (DonQuixote_Nested != null)
            {
                return DonQuixote_Nested;
            }
        }

        return null;
    }
}
