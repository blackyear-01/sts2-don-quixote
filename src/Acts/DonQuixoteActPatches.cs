using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace DonQuixote.Acts;

/// <summary>
/// 把「折射轨道线」接在本局章节列表的最后。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetRandomList))]
internal static class DonQuixoteActListPatch
{
    private static bool DonQuixote_Warned;

    private static bool DonQuixote_Logged;

    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<ActModel> __result)
    {
        try
        {
            if (__result is not List<ActModel> DonQuixote_List)
            {
                return;
            }

            ActModel DonQuixote_Act = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Act == null)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_List)
            {
                if (DonQuixote_Existing != null
                    && (ReferenceEquals(DonQuixote_Existing, DonQuixote_Act)
                        || DonQuixote_Existing.Id == DonQuixote_Act.Id))
                {
                    return;
                }
            }

            // 已经不是原版三层（别的 mod 已经补了自己的第四幕）→ 先不插。
            if (DonQuixote_List.Count != 3)
            {
                return;
            }

            // 第三层不是原版的荣耀：这一层的归属权在别的 mod 手里。
            ActModel DonQuixote_Third = DonQuixote_List[DonQuixote_List.Count - 1];
            if (DonQuixote_Third is not Glory)
            {
                if (!DonQuixote_Logged)
                {
                    DonQuixote_Logged = true;
                    DonQuixoteModEntry.DonQuixote_Logger?.Info(
                        "[DonQuixote] act3 is " + (DonQuixote_Third?.GetType().Name ?? "null")
                        + " (not Glory) -> refraction rail will be deferred to the tail at runtime", 0);
                }

                return;
            }

            DonQuixote_List.Add(DonQuixote_Act);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail appended as fourth act (acts="
                    + DonQuixote_List.Count + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act list patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 顺延：第四层已经被别的 mod 占了的话，把我们的层接在它后面。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetActInternal))]
internal static class DonQuixoteRailDeferPatch
{
    private static bool DonQuixote_Warned;

    private static bool DonQuixote_Logged;

    /// <summary>
    /// 参数走 __args 而不是按名字绑 int actIndex：SetActInternal 是 async 方法。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, object[] __args)
    {
        try
        {
            int actIndex = __args != null && __args.Length > 0 && __args[0] is int DonQuixote_Index
                ? DonQuixote_Index
                : -1;

            if (actIndex < 3)
            {
                return;
            }

            RunState? DonQuixote_State = __instance?.DebugOnlyGetState();
            if (DonQuixote_State == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_Acts = DonQuixote_State.Acts;
            if (DonQuixote_Acts == null || DonQuixote_Acts.Count == 0)
            {
                return;
            }

            // 只处理「要进的就是当前最后一层」这种情况。
            if (actIndex != DonQuixote_Acts.Count - 1)
            {
                return;
            }

            if (DonQuixote_Acts[actIndex] is DonQuixoteRefractionRail)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_Acts)
            {
                if (DonQuixote_Existing is DonQuixoteRefractionRail)
                {
                    return;
                }
            }

            DonQuixoteRefractionRail? DonQuixote_Rail = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Rail == null)
            {
                return;
            }

            ActModel DonQuixote_Mutable = DonQuixote_Rail.ToMutable();

            List<ActModel> DonQuixote_Next = DonQuixote_Acts.ToList();
            DonQuixote_Next.Add(DonQuixote_Mutable);

            AccessTools.PropertySetter(typeof(RunState), "Acts")
                .Invoke(DonQuixote_State, new object[] { DonQuixote_Next });

            DonQuixote_Mutable.GenerateRooms(DonQuixote_State);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail deferred to slot " + (DonQuixote_Next.Count - 1)
                    + " (act " + actIndex + " belongs to another mod: "
                    + DonQuixote_Acts[actIndex].GetType().Name + ", acts="
                    + DonQuixote_Next.Count + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act defer patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 让「折射轨道线」用自己的线性地图。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap))]
