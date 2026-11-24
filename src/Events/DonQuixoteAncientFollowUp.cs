using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DonQuixote.Events;

/// <summary>
/// 先古事件结束后，把本模组对应层的事件作为「独立事件房」接在后面。
/// </summary>
internal static class DonQuixoteAncientFollowUp
{
    public const string DonQuixote_Table = "events";

    public const string DonQuixote_AshEntry = "DON_QUIXOTE_EVENT_DON_QUIXOTE_EVENT_ASH";
    public const string DonQuixote_DreamEntry = "DON_QUIXOTE_EVENT_DON_QUIXOTE_EVENT_DREAM";
    public const string DonQuixote_DuelEntry = "DON_QUIXOTE_EVENT_DON_QUIXOTE_EVENT_DUEL";

    /// <summary>
    /// 本模组自带的事件条目，永远只能由先古后续触发，不进随机事件池。
    /// </summary>
    public static readonly string[] DonQuixote_Entries =
    {
        DonQuixote_AshEntry,
        DonQuixote_DreamEntry,
        DonQuixote_DuelEntry,
    };

    private static readonly HashSet<string> DonQuixote_Triggered = new HashSet<string>();

    /// <summary>
    /// 上一次触发所属的 run 实例，用来在新一局开始时清空去重表。
    /// </summary>
    private static IRunState DonQuixote_TriggeredRun;

    private static Player DonQuixote_PendingOwner;

    private static string DonQuixote_PendingEntry;

    /// <summary>
    /// 按层数取本模组事件条目（无对应事件时返回 null）。
    /// </summary>
    public static string DonQuixote_EntryForAct(int DonQuixote_actIndex)
    {
        if (DonQuixote_actIndex == 0)
        {
            return DonQuixote_AshEntry;
        }

        if (DonQuixote_actIndex == 1)
        {
            return DonQuixote_DreamEntry;
        }

        if (DonQuixote_actIndex == 2)
        {
            return DonQuixote_DuelEntry;
        }

        return null;
    }

