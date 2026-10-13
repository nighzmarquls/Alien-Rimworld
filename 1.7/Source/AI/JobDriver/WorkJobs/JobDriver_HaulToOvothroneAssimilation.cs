using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_HaulToOvothroneAssimilation : JobDriver
    {
        private const TargetIndex ItemInd = TargetIndex.A;
        private const TargetIndex ThroneInd = TargetIndex.B;
        private const TargetIndex InteractionCellInd = TargetIndex.C;

        private Thing Item => job.GetTarget(ItemInd).Thing;
        private EggSack Throne => job.GetTarget(ThroneInd).Thing as EggSack;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(ItemInd), job, 1, job.count, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(ThroneInd), job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(InteractionCellInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(ItemInd);
            this.FailOnDestroyedOrNull(ThroneInd);
            this.FailOn(() => !TryGetQueen(out Pawn _, out CompQueenAssimilation _));

            Toil gotoItem = Toils_Goto.GotoThing(ItemInd, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(ItemInd)
                .FailOn(() => !CanQueenUseItem(Item));
            yield return gotoItem;

            yield return Toils_Haul.StartCarryThing(ItemInd, subtractNumTakenFromJobCount: true)
                .FailOn(() => !CanQueenUseItem(Item));

            yield return Toils_Goto.GotoCell(InteractionCellInd, PathEndMode.OnCell)
                .FailOn(() => pawn.carryTracker.CarriedThing == null);

            yield return DropCarriedThing();

            yield return Toils_General.Do(delegate
            {
                if (TryGetQueen(out Pawn _, out CompQueenAssimilation comp) && CanQueenUseItem(Item))
                {
                    comp.Assimilate(Item, job.playerForced);
                }
            });
        }

        private bool TryGetQueen(out Pawn queen, out CompQueenAssimilation comp)
        {
            return OvothroneAssimilationHaulUtility.TryGetAssimilationComp(Throne, pawn, out queen, out comp);
        }

        private bool CanQueenUseItem(Thing item)
        {
            return TryGetQueen(out Pawn queen, out CompQueenAssimilation comp)
                && OvothroneAssimilationHaulUtility.QueenCanBenefitFromThing(item, queen, comp, out QueenAssimilationDef _, out int _);
        }

        private Toil DropCarriedThing()
        {
            Toil toil = ToilMaker.MakeToil("DropOvothroneAssimilationTarget");
            toil.initAction = delegate
            {
                Thing carriedThing = pawn.carryTracker.CarriedThing;
                if (carriedThing == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (pawn.carryTracker.TryDropCarriedThing(job.GetTarget(InteractionCellInd).Cell, ThingPlaceMode.Near, out Thing droppedThing))
                {
                    job.SetTarget(ItemInd, droppedThing);
                    ReadyForNextToil();
                    return;
                }

                EndJobWith(JobCondition.Incompletable);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
