using System;
using DonQuixote.Powers;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DonQuixote;

/// <summary>
/// 决斗对象 / 决斗的荣耀的头顶大图标记。
/// </summary>
internal static class DonQuixoteDuelMarkers
{
    public const string DonQuixote_TargetMarkName = "DonQuixoteDuelTargetMark";

    public const string DonQuixote_GloryMarkName = "DonQuixoteDuelGloryMark";

    private const string DonQuixote_TargetTexturePath = "res://DonQuixote/images/powers/duel_target_big.png";

    private const string DonQuixote_GloryTexturePath = "res://DonQuixote/images/powers/duel_glory_big.png";

    /// <summary>
    /// 图标边长 / 身体高度。
    /// </summary>
    private const float DonQuixote_SizeRatio = 0.46f;

    /// <summary>
    /// 图标底边与头顶之间的空隙 / 身体高度。
    /// </summary>
    private const float DonQuixote_GapRatio = 0.02f;

    /// <summary>
    /// 图标边长的下限 / 上限（像素，未含父级缩放）。
    /// </summary>
    private const float DonQuixote_MinSize = 36f;

    private const float DonQuixote_MaxSize = 128f;

    /// <summary>
    /// 两个状态同时存在时的左右分开距离 / 图标边长。
    /// </summary>
    private const float DonQuixote_SplitRatio = 0.55f;

    private const double DonQuixote_RefreshInterval = 0.1;

    private static Texture2D DonQuixote_TargetTexture;

    private static Texture2D DonQuixote_GloryTexture;

    private static bool DonQuixote_TexturesResolved;

    /// <summary>
    /// buff 施加路径的入口：PowerCmd.Apply 后缀在检测到决斗对象 / 决斗的荣耀 落到某个生物身上时调用。
    /// </summary>
    public static void DonQuixote_OnPowerApplied(Creature DonQuixote_Target, PowerModel DonQuixote_Power)
    {
        try
        {
            if (DonQuixote_Target == null || !DonQuixote_IsMarkedPower(DonQuixote_Power))
            {
                return;
            }

            NCreature DonQuixote_Node = DonQuixote_Target.GetCreatureNode();
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            if (!DonQuixote_Attach(DonQuixote_Node))
            {
                return;
            }

            // Apply 是 async.
            DonQuixote_SetMarkVisible(DonQuixote_Node, DonQuixote_Power, true);
            DonQuixote_RefreshGeometry(DonQuixote_Node);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] duel marker shown on {DonQuixote_Target.Name} ({DonQuixote_Power.GetType().Name})", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] duel marker on-applied failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// buff 移除路径的入口：Creature.RemovePowerInternal 后缀调用。
    /// </summary>
    public static void DonQuixote_OnPowerRemoved(Creature DonQuixote_Target, PowerModel DonQuixote_Power)
    {
        try
        {
            if (DonQuixote_Target == null || !DonQuixote_IsMarkedPower(DonQuixote_Power))
            {
                return;
            }

            NCreature DonQuixote_Node = DonQuixote_Target.GetCreatureNode();
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            DonQuixote_SetMarkVisible(DonQuixote_Node, DonQuixote_Power, false);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] duel marker hidden on {DonQuixote_Target.Name} ({DonQuixote_Power.GetType().Name})", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] duel marker on-removed failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// 给生物挂上（或复用已有的）头顶图标节点。
    /// </summary>
    public static bool DonQuixote_Attach(NCreature DonQuixote_Node)
    {
        try
        {
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return false;
            }

            NCreatureVisuals DonQuixote_Visuals = DonQuixote_Node.Visuals;
            if (DonQuixote_Visuals == null)
            {
                return false;
            }

            // 已挂过就直接复用。
            if (DonQuixote_Visuals.GetNodeOrNull<Sprite2D>(DonQuixote_TargetMarkName) != null)
            {
                return true;
            }

            if (!DonQuixote_ResolveTextures())
            {
                return false;
            }

            Sprite2D DonQuixote_TargetMark = new Sprite2D
            {
                Name = DonQuixote_TargetMarkName,
                Texture = DonQuixote_TargetTexture,
                ZIndex = 2,
                Visible = false,
            };
            DonQuixote_Visuals.AddChild(DonQuixote_TargetMark);

            Sprite2D DonQuixote_GloryMark = new Sprite2D
            {
                Name = DonQuixote_GloryMarkName,
                Texture = DonQuixote_GloryTexture,
                ZIndex = 2,
                Visible = false,
            };
            DonQuixote_Visuals.AddChild(DonQuixote_GloryMark);

            // 定时器兜底：状态是否还在、身体尺寸是否变了（怪物长大/缩小/死亡）。
            Timer DonQuixote_Timer = new Timer
            {
                Name = "DonQuixoteDuelMarkerTick",
                WaitTime = DonQuixote_RefreshInterval,
                Autostart = true,
            };
            DonQuixote_Timer.Timeout += () => DonQuixote_Refresh(DonQuixote_Node);
            DonQuixote_Visuals.AddChild(DonQuixote_Timer);

            // 进场时可能已经带着状态（读档 / 召唤），同步一次。
            DonQuixote_Refresh(DonQuixote_Node);
            return true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] duel marker attach failed: {DonQuixote_Exception}", 0);
            return false;
        }
    }

