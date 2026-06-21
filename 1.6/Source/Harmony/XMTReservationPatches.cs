using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTReservationPatches
    {
        private static void ClearJobTarget(JobDriver jobDriver, TargetIndex targetIndex)
        {
            if (jobDriver?.pawn?.MapHeld == null || jobDriver.job == null)
            {
                return;
            }

            FeralJobUtility.ClearFeralJobReservationsForTarget(jobDriver.pawn.MapHeld, jobDriver.job.GetTarget(targetIndex));
        }

        [HarmonyPatch(typeof(JobDriver_DeliverPawnToAltar), nameof(JobDriver_DeliverPawnToAltar.TryMakePreToilReservations))]
        public static class Patch_JobDriver_DeliverPawnToAltar_TryMakePreToilReservations
        {
            [HarmonyPostfix]
            public static void Postfix(JobDriver_DeliverPawnToAltar __instance)
            {
                ClearJobTarget(__instance, TargetIndex.A);
            }
        }

        [HarmonyPatch(typeof(JobDriver_DeliverPawnToCell), nameof(JobDriver_DeliverPawnToCell.TryMakePreToilReservations))]
        public static class Patch_JobDriver_DeliverPawnToCell_TryMakePreToilReservations
        {
            [HarmonyPostfix]
            public static void Postfix(JobDriver_DeliverPawnToCell __instance)
            {
                ClearJobTarget(__instance, TargetIndex.A);
            }
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.ReleaseReservations))]
        public static class Patch_Pawn_JobTracker_ReleaseReservations
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn ___pawn, LocalTargetInfo reservedItem)
            {
                FeralJobUtility.ClearFeralJobReservationsClaimedByOnTarget(___pawn, reservedItem);
            }
        }

        [HarmonyPatch(typeof(Pawn), nameof(Pawn.VerifyReservations))]
        public static class Patch_Pawn_VerifyReservations
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn __instance, Job prevJob)
            {
                if (!XMTUtility.IsXenomorph(__instance))
                {
                    return;
                }

                FeralJobUtility.ClearFeralPhysicalInteractionReservationsClaimedBy(__instance.MapHeld, __instance, prevJob);
            }
        }
    }
}
