using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XMT_RJW;

namespace XMT_Menstruation
{
    public static class EverFertile
    {
        // Only aging is bypassed. Existing anatomy, reproductive maturity, contraception,
        // pregnancy/recovery stages, and unrelated infertility still use the native rules.
        public static bool Applies(Pawn pawn) => pawn?.ageTracker?.CurLifeStage?.reproductive == true &&
            CryptimorphTextures.IsEligible(pawn);
        private static bool Applies(HediffComp womb) => Applies(womb?.Pawn) &&
            RJWApiShim.Read(womb.props, "infertile") is not true;
        public static int HealthyReserve(int reserve, int threshold) => Math.Max(reserve, Math.Max(1, threshold));

        [HarmonyPatch]
        private static class PreserveEggHealth
        {
            private static MethodBase TargetMethod() => AccessTools.PropertyGetter(MenstruationApiShim.WombType, "EggHealth");
            private static void Postfix(HediffComp __instance, ref float __result)
            {
                if (Applies(__instance)) __result = Math.Max(1f, __result);
            }
        }

        [HarmonyPatch]
        private static class InitialReserve
        {
            private static MethodBase TargetMethod() => AccessTools.Method(MenstruationApiShim.WombType, "GetOvaryPowerByAge");
            private static void Postfix(HediffComp __instance, ref int __result)
            {
                if (Applies(__instance)) __result = HealthyReserve(__result, MenstruationApiShim.ReserveThreshold(__instance));
            }
        }

        [HarmonyPatch]
        private static class RenewReserve
        {
            private static MethodBase TargetMethod() => AccessTools.Method(MenstruationApiShim.WombType, "BeforeSimulator");
            private static void Prefix(HediffComp __instance)
            {
                if (Applies(__instance)) MenstruationApiShim.RenewReserve(__instance);
            }
        }

        [HarmonyPatch]
        private static class AgeFertility
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(StatPart_FertilityByGenderAge), "AgeFactor", new[] { typeof(Pawn) });
                yield return AccessTools.Method(AccessTools.TypeByName("rjw.PawnCapacityWorker_Fertility"), "CalculateAgeImpact", new[] { typeof(Pawn) });
            }
            private static void Postfix(Pawn pawn, ref float __result)
            {
                if (Applies(pawn)) __result = 1f;
            }
        }

        [HarmonyPatch]
        private static class FertilityLabel
        {
            private static MethodBase TargetMethod() => AccessTools.PropertyGetter(MenstruationApiShim.WombType, "GetCurStageLabel");
            private static void Postfix(HediffComp __instance, ref string __result)
            {
                if (Applies(__instance)) __result += " (" + "XMT_EverFertile".Translate() + ")";
            }
        }
    }
}
