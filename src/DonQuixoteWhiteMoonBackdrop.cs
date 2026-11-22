using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 白月骑士-巴里遭遇的专属战斗背景。
///
/// 原版自定义背景要走 <c>BackgroundAssets</c> 的目录约定（每个视差层一个 tscn），
/// mod 里那样做太重，这里沿用游戏自己的图层做法：一张铺满全屏的 <see cref="TextureRect"/>。
///
/// 挂哪儿
/// ------
/// 交给 <see cref="DonQuixoteSceneOverlay"/>，统一挂到 <c>%CombatSceneContainer</c> 的 1 号位：
/// <code>
/// CombatSceneContainer   (Z = -10)
/// ├─ 0: BgContainer       原版视差背景
/// ├─ 1: 白月背景          ← 同 Z，树序在 BgContainer 之后 → 盖住背景
/// ├─ 2: BackCombatVfxContainer
/// ├─ 3: AllyContainer     我方角色
/// └─ 4: EnemyContainer    敌怪
/// </code>
/// 以前挂在 BgContainer 内部，主机上没问题，但客机上出现过「背景压住敌我双方角色」——
/// BgContainer 自己有 0.9 / 1.08 的整体缩放，子节点的 z 也是它自己那套，
/// 夹在里面不可控。挂到场景容器当兄弟节点后，绘制顺序只由树序决定，两端一致。
///
/// **不隐藏原版背景**：覆盖层万一没画出来，最坏也只是没换成白月背景，不会变成空场。
/// </summary>
internal static class DonQuixoteWhiteMoonBackdrop
{
    private const string DonQuixote_OverlayName = "DonQuixoteWhiteMoonBackdrop";

    private const string DonQuixote_Path = "res://DonQuixote/images/creatures/bg_white_moon.png";

    /// <summary>四周留的富余（像素），防止分辨率/缩放取整露边。</summary>
    private const float DonQuixote_Margin = 96f;

    private static TextureRect? DonQuixote_Overlay;

    /// <summary>
    /// 舞台卡（幸福笑容 / 噩梦的拉曼却领）正在展示：白月背景要让位。
    ///
    /// 两张图都挂在 <c>%CombatSceneContainer</c> 的同一层，谁在上全看谁后挂 ——
    /// 白月背景每帧受击动画都会重新 Attach 一次，会把舞台卡挤到下面去，
    /// 表现就是「打出舞台卡后背景还是原来那张」。
    /// </summary>
    private static bool DonQuixote_Suspended;

    /// <summary>舞台卡开始展示：立刻隐藏白月背景，并让后续的 <see cref="DonQuixote_Show"/> 不再重显。</summary>
    public static void DonQuixote_Suspend()
    {
        DonQuixote_Suspended = true;

        if (DonQuixote_Overlay != null && GodotObject.IsInstanceValid(DonQuixote_Overlay))
        {
            DonQuixote_Overlay.Visible = false;
        }
    }

    /// <summary>舞台卡收场：把白月背景放回来（只在它本来就在的时候，不会凭空创建）。</summary>
    public static void DonQuixote_Resume()
    {
        if (!DonQuixote_Suspended)
        {
            return;
        }

        DonQuixote_Suspended = false;

        if (DonQuixote_Overlay == null
            || !GodotObject.IsInstanceValid(DonQuixote_Overlay)
            || !DonQuixote_Overlay.IsInsideTree())
        {
            return;
        }

        DonQuixote_Overlay.Visible = true;

        NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
        if (DonQuixote_Room != null)
        {
            DonQuixoteSceneOverlay.DonQuixote_Attach(
                DonQuixote_Overlay, DonQuixote_Room, DonQuixoteSceneOverlay.DonQuixote_OrderBackdrop);
            DonQuixoteSceneOverlay.DonQuixote_Fill(
                DonQuixote_Overlay, DonQuixote_Room, DonQuixote_Margin);
        }
    }

    public static void DonQuixote_Show()
    {
        try
        {
            NCombatRoom? DonQuixote_Room = NCombatRoom.Instance;
            if (DonQuixote_Room == null)
            {
                return;
            }

            // 舞台卡在演：白月背景保持隐藏，等舞台收场（Resume）再说。
            if (DonQuixote_Suspended)
            {
                if (DonQuixote_Overlay != null && GodotObject.IsInstanceValid(DonQuixote_Overlay))
                {
                    DonQuixote_Overlay.Visible = false;
                }

                return;
            }

            // 已经挂好了：换房间/背景重建后可能换了父节点，重新贴一次即可。
            if (DonQuixote_Overlay != null && GodotObject.IsInstanceValid(DonQuixote_Overlay)
                && DonQuixote_Overlay.IsInsideTree())
            {
                DonQuixoteSceneOverlay.DonQuixote_Attach(
                    DonQuixote_Overlay, DonQuixote_Room, DonQuixoteSceneOverlay.DonQuixote_OrderBackdrop);
                DonQuixoteSceneOverlay.DonQuixote_Fill(DonQuixote_Overlay, DonQuixote_Room, DonQuixote_Margin);
                DonQuixote_Overlay.Visible = true;
                return;
            }

            Texture2D? DonQuixote_Texture = ResourceLoader.Load<Texture2D>(
                DonQuixote_Path, null, ResourceLoader.CacheMode.Reuse);
            if (DonQuixote_Texture == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] white moon backdrop missing: " + DonQuixote_Path, 0);
                return;
            }

            TextureRect DonQuixote_Frame = new TextureRect
            {
                Name = DonQuixote_OverlayName,
                Texture = DonQuixote_Texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZAsRelative = true,
                ZIndex = 0,
            };

            DonQuixoteSceneOverlay.DonQuixote_Attach(
                DonQuixote_Frame, DonQuixote_Room, DonQuixoteSceneOverlay.DonQuixote_OrderBackdrop);
            DonQuixote_Overlay = DonQuixote_Frame;
            DonQuixoteSceneOverlay.DonQuixote_Fill(DonQuixote_Frame, DonQuixote_Room, DonQuixote_Margin);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                $"[DonQuixote] white moon backdrop: room={DonQuixote_Room.Size} "
                + DonQuixoteSceneOverlay.DonQuixote_Describe(DonQuixote_Frame),
                0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] white moon backdrop failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}
