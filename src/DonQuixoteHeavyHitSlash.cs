using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using DonQuixote.Cards;

namespace DonQuixote;

/// <summary>
/// 重击演出：单段伤害 &gt; 20 时。
/// </summary>
internal static class DonQuixoteHeavyHitSlash
{
    /// <summary>
    /// 骨架里的动画名（make_spine.py 里同时导出了 HeavyHit / heavy_hit）。
    /// </summary>
    public const string DonQuixote_Anim = "HeavyHit";

    /// <summary>
    /// 专用轨道：0 留给角色状态机。
    /// </summary>
    private const int DonQuixote_Track = 1;

    /// <summary>
    /// 动画时长，与 make_spine.py 的 HEAVY_END 一致。
    /// </summary>
    private const double DonQuixote_Length = 2.0;

    /// <summary>
    /// 清轨时刻：动画结束后多留一点余量。
    /// </summary>
    private const double DonQuixote_ClearAt = DonQuixote_Length + 0.04;

    /// <summary>
    /// 触发门槛：单段未被格挡的伤害大于这个值才算重击。
    /// </summary>
    private const int DonQuixote_Threshold = 20;

    /// <summary>
    /// 两次演出的最小间隔，避免多段伤害连着放满屏。
    /// </summary>
    private const double DonQuixote_Cooldown = 1.3;

    private const string DonQuixote_MetaKey = "DonQuixoteHeavyHitSeq";

    private const string DonQuixote_WeaponPath = "res://DonQuixote/images/vfx/weapon_throw.png";

    // ---- 投掷物时间轴（秒，从演出开始算）--------------------------------
    // 节奏：浮现 → 慢慢自旋瞄准 → 悬停停顿等挥手 → 猛地射出 → 命中后钉住一会儿再碎掉。
    // 角色动画：0-0.75 举手蓄力，0.75 之后保持挥手姿势到 2.0s（见 make_spine.py）。
    /// <summary>
    /// 长枪浮现的时刻：角色刚举手，武器先在高处聚形。
    /// </summary>
    private const double DonQuixote_WeaponIn = 0.08;

    /// <summary>
    /// 旋转瞄准完成的时刻：此时枪尖已指向怪物。
    /// </summary>
    private const double DonQuixote_AimBy = 0.68;

    /// <summary>
    /// 停顿结束、脱手发射的时刻：留给角色把挥手动作做出来。
    /// </summary>
    private const double DonQuixote_ReleaseAt = 0.92;

    /// <summary>
    /// 命中时刻：快速发射，飞行时间压得很短。
    /// </summary>
    private const double DonQuixote_HitAt = 1.09;

    /// <summary>
    /// 命中后钉在目标身上的停留时长，之后才碎裂消失。
    /// </summary>
    private const double DonQuixote_Stick = 0.5;

    /// <summary>
    /// 枪尖在贴图上的横向比例（贴图顶部正中，由 make_heavy_art.py 生成）。
    /// </summary>
    private const float DonQuixote_TipRatioX = 0.4578f;

    /// <summary>
    /// 起飞前自转的圈数（乘 π 得弧度，所以这里是「半圈」数）。
    /// </summary>
    private const float DonQuixote_AimSpin = 3.2f;

    /// <summary>
    /// 长枪的最终显示缩放。
    /// </summary>
    private const float DonQuixote_WeaponScale = 0.6f;

    private static bool DonQuixote_AnimMissing;
    private static Texture2D? DonQuixote_WeaponTexture;
    private static bool DonQuixote_WeaponChecked;

    private static double DonQuixote_LastPlayAt = -100.0;
    private static DamageResult? DonQuixote_LastResult;
    private static long DonQuixote_Patches;
    private static string DonQuixote_LastSkip = "none";
    private static long DonQuixote_FlinchCount;
    private static string DonQuixote_FlinchNote = "none";
    private static bool DonQuixote_Logged;

