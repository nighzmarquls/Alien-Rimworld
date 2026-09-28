using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_EnterContainmentHarness : JobDriver
    {
        private const TargetIndex HarnessIndex = TargetIndex.A;

        private Building_ContainmentHarness Harness => job.GetTarget(HarnessIndex).Thing as Building_ContainmentHarness;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Harness, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(HarnessIndex);
            this.FailOn(() => Harness.ContainedThing != null ||
                Harness.ContainmentComp?.CanContain(pawn, requireDowned: false) != true);

            yield return Toils_Goto.GotoThing(HarnessIndex, PathEndMode.Touch);

            Toil enter = ToilMaker.MakeToil("EnterContainmentHarness");
            enter.initAction = delegate
            {
                Pawn actor = enter.actor;
                Building_ContainmentHarness harness = actor.CurJob.targetA.Thing as Building_ContainmentHarness;
                bool despawned = actor.DeSpawnOrDeselect(DestroyMode.Vanish);
                bool accepted = harness.TryAcceptPawn(actor);
                if (despawned && accepted)
                {
                    Find.Selector.Select(actor, playSound: false, forceDesignatorDeselect: false);
                }
            };
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
        }
    }
}