internal static class DonQuixoteRailMapPatch
{
    /// <summary>
    /// Harmony 只认两类补丁方法：方法名恰好是 Prefix/Postfix。
    /// </summary>
    [HarmonyPrefix]
    private static bool DonQuixote_Prefix(ActModel __instance, RunState runState, ref ActMap __result)
    {
        if (__instance == null)
        {
            return true;
        }

        // 按类型判断，不依赖 Id 字符串（mutable 副本的 Id 也一致，但类型更保险）。
        if (__instance is not DonQuixoteRefractionRail)
        {
            return true;
        }

        __result = new DonQuixoteRefractionRailMap(runState);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] refraction rail map created: rows=" + __result.GetRowCount()
            + " cols=" + __result.GetColumnCount(), 0);
        return false;
    }
}

/// <summary>
/// 让「折射轨道线」直接复用荣耀的美术资源。
/// </summary>
[HarmonyPatch(typeof(ActModel), "get_FilePathIdentifier")]
internal static class DonQuixoteRailActPathPatch
{
    private const string DonQuixote_RailEntry = "DON_QUIXOTE_REFRACTION_RAIL";

    private const string DonQuixote_GloryId = "glory";

    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref string __result)
    {
        try
        {
            if (__instance == null)
            {
                return;
            }

            if (string.Equals(__instance.Id.Entry, DonQuixote_RailEntry, StringComparison.OrdinalIgnoreCase))
            {
                __result = DonQuixote_GloryId;
            }
        }
        catch (Exception)
        {
            // 拿不到 Id 时保持原值即可。
        }
    }
}

/// <summary>
/// 第四层只允许出参孙这一个先古。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullAncient))]
internal static class DonQuixoteRailAncientPatch
{
    private static bool DonQuixote_Logged;

    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref EventModel __result)
    {
        if (__instance is not DonQuixoteRefractionRail)
        {
            return;
        }

        AncientEventModel? DonQuixote_samson = ModelDb.AncientEvent<Events.DonQuixoteAncientSamson>();
        if (DonQuixote_samson == null)
        {
            return;
        }

        if (!ReferenceEquals(__result, DonQuixote_samson))
        {
            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail ancient forced to Samson (was "
                    + (__result?.Id.Entry ?? "?") + ")", 0);
            }

            __result = DonQuixote_samson;
        }
    }
}

/// <summary>
/// 同一件事的展示面：地图上的先古节点也读 ActModel.Ancient。
/// </summary>
[HarmonyPatch(typeof(ActModel), "get_Ancient")]
internal static class DonQuixoteRailAncientGetterPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref AncientEventModel __result)
    {
        if (__instance is not DonQuixoteRefractionRail)
        {
            return;
        }

        AncientEventModel? DonQuixote_samson = ModelDb.AncientEvent<Events.DonQuixoteAncientSamson>();
        if (DonQuixote_samson != null)
        {
            __result = DonQuixote_samson;
        }
    }
}

/// <summary>
/// 读档时的空列表兜底：修「点继续游戏报错、进不去」。
/// </summary>
[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.FromSave))]
internal static class DonQuixoteRoomSetSavePatch
{
    [HarmonyPrefix]
    private static void Prefix(SerializableRoomSet save)
    {
        if (save == null)
        {
            return;
        }

        if (save.EventIds == null)
        {
            save.EventIds = new List<ModelId>();
        }

        if (save.NormalEncounterIds == null)
        {
            save.NormalEncounterIds = new List<ModelId>();
        }

        if (save.EliteEncounterIds == null)
        {
            save.EliteEncounterIds = new List<ModelId>();
        }
    }
}

/// <summary>
/// 第四层（index 3）点宝箱节点会黑屏。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Rooms.TreasureRoom), MethodType.Constructor, new[] { typeof(int) })]
internal static class DonQuixoteTreasureRoomPatch
{
    /// <summary>
    /// true = 走原构造函数；false = 跳过（只跳过那一句越界校验）。
    /// </summary>
    [HarmonyPrefix]
    private static bool Prefix(int actIndex)
    {
        return actIndex >= 0 && actIndex <= 2;
    }
}

/// <summary>
/// 进阶 10（双重 Boss）与恐惧模式下的「第三层双 Boss」补回。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap),
    new[] { typeof(RunState), typeof(bool) })]
internal static class DonQuixoteAct3DoubleBossPatch
{
    private static bool DonQuixote_Logged;

