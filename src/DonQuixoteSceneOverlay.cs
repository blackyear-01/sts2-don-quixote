using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 战斗房间里的「全屏覆盖图」（舞台卡背景 / 白月骑士战斗背景）的统一挂载与摆放。
/// </summary>
internal static class DonQuixoteSceneOverlay
{
    /// <summary>
    /// 覆盖图槽位：白月骑士专属背景。
    /// </summary>
    public const int DonQuixote_OrderBackdrop = 1;

    /// <summary>
    /// 覆盖图槽位：舞台卡（幸福笑容 / 噩梦的拉曼却领）。
    /// </summary>
    public const int DonQuixote_OrderStage = 2;

    /// <summary>
    /// 战斗场景容器（原版 ZIndex = -10 的那个）。
    /// </summary>
    public static Control? DonQuixote_Scene(NCombatRoom? DonQuixote_Room)
    {
        try
        {
            return DonQuixote_Room?.GetNodeOrNull<Control>("%CombatSceneContainer");
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 这场的背景里是不是画着敌怪。
    /// </summary>
    public static bool DonQuixote_BackgroundOwnsEnemies(NCombatRoom? DonQuixote_Room)
    {
        try
        {
            Node? DonQuixote_Background = DonQuixote_Room?.Background;
            if (DonQuixote_Background == null)
            {
                return false;
            }

            if (DonQuixote_Background.GetType().Name.IndexOf(
                    "KaiserCrab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return DonQuixote_HasKaiserCrabNode(DonQuixote_Background, 0);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool DonQuixote_HasKaiserCrabNode(Node DonQuixote_Node, int DonQuixote_Depth)
    {
        if (DonQuixote_Depth > 4)
        {
            return false;
        }

        foreach (Node DonQuixote_Child in DonQuixote_Node.GetChildren())
        {
            if (DonQuixote_Child == null)
            {
                continue;
            }

            if (DonQuixote_Child.Name.ToString().IndexOf(
                    "KaiserCrab", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (DonQuixote_HasKaiserCrabNode(DonQuixote_Child, DonQuixote_Depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把覆盖图挂到「原背景之上、角色之下」这一层，并保证 Z 对齐。
    /// </summary>
    public static void DonQuixote_Attach(
        Control DonQuixote_Frame, NCombatRoom DonQuixote_Room, int DonQuixote_Order = DonQuixote_OrderBackdrop)
    {
        if (!GodotObject.IsInstanceValid(DonQuixote_Frame) || !GodotObject.IsInstanceValid(DonQuixote_Room))
        {
            return;
        }

        Control? DonQuixote_SceneContainer = DonQuixote_Scene(DonQuixote_Room);
        Control? DonQuixote_Bg = DonQuixote_Room.GetNodeOrNull<Control>("%BgContainer");

        Node DonQuixote_Parent = (Node?)DonQuixote_SceneContainer ?? (Node?)DonQuixote_Bg ?? DonQuixote_Room;

        if (DonQuixote_Frame.GetParent() != DonQuixote_Parent)
        {
            if (DonQuixote_Frame.GetParent() == null)
            {
                DonQuixote_Parent.AddChild(DonQuixote_Frame);
            }
            else
            {
                DonQuixote_Frame.Reparent(DonQuixote_Parent, false);
            }
        }

        // 紧跟 BgContainer 之后，按槽位往后排。
        int DonQuixote_Index = Math.Max(DonQuixote_Order - 1, 0);
        if (DonQuixote_Bg != null && DonQuixote_Bg.GetParent() == DonQuixote_Parent)
        {
            int DonQuixote_BgIndex = DonQuixote_Bg.GetIndex();
            DonQuixote_Index = DonQuixote_BgIndex + DonQuixote_Order;

            // 高槽位但前面的槽位还空着（比如舞台上场时白月背景根本没挂）：直接贴到背景后面。
            if (DonQuixote_Order > 1
                && DonQuixote_BgIndex + 1 < DonQuixote_Parent.GetChildCount()
                && !DonQuixote_IsOverlay(DonQuixote_Parent.GetChild(DonQuixote_BgIndex + 1)))
            {
                DonQuixote_Index = DonQuixote_BgIndex + 1;
            }
        }

        int DonQuixote_Last = DonQuixote_Parent.GetChildCount() - 1;
        if (DonQuixote_Index < 0)
        {
            DonQuixote_Index = 0;
        }
        else if (DonQuixote_Index > DonQuixote_Last)
        {
            DonQuixote_Index = Math.Max(DonQuixote_Last, 0);
        }

        if (DonQuixote_Last >= 0 && DonQuixote_Frame.GetIndex() != DonQuixote_Index)
        {
            DonQuixote_Parent.MoveChild(DonQuixote_Frame, DonQuixote_Index);
        }

        // 和 BgContainer / AllyContainer / EnemyContainer 同一个相对 Z（都是 0）。
        DonQuixote_Frame.ZAsRelative = true;
        DonQuixote_Frame.ZIndex = 0;

        // 可见性不在这里碰：舞台卡展示期间白月背景会被主动隐藏。
    }

    /// <summary>
    /// 这个节点是不是我们自己的覆盖图（靠名字前缀认）。
    /// </summary>
    private static bool DonQuixote_IsOverlay(Node DonQuixote_Node)
    {
        return DonQuixote_Node != null
            && DonQuixote_Node.Name.ToString().StartsWith("DonQuixote", StringComparison.Ordinal);
    }

    /// <summary>
    /// 铺满整个屏幕（四周各留 <paramref name="DonQuixote_Margin"/> 像素富余。
    /// </summary>
    public static void DonQuixote_Fill(Control DonQuixote_Frame, NCombatRoom DonQuixote_Room, float DonQuixote_Margin)
    {
        if (!GodotObject.IsInstanceValid(DonQuixote_Frame)
            || !GodotObject.IsInstanceValid(DonQuixote_Room))
        {
            return;
        }

        if (DonQuixote_Frame.GetParent() is not Control DonQuixote_Parent
            || !GodotObject.IsInstanceValid(DonQuixote_Parent))
        {
            return;
        }

        try
        {
            Vector2 DonQuixote_Screen = DonQuixote_Room.Size;
            if (DonQuixote_Screen.X <= 1f || DonQuixote_Screen.Y <= 1f)
            {
                DonQuixote_Screen = new Vector2(2560f, 1600f);
            }

            Transform2D DonQuixote_Inverse = DonQuixote_Parent.GetGlobalTransformWithCanvas().AffineInverse();
            Vector2 DonQuixote_TopLeft = DonQuixote_Inverse * new Vector2(-DonQuixote_Margin, -DonQuixote_Margin);
            Vector2 DonQuixote_BottomRight = DonQuixote_Inverse
                * new Vector2(DonQuixote_Screen.X + DonQuixote_Margin, DonQuixote_Screen.Y + DonQuixote_Margin);

            DonQuixote_Frame.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            DonQuixote_Frame.Position = DonQuixote_TopLeft;
            DonQuixote_Frame.Size = DonQuixote_BottomRight - DonQuixote_TopLeft;
            DonQuixote_Frame.Visible = true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] overlay fill failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    /// <summary>
    /// 诊断用一行：父节点 / 索引 / Z 值。
    /// </summary>
    public static string DonQuixote_Describe(Control DonQuixote_Frame)
    {
        if (!GodotObject.IsInstanceValid(DonQuixote_Frame))
        {
            return "invalid";
        }

        Node? DonQuixote_Parent = DonQuixote_Frame.GetParent();
        return $"parent={DonQuixote_Parent?.Name} "
            + $"index={DonQuixote_Frame.GetIndex()}/{DonQuixote_Parent?.GetChildCount() ?? 0} "
            + $"z={DonQuixote_Frame.ZIndex} relative={DonQuixote_Frame.ZAsRelative} "
            + $"rect=({DonQuixote_Frame.Position.X:0},{DonQuixote_Frame.Position.Y:0} "
            + $"{DonQuixote_Frame.Size.X:0}x{DonQuixote_Frame.Size.Y:0})";
    }
}
