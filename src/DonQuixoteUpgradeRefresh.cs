using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 升级后刷新本模组卡牌所有 DynamicVar 的 PreviewValue。
/// 原版的描述高亮（{X:diff()}）读的是 PreviewValue 而不是 BaseValue，
/// 而基类 DynamicVar.UpdateCardPreview 是空实现，所以自定义数值升级后
/// 描述会一直显示旧的数字，看起来像「升级没生效」。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
internal static class DonQuixoteUpgradeVarRefreshPatch
{
    private static void Postfix(CardModel __instance)
    {
        if (__instance == null || __instance.GetType().Namespace != "DonQuixote.Cards")
        {
            return;
        }

        int DonQuixote_refreshed = 0;
        foreach (DynamicVar DonQuixote_var in __instance.DynamicVars.Values)
        {
            if (DonQuixote_var == null)
            {
                continue;
            }

            DonQuixote_var.PreviewValue = DonQuixote_var.BaseValue;
            DonQuixote_refreshed++;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] upgrade refresh " + __instance.GetType().Name + " vars=" + DonQuixote_refreshed, 0);
    }
}
