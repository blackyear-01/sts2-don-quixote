using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Unlocks;

namespace DonQuixote.Events;

/// <summary>
/// 把先古「巴里」塞进荣耀（第三层）的先古池。
/// </summary>
[HarmonyPatch(typeof(Glory), nameof(Glory.GetUnlockedAncients))]
internal static class DonQuixoteGloryAncientPoolPatch
{
    private static void Postfix(ref IEnumerable<AncientEventModel> __result)
    {
        __result = DonQuixoteBarryAncientPatch.DonQuixote_Append(__result);
    }
}

[HarmonyPatch(typeof(Glory), "get_AllAncients")]
internal static class DonQuixoteGloryAllAncientsPatch
{
    private static void Postfix(ref IEnumerable<AncientEventModel> __result)
    {
        __result = DonQuixoteBarryAncientPatch.DonQuixote_Append(__result);
    }
}

internal static class DonQuixoteBarryAncientPatch
{
    private static bool DonQuixote_Warned;

    public static IEnumerable<AncientEventModel> DonQuixote_Append(IEnumerable<AncientEventModel> DonQuixote_source)
    {
        try
        {
            AncientEventModel DonQuixote_barry = ModelDb.AncientEvent<DonQuixoteAncientBarry>();
            if (DonQuixote_barry == null)
            {
                return DonQuixote_source;
            }

            List<AncientEventModel> DonQuixote_list = DonQuixote_source?.ToList()
                ?? new List<AncientEventModel>();

            if (DonQuixote_list.Any(DonQuixote_a => ReferenceEquals(DonQuixote_a, DonQuixote_barry)
                                                      || DonQuixote_a?.Id == DonQuixote_barry.Id))
            {
                return DonQuixote_list;
            }

            DonQuixote_list.Add(DonQuixote_barry);
            return DonQuixote_list;
        }
        catch (Exception DonQuixote_Exception)
        {
            if (!DonQuixote_Warned)
            {
                DonQuixote_Warned = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] glory ancient pool patch failed: " + DonQuixote_Exception.Message, 0);
            }

            return DonQuixote_source;
        }
    }
}
