using HarmonyLib;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherFailed")]
    internal static class XMTPathFailurePatches
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, LocalTargetInfo ___destination, PathEndMode ___peMode, bool ___curPathJobIsStale)
        {
            Job job = ___pawn?.jobs?.curJob;
            if (___pawn == null || ___pawn.Dead || ___pawn.Map == null || ___curPathJobIsStale || job == null ||
                !___destination.IsValid || !XMTUtility.IsXenomorph(___pawn) ||
                job.def == XenoWorkDefOf.XMT_HiveBuilding ||
                job.def == XenoWorkDefOf.XMT_HiveRoofing ||
                job.def == XenoWorkDefOf.XMT_PathRecoveryOpenDoor ||
                job.def == XenoWorkDefOf.XMT_PathRecoveryBreach ||
                job.def == XenoWorkDefOf.XMT_PrisonEscape)
            {
                return;
            }

            ___pawn.GetMorphComp()?.NotifyPathFailure(___destination, job,
                confirmedPatherFailure: true, pathEndMode: ___peMode);
        }
    }
}
