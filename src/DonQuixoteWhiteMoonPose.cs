using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 白月骑士-巴里的表现层（spine 版）。
///
/// 骨架由 <c>tools/barry_spine.py</c> 从 <c>assets/art/barry_*.png</c> 生成，
/// 每个姿势是一个 region 附件，动画名和原版怪物状态机对齐：
/// <c>idle_loop / attack / hurt / cast / die</c>，另加意图3 用的
/// <c>bow_windup / bow_release</c>。
///
/// 为什么要骨架
/// ------------
/// 原版 <c>NCreature</c> 只在 <c>%Visuals</c> 是 SpineSprite 时才建 <c>CreatureAnimator</c>，
/// 也才会在死亡时 <c>SetAnimationTrigger("Dead")</c>。以前是静态 Sprite2D，
/// 死亡只能靠代码硬换一张 png，尺寸/锚点全得自己算；现在是骨架里的 <c>die</c> 动画，
/// 原版自己播，死亡图 = 那滩血。
///
/// 轨道约定
/// --------
/// ・track 0：原版状态机（我们 override 了 GenerateAnimator，平时只有 idle_loop 和 die）；
/// ・track 1：这个类控制的姿势。所有姿势动画播完都会停在「该停的帧」
///   （hurt/guard/attack/release 末尾是 idle，windup 末尾是拉满弓），
///   所以不需要清轨；只有死亡和回闲置时才把 track 1 清空，让 track 0 的 die / idle_loop 露出。
///
/// 意图3 期间（<c>NextMove</c> 是 WHITE_MOON_OATH）刻意不播受伤/防御：
/// 拉弓是一段 1.7 秒的起手，被打一下就换图会看起来像「动作中断」，
/// 而这一手的表现要求是完整拉满弓再松弦。
/// </summary>
internal static class DonQuixoteWhiteMoonPose
{
    public const string DonQuixote_AnimIdle = "idle_loop";

    public const string DonQuixote_AnimAttack = "attack";

    public const string DonQuixote_AnimHurt = "hurt";

    public const string DonQuixote_AnimDefend = "defend";

    public const string DonQuixote_AnimWindup = "bow_windup";

    public const string DonQuixote_AnimRelease = "bow_release";

    /// <summary>姿势轨道：0 留给原版状态机。</summary>
    private const int DonQuixote_Track = 1;

    /// <summary>松弦动画长度（bow_release 1.40s）+ 收尾余量。</summary>
    private const double DonQuixote_ReleaseWait = 1.45;

    /// <summary>和 <c>DonQuixoteWhiteMoonKnight.DonQuixote_MoveOath</c> 保持一致。</summary>
    private const string DonQuixote_OathStateId = "WHITE_MOON_OATH";

    private static readonly bool DonQuixote_Debug = true;

    /// <summary>现在端着弓的节点（拉弓起手没播完或已停在瞄准帧）。</summary>
    private static readonly HashSet<ulong> DonQuixote_Aiming = new HashSet<ulong>();

    /// <summary>这一轮意图3 已经完整起手过，避免意图刷新时重播。</summary>
    private static readonly HashSet<ulong> DonQuixote_OathCycle = new HashSet<ulong>();

    /// <summary>动画名缺失只报一次，免得每帧刷日志。</summary>
    private static readonly HashSet<string> DonQuixote_Missing = new HashSet<string>();

    // ---- 对外接口（签名和贴图版一致，调用方不用改） --------------------

    /// <summary>回闲置：清掉姿势轨，让 track 0 的 idle_loop 接管。</summary>
    public static void DonQuixote_PlayIdle(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_PlayIdle(DonQuixote_Creature);
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Node.GetInstanceId();
        DonQuixote_Aiming.Remove(DonQuixote_Key);
        DonQuixote_OathCycle.Remove(DonQuixote_Key);
        DonQuixote_ClearTrack(DonQuixote_Node);
    }

    /// <summary>
    /// 意图3 出现时起手：播一次 1.68 秒的拉弓，停在拉满弓那一帧，
    /// 一直保持到伤害结算完，再由 <see cref="DonQuixote_PlayBowRelease"/> 松弦。
    /// 同一轮只完整起手一次。
    /// </summary>
    public static void DonQuixote_HoldBowAim(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_HoldBowAim(DonQuixote_Creature);
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Node.GetInstanceId();
        if (!DonQuixote_OathCycle.Add(DonQuixote_Key))
        {
            if (!DonQuixote_IsAiming(DonQuixote_Creature))
            {
                DonQuixote_SnapBowAim(DonQuixote_Creature);
            }

            return;
        }

        DonQuixote_Aiming.Add(DonQuixote_Key);
        DonQuixote_PlayAnim(DonQuixote_Node, DonQuixote_AnimWindup, false);
        DonQuixote_Note("oath intent shown -> drawing bow");
    }

    /// <summary>离开意图3 这个循环：下一个 OATH 会重新完整起手一次。</summary>
    public static void DonQuixote_ResetOathCycle(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_ResetOathCycle(DonQuixote_Creature);
            return;
        }