    [HarmonyPrefix]
    private static void Prefix(ActModel __instance, RunState runState)
    {
        try
        {
            if (__instance == null || __instance.Index != 2)
            {
                return;
            }

            if (__instance.SecondBossEncounter != null)
            {
                return;
            }

            RunManager? DonQuixote_run = RunManager.Instance;
            RunState? DonQuixote_state = runState ?? DonQuixote_run?.DebugOnlyGetState();
            if (DonQuixote_run == null || DonQuixote_state == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_acts = DonQuixote_state.Acts;
            bool DonQuixote_railIsLast = DonQuixote_acts != null && DonQuixote_acts.Count > 0
                && DonQuixote_acts[DonQuixote_acts.Count - 1] is DonQuixoteRefractionRail;

            // 双重 Boss 的判据。
            bool DonQuixote_doubleBoss =
                DonQuixote_run.AscensionManager.HasLevel(AscensionLevel.DoubleBoss)
                || DonQuixote_state.AscensionLevel >= (int)AscensionLevel.DoubleBoss
                || DonQuixote_state.GameMode.ToString()
                    .IndexOf("dread", StringComparison.OrdinalIgnoreCase) >= 0;

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] act3 boss check: double=" + DonQuixote_doubleBoss
                + " railLast=" + DonQuixote_railIsLast
                + " second=" + (__instance.SecondBossEncounter?.Id.Entry ?? "none")
                + " asc=" + DonQuixote_state.AscensionLevel
                + " mode=" + DonQuixote_state.GameMode, 0);

            if (!DonQuixote_railIsLast)
            {
                // 最后一层不是我们的层：原版自己会处理，不插手。
                return;
            }

            if (!DonQuixote_doubleBoss)
            {
                return;
            }

            List<EncounterModel> DonQuixote_candidates = __instance.AllBossEncounters
                .Where(DonQuixote_encounter => DonQuixote_encounter != null
                    && DonQuixote_encounter.Id != __instance.BossEncounter.Id)
                .ToList();

            if (DonQuixote_candidates.Count == 0)
            {
                return;
            }

            EncounterModel? DonQuixote_second = DonQuixote_state.Rng.UpFront.NextItem(DonQuixote_candidates);
            if (DonQuixote_second == null)
            {
                return;
            }

            __instance.SetSecondBossEncounter(DonQuixote_second);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] act3 double boss restored: boss="
                    + __instance.BossEncounter.Id.Entry + " second=" + DonQuixote_second.Id.Entry
                    + " asc=" + DonQuixote_state.AscensionLevel
                    + " mode=" + DonQuixote_state.GameMode, 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act3 double boss patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 读档时的两种历史遗留修正（都在 <see cref="RunState.FromSerializable"/> 之后）。
/// </summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class DonQuixoteRailLegacySavePatch
{
    private static bool DonQuixote_Warned;

    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        try
        {
            if (__result == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_Acts = __result.Acts;
            if (DonQuixote_Acts == null || DonQuixote_Acts.Count == 0)
            {
                return;
            }

            // 情况一：让位规则之前的老存档，摘掉我们提前占住的第四层。
            if (__result.CurrentActIndex < 3
                && DonQuixote_Acts.Count == 4
                && DonQuixote_Acts[3] is DonQuixoteRefractionRail
                && DonQuixote_Acts[2] is not Glory)
            {
                AccessTools.PropertySetter(typeof(RunState), "Acts")
                    .Invoke(__result, new object[] { DonQuixote_Acts.Take(3).ToList() });

                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] legacy save: dropped refraction rail from acts (act3 is "
                    + (DonQuixote_Acts[2]?.GetType().Name ?? "null")
                    + ", it will be deferred after the other mod's fourth act)", 0);
                return;
            }

            // 情况二：已经在别人的第四层里，替它补上我们顺延的那一层。
            if (__result.CurrentActIndex < 3
                || __result.CurrentActIndex != DonQuixote_Acts.Count - 1)
            {
                return;
            }

            if (DonQuixote_Acts[__result.CurrentActIndex] is DonQuixoteRefractionRail)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_Acts)
            {
                if (DonQuixote_Existing is DonQuixoteRefractionRail)
                {
                    return;
                }
            }

            DonQuixoteRefractionRail? DonQuixote_Rail = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Rail == null)
            {
                return;
            }

