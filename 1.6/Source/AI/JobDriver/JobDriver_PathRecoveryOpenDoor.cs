using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_PathRecoveryOpenDoor : JobDriver
    {
        private Building_Door Door => TargetThingA as Building_Door;
        private IntVec3 InteractionCell => job.GetTarget(TargetIndex.B).Cell;
        private IntVec3 GoalCell => job.GetTarget(TargetIndex.C).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Door == null || !InteractionCell.IsValid || !FeralJobUtility.IsThingAvailableForJobBy(pawn, Door))
            {
                return false;
            }

            if (!FeralJobUtility.ReservePlaceForJob(pawn, job, InteractionCell))
            {
                return false;
            }

            FeralJobUtility.ReserveThingForJob(pawn, job, Door);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            bool openedByJob = false;
            IntVec3 passageDestination = IntVec3.Invalid;

            AddFailCondition(() => !openedByJob && (Door == null || Door.Destroyed || Door.Open || Door.HoldOpen || !MatureMorphPathRecovery.IsPathRecoveryDoorCandidate(pawn, Door, requireAvailability: false)));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(delegate
            {
                Building_Door door = Door;
                if (door == null || door.Destroyed)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                XMTDoorUtility.ForceHoldOpenAndOpen(Door, pawn);
                openedByJob = true;
                PathRecoveryJobUtility.TryFindPassageDestination(pawn, door.OccupiedRect(), InteractionCell, requireSafeExit: false, goalCell: GoalCell, out passageDestination);
                pawn.GetMorphComp()?.ClearPathRecovery();
            });
            yield return PathRecoveryJobUtility.GotoCellIfValid(() => passageDestination);
        }
    }
}
