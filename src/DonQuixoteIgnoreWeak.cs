using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DonQuixote;

/// <summary>
/// 「虚弱」在原版里和力量、易伤共用同一套伤害修正钩子（都是 ModifyDamage*）。
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