    /// <summary>
    /// 同步显隐与几何。
    /// </summary>
    private static void DonQuixote_Refresh(NCreature DonQuixote_Node)
    {
        try
        {
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            NCreatureVisuals DonQuixote_Visuals = DonQuixote_Node.Visuals;
            if (DonQuixote_Visuals == null || !GodotObject.IsInstanceValid(DonQuixote_Visuals))
            {
                return;
            }

            Creature DonQuixote_Entity = DonQuixote_Node.Entity;
            bool DonQuixote_Alive = DonQuixote_Entity != null && DonQuixote_Entity.IsAlive;

            bool DonQuixote_HasTarget = DonQuixote_Alive && DonQuixote_HasTargetMark(DonQuixote_Entity);
            bool DonQuixote_HasGlory = DonQuixote_Alive && DonQuixote_HasGloryMark(DonQuixote_Entity);

            DonQuixote_SetVisible(DonQuixote_Visuals, DonQuixote_TargetMarkName, DonQuixote_HasTarget);
            DonQuixote_SetVisible(DonQuixote_Visuals, DonQuixote_GloryMarkName, DonQuixote_HasGlory);

            if (DonQuixote_HasTarget || DonQuixote_HasGlory)
            {
                DonQuixote_RefreshGeometry(DonQuixote_Node);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] duel marker refresh failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// 只同步位置与缩放：以 Bounds 顶边中点为头顶，向上让出缝隙后居中放置。
    /// </summary>
    private static void DonQuixote_RefreshGeometry(NCreature DonQuixote_Node)
    {
        if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
        {
            return;
        }

        NCreatureVisuals DonQuixote_Visuals = DonQuixote_Node.Visuals;
        Control DonQuixote_Bounds = DonQuixote_Visuals?.Bounds;
        if (DonQuixote_Bounds == null || !GodotObject.IsInstanceValid(DonQuixote_Bounds))
        {
            return;
        }

        Vector2 DonQuixote_BodySize = DonQuixote_Bounds.Size;
        if (DonQuixote_BodySize.Y <= 1f || DonQuixote_BodySize.X <= 1f)
        {
            return;
        }

        Sprite2D DonQuixote_TargetMark = DonQuixote_Visuals.GetNodeOrNull<Sprite2D>(DonQuixote_TargetMarkName);
        Sprite2D DonQuixote_GloryMark = DonQuixote_Visuals.GetNodeOrNull<Sprite2D>(DonQuixote_GloryMarkName);
        if (DonQuixote_TargetMark == null && DonQuixote_GloryMark == null)
        {
            return;
        }

        bool DonQuixote_TargetOn = DonQuixote_TargetMark != null
            && GodotObject.IsInstanceValid(DonQuixote_TargetMark)
            && DonQuixote_TargetMark.Visible;
        bool DonQuixote_GloryOn = DonQuixote_GloryMark != null
            && GodotObject.IsInstanceValid(DonQuixote_GloryMark)
            && DonQuixote_GloryMark.Visible;
        if (!DonQuixote_TargetOn && !DonQuixote_GloryOn)
        {
            return;
        }

        // 身体矩形：Bounds.GlobalPosition 是左上角，Size 是身体高宽。
        float DonQuixote_DisplaySize = Mathf.Clamp(
            DonQuixote_BodySize.Y * DonQuixote_SizeRatio, DonQuixote_MinSize, DonQuixote_MaxSize);
        float DonQuixote_Gap = Mathf.Max(3f, DonQuixote_BodySize.Y * DonQuixote_GapRatio);

        // 头顶基准线：默认取身体矩形顶边。
        float DonQuixote_TopY = DonQuixote_Bounds.GlobalPosition.Y;
        Control DonQuixote_Intents = null;
        try
        {
            DonQuixote_Intents = DonQuixote_Node.IntentContainer;
        }
        catch
        {
            DonQuixote_Intents = null;
        }

        if (DonQuixote_Intents != null
            && GodotObject.IsInstanceValid(DonQuixote_Intents)
            && DonQuixote_Intents.Visible
            && DonQuixote_Intents.GetChildCount() > 0
            && DonQuixote_Intents.Size.Y > 1f)
        {
            float DonQuixote_IntentTop = DonQuixote_Intents.GlobalPosition.Y;
            if (DonQuixote_IntentTop < DonQuixote_TopY)
            {
                DonQuixote_TopY = DonQuixote_IntentTop;
            }
        }

        // 图标中心：头顶（或意图）上方 gap + 半径。
        float DonQuixote_CenterX = DonQuixote_Bounds.GlobalPosition.X + (DonQuixote_BodySize.X * 0.5f);
        float DonQuixote_CenterY = DonQuixote_TopY
            - DonQuixote_Gap
            - (DonQuixote_DisplaySize * 0.5f);

        // 两个状态同时存在时左右并用，避免互相遮住。
        float DonQuixote_Split = 0f;
        if (DonQuixote_TargetOn && DonQuixote_GloryOn)
        {
            DonQuixote_Split = DonQuixote_DisplaySize * DonQuixote_SplitRatio * 0.5f;
        }

        // 父级可能有非 1 缩放（大怪/小怪），图标尺寸要按父级全局缩放反算。
        float DonQuixote_ParentScale = 1f;
        try
        {
            Vector2 DonQuixote_GlobalScale = DonQuixote_Visuals.GlobalScale;
            DonQuixote_ParentScale = Mathf.Max(0.001f, Mathf.Abs(DonQuixote_GlobalScale.Y));
        }
        catch
        {
            DonQuixote_ParentScale = 1f;
        }

        if (DonQuixote_TargetOn)
        {
            DonQuixote_PlaceMark(
                DonQuixote_Visuals,
                DonQuixote_TargetMark,
                DonQuixote_TargetTexture,
                new Vector2(DonQuixote_CenterX - DonQuixote_Split, DonQuixote_CenterY),
                DonQuixote_DisplaySize,
                DonQuixote_ParentScale);
        }

        if (DonQuixote_GloryOn)
        {
            DonQuixote_PlaceMark(
                DonQuixote_Visuals,
                DonQuixote_GloryMark,
                DonQuixote_GloryTexture,
                new Vector2(DonQuixote_CenterX + DonQuixote_Split, DonQuixote_CenterY),
                DonQuixote_DisplaySize,
                DonQuixote_ParentScale);
        }
    }

    private static void DonQuixote_PlaceMark(
        NCreatureVisuals DonQuixote_Visuals,
        Sprite2D DonQuixote_Mark,
        Texture2D DonQuixote_Texture,
        Vector2 DonQuixote_GlobalCenter,
        float DonQuixote_DisplaySize,
        float DonQuixote_ParentScale)
    {
        if (DonQuixote_Mark == null || !GodotObject.IsInstanceValid(DonQuixote_Mark))
        {
            return;
        }

        int DonQuixote_TextureHeight = DonQuixote_Texture?.GetHeight() ?? 0;
        if (DonQuixote_TextureHeight <= 0)
        {
            return;
        }

        float DonQuixote_Scale = DonQuixote_DisplaySize / DonQuixote_TextureHeight / DonQuixote_ParentScale;
        DonQuixote_Mark.Scale = new Vector2(DonQuixote_Scale, DonQuixote_Scale);
        DonQuixote_Mark.Position = DonQuixote_Visuals.ToLocal(DonQuixote_GlobalCenter);
    }

    private static void DonQuixote_SetMarkVisible(NCreature DonQuixote_Node, PowerModel DonQuixote_Power, bool DonQuixote_Visible)
    {
        NCreatureVisuals DonQuixote_Visuals = DonQuixote_Node?.Visuals;
        if (DonQuixote_Visuals == null || !GodotObject.IsInstanceValid(DonQuixote_Visuals))
        {
            return;
        }

        string DonQuixote_Name = DonQuixote_IsTargetPower(DonQuixote_Power)
            ? DonQuixote_TargetMarkName
            : DonQuixote_GloryMarkName;
        DonQuixote_SetVisible(DonQuixote_Visuals, DonQuixote_Name, DonQuixote_Visible);

        // 另一个标记的显隐也跟着刷新一次，保证并排布局立刻正确。
        string DonQuixote_Other = DonQuixote_Name == DonQuixote_TargetMarkName
            ? DonQuixote_GloryMarkName
            : DonQuixote_TargetMarkName;
        Sprite2D DonQuixote_OtherMark = DonQuixote_Visuals.GetNodeOrNull<Sprite2D>(DonQuixote_Other);
        if (DonQuixote_OtherMark != null && GodotObject.IsInstanceValid(DonQuixote_OtherMark))
        {
            Creature DonQuixote_Entity = DonQuixote_Node.Entity;
            bool DonQuixote_OtherOn = DonQuixote_Entity != null
                && DonQuixote_Entity.IsAlive
                && (DonQuixote_Other == DonQuixote_TargetMarkName
                    ? DonQuixote_HasTargetMark(DonQuixote_Entity)
                    : DonQuixote_HasGloryMark(DonQuixote_Entity));
            DonQuixote_OtherMark.Visible = DonQuixote_OtherOn;
        }
    }

    private static void DonQuixote_SetVisible(NCreatureVisuals DonQuixote_Visuals, string DonQuixote_Name, bool DonQuixote_Visible)
    {
        Sprite2D DonQuixote_Mark = DonQuixote_Visuals.GetNodeOrNull<Sprite2D>(DonQuixote_Name);
        if (DonQuixote_Mark != null && GodotObject.IsInstanceValid(DonQuixote_Mark))
        {
            DonQuixote_Mark.Visible = DonQuixote_Visible;
        }
    }

    private static bool DonQuixote_IsMarkedPower(PowerModel DonQuixote_Power)
    {
        return DonQuixote_IsTargetPower(DonQuixote_Power) || DonQuixote_IsGloryPower(DonQuixote_Power);
    }

    /// <summary>
    /// 走「决斗对象」那套头顶图标的类型：玩家侧的决斗对象、白月骑士-决斗对象。
    /// </summary>
    private static bool DonQuixote_IsTargetPower(PowerModel DonQuixote_Power)
    {
        return DonQuixote_Power is DonQuixotePowerDuelTarget
            || DonQuixote_Power is DonQuixotePowerWhiteMoonDuel;
    }

    /// <summary>
    /// 走「决斗的荣耀」那套头顶图标的类型：玩家侧 / 白月骑士侧。
    /// </summary>
    private static bool DonQuixote_IsGloryPower(PowerModel DonQuixote_Power)
    {
        return DonQuixote_Power is DonQuixotePowerDuelGlory
            || DonQuixote_Power is DonQuixotePowerWhiteMoonGlory;
    }

    private static bool DonQuixote_HasTargetMark(Creature DonQuixote_Entity)
    {
        return DonQuixote_HasPower<DonQuixotePowerDuelTarget>(DonQuixote_Entity)
            || DonQuixote_HasPower<DonQuixotePowerWhiteMoonDuel>(DonQuixote_Entity);
    }

    private static bool DonQuixote_HasGloryMark(Creature DonQuixote_Entity)
    {
        return DonQuixote_HasPower<DonQuixotePowerDuelGlory>(DonQuixote_Entity)
            || DonQuixote_HasPower<DonQuixotePowerWhiteMoonGlory>(DonQuixote_Entity);
    }

    private static bool DonQuixote_HasPower<T>(Creature DonQuixote_Entity)
        where T : PowerModel
    {
        if (DonQuixote_Entity == null)
        {
            return false;
        }

        var DonQuixote_Powers = DonQuixote_Entity.Powers;
        for (int DonQuixote_I = 0; DonQuixote_I < DonQuixote_Powers.Count; DonQuixote_I++)
        {
            if (DonQuixote_Powers[DonQuixote_I] is T)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DonQuixote_ResolveTextures()
    {
        if (DonQuixote_TexturesResolved)
        {
            return DonQuixote_TargetTexture != null && DonQuixote_GloryTexture != null;
        }

        DonQuixote_TexturesResolved = true;
        DonQuixote_TargetTexture = DonQuixote_LoadTexture(DonQuixote_TargetTexturePath);
        DonQuixote_GloryTexture = DonQuixote_LoadTexture(DonQuixote_GloryTexturePath);
        return DonQuixote_TargetTexture != null && DonQuixote_GloryTexture != null;
    }

    private static Texture2D DonQuixote_LoadTexture(string DonQuixote_Path)
    {
        try
        {
            if (!ResourceLoader.Exists(DonQuixote_Path))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] duel marker texture missing: {DonQuixote_Path}", 0);
                return null;
            }

            return ResourceLoader.Load<Texture2D>(DonQuixote_Path);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] duel marker texture load failed: {DonQuixote_Exception.Message}", 0);
            return null;
        }
    }
}

/// <summary>
/// 每个生物节点就绪时预挂头顶图标（默认隐藏，状态出现才点亮）。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class DonQuixoteDuelMarkerReadyPatch
{
    private static void Postfix(NCreature __instance)
    {
        DonQuixoteDuelMarkers.DonQuixote_Attach(__instance);
    }
}

/// <summary>
/// 决斗对象 / 决斗的荣耀施加到单位身上的瞬间：立刻点亮头顶图标。
/// </summary>
[HarmonyPatch(
    typeof(PowerCmd),
    nameof(PowerCmd.Apply),
    new[] { typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class DonQuixoteDuelMarkerApplyPatch
{
    private static void Postfix(Creature target, PowerModel power)
    {
        DonQuixoteDuelMarkers.DonQuixote_OnPowerApplied(target, power);
    }
}

/// <summary>
/// 状态移除的瞬间（到期递减到 0 / 主动移除）：立刻熄灭头顶图标。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.RemovePowerInternal))]
internal static class DonQuixoteDuelMarkerRemovePatch
{
    private static void Postfix(Creature __instance, PowerModel power)
    {
        DonQuixoteDuelMarkers.DonQuixote_OnPowerRemoved(__instance, power);
    }
}
