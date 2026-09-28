using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_TransferContainedPawn : JobDriver
    {
        private const TargetIndex SourceIndex = TargetIndex.A;
        private const TargetIndex DestinationIndex = TargetIndex.B;
        private const TargetIndex PawnIndex = TargetIndex.C;

        private Thing Source => job.GetTarget(SourceIndex).Thing;
        private Thing Destination => job.GetTarget(DestinationIndex).Thing;
        private Pawn Occupant => job.GetTarget(PawnIndex).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Source, job, errorOnFailed: errorOnFailed) &&
                pawn.Reserve(Destination, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(SourceIndex);
            this.FailOnDestroyedOrNull(DestinationIndex);

            yield return Toils_Goto.GotoThing(SourceIndex, PathEndMode.Touch);

            Toil wait = Toils_General.WaitWith(SourceIndex, 120, useProgressBar: true);
            wait.FailOn(() => XMTContainmentUtility.HeldPawn(Source) != Occupant ||
                !XMTContainmentUtility.IsTransferDestination(Destination, Occupant));
            yield return wait;

            Toil release = ToilMaker.MakeToil("RemovePawnFromSourceHolder");
            release.initAction = delegate
            {
                if (XMTContainmentUtility.HeldPawn(Source) != Occupant ||
                    !XMTContainmentUtility.IsTransferDestination(Destination, Occupant) ||
                    !XMTContainmentUtility.Eject(Occupant))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            release.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return release;

            yield return Toils_Goto.GotoThing(PawnIndex, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(PawnIndex);
            yield return Toils_Goto.GotoThing(DestinationIndex, PathEndMode.Touch);

            Toil place = ToilMaker.MakeToil("PlacePawnInDestinationHolder");
            place.initAction = delegate
            {
                bool accepted = Destination is Building_Bed bed
                    ? TryPlaceInPrisonerBed(bed, Occupant)
                    : XMTContainmentUtility.TryAcceptPawn(Destination, Occupant);
                if (!accepted)
                {
                    if (pawn.carryTracker.CarriedThing == Occupant)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near,
                            out Thing _, null);
                    }
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            place.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return place;
        }

        private bool TryPlaceInPrisonerBed(Building_Bed bed, Pawn target)
        {
            if (bed == null || !XMTContainmentUtility.IsTransferDestination(bed, target) ||
                pawn.carryTracker.CarriedThing != target)
            {
                return false;
            }

            IntVec3 dropCell = IntVec3.Invalid;
            for (int i = 0; i < bed.SleepingSlotsCount; i++)
            {
                IntVec3 cell = bed.GetSleepingSlotPos(i);
                if (bed.GetCurOccupantAt(cell) == null)
                {
                    dropCell = cell;
                    break;
                }
            }
            if (!dropCell.IsValid || !pawn.carryTracker.TryDropCarriedThing(dropCell,
                ThingPlaceMode.Direct, out Thing _, null))
            {
                return false;
            }

            target.ownership?.ClaimBedIfNonMedical(bed);
            return true;
        }
    }
}
