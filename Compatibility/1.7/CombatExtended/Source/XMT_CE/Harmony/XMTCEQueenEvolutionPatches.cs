using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace XMT_CE
{
    [HarmonyPatch(typeof(CompAbilityOverrun), "BaseEdificeDamage")]
    internal static class XMTCEQueenOverrunDamagePatch
    {
        private const float VanillaReferenceBluntArmor = 0.8f;
        private const float CEReferenceBluntArmor = 70f;
        private const float MaximumArmorDamageFactor = 2f;

        [HarmonyPostfix]
        private static void NormalizeCEArmorScale(Pawn queen, ref float __result)
        {
            if (queen == null || __result <= 0f)
            {
                return;
            }

            float ceBluntArmor = Mathf.Max(0f, queen.GetStatValue(StatDefOf.ArmorRating_Blunt));
            if (ceBluntArmor <= 0f)
            {
                return;
            }

            float normalizedArmorFactor = Mathf.Min(
                MaximumArmorDamageFactor,
                ceBluntArmor * VanillaReferenceBluntArmor / CEReferenceBluntArmor);

            __result = Mathf.Max(0f, Mathf.Round(__result * normalizedArmorFactor / ceBluntArmor));
        }
    }
}
