using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_OpenDoorMischief : JobDriver
    {
        private Building_Door Door => TargetThingA as Building_Door;
        private IntVec3 InteractionCell => job.GetTarget(TargetIndex.B).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Door == null || !FeralJobUtility.IsThingAvailableForJobBy(pawn, Door) || !InteractionCell.IsValid)
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
            AddFailCondition(() => Door == null || Door.Open || Door.HoldOpen || !XMTMischiefUtility.IsDarkEnoughForMischief(InteractionCell, pawn.Map));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Do(delegate
            {
                XMTDoorUtility.ForceHoldOpenAndOpen(Door, pawn);
                XMTMischiefUtility.NotifyMischiefCompleted(pawn);
            });
        }
    }
}
