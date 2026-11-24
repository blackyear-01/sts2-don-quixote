using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 先古对话：给所有先古事件补上「堂吉诃德」专属的对话组。
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "get_DialogueSet")]
internal static class DonQuixoteAncientDialoguePatch
{
    /// <summary>
    /// 角色 ModelId.Entry.
    /// </summary>
    public const string DonQuixote_CharEntry = "DON_QUIXOTE_CHARACTER_DON_QUIXOTE_CHARACTER";

    /// <summary>
    /// 先古 Entry -> 该先古对话的行数（行内容在本地化。
    /// </summary>
    private static readonly Dictionary<string, int> DonQuixote_LineCounts = new Dictionary<string, int>
    {
        ["NEOW"] = 3,
        ["TEZCATARA"] = 3,
        ["DARV"] = 3,
        ["NONUPEIPE"] = 1,
        ["OROBAS"] = 3,
        ["PAEL"] = 1,
        ["TANX"] = 3,
        ["THE_ARCHITECT"] = 3,
        ["VAKUU"] = 1,
    };

    /// <summary>
    /// 已经处理过的 DialogueSet（游戏里每个先古只缓存一个实例）。
    /// </summary>
    private static readonly ConditionalWeakTable<AncientDialogueSet, object> DonQuixote_Done =
        new ConditionalWeakTable<AncientDialogueSet, object>();

    private static readonly HashSet<string> DonQuixote_Logged = new HashSet<string>();

    [HarmonyPostfix]
    private static void Postfix(AncientEventModel __instance, AncientDialogueSet __result)
    {
        DonQuixote_Apply(__instance, __result);
    }

    /// <summary>
    /// 把堂吉诃德的对话塞进这一份 DialogueSet（幂等）。
    /// </summary>
    internal static void DonQuixote_Apply(AncientEventModel DonQuixote_ancient, AncientDialogueSet DonQuixote_set)
    {
        try
        {
            if (DonQuixote_ancient == null || DonQuixote_set == null)
            {
                return;
            }

            string DonQuixote_entry = DonQuixote_ancient.Id.Entry;
            int DonQuixote_lines;
            if (!DonQuixote_LineCounts.TryGetValue(DonQuixote_entry, out DonQuixote_lines))
            {
                return;
            }

            lock (DonQuixote_Done)
            {
                object DonQuixote_marker;
                if (DonQuixote_Done.TryGetValue(DonQuixote_set, out DonQuixote_marker))
                {
                    return;
                }

                DonQuixote_Done.Add(DonQuixote_set, new object());
            }

            Dictionary<string, IReadOnlyList<AncientDialogue>> DonQuixote_table =
                DonQuixote_set.CharacterDialogues;
            if (DonQuixote_table == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] ancient " + DonQuixote_entry + " has no CharacterDialogues table", 0);
                return;
            }

            AncientDialogue DonQuixote_dialogue = DonQuixote_Build(DonQuixote_entry, DonQuixote_lines);
            if (DonQuixote_dialogue == null)
            {
                return;
            }

            // 表里若已有「堂吉诃德」的条目（可能是别的模组或引擎自动补的占位对话）。
            IReadOnlyList<AncientDialogue> DonQuixote_existing;
            if (DonQuixote_table.TryGetValue(DonQuixote_CharEntry, out DonQuixote_existing)
                && DonQuixote_existing != null
                && DonQuixote_existing.Count > 0)
            {
                foreach (AncientDialogue DonQuixote_old in DonQuixote_existing)
                {
                    DonQuixoteModEntry.DonQuixote_Logger?.Info(
                        "[DonQuixote] ancient " + DonQuixote_entry + " replaced dialogue key="
                        + (DonQuixote_LocKeyOf(DonQuixote_old) ?? "(none)")
                        + " lines=" + (DonQuixote_old?.Lines.Count ?? 0)
                        + " visit=" + (DonQuixote_old?.VisitIndex?.ToString() ?? "null")
                        + " repeating=" + (DonQuixote_old?.IsRepeating ?? false), 0);
                }
            }

            DonQuixote_table[DonQuixote_CharEntry] =
                new List<AncientDialogue> { DonQuixote_dialogue };

            // 新加的这条也要生成 LineText / Speaker / 按钮文本。
            DonQuixote_set.PopulateLocKeys(DonQuixote_entry);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] ancient dialogue added for " + DonQuixote_entry
                + " lines=" + DonQuixote_lines, 0);
        }
        catch (Exception DonQuixote_exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] ancient dialogue patch failed: " + DonQuixote_exception, 0);
        }
    }

    /// <summary>
    /// 取一条对话第一行的本地化键（用来判断这条对话到底能不能显示）。
    /// </summary>
    private static string DonQuixote_LocKeyOf(AncientDialogue DonQuixote_dialogue)
    {
        if (DonQuixote_dialogue == null || DonQuixote_dialogue.Lines.Count == 0)
        {
            return null;
        }

        LocString DonQuixote_text = DonQuixote_dialogue.Lines[0].LineText;
        return DonQuixote_text == null ? null : DonQuixote_text.LocEntryKey;
    }

    private static AncientDialogue DonQuixote_Build(string DonQuixote_entry, int DonQuixote_lines)
    {
        // 没有对应本地化就整段跳过，避免出现空白对话。
        string DonQuixote_firstKey =
            DonQuixote_entry + ".talk." + DonQuixote_CharEntry + ".0-0.ancient";
        if (!LocString.Exists("ancients", DonQuixote_firstKey))
        {
            if (DonQuixote_Logged.Add(DonQuixote_entry))
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] ancient dialogue loc missing: " + DonQuixote_firstKey, 0);
            }

            return null;
        }

        string[] DonQuixote_sfx = new string[DonQuixote_lines];
        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_lines; DonQuixote_i++)
        {
            DonQuixote_sfx[DonQuixote_i] = string.Empty;
        }

        return new AncientDialogue(DonQuixote_sfx)
        {
            // 每次遇到都触发：VisitIndex = 0 + IsRepeating = true。
            VisitIndex = 0,
            IsRepeating = true,
        };
    }

    /// <summary>
    /// 启动自检：把每个先古的对话集读一遍并把结果写进日志，便于定位。
    /// </summary>
    internal static void DonQuixote_SelfTest()
    {
        try
        {
            foreach (AncientEventModel DonQuixote_ancient in ModelDb.AllAncients.ToList())
            {
                try
                {
                    if (DonQuixote_ancient == null)
                    {
                        continue;
                    }

                    string DonQuixote_entry = DonQuixote_ancient.Id.Entry;
                    AncientDialogueSet DonQuixote_set = DonQuixote_ancient.DialogueSet;
                    int DonQuixote_count = -1;
                    IReadOnlyList<AncientDialogue> DonQuixote_list;
                    if (DonQuixote_set.CharacterDialogues != null
                        && DonQuixote_set.CharacterDialogues.TryGetValue(DonQuixote_CharEntry, out DonQuixote_list)
                        && DonQuixote_list != null)
                    {
                        DonQuixote_count = DonQuixote_list.Count;
                    }

                    int DonQuixote_valid = -1;
                    bool DonQuixote_picked = false;
                    try
                    {
                        ModelId DonQuixote_charId = ModelDb.Character<DonQuixoteCharacter>().Id;
                        List<AncientDialogue> DonQuixote_pool = DonQuixote_set
                            .GetValidDialogues(DonQuixote_charId, 3, 9, true)
                            .ToList();
                        DonQuixote_valid = DonQuixote_pool.Count;

                        // 玩家遇到先古时选中的一定是「第一行文本键里带角色 Entry」的那条。
                        DonQuixote_picked = DonQuixote_pool.Any(
                            DonQuixote_d => DonQuixote_d != null
                                && DonQuixote_d.Lines.Count > 0
                                && DonQuixote_d.Lines[0].LineText != null
                                && DonQuixote_d.Lines[0].LineText.LocEntryKey != null
                                && DonQuixote_d.Lines[0].LineText.LocEntryKey.Contains(DonQuixote_CharEntry));
                    }
                    catch (Exception DonQuixote_inner)
                    {
                        DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                            "[DonQuixote] ancient selftest " + DonQuixote_entry
                            + " pool failed: " + DonQuixote_inner.Message, 0);
                    }

                    DonQuixoteModEntry.DonQuixote_Logger?.Info(
                        "[DonQuixote] ancient selftest " + DonQuixote_entry
                        + " mine=" + DonQuixote_count + " valid=" + DonQuixote_valid
                        + " picked=" + DonQuixote_picked, 0);
                }
                catch (Exception DonQuixote_ex)
                {
                    DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                        "[DonQuixote] ancient selftest failed: " + DonQuixote_ex.Message, 0);
                }
            }
        }
        catch (Exception DonQuixote_exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] ancient selftest aborted: " + DonQuixote_exception.Message, 0);
        }
    }
}

