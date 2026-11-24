using System;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using STS2RitsuLib;

namespace DonQuixote;

/// <summary>
/// Paints Don Quixote's own full screen artwork behind the game over screen.
/// </summary>
internal static class DonQuixoteGameOverArt
{
    private const string DonQuixote_DeathTexturePath = "res://DonQuixote/images/ui/death_screen.png";

    private const string DonQuixote_BackdropName = "DonQuixoteDeathScreen";

    public static void DonQuixote_Subscribe()
    {
        RitsuLibFramework.SubscribeLifecycle<GameOverScreenCreatedEvent>(
            DonQuixote_OnGameOverScreenCreated,
            true);
    }

    private static void DonQuixote_OnGameOverScreenCreated(GameOverScreenCreatedEvent DonQuixote_Event)
    {
        try
        {
            if (!DonQuixote_IsDonQuixoteRun(DonQuixote_Event.RunState))
            {
                return;
            }

            NGameOverScreen? DonQuixote_Screen = DonQuixote_Event.Screen;
            if (DonQuixote_Screen == null || !GodotObject.IsInstanceValid(DonQuixote_Screen))
            {
                return;
            }

            if (DonQuixote_Screen.GetNodeOrNull<TextureRect>(DonQuixote_BackdropName) != null)
            {
                return;
            }

            Texture2D? DonQuixote_Texture = ResourceLoader.Load<Texture2D>(DonQuixote_DeathTexturePath);
            if (DonQuixote_Texture == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] game over art missing: {DonQuixote_DeathTexturePath}", 0);
                return;
            }

            TextureRect DonQuixote_Backdrop = new()
            {
                Name = DonQuixote_BackdropName,
                Texture = DonQuixote_Texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            DonQuixote_Screen.AddChild(DonQuixote_Backdrop);
            DonQuixote_Screen.MoveChild(DonQuixote_Backdrop, 0);
            DonQuixote_Backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            DonQuixote_Backdrop.SetOffsetsPreset(Control.LayoutPreset.FullRect);
            DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] game over artwork applied", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] game over artwork failed: {DonQuixote_Exception.Message}", 0);
        }
    }

    private static bool DonQuixote_IsDonQuixoteRun(RunState? DonQuixote_RunState)
    {
        if (DonQuixote_RunState?.Players == null || DonQuixote_RunState.Players.Count == 0)
        {
            return false;
        }

        return DonQuixote_RunState.Players[0].Character is DonQuixoteCharacter;
    }
}
