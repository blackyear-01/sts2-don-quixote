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
/// 中「正在凝固的血」的怪物会被一根血刺 45° 贯穿：下半截压在怪物身后，
/// 上半截盖在怪物身前，视觉上就是一根完整的长刺从身体里穿过去。
///
/// 层级（关键）：Godot 的 z_index 只在**兄弟节点之间**排序，所以前后两半
/// 必须是 NCreatureVisuals 根节点的直接子节点（与身体 %Visuals 平级）：
/// - 后半 Node2D  z_index = -1 → 排在身体之前绘制 → 藏在身体背后
/// - 前半 Node2D  z_index = +1 → 排在身体之后绘制 → 盖在身体前面
/// （之前把两半都挂在 Bounds 子树里，z_index 只在 Bounds 的孩子之间生效，
/// 整根刺作为 Bounds 子树画在身体之前，全被身体盖住。）
///
/// 时机（事件驱动 + 轮询兜底）：
/// - PowerCmd.Apply 后缀：buff 施加到怪物身上的那一刻立刻显示；
/// - Creature.RemovePowerInternal 后缀：buff 被移除（到期/驱散）的那一刻立刻隐藏；
/// - NCreature._Ready 后缀：怪物进场时先挂好隐藏的控制器；
/// - 控制器内 Timer：兜底处理死亡、层叠变化等边界情况。
/// </summary>
internal static class DonQuixoteClotSpike
{
    public const string DonQuixote_FrontName = "DonQuixoteClotSpikeFront";

    public const string DonQuixote_BackName = "DonQuixoteClotSpikeBack";

    private const string DonQuixote_FrontTexturePath = "res://DonQuixote/images/vfx/clot_spike_front.png";

    private const string DonQuixote_BackTexturePath = "res://DonQuixote/images/vfx/clot_spike_back.png";

    /// <summary>倾角（度）。正值让尖端朝右上、尾端朝左下（水平翻转后的 45° 斜贯穿）。</summary>
    private const float DonQuixote_TiltDegrees = 45f;

    /// <summary>血刺总长 / 怪物身体高度。&gt;1 才会从身体上下两端露出来。</summary>
    private const float DonQuixote_LengthRatio = 2.7f;

    /// <summary>贯穿点落在身体高度上的位置（0 = 头顶，1 = 脚底，0.5 = 正中心）。</summary>
    private const float DonQuixote_PierceHeightRatio = 0.5f;

    private const double DonQuixote_RefreshInterval = 0.1;

    private static Texture2D DonQuixote_FrontTexture;

    private static Texture2D DonQuixote_BackTexture;

    private static bool DonQuixote_TexturesResolved;

    /// <summary>
    /// buff 施加路径的入口：PowerCmd.Apply 后缀补丁在检测到「正在凝固的血」
    /// 落到某个生物身上时调用这里，立刻点亮血刺。
    /// </summary>
    public static void DonQuixote_OnPowerApplied(Creature DonQuixote_Target, PowerModel DonQuixote_Power)
    {
        try
        {
            if (DonQuixote_Target == null || DonQuixote_Power is not DonQuixotePowerClottingBlood)
            {
                return;
            }

            if (!DonQuixote_Target.IsMonster)
            {
                return;
            }

            NCreature DonQuixote_Node = DonQuixote_Target.GetCreatureNode();
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] clot spike: applied but no creature node in room", 0);
                return;
            }

            if (!DonQuixote_Attach(DonQuixote_Node))
            {
                return;
            }

            // Apply 是 async，此后缀运行时 power 尚未进列表，不能靠 Refresh 的
            // HasClottingBlood 判定；直接置可见 + 同步几何，实现「施加瞬间出现」。
            // 若最终施加被免疫/取消，0.1s 的轮询会把它修正回去。
            DonQuixote_RefreshGeometry(DonQuixote_Node);
            DonQuixote_SetVisible(DonQuixote_Node, true);