/// <summary>
/// 兜底：即使 visit 计数把它排到了「首次见面对话」后面，也保证选出堂吉诃德的那条。
/// </summary>
[HarmonyPatch(typeof(AncientDialogueSet), nameof(AncientDialogueSet.GetValidDialogues))]
internal static class DonQuixoteAncientDialogueSelectPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        AncientDialogueSet __instance,
        ModelId characterId,
        ref IEnumerable<AncientDialogue> __result)
    {
        try
        {
            if (__instance == null || characterId == null)
            {
                return;
            }

            if (characterId.Entry != DonQuixoteAncientDialoguePatch.DonQuixote_CharEntry)
            {
                return;
            }

            IReadOnlyList<AncientDialogue> DonQuixote_mine;
            if (__instance.CharacterDialogues == null
                || !__instance.CharacterDialogues.TryGetValue(
                    DonQuixoteAncientDialoguePatch.DonQuixote_CharEntry, out DonQuixote_mine)
                || DonQuixote_mine == null
                || DonQuixote_mine.Count == 0)
            {
                return;
            }

            List<AncientDialogue> DonQuixote_list =
                __result == null ? new List<AncientDialogue>() : __result.ToList();
            if (DonQuixote_list.Count == DonQuixote_mine.Count
                && DonQuixote_list.All(DonQuixote_d => DonQuixote_mine.Contains(DonQuixote_d)))
            {
                return;
            }

            // 游戏在 NEventRoom 里用 Rng.Chaotic.NextItem 从这个池子里随机抽一条。
            __result = new List<AncientDialogue>(DonQuixote_mine);
        }
        catch (Exception DonQuixote_exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] ancient dialogue select patch failed: " + DonQuixote_exception.Message, 0);
        }
    }
}