    // ------------------------------------------------------------------
    // 触发
    // ------------------------------------------------------------------

    /// <summary>
    /// 伤害结算后判定：玩家是堂吉诃德、单段未格挡伤害 &gt; 20、不是长枪卡 （长枪卡自己有 LancePose 演出。
    /// </summary>
    public static void DonQuixote_OnDamageGiven(Creature? DonQuixote_Dealer, DamageResult? DonQuixote_Result, CardModel? DonQuixote_Card)
    {
        try
        {
            DonQuixote_Patches++;

            if (DonQuixote_Dealer == null || DonQuixote_Result == null)
            {
                DonQuixote_LastSkip = "no dealer/result";
                return;
            }

            if (ReferenceEquals(DonQuixote_Result, DonQuixote_LastResult))
            {
                DonQuixote_LastSkip = "same result";
                return;
            }

            if (!DonQuixote_Dealer.IsPlayer)
            {
                DonQuixote_LastSkip = "not player";
                return;
            }

            if (DonQuixote_Dealer.Player?.Character is not DonQuixoteCharacter)
            {
                DonQuixote_LastSkip = "not don quixote";
                return;
            }

            if (DonQuixote_Result.UnblockedDamage <= DonQuixote_Threshold)
            {
                DonQuixote_LastSkip = "damage " + DonQuixote_Result.UnblockedDamage;
                return;
            }

            // 长枪卡走 LancePose，重击演出让位。
            if (DonQuixote_Card is DonQuixoteKnightLance or DonQuixoteBloodFeastLance)
            {
                DonQuixote_LastSkip = "lance card";
                return;
            }

            double DonQuixote_Now = Time.GetTicksMsec() / 1000.0;
            if (DonQuixote_Now - DonQuixote_LastPlayAt < DonQuixote_Cooldown)
            {
                DonQuixote_LastSkip = "cooldown";
                return;
            }

            DonQuixote_LastResult = DonQuixote_Result;
            DonQuixote_LastPlayAt = DonQuixote_Now;
            DonQuixote_LastSkip = "played";

            DonQuixote_Play(DonQuixote_Dealer, DonQuixote_Result.Receiver);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] heavy hit trigger failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    private static void DonQuixote_Play(Creature DonQuixote_Owner, Creature? DonQuixote_Target)
    {
        if (DonQuixote_AnimMissing)
        {
            return;
        }

        NCreature? DonQuixote_Node = DonQuixote_Owner.GetCreatureNode();
        if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
        {
            return;
        }

        if (!DonQuixote_Node.HasSpineAnimation)
        {
            return;
        }

        DonQuixote_Node.SpineAnimation.SetAnimation(DonQuixote_Anim, false, DonQuixote_Track);

        string? DonQuixote_Current = DonQuixote_Node.SpineAnimation.GetCurrentAnimationName(DonQuixote_Track);
        if (!string.Equals(DonQuixote_Current, DonQuixote_Anim, StringComparison.OrdinalIgnoreCase))
        {
            DonQuixote_AnimMissing = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] spine has no '{DonQuixote_Anim}' animation (current: '{DonQuixote_Current}'), heavy hit disabled", 0);
            return;
        }

        // 连击覆盖：旧的清轨定时器靠序号失效，免得把新演出清掉。
        long DonQuixote_Seq = DonQuixote_Node.GetMeta(DonQuixote_MetaKey, 0).AsInt64() + 1;
        DonQuixote_Node.SetMeta(DonQuixote_MetaKey, DonQuixote_Seq);

        SceneTree? DonQuixote_Tree = DonQuixote_Node.GetTree();
        if (DonQuixote_Tree == null)
        {
            return;
        }

        // 投掷物：在武器浮现的时刻再生成，此时角色已经换成举手的重击立绘。
        if (DonQuixote_Target != null)
        {
            SceneTreeTimer DonQuixote_Spawn = DonQuixote_Tree.CreateTimer(DonQuixote_WeaponIn);
            DonQuixote_Spawn.Timeout += () => DonQuixote_ThrowWeapon(DonQuixote_Node, DonQuixote_Target);
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

            MegaAnimationState? DonQuixote_State = DonQuixote_Node.SpineAnimation.GetAnimationState();
            DonQuixote_State?.AddEmptyAnimation(DonQuixote_Track);
        };
    }

