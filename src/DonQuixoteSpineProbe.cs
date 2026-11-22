using System;
using Godot;
using DonQuixote_Logger = MegaCrit.Sts2.Core.Logging.Logger;
using STS2RitsuLib;

namespace DonQuixote;

/// <summary>
/// One-shot diagnostic: tries to load the hand-authored Spine resources so we
/// can see in godot.log whether JSON skeletons and the .spatlas wrapper are
/// accepted by the spine-godot runtime.
/// </summary>
internal static class DonQuixoteSpineProbe
{
    private const string DonQuixote_AtlasRes = "res://DonQuixote/animations/don_quixote.spatlas";
    private const string DonQuixote_SkelRes = "res://DonQuixote/animations/don_quixote.spjson";
    private const string DonQuixote_DataRes = "res://DonQuixote/animations/don_quixote_skel_data.tres";
    private const string DonQuixote_SceneRes = "res://DonQuixote/scenes/creature_visuals/don_quixote_spine.tscn";
    private const string DonQuixote_IconSceneRes = "res://DonQuixote/scenes/character_icon.tscn";
    private const string DonQuixote_IconTexRes = "res://DonQuixote/images/char/icon.png";
    private const string DonQuixote_IconOutlineTexRes = "res://DonQuixote/images/char/icon_outline.png";
    private const string DonQuixote_CharSelectBgSceneRes = "res://DonQuixote/scenes/char_select_bg.tscn";

    public static void DonQuixote_Run(DonQuixote_Logger DonQuixote_Logger)
    {
        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_AtlasRes);
        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_SkelRes);
        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_DataRes);
        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_SceneRes);

        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_IconSceneRes);
        DonQuixote_LogExists(DonQuixote_Logger, DonQuixote_CharSelectBgSceneRes);
        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_IconSceneRes, "character icon scene");
        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_CharSelectBgSceneRes, "char select bg scene");
        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_IconTexRes, "character icon texture");
        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_IconOutlineTexRes, "character icon outline texture");

        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_AtlasRes, "atlas");
        DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_SkelRes, "skeleton");
        Resource DonQuixote_Data = DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_DataRes, "data");
        if (DonQuixote_Data != null)
        {
            try
            {
                Variant DonQuixote_Loaded = DonQuixote_Data.Call("is_skeleton_data_loaded");
                DonQuixote_Logger.Info($"[DonQuixote] spine data is_skeleton_data_loaded={DonQuixote_Loaded}", 0);
                Variant DonQuixote_Name = DonQuixote_Data.Call("get_skeleton_name");
                DonQuixote_Logger.Info($"[DonQuixote] spine data skeleton name={DonQuixote_Name}", 0);
            }
            catch (Exception DonQuixote_Ex)
            {
                DonQuixote_Logger.Warn($"[DonQuixote] spine data probe failed: {DonQuixote_Ex.Message}", 0);
            }
        }

        Resource DonQuixote_Scene = DonQuixote_TryLoad(DonQuixote_Logger, DonQuixote_SceneRes, "scene");
        if (DonQuixote_Scene is PackedScene DonQuixote_Packed)
        {
            try
            {
                Node? DonQuixote_Node = DonQuixote_Packed.Instantiate();
                if (DonQuixote_Node != null)
                {
                    Node? DonQuixote_Visuals = DonQuixote_Node.GetNodeOrNull("Visuals");
                    DonQuixote_Logger.Info($"[DonQuixote] scene visuals node={DonQuixote_Visuals?.GetType().Name ?? "null"}", 0);
                    DonQuixote_Node.QueueFree();
                }
            }
            catch (Exception DonQuixote_Ex)
            {
                DonQuixote_Logger.Warn($"[DonQuixote] scene instantiate failed: {DonQuixote_Ex.Message}", 0);
            }
        }
    }

    private static void DonQuixote_LogExists(DonQuixote_Logger DonQuixote_Logger, string DonQuixote_Path)
    {
        DonQuixote_Logger.Info($"[DonQuixote] ResourceLoader.Exists({DonQuixote_Path})={ResourceLoader.Exists(DonQuixote_Path)}", 0);
    }

    private static Resource DonQuixote_TryLoad(DonQuixote_Logger DonQuixote_Logger, string DonQuixote_Path, string DonQuixote_Label)
    {
        try
        {
            Resource DonQuixote_Res = ResourceLoader.Load(DonQuixote_Path);
            DonQuixote_Logger.Info(
                $"[DonQuixote] {DonQuixote_Label} load -> {(DonQuixote_Res == null ? "null" : DonQuixote_Res.GetType().Name)}",
                0);
            return DonQuixote_Res;
        }
        catch (Exception DonQuixote_Ex)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] {DonQuixote_Label} load threw: {DonQuixote_Ex.Message}", 0);
            return null;
        }
    }
}

