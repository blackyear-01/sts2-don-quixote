using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;

namespace DonQuixote;

/// <summary>
/// Instantiates Don Quixote's combat rig, preferring the hand-authored Spine
/// scene and falling back to the frame-based Sprite2D scene when the spine
/// resources cannot be resolved by the runtime.
/// </summary>
internal static class DonQuixoteVisualsFactory
{
    public const string DonQuixote_SpineScenePath = "res://DonQuixote/scenes/creature_visuals/don_quixote_spine.tscn";

    public const string DonQuixote_SpriteScenePath = "res://DonQuixote/scenes/creature_visuals/don_quixote.tscn";

    public static NCreatureVisuals? DonQuixote_Create()
    {
        NCreatureVisuals? DonQuixote_Spine = DonQuixote_TryInstantiate(DonQuixote_SpineScenePath, "spine");
        if (DonQuixote_Spine != null)
        {
            return DonQuixote_Spine;
        }

        return DonQuixote_TryInstantiate(DonQuixote_SpriteScenePath, "sprite");
    }

    private static NCreatureVisuals? DonQuixote_TryInstantiate(string DonQuixote_Path, string DonQuixote_Label)
    {
        try
        {
            if (!ResourceLoader.Exists(DonQuixote_Path))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] {DonQuixote_Label} scene missing: {DonQuixote_Path}", 0);
                return null;
            }

            if (ResourceLoader.Load(DonQuixote_Path) is not PackedScene DonQuixote_Packed)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    $"[DonQuixote] {DonQuixote_Label} scene is not a PackedScene: {DonQuixote_Path}", 0);
                return null;
            }

            Node DonQuixote_Node = DonQuixote_Packed.Instantiate();
            if (DonQuixote_Node is NCreatureVisuals DonQuixote_Visuals)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    $"[DonQuixote] using {DonQuixote_Label} combat visuals", 0);
                return DonQuixote_Visuals;
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] {DonQuixote_Label} root is {DonQuixote_Node.GetType().Name}, expected NCreatureVisuals", 0);
            DonQuixote_Node.QueueFree();
            return null;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                $"[DonQuixote] {DonQuixote_Label} visuals failed: {DonQuixote_Exception.Message}", 0);
            return null;
        }
    }
}