        DonQuixote_OathCycle.Remove(DonQuixote_Node.GetInstanceId());
    }

    /// <summary>
    /// 兜底：直接把拉弓轨推到最后一帧（不重播 1.68 秒的起手）。
    /// 用在意图刷新没赶上（刚进场）或者动作被打断之后。
    /// </summary>
    public static void DonQuixote_SnapBowAim(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_SnapBowAim(DonQuixote_Creature);
            return;
        }

        DonQuixote_Aiming.Add(DonQuixote_Node.GetInstanceId());
        DonQuixote_PlayAnim(DonQuixote_Node, DonQuixote_AnimWindup, false);

        try
        {
            MegaAnimationState? DonQuixote_State = DonQuixote_Node.SpineAnimation.GetAnimationState();
            MegaTrackEntry? DonQuixote_Entry = DonQuixote_State?.GetCurrent(DonQuixote_Track);
            if (DonQuixote_Entry != null)
            {
                DonQuixote_Entry.SetTrackTime(DonQuixote_Entry.GetAnimationEnd());
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_Note("snap bow aim failed: " + DonQuixote_Exception.Message);
        }
    }

    /// <summary>现在是不是「端着弓」（起手途中或已停在瞄准帧）。</summary>
    public static bool DonQuixote_IsAiming(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            return DonQuixoteWhiteMoonSpritePose.DonQuixote_IsAiming(DonQuixote_Creature);
        }

        if (DonQuixote_Aiming.Contains(DonQuixote_Node.GetInstanceId()))
        {
            return true;
        }

        try
        {
            string? DonQuixote_Current =
                DonQuixote_Node.SpineAnimation.GetCurrentAnimationName(DonQuixote_Track);
            return string.Equals(DonQuixote_Current, DonQuixote_AnimWindup, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>受到伤害且真的掉了血：换成「受伤」动画（意图3 期间跳过）。</summary>
    public static void DonQuixote_PlayHurt(Creature DonQuixote_Creature)
    {
        DonQuixote_PlayReaction(DonQuixote_Creature, DonQuixote_AnimHurt, "hurt");
    }

    /// <summary>受到伤害但被格挡吃干净：换成「防御」动画（意图3 期间跳过）。</summary>
    public static void DonQuixote_PlayGuard(Creature DonQuixote_Creature)
    {
        DonQuixote_PlayReaction(DonQuixote_Creature, DonQuixote_AnimDefend, "guard");
    }

    /// <summary>意图 1 / 4：整套挥砍，播完回闲置。</summary>
    public static void DonQuixote_PlayAttack(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_PlayAttack(DonQuixote_Creature);
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Node.GetInstanceId();
        DonQuixote_Aiming.Remove(DonQuixote_Key);
        DonQuixote_OathCycle.Remove(DonQuixote_Key);
        DonQuixote_PlayAnim(DonQuixote_Node, DonQuixote_AnimAttack, false);
    }

    /// <summary>意图 3 后半段：松弦→收势，播完回闲置。</summary>
    public static async System.Threading.Tasks.Task DonQuixote_PlayBowRelease(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            await DonQuixoteWhiteMoonSpritePose.DonQuixote_PlayBowRelease(DonQuixote_Creature);
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Node.GetInstanceId();
        DonQuixote_Aiming.Remove(DonQuixote_Key);
        DonQuixote_OathCycle.Remove(DonQuixote_Key);
        DonQuixote_PlayAnim(DonQuixote_Node, DonQuixote_AnimRelease, false);

        // 等动画放完再让状态机继续：松弦 1.40 秒，多留 50ms 收尾。
        try
        {
            SceneTree? DonQuixote_Tree = DonQuixote_Node.GetTree();
            if (DonQuixote_Tree != null)
            {
                await DonQuixote_Node.ToSignal(
                    DonQuixote_Tree.CreateTimer(DonQuixote_ReleaseWait), SceneTreeTimer.SignalName.Timeout);
            }
        }
        catch (Exception)
        {
            // 节点被销毁时 ToSignal 会抛，忽略。
        }
    }

    /// <summary>
    /// 死亡瞬间：把姿势轨清空，让 track 0 的 <c>die</c> 动画（那滩血）显示出来。
    /// 由 <see cref="DonQuixoteWhiteMoonDeathPosePatch"/> 在 <c>StartDeathAnim</c> 之前调。
    /// </summary>
    public static void DonQuixote_Stop(Creature DonQuixote_Creature)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            DonQuixoteWhiteMoonSpritePose.DonQuixote_Stop(DonQuixote_Creature);
            return;
        }

        ulong DonQuixote_Key = DonQuixote_Node.GetInstanceId();
        DonQuixote_Aiming.Remove(DonQuixote_Key);
        DonQuixote_OathCycle.Remove(DonQuixote_Key);
        DonQuixote_ClearTrack(DonQuixote_Node);
    }

    // ---- 内部 ----------------------------------------------------------

    /// <summary>受伤 / 防御共用的「反应」流程：先挡意图3，再换图。</summary>
    private static void DonQuixote_PlayReaction(
        Creature DonQuixote_Creature, string DonQuixote_Anim, string DonQuixote_Label)
    {
        if (!DonQuixote_TryNode(DonQuixote_Creature, out NCreature DonQuixote_Node))
        {
            if (DonQuixote_Label == "hurt")
            {
                DonQuixoteWhiteMoonSpritePose.DonQuixote_PlayHurt(DonQuixote_Creature);
            }
            else
            {
                DonQuixoteWhiteMoonSpritePose.DonQuixote_PlayGuard(DonQuixote_Creature);
            }

            return;
        }

        // 兜底：战斗房间刚建好时背景可能还没挂上（老逻辑留下的习惯）。
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Show();

        // 意图3 的动作是完整的一套拉弓：这一手期间挨打也不换图，
        // 否则 1.68 秒的起手会被一张受伤图切成两段。
        if (DonQuixote_OathInProgress(DonQuixote_Creature))
        {
            if (DonQuixote_Debug)
            {
                DonQuixote_Note(DonQuixote_Label + " ignored (oath in progress)");
            }

            return;
        }

        DonQuixote_PlayAnim(DonQuixote_Node, DonQuixote_Anim, false);
        if (DonQuixote_Debug)
        {
            DonQuixote_Note(DonQuixote_Label + " -> " + DonQuixote_Anim);
        }
    }

    /// <summary>下一手是不是「意图3」。意图3 期间不播受伤/防御。</summary>
    private static bool DonQuixote_OathInProgress(Creature DonQuixote_Creature)
    {
        return DonQuixote_Creature?.Monster is Monsters.DonQuixoteWhiteMoonKnight DonQuixote_Knight
            && DonQuixote_Knight.NextMove?.StateId == DonQuixote_OathStateId;
    }

    private static void DonQuixote_PlayAnim(NCreature DonQuixote_Node, string DonQuixote_Anim, bool DonQuixote_Loop)
    {
        try
        {
            DonQuixote_Node.SpineAnimation.SetAnimation(DonQuixote_Anim, DonQuixote_Loop, DonQuixote_Track);

            string? DonQuixote_Current =
                DonQuixote_Node.SpineAnimation.GetCurrentAnimationName(DonQuixote_Track);
            if (!string.Equals(DonQuixote_Current, DonQuixote_Anim, StringComparison.OrdinalIgnoreCase)
                && DonQuixote_Missing.Add(DonQuixote_Anim))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] barry spine has no '{DonQuixote_Anim}' animation "
                    + $"(current: '{DonQuixote_Current}'), check tools/barry_spine.py", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_Note("play '" + DonQuixote_Anim + "' failed: " + DonQuixote_Exception.Message);
        }
    }

    /// <summary>清空姿势轨：track 0 的 idle_loop / die 重新露出。</summary>
    private static void DonQuixote_ClearTrack(NCreature DonQuixote_Node)
    {
        try
        {
            DonQuixote_Node.SpineAnimation.GetAnimationState()?.AddEmptyAnimation(DonQuixote_Track);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_Note("clear pose track failed: " + DonQuixote_Exception.Message);
        }
    }

    /// <summary>取巴里的 <see cref="NCreature"/>；不是骨架版（或节点没建好）返回 false。</summary>
    private static bool DonQuixote_TryNode(Creature DonQuixote_Creature, out NCreature DonQuixote_Node)
    {
        DonQuixote_Node = DonQuixote_Find(DonQuixote_Creature);
        if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
        {
            return false;
        }

        try
        {
            return DonQuixote_Node.HasSpineAnimation;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static NCreature? DonQuixote_Find(Creature DonQuixote_Creature)
    {
        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        if (DonQuixote_Room == null || DonQuixote_Creature == null)
        {
            return null;
        }

        NCreature? DonQuixote_Node = DonQuixote_Room.GetCreatureNode(DonQuixote_Creature);
        if (DonQuixote_Node != null)
        {
            return DonQuixote_Node;
        }

        foreach (NCreature DonQuixote_Candidate in DonQuixote_Room.CreatureNodes)
        {
            if (ReferenceEquals(DonQuixote_Candidate.Entity, DonQuixote_Creature))
            {
                return DonQuixote_Candidate;
            }
        }

        return null;
    }

    private static void DonQuixote_Note(string DonQuixote_Message)
    {
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] white moon " + DonQuixote_Message, 0);
    }
}

/// <summary>
/// 死亡前把姿势轨清掉：原版 <c>StartDeathAnim</c> 会在 track 0 触发 "Dead"，
/// 但 track 1 上还端着上一手的姿势（权重 1，盖在上面），死亡图就看不见了。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class DonQuixoteWhiteMoonDeathPosePatch
{
    private static void Prefix(NCreature __instance)
    {
        try
        {
            Creature? DonQuixote_Entity = __instance?.Entity;
            if (DonQuixote_Entity?.Monster is Monsters.DonQuixoteWhiteMoonKnight)
            {
                DonQuixoteWhiteMoonPose.DonQuixote_Stop(DonQuixote_Entity);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] white moon death pose failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}
