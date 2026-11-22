using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 战斗房间里的「全屏覆盖图」（舞台卡背景 / 白月骑士战斗背景）的统一挂载与摆放。
///
/// 为什么要单独抽出来
/// ------------------
/// 之前两张图都挂进 <c>%BgContainer</c>（原版视差背景层）里当最后一个子节点，
/// 在主机上看着没问题，但有两个坑：
///  ・原版背景层的子节点自带 z 值（视差层是 -28 / -11 之类），
///    <c>BgContainer</c> 又会被 <c>NCombatSceneContainer</c> 整体缩放 0.9 / 1.08，
///    覆盖图夹在里面，客机上就出现过「压住敌我双方角色」；
///  ・帝王蟹（Kaiser Crab）那只 BOSS 的两只螯**画在背景场景里**（背景里的 Spine），
///    盖住背景等于盖住 BOSS。
///
/// 正确做法：挂到 <c>%CombatSceneContainer</c> 的 1 号位。
/// <code>
/// CombatSceneContainer   (Z = -10，NCombatRoom._Ready 里设)
/// ├─ 0: BgContainer       原版视差背景（整棵子树在这一层之后，天然被盖住）
/// ├─ 1: 我们的覆盖图       ← 同 Z，树序排在 BgContainer 后 → 盖住背景
/// ├─ 2: BackCombatVfxContainer
/// ├─ 3: AllyContainer     我方角色
/// └─ 4: EnemyContainer    敌怪
/// </code>
/// 同 Z 时「后入树者在上」，所以覆盖图永远在角色容器之前绘制 —— 不管客机/主机、
/// 不管 BgContainer 被缩放成什么样，都压不到角色。
/// </summary>
internal static class DonQuixoteSceneOverlay
{
    /// <summary>
    /// 覆盖图槽位：白月骑士专属背景。紧贴原版背景层（<c>BgContainer</c> 之后的第一位）。
    /// </summary>
    public const int DonQuixote_OrderBackdrop = 1;

    /// <summary>
    /// 覆盖图槽位：舞台卡（幸福笑容 / 噩梦的拉曼却领）。排在白月背景之后 ——
    /// 巴里战里两张图会同时存在，舞台卡必须压在白月背景上面，否则打出去看不见。
    /// </summary>
    public const int DonQuixote_OrderStage = 2;

    /// <summary>战斗场景容器（原版 <c>ZIndex = -10</c> 的那个）。</summary>
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
    ///
    /// 目前只有帝王蟹（<c>NKaiserCrabBossBackground</c>）：它的两只螯就是真正的敌怪，
    /// 画在背景场景的 Spine 里。这种场次不能拿覆盖图去盖背景，否则 BOSS 直接消失。
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
    /// 重复调用是幂等的：已经在正确位置就只刷新一次 Z。
    ///
    /// <paramref name="DonQuixote_Order"/> 是槽位：见
    /// <see cref="DonQuixote_OrderBackdrop"/> / <see cref="DonQuixote_OrderStage"/>。
    /// 两个覆盖图同时存在时（巴里战 + 舞台卡），槽位小的先、槽位大的后 ——
    /// 以前两者都抢 1 号位，谁后挂谁把别人挤下去，舞台上打出舞台卡就看不见了。
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

        // 紧跟 BgContainer 之后，按槽位往后排。取不到 BgContainer 就退到最前面
        // （仍然在角色容器之前）。
        int DonQuixote_Index = Math.Max(DonQuixote_Order - 1, 0);
        if (DonQuixote_Bg != null && DonQuixote_Bg.GetParent() == DonQuixote_Parent)
        {
            int DonQuixote_BgIndex = DonQuixote_Bg.GetIndex();
            DonQuixote_Index = DonQuixote_BgIndex + DonQuixote_Order;

            // 高槽位但前面的槽位还空着（比如舞台上场时白月背景根本没挂）：
            // 直接贴到背景后面，不去越过原版的 BackCombatVfxContainer。
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

        // 和 BgContainer / AllyContainer / EnemyContainer 同一个相对 Z（都是 0），
        // 于是绘制顺序完全由树序决定 —— 我们排在角色容器前面，永远压不到人。
        DonQuixote_Frame.ZAsRelative = true;
        DonQuixote_Frame.ZIndex = 0;

        // 可见性不在这里碰：舞台卡展示期间白月背景会被主动隐藏，
        // 这里无条件 Visible = true 会让 watchdog 每 0.4 秒把它拽回来。
    }

    /// <summary>这个节点是不是我们自己的覆盖图（靠名字前缀认）。</summary>
    private static bool DonQuixote_IsOverlay(Node DonQuixote_Node)
    {
        return DonQuixote_Node != null
            && DonQuixote_Node.Name.ToString().StartsWith("DonQuixote", StringComparison.Ordinal);
    }

    /// <summary>
    /// 铺满整个屏幕（四周各留 <paramref name="DonQuixote_Margin"/> 像素富余，防露边）。
    ///
    /// 因为父节点（CombatSceneContainer）带相机缩放/位移，直接用像素写 offsets 会跑位，
    /// 所以先把屏幕坐标反变换回父节点本地坐标，再写 Position / Size。
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

    /// <summary>诊断用一行：父节点 / 索引 / Z 值。</summary>
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
