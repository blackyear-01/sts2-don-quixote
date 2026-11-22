using System;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DonQuixote;

/// <summary>
/// 「不能动」的敌方增益黑名单：被这些效果挡下的状态，既不能被
/// 「公平的决斗」复制，也不能被「伤口破坏」解除。
///
/// 三类会被挡下：
/// 1) 本模组自己的资源类状态（血宴 / 堕落值 / 摇曳等，拿走或复制都没有意义还容易出 bug）；
/// 2) InstanceType 不是 None 的状态（多实例状态 / 与施加者绑定的状态，动它就会破坏绑定）；
/// 3) 与原版怪物强绑定、动一下就出问题的状态（下面显式列出）。
///    其中最典型的是沙坑（<see cref="SandpitPower"/>）：它是无厌沙虫吞人的计数器，
///    复制给玩家或从沙虫身上解掉都会让战斗流程错乱。
/// </summary>
internal static class DonQuixotePowerBlacklist
{
    /// <summary>按类型拉黑。</summary>
    internal static readonly Type[] DonQuixote_BlockedTypes =
    {
        typeof(SandpitPower),
        typeof(MinionPower),
        typeof(IllusionPower),
        typeof(SummonNextTurnPower),
        typeof(SteamEruptionPower),
        typeof(KnockdownPower),
        typeof(PersonalHivePower),
        typeof(VitalSparkPower),
        typeof(ReattachPower),
        typeof(PaperCutsPower),
        typeof(PainfulStabsPower),
        typeof(AdaptablePower),
        typeof(GalvanicPower),
    };

    /// <summary>按条目 id 拉黑：连怪物 / 其他模组的状态也能覆盖。</summary>
    internal static readonly string[] DonQuixote_BlockedIds =
    {
        "SANDPIT_POWER",
        "PERSONAL_HIVE_POWER",
        "VITAL_SPARK_POWER",
        "REATTACH_POWER",
        "PAPER_CUTS_POWER",
        "PAINFUL_STABS_POWER",
        "ADAPTABLE_POWER",
        "GALVANIC_POWER",
        "LIVING_SHIELD",
        "LIVING_SHIELD_POWER",
    };

    /// <summary>标题兜底黑名单：id 与类型都猜不到时（例如只存在于其他模组的状态），按显示名挡。</summary>
    internal static readonly string[] DonQuixote_BlockedTitles =
    {
        "沙坑",
        "人体蜂房",
        "活力火花",
        "接续",
        "纸伤难愈",
        "活体盾",
        "留电",
        "疼痛戳刺",
        "适者生存",
        "流电",
        "Sandpit",
        "Personal Hive",
        "Vital Spark",
        "Reattach",
        "Paper Cuts",
        "Living Shield",
        "Painful Stabs",
        "Adaptable",
        "Galvanic",
    };

    internal static bool DonQuixote_IsBlocked(PowerModel DonQuixote_power)
    {
        if (DonQuixote_power == null)
        {
            return true;
        }

        Type DonQuixote_type = DonQuixote_power.GetType();

        // 本模组的状态一律不碰。
        if (DonQuixote_type.Namespace == "DonQuixote.Powers")
        {
            return true;
        }

        // 多实例 / 与施加者绑定的状态。
        if (DonQuixote_power.InstanceType != PowerInstanceType.None)
        {
            return true;
        }

        foreach (Type DonQuixote_blocked in DonQuixote_BlockedTypes)
        {
            if (DonQuixote_type == DonQuixote_blocked)
            {
                return true;
            }
        }

        string DonQuixote_entry = DonQuixote_power.Id != null ? DonQuixote_power.Id.Entry : string.Empty;
        if (!string.IsNullOrEmpty(DonQuixote_entry))
        {
            foreach (string DonQuixote_blockedId in DonQuixote_BlockedIds)
            {
                if (string.Equals(DonQuixote_entry, DonQuixote_blockedId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        try
        {
            string DonQuixote_title = DonQuixote_power.Title != null ? DonQuixote_power.Title.GetRawText() : null;
            if (!string.IsNullOrEmpty(DonQuixote_title))
            {
                string DonQuixote_trimmed = DonQuixote_title.Trim();
                foreach (string DonQuixote_blockedTitle in DonQuixote_BlockedTitles)
                {
                    if (DonQuixote_trimmed.IndexOf(DonQuixote_blockedTitle, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 标题取不到就只按类型/id 判定。
        }

        return false;
    }
}
