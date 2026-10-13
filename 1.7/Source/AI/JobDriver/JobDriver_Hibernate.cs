using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_Hibernate : JobDriver
    {
        float workDone;
        CompMatureMorph pawnMorph;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            IntVec3 hibernationCell = job.GetTarget(TargetIndex.A).Cell;
            if (pawn.MapHeld != null && pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, hibernationCell))
            {
                return true;
            }

            return FeralJobUtility.ReservePlaceForJob(pawn, job, hibernationCell);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !HibernationCellAvailable());

            yield return Toils_Goto.GotoCell(TargetA.Cell, PathEndMode.OnCell);
            yield return FormCocoon();
        }

        private bool HibernationCellAvailable()
        {
            Map map = pawn?.Map;
            IntVec3 cell = job.GetTarget(TargetIndex.A).Cell;
            if (map == null || !cell.InBounds(map) || cell.GetEdifice(map) != null)
            {
                return false;
            }

            Pawn occupyingPawn = cell.GetFirstPawn(map);
            return occupyingPawn == null || occupyingPawn == pawn;
        }

        private Toil FormCocoon()
        {
            pawnMorph = pawn.GetMorphComp();
            Toil toil = ToilMaker.MakeToil("FormCocoon");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {

                workDone += 0.0004f;
                if (workDone > 1)
                {
                    IntVec3 cell = job.GetTarget(TargetIndex.A).Cell;
                    bool ownsCell = pawn?.MapHeld != null &&
                                    pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, cell);
                    if (!ownsCell || pawn.Position != cell || !HibernationCellAvailable() ||
                        pawnMorph?.TryEnterHibernation(cell) != true)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            toil.PlaySustainerOrSound(() => SoundDefOf.Vomit);
            return toil;
        }
    }
}