    /// <summary>
    /// 先古事件结束时登记待播放的事件，等玩家点「继续」再切场景。
    /// </summary>
    public static void DonQuixote_Queue(AncientEventModel DonQuixote_host, int DonQuixote_actIndex)
    {
        DonQuixote_PendingOwner = DonQuixote_host?.Owner;
        DonQuixote_PendingEntry = DonQuixote_EntryForAct(DonQuixote_actIndex);

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] ancient follow-up queued: " + DonQuixote_PendingEntry, 0);
    }

    /// <summary>
    /// 是否有待播放的后续事件（不清空）。
    /// </summary>
    public static bool DonQuixote_HasPending()
    {
        return !string.IsNullOrEmpty(DonQuixote_PendingEntry) && DonQuixote_PendingOwner != null;
    }

    /// <summary>
    /// 等所有玩家的先古事件都结束再切场景。
    /// </summary>
    public static async Task DonQuixote_LaunchPendingWhenReady()
    {
        EventSynchronizer DonQuixote_sync = null;
        try
        {
            DonQuixote_sync = RunManager.Instance?.EventSynchronizer;
            for (int DonQuixote_wait = 0; DonQuixote_wait < 300 && DonQuixote_sync != null; DonQuixote_wait++)
            {
                if (DonQuixote_OthersEventsFinished(DonQuixote_sync))
                {
                    break;
                }

                await Task.Delay(100);
            }

            // 还有别人的事件没结束就绝对不能开新事件：引擎会把「上一个事件」 悬空（日志会打印 "... but event EVENT.OROBAS ... is not yet finished!"）。
            if (DonQuixote_sync != null && !DonQuixote_OthersEventsFinished(DonQuixote_sync))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] follow-up skipped: another player's event is still open", 0);
                DonQuixote_PendingEntry = null;
                DonQuixote_PendingOwner = null;
                return;
            }

            if (DonQuixote_sync != null)
            {
                // 光等 IsFinished 不够：对端可能还在跑先古的「选项奖励」任务 （例如捏奥发遗物）。
                await DonQuixote_sync.AwaitPendingOptionTasks();
                await Task.Delay(150);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up wait failed: " + DonQuixote_Exception.Message, 0);
        }

        // 联机时只能有一个客户端去 BeginEvent：两端各自开一次会各自造出 一套事件实例。
        if (DonQuixote_sync != null && !DonQuixote_IsInitiator(DonQuixote_sync))
        {
            bool DonQuixote_shown = false;
            for (int DonQuixote_wait = 0; DonQuixote_wait < 80; DonQuixote_wait++)
            {
                if (DonQuixote_TryShowLocalOwnEvent())
                {
                    DonQuixote_shown = true;
                    break;
                }

                await Task.Delay(100);
            }

            if (DonQuixote_shown)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] follow-up shown on non-initiator client", 0);
                return;
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up not received from initiator, opening locally", 0);
        }

        if (!DonQuixote_LaunchPending())
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] follow-up launch skipped (already handled)", 0);
        }
    }

    /// <summary>
    /// 本端是不是「发起端」。
    /// </summary>
    private static bool DonQuixote_IsInitiator(EventSynchronizer DonQuixote_sync)
    {
        try
        {
            System.Reflection.PropertyInfo DonQuixote_property = typeof(EventSynchronizer).GetProperty(
                "LocalPlayer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);

            if (DonQuixote_property?.GetValue(DonQuixote_sync) is not Player DonQuixote_local)
            {
                return true;
            }

            IReadOnlyList<Player> DonQuixote_players = DonQuixote_local.RunState?.Players;
            if (DonQuixote_players == null || DonQuixote_players.Count <= 1)
            {
                return true;
            }

            ulong DonQuixote_min = ulong.MaxValue;
            foreach (Player DonQuixote_player in DonQuixote_players)
            {
                if (DonQuixote_player != null && DonQuixote_player.NetId < DonQuixote_min)
                {
                    DonQuixote_min = DonQuixote_player.NetId;
                }
            }

            return DonQuixote_local.NetId == DonQuixote_min;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// 反射读 internal 的 EventSynchronizer.LocalPlayer。
    /// </summary>
    private static Player DonQuixote_TryGetLocalPlayer(EventSynchronizer DonQuixote_sync)
    {
        try
        {
            System.Reflection.PropertyInfo DonQuixote_property = typeof(EventSynchronizer).GetProperty(
                "LocalPlayer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);

            return DonQuixote_property?.GetValue(DonQuixote_sync) as Player;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 逐玩家检查「他的事件是否已经结束」。
    /// </summary>
    private static bool DonQuixote_AllEventsFinished(EventSynchronizer DonQuixote_sync)
    {
        IReadOnlyList<Player> DonQuixote_players = null;
        try
        {
            Player DonQuixote_local = DonQuixote_TryGetLocalPlayer(DonQuixote_sync);
            DonQuixote_players = DonQuixote_local?.RunState?.Players;
        }
        catch (Exception)
        {
            DonQuixote_players = null;
        }

        if (DonQuixote_players != null && DonQuixote_players.Count > 0)
        {
            foreach (Player DonQuixote_player in DonQuixote_players)
            {
                if (DonQuixote_player == null)
                {
                    continue;
                }

                EventModel DonQuixote_playerEvent = null;
                try
                {
                    DonQuixote_playerEvent = DonQuixote_sync.GetEventForPlayer(DonQuixote_player);
                }
                catch (Exception)
                {
                    DonQuixote_playerEvent = null;
                }

                if (DonQuixote_playerEvent != null && !DonQuixote_playerEvent.IsFinished)
                {
                    return false;
                }
            }
        }

        // 本端实例再兜一层：EventSynchronizer.Events 里的实例即使已结束也还在列表里。
        IReadOnlyList<EventModel> DonQuixote_events = DonQuixote_sync.Events;
        if (DonQuixote_events == null || DonQuixote_events.Count == 0)
        {
            return true;
        }

        foreach (EventModel DonQuixote_event in DonQuixote_events)
        {
            if (DonQuixote_event != null && !DonQuixote_event.IsFinished)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 只检查「别的玩家」的事件是否都结束了。
    /// </summary>
    private static bool DonQuixote_OthersEventsFinished(EventSynchronizer DonQuixote_sync)
    {
        try
        {
            Player DonQuixote_local = DonQuixote_TryGetLocalPlayer(DonQuixote_sync);
            IReadOnlyList<Player> DonQuixote_players = DonQuixote_local?.RunState?.Players;
            if (DonQuixote_local == null || DonQuixote_players == null || DonQuixote_players.Count <= 1)
            {
                return true;
            }

            foreach (Player DonQuixote_player in DonQuixote_players)
            {
                if (DonQuixote_player == null || DonQuixote_player.NetId == DonQuixote_local.NetId)
                {
                    continue;
                }

                EventModel DonQuixote_playerEvent = null;
                try
                {
                    DonQuixote_playerEvent = DonQuixote_sync.GetEventForPlayer(DonQuixote_player);
                }
                catch (Exception)
                {
                    DonQuixote_playerEvent = null;
                }

                if (DonQuixote_playerEvent != null && !DonQuixote_playerEvent.IsFinished)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// 用本模组事件替换当前事件房。
    /// </summary>
    public static bool DonQuixote_LaunchPending()
    {
        string DonQuixote_entry = DonQuixote_PendingEntry;
        Player DonQuixote_owner = DonQuixote_PendingOwner;
        DonQuixote_PendingEntry = null;
        DonQuixote_PendingOwner = null;

        if (string.IsNullOrEmpty(DonQuixote_entry) || DonQuixote_owner == null)
        {
            return false;
        }

        EventModel DonQuixote_canonical = ModelDb.AllEvents
            .FirstOrDefault(DonQuixote_event => DonQuixote_event != null
                && DonQuixote_event.Id.Entry == DonQuixote_entry);

        // 用类型做兜底匹配：注册器给的 Id 前缀若与本地化 key 不一致，仍能取到模板。
        if (DonQuixote_canonical == null)
        {
            DonQuixote_canonical = ModelDb.AllEvents.FirstOrDefault(DonQuixote_event =>
                DonQuixote_event != null
                && ((DonQuixote_entry == DonQuixote_AshEntry && DonQuixote_event is DonQuixoteEventAsh)
                    || (DonQuixote_entry == DonQuixote_DreamEntry && DonQuixote_event is DonQuixoteEventDream)
                    || (DonQuixote_entry == DonQuixote_DuelEntry && DonQuixote_event is DonQuixoteEventDuel)));
        }

        if (DonQuixote_canonical == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up event not registered: " + DonQuixote_entry, 0);
            return false;
        }

        EventSynchronizer DonQuixote_sync = RunManager.Instance?.EventSynchronizer;
        if (DonQuixote_sync == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up skipped: no event synchronizer", 0);
            return false;
        }

        try
        {
            DonQuixote_sync.BeginEvent(DonQuixote_canonical);

            if (DonQuixote_ShowRoom(DonQuixote_sync, DonQuixote_owner, DonQuixote_entry))
            {
                return true;
            }

            return false;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Error(
                "[DonQuixote] follow-up event failed: " + DonQuixote_Exception, 1);
            return false;
        }
    }

    /// <summary>
    /// 已经显示过的事件实例，避免同一份实例被重复建房间（Proceed 会被点多次）。
    /// </summary>
    private static EventModel DonQuixote_ShownEvent;

    /// <summary>
    /// 把本地玩家手里那份本模组事件实例显示成事件房。
    /// </summary>
    public static bool DonQuixote_ShowRoom(
        EventSynchronizer DonQuixote_sync,
        Player DonQuixote_owner,
        string DonQuixote_entry)
    {
        EventModel DonQuixote_local = DonQuixote_sync?.GetLocalEvent();
        if (DonQuixote_local == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up skipped: no local event instance", 0);
            return false;
        }

        if (DonQuixote_local.IsFinished)
        {
            return false;
        }

        if (ReferenceEquals(DonQuixote_ShownEvent, DonQuixote_local))
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] follow-up event already shown: " + DonQuixote_entry, 0);
            return true;
        }

        if (DonQuixote_owner == null)
        {
            return false;
        }

        NEventRoom DonQuixote_room = NEventRoom.Create(DonQuixote_local, DonQuixote_owner.RunState, false);
        if (DonQuixote_room == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] follow-up skipped: event room could not be created", 0);
            return false;
        }

        DonQuixote_ShownEvent = DonQuixote_local;
        NRun.Instance?.SetCurrentRoom(DonQuixote_room);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] follow-up event shown: " + DonQuixote_local.Id.Entry, 0);
        return true;
    }

    /// <summary>
    /// 联机对端的兜底路径：本端没有 pending（事件不是本端点出来的）。
    /// </summary>
    public static bool DonQuixote_TryShowLocalOwnEvent()
    {
        EventSynchronizer DonQuixote_sync = RunManager.Instance?.EventSynchronizer;
        if (DonQuixote_sync == null)
        {
            return false;
        }

        EventModel DonQuixote_local = DonQuixote_sync.GetLocalEvent();
        if (DonQuixote_local == null || DonQuixote_local.IsFinished)
        {
            return false;
        }

        if (!DonQuixote_Entries.Contains(DonQuixote_local.Id.Entry))
        {
            return false;
        }

        Player DonQuixote_owner = DonQuixote_local.Owner;
        if (DonQuixote_owner == null)
        {
            return false;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] follow-up picked up on peer side: " + DonQuixote_local.Id.Entry, 0);

        return DonQuixote_ShowRoom(DonQuixote_sync, DonQuixote_owner, DonQuixote_local.Id.Entry);
    }

    public static bool DonQuixote_ShouldTrigger(AncientEventModel DonQuixote_host, out int DonQuixote_actIndex)
    {
        DonQuixote_actIndex = -1;

        Player DonQuixote_owner = DonQuixote_host?.Owner;
        if (DonQuixote_owner == null)
        {
            return false;
        }

        if (DonQuixote_host.IsFinished)
        {
            return false;
        }

        int DonQuixote_index = DonQuixote_owner.RunState?.CurrentActIndex ?? -1;
        if (DonQuixote_index < 0 || DonQuixote_index > 2)
        {
            return false;
        }

        // 换了一局（放弃存档重开房间、读档进新 run）就清空去重表。
        if (!ReferenceEquals(DonQuixote_TriggeredRun, DonQuixote_owner.RunState))
        {
            DonQuixote_TriggeredRun = DonQuixote_owner.RunState;
            DonQuixote_Triggered.Clear();
            DonQuixote_PendingEntry = null;
            DonQuixote_PendingOwner = null;
        }

        string DonQuixote_key = DonQuixote_owner.NetId + ":" + DonQuixote_index;
        if (DonQuixote_Triggered.Contains(DonQuixote_key))
        {
            return false;
        }

        DonQuixote_Triggered.Add(DonQuixote_key);
        DonQuixote_actIndex = DonQuixote_index;
        return true;
    }
}

/// <summary>
/// 先古事件收尾时登记后续事件，先古事件本身照常结束。
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "Done")]
internal static class DonQuixoteAncientDonePatch
{
    private static bool Prefix(AncientEventModel __instance)
    {
        try
        {
            if (!DonQuixoteAncientFollowUp.DonQuixote_ShouldTrigger(__instance, out int DonQuixote_actIndex))
            {
                return true;
            }

            DonQuixoteAncientFollowUp.DonQuixote_Queue(__instance, DonQuixote_actIndex);
            return true;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Error(
                "[DonQuixote] ancient follow-up queue failed: " + DonQuixote_Exception, 1);
            return true;
        }
    }
}

/// <summary>
/// 玩家在先古事件结束页点「继续」时，不返回地图，而是切到本模组的事件房。
/// </summary>
[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class DonQuixoteEventProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        try
        {
            if (DonQuixoteAncientFollowUp.DonQuixote_HasPending())
            {
                __result = DonQuixoteAncientFollowUp.DonQuixote_LaunchPendingWhenReady();
                return false;
            }

            // 联机对端：事件已经由对端在网络上开始，本端还停在先古房间。
            if (DonQuixoteAncientFollowUp.DonQuixote_TryShowLocalOwnEvent())
            {
                __result = Task.CompletedTask;
                return false;
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Error(
                "[DonQuixote] follow-up launch failed: " + DonQuixote_Exception, 1);
            return true;
        }

        return true;
    }
}

/// <summary>
/// 联机兜底：1) 选项消息重复/迟到时会落到已经结束的事件实例上。
/// </summary>
[HarmonyPatch(typeof(EventSynchronizer), "ChooseOptionForEvent")]
internal static class DonQuixoteDuplicateEventOptionPatch
{
    private static bool Prefix(EventSynchronizer __instance, Player player, ref int optionIndex)
    {
        try
        {
            EventModel DonQuixote_event = __instance.GetEventForPlayer(player);
            if (DonQuixote_event != null && DonQuixote_event.IsFinished)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] ignored duplicate event option for finished event "
                    + DonQuixote_event.Id.Entry, 0);
                return false;
            }

            if (DonQuixote_event is DonQuixoteEventDream
                && optionIndex >= 0
                && DonQuixote_event.CurrentOptions.Count > 0
                && optionIndex >= DonQuixote_event.CurrentOptions.Count)
            {
                int DonQuixote_fallback = DonQuixote_event.CurrentOptions.Count - 1;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] event option index " + optionIndex + " out of range for player "
                    + player?.NetId + " (" + DonQuixote_event.CurrentOptions.Count
                    + " option(s)), falling back to " + DonQuixote_fallback, 0);
                optionIndex = DonQuixote_fallback;
            }
        }
        catch (Exception)
        {
            // 取不到事件实例时交回原版逻辑，由它给出原本的报错。
        }

        return true;
    }
}