    // ------------------------------------------------------------------
    // 投掷物：血马长枪
    // ------------------------------------------------------------------

    /// <summary>
    /// 生成血马长枪并从身体后上方甩向目标。
    /// </summary>
    private static void DonQuixote_ThrowWeapon(NCreature DonQuixote_Owner, Creature DonQuixote_TargetCreature)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(DonQuixote_Owner))
            {
                return;
            }

            NCreature? DonQuixote_TargetNode = DonQuixote_TargetCreature?.GetCreatureNode();
            if (DonQuixote_TargetNode == null || !GodotObject.IsInstanceValid(DonQuixote_TargetNode))
            {
                return;
            }

            Texture2D? DonQuixote_Texture = DonQuixote_ResolveWeaponTexture();
            if (DonQuixote_Texture == null)
            {
                return;
            }

            Node? DonQuixote_Parent = DonQuixote_Owner.GetParent();
            if (DonQuixote_Parent == null || !GodotObject.IsInstanceValid(DonQuixote_Parent))
            {
                return;
            }

            float DonQuixote_OwnerH = DonQuixote_BodyHeight(DonQuixote_Owner, 220f);
            float DonQuixote_TargetH = DonQuixote_BodyHeight(DonQuixote_TargetNode, 180f);

            Vector2 DonQuixote_Foot = DonQuixote_Owner.GlobalPosition;

            // 枪尖相对「贴图中心」的偏移：贴图顶部正中就是枪尖。
            Vector2 DonQuixote_TipLocal = new Vector2(
                DonQuixote_Texture.GetWidth() * (DonQuixote_TipRatioX - 0.5f),
                -DonQuixote_Texture.GetHeight() * 0.5f) * DonQuixote_WeaponScale;

            // 枪尖的待发点：头顶再往上留一段距离、略偏身后。
            Vector2 DonQuixote_TipAt = new Vector2(
                DonQuixote_Foot.X - 0.26f * DonQuixote_OwnerH,
                DonQuixote_Foot.Y - DonQuixote_OwnerH - 0.34f * DonQuixote_OwnerH);

            // 目标：怪物身体中部，而不是脚尖。
            Vector2 DonQuixote_End = DonQuixote_TargetNode.GlobalPosition + new Vector2(0f, -0.45f * DonQuixote_TargetH);

            // 让贴图的「正上方」对准目标：局部 (0,-1) 旋转 aim 后应等于这个方向。
            Vector2 DonQuixote_Dir = (DonQuixote_End - DonQuixote_TipAt).Normalized();
            float DonQuixote_Aim = Mathf.Atan2(DonQuixote_Dir.X, -DonQuixote_Dir.Y);

            // 旋转轴改成图像中心之后。
            Vector2 DonQuixote_TipOffset = DonQuixote_Rotate(DonQuixote_TipLocal, DonQuixote_Aim);
            Vector2 DonQuixote_Center = DonQuixote_TipAt - DonQuixote_TipOffset;
            Vector2 DonQuixote_CenterEnd = DonQuixote_End - DonQuixote_TipOffset;

            Vector2 DonQuixote_FullScale = new Vector2(DonQuixote_WeaponScale, DonQuixote_WeaponScale);

            Sprite2D DonQuixote_Bullet = new Sprite2D
            {
                Name = "DonQuixoteHeavyHitWeapon",
                Texture = DonQuixote_Texture,
                ZIndex = 20,
                // offset 留空 = 原点在图片中心，rotation 就是绕图像中心自转（不再绕枪尖）。
                Offset = Vector2.Zero,
                GlobalPosition = DonQuixote_Center,
                Rotation = DonQuixote_Aim - DonQuixote_AimSpin * Mathf.Pi,
                Scale = DonQuixote_FullScale * 0.2f,
                Modulate = new Color(1f, 1f, 1f, 0f),
            };

            DonQuixote_Parent.AddChild(DonQuixote_Bullet);
            DonQuixote_Bullet.GlobalPosition = DonQuixote_Center;

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    $"[DonQuixote] heavy hit weapon center={DonQuixote_Center} tipAt={DonQuixote_TipAt} "
                    + $"end={DonQuixote_End} ownerH={DonQuixote_OwnerH:0} targetH={DonQuixote_TargetH:0}", 0);
            }

            float DonQuixote_SpinTime = (float)(DonQuixote_AimBy - DonQuixote_WeaponIn);
            float DonQuixote_HoldTime = (float)(DonQuixote_ReleaseAt - DonQuixote_AimBy);
            float DonQuixote_FlyTime = (float)(DonQuixote_HitAt - DonQuixote_ReleaseAt);

            Tween DonQuixote_Tween = DonQuixote_Bullet.CreateTween();
            DonQuixote_Tween.SetParallel(true);

            // 1) 自旋瞄准：绕图片中心转到位，本体同时放大显形。
            DonQuixote_Tween.TweenProperty(DonQuixote_Bullet, "rotation", DonQuixote_Aim, DonQuixote_SpinTime)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            DonQuixote_Tween.TweenProperty(DonQuixote_Bullet, "scale", DonQuixote_FullScale, DonQuixote_SpinTime)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            DonQuixote_Tween.TweenProperty(DonQuixote_Bullet, "modulate:a", 1.0f, DonQuixote_SpinTime * 0.55f);

            // 2) 悬停蓄力：转完就定住，等角色挥手；只有极缓的胀缩，看着像绷紧。
            DonQuixote_Tween.Chain().TweenProperty(DonQuixote_Bullet, "scale", DonQuixote_FullScale * 1.06f, DonQuixote_HoldTime)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);

            // 3) 发射：挥完手就直冲出去；角度已锁定，中心平移到反推出来的终点，枪尖正好扎进怪物。
            DonQuixote_Tween.Chain().TweenProperty(DonQuixote_Bullet, "global_position", DonQuixote_CenterEnd, DonQuixote_FlyTime)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            DonQuixote_Tween.TweenProperty(DonQuixote_Bullet, "scale", DonQuixote_FullScale * 0.92f, DonQuixote_FlyTime);

            // 4) 命中：先让怪物抖一下（走游戏自己的 hurt 触发器）。
            DonQuixote_Tween.Chain().TweenCallback(Callable.From(
                () => DonQuixote_FlinchTarget(DonQuixote_TargetCreature, DonQuixote_TargetNode)));
            DonQuixote_Tween.Chain().TweenInterval(DonQuixote_Stick);
            DonQuixote_Tween.Chain().TweenProperty(DonQuixote_Bullet, "scale", DonQuixote_FullScale * 0.2f, 0.12)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            DonQuixote_Tween.TweenProperty(DonQuixote_Bullet, "modulate:a", 0.0f, 0.12);
            DonQuixote_Tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(DonQuixote_Bullet))
                {
                    DonQuixote_Bullet.QueueFree();
                }
            }));
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] heavy hit weapon failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>
    /// 命中瞬间让怪物抖一下：直接走游戏自己的受击触发器 （SetAnimationTrigger("Hit")。
    /// </summary>
    private static void DonQuixote_FlinchTarget(Creature? DonQuixote_Target, NCreature DonQuixote_TargetNode)
    {
        try
        {
            if (DonQuixote_TargetNode == null || !GodotObject.IsInstanceValid(DonQuixote_TargetNode))
            {
                DonQuixote_FlinchNote = "no target node";
                return;
            }

            if (DonQuixote_Target != null && !DonQuixote_Target.IsAlive)
            {
                DonQuixote_FlinchNote = "target dead";
                return;
            }

            if (DonQuixote_TargetNode.IsPlayingDeathAnimation)
            {
                DonQuixote_FlinchNote = "dying";
                return;
            }

            if (!DonQuixote_TargetNode.HasSpineAnimation)
            {
                DonQuixote_FlinchNote = "no spine";
                return;
            }

            DonQuixote_TargetNode.SetAnimationTrigger("Hit");
            DonQuixote_FlinchCount++;
            DonQuixote_FlinchNote = "ok";
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_FlinchNote = "failed: " + DonQuixote_Exception.Message;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] heavy hit target flinch failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>
    /// 二维旋转，角度正方向与 Godot 一致（y 轴向下，视觉上顺时针）。
    /// </summary>
    private static Vector2 DonQuixote_Rotate(Vector2 DonQuixote_V, float DonQuixote_Angle)
    {
        float DonQuixote_Cos = Mathf.Cos(DonQuixote_Angle);
        float DonQuixote_Sin = Mathf.Sin(DonQuixote_Angle);
        return new Vector2(
            DonQuixote_V.X * DonQuixote_Cos - DonQuixote_V.Y * DonQuixote_Sin,
            DonQuixote_V.X * DonQuixote_Sin + DonQuixote_V.Y * DonQuixote_Cos);
    }

    /// <summary>
    /// 角色显示高度：取身体矩形的 y 边长，拿不到就用兜底值。
    /// </summary>
    private static float DonQuixote_BodyHeight(NCreature DonQuixote_Node, float DonQuixote_Fallback)
    {
        try
        {
            Control? DonQuixote_Bounds = DonQuixote_Node?.Visuals?.Bounds;
            if (DonQuixote_Bounds != null
                && GodotObject.IsInstanceValid(DonQuixote_Bounds)
                && DonQuixote_Bounds.Size.Y > 1f)
            {
                return DonQuixote_Bounds.Size.Y;
            }
        }
        catch
        {
            // 忽略：退回到兜底值。
        }

        return DonQuixote_Fallback;
    }

    private static Texture2D? DonQuixote_ResolveWeaponTexture()
    {
        if (DonQuixote_WeaponChecked)
        {
            return DonQuixote_WeaponTexture;
        }

        DonQuixote_WeaponChecked = true;
        try
        {
            if (!ResourceLoader.Exists(DonQuixote_WeaponPath))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] heavy hit weapon texture missing: {DonQuixote_WeaponPath}", 0);
                return null;
            }

            DonQuixote_WeaponTexture = ResourceLoader.Load<Texture2D>(DonQuixote_WeaponPath);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] heavy hit weapon texture failed: {DonQuixote_Exception.Message}", 0);
        }

        return DonQuixote_WeaponTexture;
    }

    // ------------------------------------------------------------------
    // 诊断
    // ------------------------------------------------------------------

    public static string DonQuixote_Probe()
    {
        return $"threshold>{DonQuixote_Threshold} anim={DonQuixote_Anim} len={DonQuixote_Length:0.00}s "
            + $"weapon={DonQuixote_WeaponPath} missing={DonQuixote_AnimMissing} "
            + $"hits={DonQuixote_Patches} last={DonQuixote_LastSkip} "
            + $"flinch={DonQuixote_FlinchCount}({DonQuixote_FlinchNote})";
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class DonQuixoteHeavyHitSlashPatch
{
    [HarmonyPostfix]
    private static void DonQuixote_Postfix(Creature? __2, DamageResult? __3, CardModel? __6)
    {
        DonQuixoteHeavyHitSlash.DonQuixote_OnDamageGiven(__2, __3, __6);
    }
}
