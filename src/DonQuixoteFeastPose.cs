using System;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DonQuixote;

/// <summary>
/// 「血宴时刻」打出时的演出：播一次 feast_moment（16 帧血光，约 1.45s），播完把姿势轨清空回 idle。
/// 姿势轨用 1，轨道 0 留给角色状态机，所以不会打断受击/死亡。
/// 所有 spine 调用都先过 IsAnimationStateReady()/TryGetAnimationState() 并单独 try/catch：
/// 骨架没准备好（战斗刚开场、形态切换、节点已释放）时静默跳过，绝不让异常漏进出牌流程。
/// </summary>
internal static class DonQuixoteFeastPose
{
    /// <summary>
    /// 血宴时刻演出动画名（骨架 don_quixote.spjson 里的 feast_moment）。
    /// </summary>
    private const string DonQuixote_Anim = "feast_moment";

    /// <summary>
    /// 演出结束后兜底用的基础待机动画（轨道 0 上没有动画时补上，避免停在 setup pose）。
    /// </summary>
    private const string DonQuixote_IdleAnim = "idle_loop";

    /// <summary>
    /// 专用轨道：0 留给原版状态机。
    /// </summary>
    private const int DonQuixote_Track = 1;

    /// <summary>
    /// 基础轨道（原版状态机用的那条）。
    /// </summary>
    private const int DonQuixote_BaseTrack = 0;

    /// <summary>
    /// 16 帧 × 0.09s = 1.44s，动画内部首尾各有 0.08s 淡入淡出，末尾留一点给最后一帧显示。
    /// </summary>
    private const double DonQuixote_Length = 1.50;

    private const double DonQuixote_ClearAt = DonQuixote_Length + 0.06;

    private const string DonQuixote_MetaKey = "DonQuixoteFeastPoseSeq";

    private static bool DonQuixote_AnimMissing;

    /// <summary>
    /// 打出「血宴时刻」时调用。
    /// </summary>
    public static void DonQuixote_Play(Creature DonQuixote_Creature)
    {
        try
        {
            if (DonQuixote_Creature == null || DonQuixote_AnimMissing)
            {
                return;
            }

            NCreature? DonQuixote_Node = DonQuixote_Creature.GetCreatureNode();
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            if (!DonQuixote_Node.HasSpineAnimation)
            {
                return;
            }

            MegaSprite? DonQuixote_Sprite = DonQuixote_Node.Visuals?.SpineBody;
            if (DonQuixote_Sprite == null || !DonQuixote_Sprite.IsAnimationStateReady())
            {
                // 骨架还没初始化完（战斗首帧、形态切换中）：放过这次演出，绝不抛。
                return;
            }

            MegaAnimationState? DonQuixote_State = DonQuixote_Sprite.TryGetAnimationState();
            if (DonQuixote_State == null)
            {
                return;
            }

            DonQuixote_State.SetAnimation(DonQuixote_Anim, false, DonQuixote_Track);

            string? DonQuixote_Current = DonQuixote_State.GetCurrentAnimationName(DonQuixote_Track);
            if (!string.Equals(DonQuixote_Current, DonQuixote_Anim, StringComparison.OrdinalIgnoreCase))
            {
                DonQuixote_AnimMissing = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] spine has no '{DonQuixote_Anim}' animation (current: '{DonQuixote_Current}'), feast pose disabled", 0);
                return;
            }

            long DonQuixote_Seq = DonQuixote_Node.GetMeta(DonQuixote_MetaKey, 0).AsInt64() + 1;
            DonQuixote_Node.SetMeta(DonQuixote_MetaKey, DonQuixote_Seq);

            SceneTree? DonQuixote_Tree = DonQuixote_Node.GetTree();
            if (DonQuixote_Tree == null)
            {
                return;
            }

            SceneTreeTimer DonQuixote_Timer = DonQuixote_Tree.CreateTimer(DonQuixote_ClearAt);
            DonQuixote_Timer.Timeout += () =>
            {
                try
                {
                    if (!GodotObject.IsInstanceValid(DonQuixote_Node))
                    {
                        return;
                    }

                    if (!GodotObject.IsInstanceValid(DonQuixote_Sprite.BoundObject))
                    {
                        return;
                    }

                    if (DonQuixote_Node.GetMeta(DonQuixote_MetaKey, 0).AsInt64() != DonQuixote_Seq)
                    {
                        return;
                    }

                    MegaAnimationState? DonQuixote_EndState = DonQuixote_Sprite.TryGetAnimationState();
                    if (DonQuixote_EndState == null)
                    {
                        return;
                    }

                    // 清掉姿势轨，槽色/附件回到 setup pose。
                    DonQuixote_EndState.AddEmptyAnimation(DonQuixote_Track);

                    // 基础轨若空着（例如演出盖掉了入场 idle），补回去，避免停在 setup pose。
                    string? DonQuixote_Base = DonQuixote_EndState.GetCurrentAnimationName(DonQuixote_BaseTrack);
                    if (string.IsNullOrEmpty(DonQuixote_Base))
                    {
                        DonQuixote_EndState.SetAnimation(DonQuixote_IdleAnim, true, DonQuixote_BaseTrack);
                    }
                }
                catch (Exception DonQuixote_ex2)
                {
                    DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                        $"[DonQuixote] feast pose restore failed: {DonQuixote_ex2.Message}", 0);
                }
            };
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn($"[DonQuixote] feast pose failed: {DonQuixote_ex.Message}", 0);
        }
    }
}
