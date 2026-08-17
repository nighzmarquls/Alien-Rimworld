using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_LoadPawnIntoBioContainer : JobDriver
    {
        private const TargetIndex PawnIndex = TargetIndex.A;
        private const TargetIndex ContainerIndex = TargetIndex.B;

        private Pawn TargetPawn => job.GetTarget(PawnIndex).Pawn;
        private Building_BioContainer Container => job.GetTarget(ContainerIndex).Thing as Building_BioContainer;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, errorOnFailed: errorOnFailed) &&
                pawn.Reserve(Container, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(PawnIndex);
            this.FailOnDestroyedOrNull(ContainerIndex);
            this.FailOn(() => Container.ContainedThing != null || Container.BioContainerComp?.CanContain(TargetPawn) != true);

            yield return Toils_Goto.GotoThing(PawnIndex, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(PawnIndex);
            yield return Toils_Goto.GotoThing(ContainerIndex, PathEndMode.Touch);

            Toil insert = ToilMaker.MakeToil("LoadPawnIntoBioContainer");
            insert.initAction = delegate
            {
                if (Container.TryAcceptPawn(TargetPawn, medicalExtraction: false))
                {
                    return;
                }

                if (pawn.carryTracker.CarriedThing == TargetPawn)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _, null);
                }
                EndJobWith(JobCondition.Incompletable);
            };
            insert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insert;
        }
    }
}
