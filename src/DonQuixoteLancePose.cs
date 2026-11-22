using System;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DonQuixote;

/// <summary>
/// 「骑士的长枪」「饱食鲜血的长枪」打出时的举枪突刺姿势。
///
/// 姿势不再是贴在节点上的一张 png，而是骨架里的一帧：<c>lance</c> 附件
/// （由 tools/make_spine.py 从 资源\人物\长枪.png 打进 don_quixote.png 图集），
/// 尺寸和位置跟其它姿势用同一套口径算——按角色本人的包围盒对齐脚底中心、
/// 缩放到和 idle 相同的角色身高，所以不会出现大小不符或者飘在空中。
///
/// 播放在 track 1 上：出牌时游戏自己的状态机（Attack / Cast / Hit）走的是
/// track 0，不会把这段姿势顶掉；播完把 track 1 清空，状态机继续接管。
/// </summary>
internal static class DonQuixoteLancePose
{
    /// <summary>骨架里的姿势动画名（make_spine.py 里同时导出了 LancePose / lance_pose）。</summary>
    public const string DonQuixote_Anim = "LancePose";

    /// <summary>专用轨道：0 留给角色状态机。</summary>
    private const int DonQuixote_Track = 1;

    /// <summary>姿势时长（秒），与 make_spine.py 的 DonQuixote_lance_end 保持一致。</summary>
    private const double DonQuixote_Length = 0.86;

    /// <summary>清轨道的时刻：姿势结束后再多留一点余量，避免和最后一帧抢。</summary>
    private const double DonQuixote_ClearAt = DonQuixote_Length + 0.04;

    private const string DonQuixote_MetaKey = "DonQuixoteLancePoseSeq";

    private static bool DonQuixote_AnimMissing;

    /// <summary>打出长枪卡时调用：播一次举枪突刺姿势。</summary>
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

            DonQuixote_Node.SpineAnimation.SetAnimation(DonQuixote_Anim, false, DonQuixote_Track);

            // 骨架里可能没有这段动画（比如玩家换了旧版资源），spine 只打日志不抛异常，
            // 所以用当前动画名探测一次，失败就不再重试。
            string? DonQuixote_Current = DonQuixote_Node.SpineAnimation.GetCurrentAnimationName(DonQuixote_Track);
            if (!string.Equals(DonQuixote_Current, DonQuixote_Anim, StringComparison.OrdinalIgnoreCase))
            {
                DonQuixote_AnimMissing = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] spine has no '{DonQuixote_Anim}' animation (current: '{DonQuixote_Current}'), lance pose disabled", 0);
                return;
            }

            // 连打时后面这一枪会覆盖前一段：用序号让旧的定时器失效，免得它把新姿势清掉。
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
                if (!GodotObject.IsInstanceValid(DonQuixote_Node))
                {
                    return;
                }

                if (DonQuixote_Node.GetMeta(DonQuixote_MetaKey, 0).AsInt64() != DonQuixote_Seq)
                {
                    return;
                }

                // 清掉 track 1，让状态机的 idle / attack / dead 重新接管 body_slot。
                MegaAnimationState? DonQuixote_State = DonQuixote_Node.SpineAnimation.GetAnimationState();
                DonQuixote_State?.AddEmptyAnimation(DonQuixote_Track);
            };
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_AnimMissing = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] lance pose failed: {DonQuixote_Exception.Message}", 0);
        }
    }
}
