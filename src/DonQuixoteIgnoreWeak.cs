using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DonQuixote;

/// <summary>
/// 「虚弱」在原版里和力量、易伤共用同一套伤害修正钩子（都是 ModifyDamage*），
/// 没有单独的开关。被刺穿吧的百分比伤害要「吃力量/易伤/堕落增伤，但不吃虚弱减伤」，
/// 所以只在这一次结算期间把施法者身上虚弱的倍率还原成 1（含纸鹤一类的虚弱强化）。
/// </summary>
[HarmonyPatch(typeof(WeakPower), nameof(WeakPower.ModifyDamageMultiplicative))]
internal static class DonQuixoteIgnoreWeakPatch
{
    private static void Postfix(WeakPower __instance, ref decimal __result)
    {
        if (!DonQuixoteBloodMagic.DonQuixote_SkipWeakReduction || __instance == null)
        {
            return;
        }

        Creature DonQuixote_owner = __instance.Owner;
        Creature DonQuixote_dealer = DonQuixoteBloodMagic.DonQuixote_SkipWeakDealer;
        if (DonQuixote_owner != null && DonQuixote_dealer != null && ReferenceEquals(DonQuixote_owner, DonQuixote_dealer))
        {
            __result = 1m;
        }
    }
}
