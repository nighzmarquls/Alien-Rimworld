using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    internal class JobDriver_MoveBuilding : JobDriver
    {
        private const TargetIndex ItemInd = TargetIndex.A;

        private const TargetIndex InstallIndex = TargetIndex.B;

        protected Thing Item => job.GetTarget(TargetIndex.A).Thing;

        private bool StorageMove => job.GetTarget(TargetIndex.C).IsValid;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn?.MapHeld == null || Item == null || !InstallIndexCell.IsValid)
            {
                return false;
            }

            if (!pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, Item))
            {
                if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, Item))
                {
                    return false;
                }

                FeralJobUtility.ReserveThingForJob(pawn, job, Item);
            }

            if (!pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, InstallIndexCell))
            {
                return FeralJobUtility.ReservePlaceForJob(pawn, job, InstallIndexCell);
            }

            return true;
        }

        private IntVec3 InstallIndexCell => job.GetTarget(InstallIndex).Cell;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return RevalidateDestination();
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return PlaceFromMinifiedThing();
        }

        private Toil RevalidateDestination()
        {
            Toil toil = ToilMaker.MakeToil("RevalidateOvomorphDestination");
            toil.initAction = delegate
            {
                MinifiedThing minifiedThing = CarriedMinifiedThing();
                Thing innerThing = minifiedThing?.InnerThing;
                if (innerThing == null)
                {
                    DropCarriedThingAndFail();
                    return;
                }

                bool destinationValid = StorageMove
                    ? XMTZoneUtility.CanInstallStorageThingAt(innerThing, InstallIndexCell, pawn)
                    : XMTZoneUtility.CanInstallMovedThingAt(innerThing, InstallIndexCell, pawn);

                if (destinationValid)
                {
                    return;
                }

                if (StorageMove &&
                    XMTZoneUtility.TryFindStorageDestination(innerThing, pawn, out IntVec3 replacement) &&
                    FeralJobUtility.ReservePlaceForJob(pawn, job, replacement))
                {
                    job.SetTarget(TargetIndex.B, replacement);
                    job.SetTarget(TargetIndex.C, replacement);
                    return;
                }

                DropCarriedThingAndFail();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil PlaceFromMinifiedThing()
        {
            Toil toil = ToilMaker.MakeToil("InstallMovedOvomorph");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                MinifiedThing minifiedThing = CarriedMinifiedThing();
                Thing innerThing = minifiedThing?.InnerThing;
                if (innerThing == null)
                {
                    DropCarriedThingAndFail();
                    return;
                }

                bool destinationValid = StorageMove
                    ? XMTZoneUtility.CanInstallStorageThingAt(innerThing, InstallIndexCell, pawn)
                    : XMTZoneUtility.CanInstallMovedThingAt(innerThing, InstallIndexCell, pawn);
                if (!destinationValid)
                {
                    DropCarriedThingAndFail();
                    return;
                }

                if (minifiedThing.Spawned)
                {
                    minifiedThing.DeSpawn();
                }

                XMTZoneUtility.MoveLooseItemsAside(innerThing, InstallIndexCell, pawn.Map);
                Thing placedBuilding = GenSpawn.Spawn(
                    innerThing,
                    InstallIndexCell,
                    pawn.Map,
                    WipeMode.VanishOrMoveAside);
                if (placedBuilding == null)
                {
                    DropCarriedThingAndFail();
                    return;
                }

                minifiedThing.InnerThing = null;
                if (pawn.carryTracker.CarriedThing == minifiedThing)
                {
                    pawn.carryTracker.innerContainer.Remove(minifiedThing);
                }

                minifiedThing.Destroy();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private MinifiedThing CarriedMinifiedThing()
        {
            return pawn?.carryTracker?.CarriedThing as MinifiedThing ??
                   job.GetTarget(TargetIndex.A).Thing as MinifiedThing;
        }

        private void DropCarriedThingAndFail()
        {
            if (pawn?.carryTracker?.CarriedThing != null && pawn.MapHeld != null)
            {
                pawn.carryTracker.TryDropCarriedThing(
                    pawn.Position,
                    ThingPlaceMode.Near,
                    out Thing _,
                    null);
            }

            EndJobWith(JobCondition.Incompletable);
        }
    }
}