/// <summary>
/// 本模组事件只能由先古后续触发，从事件池里剔除，避免在地图上随机遇到。
/// </summary>
[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.GenerateRooms),
    new[] { typeof(MegaCrit.Sts2.Core.Runs.RunState) })]
internal static class DonQuixoteEventPoolFilterPatch
{
    private static System.Reflection.FieldInfo DonQuixote_RoomsField;

    private static System.Reflection.FieldInfo DonQuixote_EventsField;

    private static void Postfix(ActModel __instance)
    {
        try
        {
            DonQuixote_RoomsField ??= AccessTools.Field(typeof(ActModel), "_rooms");
            if (DonQuixote_RoomsField == null)
            {
                return;
            }

            object DonQuixote_rooms = DonQuixote_RoomsField.GetValue(__instance);
            if (DonQuixote_rooms == null)
            {
                return;
            }

            DonQuixote_EventsField ??= AccessTools.Field(DonQuixote_rooms.GetType(), "events");
            if (DonQuixote_EventsField?.GetValue(DonQuixote_rooms) is not List<EventModel> DonQuixote_events)
            {
                return;
            }

            int DonQuixote_removed = DonQuixote_events.RemoveAll(
                DonQuixote_event => DonQuixote_event != null
                    && DonQuixoteAncientFollowUp.DonQuixote_Entries.Contains(DonQuixote_event.Id.Entry));

            if (DonQuixote_removed > 0)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] removed " + DonQuixote_removed
                    + " follow-up event(s) from the random event pool", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event pool filter failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}
