using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DonQuixote.Enchantments;
using DonQuixote.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Networking.ManagedActions;

namespace DonQuixote;

/// <summary>
/// 右键附魔「不可摧毁卡牌」的联网实现。
/// </summary>
internal static class DonQuixoteEnchantAction
{
    private const string DonQuixote_ModuleId = "donquixote";
    private const string DonQuixote_ActionKey = "indestructible_enchant";

    /// <summary>
    /// 载荷里的牌堆序号只是「提示」，找不到就退回按牌 id 扫描，两端规则一致。
    /// </summary>
    internal readonly struct DonQuixoteEnchantPayload
    {
        public DonQuixoteEnchantPayload(ulong ownerNetId, string cardEntry, PileType pileType, int index)
        {
            OwnerNetId = ownerNetId;
            CardEntry = cardEntry;
            PileType = pileType;
            Index = index;
        }

        public ulong OwnerNetId { get; }

        public string CardEntry { get; }

        public PileType PileType { get; }

        public int Index { get; }
    }

    private static RitsuLibManagedNetActionDescriptor<DonQuixoteEnchantPayload> DonQuixote_descriptor;

    private static RitsuLibManagedNetActionDescriptor<DonQuixoteEnchantPayload> DonQuixote_Descriptor
    {
        get
        {
            if (DonQuixote_descriptor == null)
            {
                DonQuixote_descriptor = new RitsuLibManagedNetActionDescriptor<DonQuixoteEnchantPayload>(
                    DonQuixote_ModuleId,
                    DonQuixote_ActionKey,
                    DonQuixote_Serialize,
                    DonQuixote_Deserialize,
                    DonQuixote_Execute,
                    GameActionType.CombatPlayPhaseOnly);
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
                "[DonQuixote] enchant net action registered", 0);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] enchant net action register failed: " + DonQuixote_ex.Message, 0);
        }
    }

    /// <summary>
    /// 请求一次附魔：能走行动队列就走队列（各端都会执行），单机则直接本地结算。
    /// </summary>
    public static bool DonQuixote_Request(Player DonQuixote_player, CardModel DonQuixote_card)
    {
        if (DonQuixote_player == null || DonQuixote_card == null)
        {
            return false;
        }

        PileType DonQuixote_pileType = DonQuixote_card.Pile?.Type ?? PileType.Hand;
        int DonQuixote_index = DonQuixote_IndexOf(DonQuixote_card);

        var DonQuixote_payload = new DonQuixoteEnchantPayload(
            DonQuixote_player.NetId,
            DonQuixote_card.Id?.Entry ?? string.Empty,
            DonQuixote_pileType,
            DonQuixote_index);

        bool DonQuixote_queued = false;
        try
        {
            DonQuixote_queued = RitsuLibManagedNetActions.Request(
                RunManager.Instance, DonQuixote_Descriptor, DonQuixote_payload, DonQuixote_player.NetId);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] enchant request failed: " + DonQuixote_ex.Message, 0);
        }

        if (DonQuixote_queued)
        {
            return true;
        }

        // 只有单机（根本没有 NetService）才允许本地直接结算。
        if (RunManager.Instance?.NetService != null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] enchant request was not queued in multiplayer; skipped", 0);
            return true;
        }

        TaskHelper.RunSafely(DonQuixote_Apply(
            new BlockingPlayerChoiceContext(), DonQuixote_player, DonQuixote_payload));
        return true;
    }

    private static int DonQuixote_IndexOf(CardModel DonQuixote_card)
    {
        try
        {
            CardPile DonQuixote_pile = DonQuixote_card.Pile;
            if (DonQuixote_pile == null)
            {
                return -1;
            }

            for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_pile.Cards.Count; DonQuixote_i++)
            {
                if (ReferenceEquals(DonQuixote_pile.Cards[DonQuixote_i], DonQuixote_card))
                {
                    return DonQuixote_i;
                }
            }
        }
        catch
        {
            // 忽略：序号只是提示。
        }

        return -1;
    }

    private static async Task DonQuixote_Execute(
        RitsuLibManagedNetActionContext<DonQuixoteEnchantPayload> DonQuixote_context)
    {
        DonQuixoteEnchantPayload DonQuixote_payload = DonQuixote_context.Message;
        RunState DonQuixote_state = RunManager.Instance?.DebugOnlyGetState();
        Player DonQuixote_player = DonQuixote_state?.Players
            .FirstOrDefault(p => p != null && p.NetId == DonQuixote_payload.OwnerNetId);

        if (DonQuixote_player?.Creature == null)
        {
            return;
        }

        await DonQuixote_Apply(DonQuixote_context.PlayerChoiceContext, DonQuixote_player, DonQuixote_payload);
    }

    /// <summary>
    /// 两端共用同一份结算逻辑：按「牌堆 + 序号 + 牌 id」重新找到那张牌。
    /// </summary>
    private static async Task DonQuixote_Apply(
        PlayerChoiceContext DonQuixote_ctx,
        Player DonQuixote_player,
        DonQuixoteEnchantPayload DonQuixote_payload)
    {
        CardModel DonQuixote_card = DonQuixote_FindCard(DonQuixote_player, DonQuixote_payload);
        if (DonQuixote_card == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] enchant target not found (" + DonQuixote_payload.CardEntry + ")", 0);
            return;
        }

        if (DonQuixote_card.Enchantment != null)
        {
            return;
        }

        if (DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_player.Creature) < DonQuixoteRightClick.DonQuixote_EnchantCost)
        {
            return;
        }

        try
        {
            await DonQuixoteBloodMagic.DonQuixote_ConsumeBloodFeast(
                DonQuixote_ctx,
                DonQuixote_player.Creature,
                DonQuixoteRightClick.DonQuixote_EnchantCost,
                DonQuixote_card);

            CardCmd.Enchant<DonQuixoteIndestructibleCard>(DonQuixote_card, 1m);

            // 遗物效果：附魔成功时获得 30 点「消耗血宴总数」。
            await DonQuixoteBloodMagic.DonQuixote_AccumulateFeastTotal(
                DonQuixote_ctx,
                DonQuixote_player.Creature,
                DonQuixoteRightClick.DonQuixote_EnchantCost,
                DonQuixote_card);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] right click enchant: " + DonQuixote_card.Id.Entry + " (-30 blood feast)", 0);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] right click enchant failed: " + DonQuixote_ex.Message, 0);
        }
    }

    private static CardModel DonQuixote_FindCard(Player DonQuixote_player, DonQuixoteEnchantPayload DonQuixote_payload)
    {
        CardPile DonQuixote_pile;
        try
        {
            DonQuixote_pile = DonQuixote_payload.PileType.GetPile(DonQuixote_player);
        }
        catch
        {
            DonQuixote_pile = null;
        }

        if (DonQuixote_pile?.Cards == null)
        {
            return null;
        }

        System.Collections.Generic.IReadOnlyList<CardModel> DonQuixote_cards = DonQuixote_pile.Cards;
        if (DonQuixote_payload.Index >= 0 && DonQuixote_payload.Index < DonQuixote_cards.Count)
        {
            CardModel DonQuixote_hit = DonQuixote_cards[DonQuixote_payload.Index];
            if (DonQuixote_hit != null && DonQuixote_hit.Id?.Entry == DonQuixote_payload.CardEntry)
            {
                return DonQuixote_hit;
            }
        }

        // 序号对不上（牌堆顺序变了）就按牌 id 找第一张还没附魔的同类牌，两端规则完全一致。
        foreach (CardModel DonQuixote_other in DonQuixote_cards)
        {
            if (DonQuixote_other != null
                && DonQuixote_other.Id?.Entry == DonQuixote_payload.CardEntry
                && DonQuixote_other.Enchantment == null)
            {
                return DonQuixote_other;
            }
        }

        return null;
    }

    private static byte[] DonQuixote_Serialize(DonQuixoteEnchantPayload DonQuixote_payload)
    {
        byte[] DonQuixote_entry = Encoding.UTF8.GetBytes(DonQuixote_payload.CardEntry ?? string.Empty);
        if (DonQuixote_entry.Length > 200)
        {
            throw new InvalidOperationException("card entry too long for enchant payload");
        }

        byte[] DonQuixote_buffer = new byte[8 + 1 + 4 + 1 + DonQuixote_entry.Length];
        int DonQuixote_offset = 0;
        BinaryPrimitives.WriteUInt64LittleEndian(DonQuixote_buffer.AsSpan(DonQuixote_offset), DonQuixote_payload.OwnerNetId);
        DonQuixote_offset += 8;
        DonQuixote_buffer[DonQuixote_offset] = (byte)DonQuixote_payload.PileType;
        DonQuixote_offset += 1;
        BinaryPrimitives.WriteInt32LittleEndian(DonQuixote_buffer.AsSpan(DonQuixote_offset), DonQuixote_payload.Index);
        DonQuixote_offset += 4;
        DonQuixote_buffer[DonQuixote_offset] = (byte)DonQuixote_entry.Length;
        DonQuixote_offset += 1;
        DonQuixote_entry.CopyTo(DonQuixote_buffer, DonQuixote_offset);
        return DonQuixote_buffer;
    }

    private static DonQuixoteEnchantPayload DonQuixote_Deserialize(ReadOnlySpan<byte> DonQuixote_bytes)
    {
        if (DonQuixote_bytes.Length < 14)
        {
            return default;
        }

        int DonQuixote_offset = 0;
        ulong DonQuixote_ownerNetId = BinaryPrimitives.ReadUInt64LittleEndian(DonQuixote_bytes.Slice(DonQuixote_offset));
        DonQuixote_offset += 8;
        PileType DonQuixote_pileType = (PileType)DonQuixote_bytes[DonQuixote_offset];
        DonQuixote_offset += 1;
        int DonQuixote_index = BinaryPrimitives.ReadInt32LittleEndian(DonQuixote_bytes.Slice(DonQuixote_offset));
        DonQuixote_offset += 4;
        int DonQuixote_length = DonQuixote_bytes[DonQuixote_offset];
        DonQuixote_offset += 1;

        if (DonQuixote_length <= 0 || DonQuixote_offset + DonQuixote_length > DonQuixote_bytes.Length)
        {
            return default;
        }

        string DonQuixote_entry = Encoding.UTF8.GetString(
            DonQuixote_bytes.Slice(DonQuixote_offset, DonQuixote_length));
        return new DonQuixoteEnchantPayload(DonQuixote_ownerNetId, DonQuixote_entry, DonQuixote_pileType, DonQuixote_index);
    }
}
