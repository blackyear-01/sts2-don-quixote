using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace DonQuixote.Events;

/// <summary>
/// 事件选项的联网结算。
/// </summary>
internal static class DonQuixoteEventAction
{
    private const string DonQuixote_ModuleId = "donquixote";
    private const string DonQuixote_ActionKey = "event_option";

    internal readonly struct DonQuixoteEventPayload
    {
        public DonQuixoteEventPayload(ulong ownerNetId, string optionKey)
        {
            OwnerNetId = ownerNetId;
            OptionKey = optionKey;
        }

        public ulong OwnerNetId { get; }

        public string OptionKey { get; }
    }

    private static RitsuLibManagedNetActionDescriptor<DonQuixoteEventPayload> DonQuixote_descriptor;

    private static RitsuLibManagedNetActionDescriptor<DonQuixoteEventPayload> DonQuixote_Descriptor
    {
        get
        {
            if (DonQuixote_descriptor == null)
            {
                DonQuixote_descriptor = new RitsuLibManagedNetActionDescriptor<DonQuixoteEventPayload>(
                    DonQuixote_ModuleId,
                    DonQuixote_ActionKey,
                    DonQuixote_Serialize,
                    DonQuixote_Deserialize,
                    DonQuixote_Execute,
                    GameActionType.NonCombat);
            }

            return DonQuixote_descriptor;
        }
    }

