using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DonQuixote;

/// <summary>
/// 白月骑士-巴里遭遇的专属战斗背景。
/// </summary>
internal static class DonQuixoteWhiteMoonBackdrop
{
    private const string DonQuixote_OverlayName = "DonQuixoteWhiteMoonBackdrop";

    private const string DonQuixote_Path = "res://DonQuixote/images/creatures/bg_white_moon.png";

    /// <summary>
    /// 四周留的富余（像素），防止分辨率/缩放取整露边。
    /// </summary>
    private const float DonQuixote_Margin = 96f;

    private static TextureRect? DonQuixote_Overlay;

    /// <summary>
    /// 舞台卡（幸福笑容 / 噩梦的拉曼却领）正在展示：白月背景要让位。
    /// </summary>
    private static bool DonQuixote_Suspended;

    /// <summary>
    /// 舞台卡开始展示：立刻隐藏白月背景。
    /// </summary>
    public static void DonQuixote_Suspend()
    {
        DonQuixote_Suspended = true;

        if (DonQuixote_Overlay != null && GodotObject.IsInstanceValid(DonQuixote_Overlay))
        {
            DonQuixote_Overlay.Visible = false;
        }
    }

    /// <summary>
    /// 舞台卡收场：把白月背景放回来（只在它本来就在的时候，不会凭空创建）。
    /// </summary>
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