            ActModel DonQuixote_Mutable = DonQuixote_Rail.ToMutable();
            List<ActModel> DonQuixote_Next = DonQuixote_Acts.ToList();
            DonQuixote_Next.Add(DonQuixote_Mutable);

            AccessTools.PropertySetter(typeof(RunState), "Acts")
                .Invoke(__result, new object[] { DonQuixote_Next });

            DonQuixote_Mutable.GenerateRooms(__result);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] legacy save: refraction rail restored to slot "
                + (DonQuixote_Next.Count - 1) + " after another mod's act "
                + __result.CurrentActIndex + " (" + DonQuixote_Acts[__result.CurrentActIndex].GetType().Name
                + ")", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] legacy act fix failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 从「有第二 Boss 的一层」切到「只有单 Boss 的一层」时黑屏的修复。
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class DonQuixoteMapScreenStaleNodePatch
{
    private static readonly FieldInfo? DonQuixote_BossField =
        AccessTools.Field(typeof(NMapScreen), "_bossPointNode");

    private static readonly FieldInfo? DonQuixote_SecondBossField =
        AccessTools.Field(typeof(NMapScreen), "_secondBossPointNode");

    private static readonly FieldInfo? DonQuixote_StartingField =
        AccessTools.Field(typeof(NMapScreen), "_startingPointNode");

    private static readonly FieldInfo? DonQuixote_DictField =
        AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");

    private static bool DonQuixote_Logged;

    [HarmonyPrefix]
    private static void Prefix(NMapScreen __instance, ActMap map)
    {
        try
        {
            if (__instance == null || map == null)
            {
                return;
            }

            bool DonQuixote_HadStale = false;

            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_BossField, "_bossPointNode");
            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_SecondBossField, "_secondBossPointNode");
            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_StartingField, "_startingPointNode");
            DonQuixote_HadStale |= DonQuixote_PruneDictionary(__instance);

            if (DonQuixote_HadStale && !DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] map screen: cleared stale point nodes before switching to "
                    + (map.GetType().Name) + " (" + map.GetRowCount() + "x" + map.GetColumnCount()
                    + ", secondBoss=" + (map.SecondBossMapPoint != null) + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] map screen prefix failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    /// <summary>
    /// 异常兜底：SetMap 真的抛了就在清理完的状态上重试一次。
    /// </summary>
    [HarmonyFinalizer]
    private static Exception? Finalizer(NMapScreen __instance, ActMap map, ulong seed, bool clearDrawings,
        Exception? __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Warn(
            "[DonQuixote] map screen SetMap threw (" + __exception.GetType().Name + ": "
            + __exception.Message + "), retrying once", 0);

        try
        {
            __instance.SetMap(map, seed, clearDrawings);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] map screen SetMap retry succeeded", 0);
            return null;
        }
        catch (Exception DonQuixote_RetryException)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] map screen SetMap retry failed: "
                + DonQuixote_RetryException.GetType().Name + ": " + DonQuixote_RetryException.Message, 0);
            return __exception;
        }
    }

    private static bool DonQuixote_Release(NMapScreen screen, FieldInfo? field, string name)
    {
        if (field == null)
        {
            return false;
        }

        object? DonQuixote_Value = field.GetValue(screen);
        if (DonQuixote_Value == null)
        {
            return false;
        }

        if (DonQuixote_Value is not Node DonQuixote_Node)
        {
            field.SetValue(screen, null);
            return true;
        }

        bool DonQuixote_Valid = GodotObject.IsInstanceValid(DonQuixote_Node);
        if (DonQuixote_Valid)
        {
            DonQuixote_Node.QueueFreeSafely();
        }
        else
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] map screen: " + name + " was already disposed", 0);
        }

        field.SetValue(screen, null);
        return true;
    }

    private static bool DonQuixote_PruneDictionary(NMapScreen screen)
    {
        if (DonQuixote_DictField?.GetValue(screen) is not IDictionary DonQuixote_Dict
            || DonQuixote_Dict.Count == 0)
        {
            return false;
        }

        // 整张表都清掉：SetMap 自己开头也会 Clear。
        int DonQuixote_Count = DonQuixote_Dict.Count;
        DonQuixote_Dict.Clear();
        return DonQuixote_Count > 0;
    }
}
