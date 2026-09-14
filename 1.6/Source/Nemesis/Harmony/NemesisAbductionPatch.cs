using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(KidnappedPawnsTracker), nameof(KidnappedPawnsTracker.Kidnap), new System.Type[] { typeof(Pawn), typeof(Pawn) })]
    internal static class NemesisAbductionPatch
    {
        private static void Prefix(Pawn pawn, Pawn kidnapper, out bool __state)
        {
            __state = pawn?.Faction == Faction.OfPlayer && kidnapper != null && XMTUtility.IsXenomorph(kidnapper);
        }

        private static void Postfix(KidnappedPawnsTracker __instance, Pawn pawn, Pawn kidnapper, bool __state)
        {
            if (__state && __instance.KidnappedPawnsListForReading.Contains(pawn))
            {
                NemesisEvidenceReporter.ReportAbduction(pawn, kidnapper);
            }
        }
    }
}
