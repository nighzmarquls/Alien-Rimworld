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
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFailCondition(() => Door == null || Door.Open || Door.HoldOpen || !CompMatureMorph.IsPathRecoveryDoorCandidate(pawn, Door, requireAvailability: false));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(delegate
            {
                XMTDoorUtility.ForceHoldOpenAndOpen(Door, pawn);
                pawn.GetMorphComp()?.ClearPathRecovery();
            });
        }
    }
}