            // 一次性诊断：把关键几何参数写进日志，若仍不可见可据此定位
            NCreatureVisuals DonQuixote_DiagVisuals = DonQuixote_Node.Visuals;
            Control DonQuixote_DiagBounds = DonQuixote_DiagVisuals?.Bounds;
            Vector2 DonQuixote_DiagSize = DonQuixote_DiagBounds?.Size ?? Vector2.Zero;
            Node2D DonQuixote_DiagFront = DonQuixote_DiagVisuals?.GetNodeOrNull<Node2D>(DonQuixote_FrontName);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] clot spike shown on {DonQuixote_Target.Name} bounds={DonQuixote_DiagSize} frontPos={(DonQuixote_DiagFront?.Position.ToString() ?? "null")} frontScale={(DonQuixote_DiagFront?.Scale.ToString() ?? "null")} tex={DonQuixote_FrontTexture.GetWidth()}x{DonQuixote_FrontTexture.GetHeight()}+{DonQuixote_BackTexture.GetWidth()}x{DonQuixote_BackTexture.GetHeight()}", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] clot spike on-applied failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// buff 移除路径的入口：Creature.RemovePowerInternal 后缀补丁调用，立刻熄灭血刺。
    /// </summary>
    public static void DonQuixote_OnPowerRemoved(Creature DonQuixote_Target, PowerModel DonQuixote_Power)
    {
        try
        {
            if (DonQuixote_Target == null || DonQuixote_Power is not DonQuixotePowerClottingBlood)
            {
                return;
            }

            NCreature DonQuixote_Node = DonQuixote_Target.GetCreatureNode();
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            DonQuixote_SetVisible(DonQuixote_Node, false);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] clot spike hidden on {DonQuixote_Target.Name} (power removed)", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] clot spike on-removed failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// 给怪物挂上（或复用已有的）血刺控制器。返回是否已就绪可用。
    /// 前后两半挂成 NCreatureVisuals 的直接子节点，与身体 %Visuals 平级，
    /// 这样 z_index = ±1 才能分别在身体的身后 / 身前绘制。
    /// </summary>
    public static bool DonQuixote_Attach(NCreature DonQuixote_Node)
    {
        try
        {
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return false;
            }

            Creature DonQuixote_Entity = DonQuixote_Node.Entity;
            if (DonQuixote_Entity == null || !DonQuixote_Entity.IsMonster)
            {
                return false;
            }

            NCreatureVisuals DonQuixote_Visuals = DonQuixote_Node.Visuals;
            if (DonQuixote_Visuals == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] clot spike skipped: no visuals on {DonQuixote_Node.Name}", 0);
                return false;
            }

            // 血刺节点挂在 Visuals 下面（NCreature 的孙节点），存在性检查必须查 Visuals
            if (DonQuixote_Visuals.GetNodeOrNull<Node2D>(DonQuixote_FrontName) != null)
            {
                return true;
            }

            // Bounds 只用来取身体矩形，不再作为父节点（z_index 在它的子树里不生效）
            Control DonQuixote_Bounds = DonQuixote_Visuals.Bounds;
            if (DonQuixote_Bounds == null || !GodotObject.IsInstanceValid(DonQuixote_Bounds))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] clot spike skipped: no Bounds on {DonQuixote_Node.Name}", 0);
                return false;
            }

            if (!DonQuixote_ResolveTextures())
            {
                return false;
            }

            float DonQuixote_Rotation = Mathf.DegToRad(DonQuixote_TiltDegrees);

            // 上半截：尖端朝上的那一半，z_index = +1 画在怪物身体前面
            Node2D DonQuixote_Front = new Node2D
            {
                Name = DonQuixote_FrontName,
                Rotation = DonQuixote_Rotation,
                ZIndex = 1,
                Visible = false,
            };
            Sprite2D DonQuixote_FrontSprite = new Sprite2D
            {
                Name = "sprite",
                Texture = DonQuixote_FrontTexture,
            };
            // 沿父节点的局部 -y（旋转后指向左上）伸出去
            DonQuixote_FrontSprite.Position = new Vector2(0f, -DonQuixote_FrontTexture.GetHeight() * 0.5f);
            DonQuixote_Front.AddChild(DonQuixote_FrontSprite);

            // 下半截：藏在怪物背后。z_index 必须为 0——负值会把整截排到房间
            // 地面等 z=0 图层之后而被背景吞掉；「在身体背后」只靠树序实现
            // （挂载后 MoveChild 到身体节点之前，见下方）。
            Node2D DonQuixote_Back = new Node2D
            {
                Name = DonQuixote_BackName,
                Rotation = DonQuixote_Rotation,
                ZIndex = 0,
                Visible = false,
            };
            Sprite2D DonQuixote_BackSprite = new Sprite2D
            {
                Name = "sprite",
                Texture = DonQuixote_BackTexture,
            };
            // 沿父节点的局部 +y（旋转后指向右下）伸出去
            DonQuixote_BackSprite.Position = new Vector2(0f, DonQuixote_BackTexture.GetHeight() * 0.5f);
            DonQuixote_Back.AddChild(DonQuixote_BackSprite);

            // 定时器兜底：状态是否还在、身体尺寸是否变了（怪物长大/缩小/死亡）
            Timer DonQuixote_Timer = new Timer
            {
                Name = "DonQuixoteClotSpikeTick",
                WaitTime = DonQuixote_RefreshInterval,
                Autostart = true,
            };
            DonQuixote_Timer.Timeout += () => DonQuixote_Refresh(DonQuixote_Node);

            DonQuixote_Visuals.AddChild(DonQuixote_Back);
            DonQuixote_Visuals.AddChild(DonQuixote_Front);
            DonQuixote_Visuals.AddChild(DonQuixote_Timer);

            // 层级方案：后半截 z=0 且树序在身体 %Visuals 之前 → 画在身体背后但
            // 仍在正常图层组内（不会被房间地面吞掉）；前半截 z=+1 且树序在身体
            // 之后 → 盖在身体前面。
            Node DonQuixote_Body = DonQuixote_Visuals.GetNodeOrNull("Visuals");
            if (DonQuixote_Body != null)
            {
                DonQuixote_Visuals.MoveChild(DonQuixote_Back, DonQuixote_Body.GetIndex());
            }
            else
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] clot spike: no %Visuals body found on {DonQuixote_Node.Name}, back half relies on z_index only", 0);
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] clot spike controller attached to {DonQuixote_Node.Name}", 0);
            return true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] clot spike attach failed: {DonQuixote_Exception}", 0);
            return false;
        }
    }

    /// <summary>
    /// 同步血刺的显示状态与几何。只有怪物活着且身上还有「正在凝固的血」时才显示。
    /// </summary>
    private static void DonQuixote_Refresh(NCreature DonQuixote_Node)
    {
        try
        {
            if (DonQuixote_Node == null || !GodotObject.IsInstanceValid(DonQuixote_Node))
            {
                return;
            }

            Creature DonQuixote_Entity = DonQuixote_Node.Entity;
            bool DonQuixote_Active = DonQuixote_Entity != null
                && DonQuixote_Entity.IsAlive
                && DonQuixote_HasClottingBlood(DonQuixote_Entity);
            DonQuixote_SetVisible(DonQuixote_Node, DonQuixote_Active);
            if (DonQuixote_Active)
            {
                DonQuixote_RefreshGeometry(DonQuixote_Node);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] clot spike refresh failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// 只同步位置与缩放（不做显示状态判定）：贯穿点跟随身体矩形，长度跟随身体高度。
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

        // 贯穿点：Bounds 矩形的水平中心、高度 46% 处。
        // 用全局坐标再转回 visuals 根的局部坐标，兼容 Bounds 的任意嵌套与自身变换。
        Vector2 DonQuixote_PierceGlobal = DonQuixote_Bounds.GlobalPosition + new Vector2(
            DonQuixote_BodySize.X * 0.5f,
            DonQuixote_BodySize.Y * DonQuixote_PierceHeightRatio);
        Vector2 DonQuixote_PierceLocal = DonQuixote_Visuals.ToLocal(DonQuixote_PierceGlobal);

        float DonQuixote_TextureLength = DonQuixote_FrontTexture.GetHeight() + DonQuixote_BackTexture.GetHeight();
        if (DonQuixote_TextureLength <= 0f)
        {
            return;
        }

        float DonQuixote_Uniform = DonQuixote_BodySize.Y * DonQuixote_LengthRatio / DonQuixote_TextureLength;
        Vector2 DonQuixote_Scale = new Vector2(DonQuixote_Uniform, DonQuixote_Uniform);

        NCreatureVisuals DonQuixote_Root = DonQuixote_Node.Visuals;
        if (DonQuixote_Root == null || !GodotObject.IsInstanceValid(DonQuixote_Root))
        {
            return;
        }

        Node2D DonQuixote_Front = DonQuixote_Root.GetNodeOrNull<Node2D>(DonQuixote_FrontName);
        if (DonQuixote_Front != null && GodotObject.IsInstanceValid(DonQuixote_Front))
        {
            DonQuixote_Front.Position = DonQuixote_PierceLocal;
            DonQuixote_Front.Scale = DonQuixote_Scale;
        }

        Node2D DonQuixote_Back = DonQuixote_Root.GetNodeOrNull<Node2D>(DonQuixote_BackName);
        if (DonQuixote_Back != null && GodotObject.IsInstanceValid(DonQuixote_Back))
        {
            DonQuixote_Back.Position = DonQuixote_PierceLocal;
            DonQuixote_Back.Scale = DonQuixote_Scale;
        }
    }

    private static void DonQuixote_SetVisible(NCreature DonQuixote_Node, bool DonQuixote_Visible)
    {
        NCreatureVisuals DonQuixote_Root = DonQuixote_Node.Visuals;
        if (DonQuixote_Root == null || !GodotObject.IsInstanceValid(DonQuixote_Root))
        {
            return;
        }

        Node2D DonQuixote_Front = DonQuixote_Root.GetNodeOrNull<Node2D>(DonQuixote_FrontName);
        if (DonQuixote_Front != null && GodotObject.IsInstanceValid(DonQuixote_Front))
        {
            DonQuixote_Front.Visible = DonQuixote_Visible;
        }

        Node2D DonQuixote_Back = DonQuixote_Root.GetNodeOrNull<Node2D>(DonQuixote_BackName);
        if (DonQuixote_Back != null && GodotObject.IsInstanceValid(DonQuixote_Back))
        {
            DonQuixote_Back.Visible = DonQuixote_Visible;
        }
    }

    private static bool DonQuixote_HasClottingBlood(Creature DonQuixote_Entity)
    {
        var DonQuixote_Powers = DonQuixote_Entity.Powers;
        for (int DonQuixote_I = 0; DonQuixote_I < DonQuixote_Powers.Count; DonQuixote_I++)
        {
            if (DonQuixote_Powers[DonQuixote_I] is DonQuixotePowerClottingBlood)
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
            return DonQuixote_FrontTexture != null && DonQuixote_BackTexture != null;
        }

        DonQuixote_TexturesResolved = true;
        DonQuixote_FrontTexture = DonQuixote_LoadTexture(DonQuixote_FrontTexturePath);
        DonQuixote_BackTexture = DonQuixote_LoadTexture(DonQuixote_BackTexturePath);
        return DonQuixote_FrontTexture != null && DonQuixote_BackTexture != null;
    }

    private static Texture2D DonQuixote_LoadTexture(string DonQuixote_Path)
    {
        try
        {
            if (!ResourceLoader.Exists(DonQuixote_Path))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn($"[DonQuixote] clot spike texture missing: {DonQuixote_Path}", 0);
                return null;
            }

            return ResourceLoader.Load<Texture2D>(DonQuixote_Path);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] clot spike texture load failed: {DonQuixote_Exception.Message}", 0);
            return null;
        }
    }
}

