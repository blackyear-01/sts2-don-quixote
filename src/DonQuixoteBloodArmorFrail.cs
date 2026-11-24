using System;
using DonQuixote.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DonQuixote;

/// <summary>
/// 血铠：生效期间，护甲的结算不受「脆弱」削减。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class DonQuixoteBloodArmorFrailPatch
{
    private const decimal DonQuixote_FrailFactor = 0.75m;

    private static void Postfix(
        Creature __1,
        decimal __2,
        ValueProp __3,
        ref decimal __result)
    {
        // 摇曳【血魔】的护盾要求完全裸值结算：直接返回原始数值。
        if (DonQuixoteBloodMagic.DonQuixote_RawBlockGain)
        {
            if (__result != __2)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] wavering block ignores all modifiers: " + __result + " -> " + __2, 0);
            }

            __result = __2;
            return;
        }

        if (__1 == null || __2 <= 0m)
        {
            return;
        }

        DonQuixotePowerBloodArmor DonQuixote_armor = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBloodArmor>(__1);
        if (DonQuixote_armor == null || DonQuixote_armor.Amount <= 0)
        {
            return;
        }

        if (DonQuixoteBloodMagic.DonQuixote_Find<FrailPower>(__1) == null)
        {
            return;
        }

        if (!ValuePropExtensions.IsPoweredCardOrMonsterMoveBlock(__3))
        {
            return;
        }

        decimal DonQuixote_restored = Math.Ceiling(__result / DonQuixote_FrailFactor);
        if (DonQuixote_restored > __result)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] blood armor ignores frail: " + __result + " -> " + DonQuixote_restored, 0);
            __result = DonQuixote_restored;
        }
    }
}
