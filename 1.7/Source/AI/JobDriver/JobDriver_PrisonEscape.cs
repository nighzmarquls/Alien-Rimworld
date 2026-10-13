using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_PrisonEscape : JobDriver
    {
        private IntVec3 Destination => job.GetTarget(TargetIndex.A).Cell;
        private Building_Door Door => job.GetTarget(TargetIndex.B).Thing as Building_Door;
        private IntVec3 InteractionCell => job.GetTarget(TargetIndex.C).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!Destination.IsValid)
            {
                return false;
            }

            if (Door == null)
            {
                return FeralJobUtility.ReservePlaceForJob(pawn, job, Destination);
            }

            if (!XMTDoorUtility.CanForceOpenConventionally(Door) || !InteractionCell.IsValid ||
                !FeralJobUtility.ReservePlaceForJob(pawn, job, InteractionCell) ||
                !FeralJobUtility.ReservePlaceForJob(pawn, job, Destination))
            {
                return false;
            }

            FeralJobUtility.ReserveThingForJob(pawn, job, Door);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !Destination.IsValid);

            if (Door != null)
            {
                this.FailOn(() => Door.Destroyed || !XMTDoorUtility.CanForceOpenConventionally(Door));
                yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
                yield return Toils_General.Do(() => XMTDoorUtility.ForceHoldOpenAndOpen(Door, pawn));
            }

            Toil escapeToil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] Prison escape destination toil for " + pawn +
                    " climbSupport=" + ClimbUtility.HasClimbSupport(escapeToil) +
                    " destination=" + Destination + ".");
            }
            yield return escapeToil;
            yield return Toils_General.Do(() => pawn.GetMorphComp()?.NotifyPrisonEscapeCompleted());
        }
    }
}
