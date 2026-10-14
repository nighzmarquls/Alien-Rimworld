using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTReservationPatches
    {
        [HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.ReleaseAllClaimedBy))]
        public static class Patch_ReservationManager_ReleaseAllClaimedBy
        {
            [HarmonyPrefix]
            public static bool Prefix(ReservationManager __instance, Pawn __0)
            {
                PawnClimber owner = PawnClimber.ReservationOwnerFor(__0);
                if (owner?.PreservingDepartureReservations != true)
                {
                    return true;
                }

                List<Job> jobsToRelease = new List<Job>();
                foreach (ReservationManager.Reservation reservation in __instance.ReservationsReadOnly)
                {
                    if (reservation.Claimant == __0 && reservation.Job != owner.DetachedJob &&
                        !jobsToRelease.Contains(reservation.Job))
                    {
                        jobsToRelease.Add(reservation.Job);
                    }
                }
                foreach (Job job in jobsToRelease)
                {
                    __instance.ReleaseClaimedBy(__0, job);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(EnrouteManager), nameof(EnrouteManager.ReleaseAllClaimedBy))]
        public static class Patch_EnrouteManager_ReleaseAllClaimedBy
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn __0)
            {
                PawnClimber owner = PawnClimber.ReservationOwnerFor(__0);
                return owner == null || !owner.PreservesEnrouteReservations(__0);
            }
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentOrQueuedJob))]
        public static class Patch_Pawn_JobTracker_EndCurrentOrQueuedJob
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn ___pawn, Job __0, JobCondition __1)
            {
                PawnClimber owner = PawnClimber.ReservationOwnerFor(___pawn);
                return owner == null || owner.DetachedJob != __0 || !owner.CancelDetachedJob(__1);
            }
        }

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
