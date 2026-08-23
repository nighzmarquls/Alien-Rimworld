using HarmonyLib;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTPheromonePatches
    {
        [HarmonyPatch(typeof(Hediff), nameof(Hediff.LabelBase), MethodType.Getter)]
        private static class Patch_Hediff_LabelBase
        {
            [HarmonyPostfix]
            private static void Postfix(Hediff __instance, ref string __result)
            {
                if (PheromoneUtility.TryGetHumanGlandLabel(__instance, out string label))
                {
                    __result = label;
                }
            }
        }

        [HarmonyPatch(typeof(Hediff), nameof(Hediff.Description), MethodType.Getter)]
        private static class Patch_Hediff_Description
        {
            [HarmonyPostfix]
            private static void Postfix(Hediff __instance, ref string __result)
            {
                if (PheromoneUtility.TryGetHumanGlandDescription(__instance, out string description))
                {
                    __result = description;
                }
            }
        }
    }
}
