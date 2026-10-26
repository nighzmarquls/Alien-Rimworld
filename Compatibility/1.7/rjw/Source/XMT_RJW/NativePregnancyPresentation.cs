using HarmonyLib;
using Verse;
using Xenomorphtype;

namespace XMT_RJW
{
    // Upstream pregnancy classes cannot inherit our native presentation adapters.
    // Only the pregnancy defs registered by this integration enter the shared presentation path.
    internal static class NativePregnancyPresentation
    {
        private static bool Applies(Hediff hediff) =>
            hediff.def.modExtensions?.Contains(RJWReproductionDef.Current.corruptedPregnancyPresentation) == true &&
            hediff.TryGetComp<HediffComp_CorruptedPregnancy>()?.IsHorror == true;

        [HarmonyPatch(typeof(Hediff), nameof(Hediff.LabelBase), MethodType.Getter)]
        private static class Label
        {
            private static void Postfix(Hediff __instance, ref string __result)
            {
                if (Applies(__instance)) __result = MutationTissueAssessment.HealthLabel(__instance, __result);
            }
        }

        [HarmonyPatch(typeof(Hediff), nameof(Hediff.Description), MethodType.Getter)]
        private static class Description
        {
            private static void Postfix(Hediff __instance, ref string __result)
            {
                if (Applies(__instance)) __result = MutationTissueAssessment.HealthDescription(__instance, __result);
            }
        }
    }
}