    public static void DonQuixote_Register()
    {
        try
        {
            RitsuLibManagedNetActions.Register(DonQuixote_Descriptor);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event option net action registered", 0);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event option net action register failed: " + DonQuixote_ex.Message, 0);
        }
    }

    /// <summary>
    /// 请求结算一次事件选项。
    /// </summary>
    public static bool DonQuixote_Request(Player DonQuixote_player, string DonQuixote_optionKey)
    {
        if (DonQuixote_player == null || string.IsNullOrEmpty(DonQuixote_optionKey))
        {
            return false;
        }

        // RitsuLib 的 Request 只允许「行动主人本人」入队（owner 必须等于本地 NetId）。
        if (!DonQuixote_IsLocalActionOwner(DonQuixote_player))
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event option " + DonQuixote_optionKey
                + " belongs to another player; waiting for their request", 0);
            return true;
        }

        var DonQuixote_payload = new DonQuixoteEventPayload(DonQuixote_player.NetId, DonQuixote_optionKey);

        bool DonQuixote_queued = false;
        try
        {
            DonQuixote_queued = RitsuLibManagedNetActions.Request(
                RunManager.Instance, DonQuixote_Descriptor, DonQuixote_payload, DonQuixote_player.NetId);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event option request failed: " + DonQuixote_ex.Message, 0);
        }

        if (DonQuixote_queued)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event option queued: " + DonQuixote_optionKey
                + " player=" + DonQuixote_player.NetId, 0);
            return true;
        }

        // 只有单机才允许本地直接结算；联机下没能入队就放弃，避免单端改状态。
        if (RunManager.Instance?.NetService != null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event option request was not queued in multiplayer; skipped", 0);
            return true;
        }

        TaskHelper.RunSafely(DonQuixote_Apply(
            new BlockingPlayerChoiceContext(), DonQuixote_player, DonQuixote_payload));
        return true;
    }

    /// <summary>
    /// 单机恒为 true；联机时只有「这个选项的主人」才是本机时为 true。
    /// </summary>
    private static bool DonQuixote_IsLocalActionOwner(Player DonQuixote_player)
    {
        try
        {
            MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService DonQuixote_net = RunManager.Instance?.NetService;
            if (DonQuixote_net == null)
            {
                return true;
            }

            // 单机（以及回放）没有别人，任何选项都由本地结算。
            MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType DonQuixote_type = DonQuixote_net.Type;
            if (DonQuixote_type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host
                && DonQuixote_type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Client)
            {
                return true;
            }

            return DonQuixote_net.NetId == DonQuixote_player.NetId;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static async Task DonQuixote_Execute(
        RitsuLibManagedNetActionContext<DonQuixoteEventPayload> DonQuixote_context)
    {
        DonQuixoteEventPayload DonQuixote_payload = DonQuixote_context.Message;
        RunState DonQuixote_state = RunManager.Instance?.DebugOnlyGetState();
        Player DonQuixote_player = DonQuixote_state?.Players
            .FirstOrDefault(p => p != null && p.NetId == DonQuixote_payload.OwnerNetId);

        if (DonQuixote_player?.Creature == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event option target player not found: " + DonQuixote_payload.OwnerNetId, 0);
            return;
        }

        await DonQuixote_Apply(DonQuixote_context.PlayerChoiceContext, DonQuixote_player, DonQuixote_payload);
    }

    /// <summary>
    /// 两端共用同一份结算逻辑，任何端执行结果都相同。
    /// </summary>
    private static async Task DonQuixote_Apply(
        PlayerChoiceContext DonQuixote_ctx,
        Player DonQuixote_player,
        DonQuixoteEventPayload DonQuixote_payload)
    {
        string DonQuixote_key = DonQuixote_payload.OptionKey;

        try
        {
            switch (DonQuixote_key)
            {
                case "ACCEPT":
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteSanchoFollower>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, 30);
                    break;

                case "LEAVE":
                    await DonQuixoteEventHelper.DonQuixote_GiveRandomUpgradedUncommon(DonQuixote_player);
                    break;

                case "HUNT":
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteNikolina>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, 30);
                    break;

                case "CONFESS":
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteGullinbursti>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, 30);
                    break;

                case "PARADE":
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteDulcinea>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, 30);
                    break;

                case "ADVENTURE":
                    await CreatureCmd.GainMaxHp(DonQuixote_player.Creature, 10m);
                    await DonQuixoteEventHelper.DonQuixote_ChooseUpgradedUncommon(
                        DonQuixote_ctx,
                        DonQuixote_player,
                        3,
                        new MegaCrit.Sts2.Core.Localization.LocString(
                            "events", "DON_QUIXOTE_EVENT_PICK.title"));
                    break;

                case "KEEP":
                    await DonQuixoteEventHelper.DonQuixote_LoseHp(DonQuixote_ctx, DonQuixote_player, 20m);
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteMoonFlight>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, 40);
                    break;

                case "LET_GO":
                    await DonQuixoteEventHelper.DonQuixote_LoseMaxHp(DonQuixote_ctx, DonQuixote_player, 20m);
                    await DonQuixoteEventHelper.DonQuixote_RemoveRelic<Relics.DonQuixoteSanchoFollower>(DonQuixote_player);
                    await DonQuixoteEventHelper.DonQuixote_GrantRelic<Relics.DonQuixoteFarewell>(DonQuixote_player);
                    DonQuixoteEventHelper.DonQuixote_ChangeCorruption(DonQuixote_player, -20);
                    break;

                case "WATCH":
                    await CreatureCmd.GainMaxHp(DonQuixote_player.Creature, 10m);
                    break;

                default:
                    DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                        "[DonQuixote] unknown event option key: " + DonQuixote_key, 0);
                    break;
            }

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event option resolved: " + DonQuixote_key
                + " player=" + DonQuixote_player.NetId, 0);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event option " + DonQuixote_key + " failed: " + DonQuixote_ex, 0);
        }
    }

    private static byte[] DonQuixote_Serialize(DonQuixoteEventPayload DonQuixote_payload)
    {
        byte[] DonQuixote_key = Encoding.UTF8.GetBytes(DonQuixote_payload.OptionKey ?? string.Empty);
        if (DonQuixote_key.Length > 64)
        {
            throw new InvalidOperationException("option key too long");
        }

        byte[] DonQuixote_buffer = new byte[8 + 1 + DonQuixote_key.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(DonQuixote_buffer.AsSpan(0), DonQuixote_payload.OwnerNetId);
        DonQuixote_buffer[8] = (byte)DonQuixote_key.Length;
        DonQuixote_key.CopyTo(DonQuixote_buffer, 9);
        return DonQuixote_buffer;
    }

    private static DonQuixoteEventPayload DonQuixote_Deserialize(ReadOnlySpan<byte> DonQuixote_bytes)
    {
        if (DonQuixote_bytes.Length < 10)
        {
            return default;
        }

        ulong DonQuixote_ownerNetId = BinaryPrimitives.ReadUInt64LittleEndian(DonQuixote_bytes.Slice(0));
        int DonQuixote_length = DonQuixote_bytes[8];
        if (DonQuixote_length <= 0 || 9 + DonQuixote_length > DonQuixote_bytes.Length)
        {
            return default;
        }

        string DonQuixote_key = Encoding.UTF8.GetString(DonQuixote_bytes.Slice(9, DonQuixote_length));
        return new DonQuixoteEventPayload(DonQuixote_ownerNetId, DonQuixote_key);
    }
}