/// <summary>
/// 每个生物节点就绪时预挂血刺控制器（默认隐藏，状态出现才点亮）。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class DonQuixoteClotSpikeReadyPatch
{
    private static void Postfix(NCreature __instance)
    {
        DonQuixoteClotSpike.DonQuixote_Attach(__instance);
    }
}

/// <summary>
/// 「正在凝固的血」施加到怪物的瞬间：立刻显示血刺。
/// 非泛型 PowerCmd.Apply 是所有施加路径（含泛型包装）的汇聚点；
/// 对已有实例叠层时游戏会改走 ModifyAmount，但那时血刺早已显示，无需处理。
/// </summary>
[HarmonyPatch(
    typeof(PowerCmd),
    nameof(PowerCmd.Apply),
    new[] { typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class DonQuixoteClotSpikeApplyPatch
{
    private static void Postfix(Creature target, PowerModel power)
    {
        DonQuixoteClotSpike.DonQuixote_OnPowerApplied(target, power);
    }
}

/// <summary>
/// 「正在凝固的血」从怪物身上移除的瞬间（到期递减到 0 / 主动移除）：立刻隐藏血刺。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.RemovePowerInternal))]
internal static class DonQuixoteClotSpikeRemovePatch
{
    private static void Postfix(Creature __instance, PowerModel power)
    {
        DonQuixoteClotSpike.DonQuixote_OnPowerRemoved(__instance, power);
    }
}
