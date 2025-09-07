using RimWorld;
using RimWorld.Planet;
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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }



        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return PlaceFromMinifiedThing();
        }

        private Toil PlaceFromMinifiedThing()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                MinifiedThing thing = pawn.CurJob.targetA.Thing as MinifiedThing;
                if (thing != null)
                {
                    Thing InnerThing = thing.InnerThing;
                    if (InnerThing != null)
                    {
                        if (thing.Spawned)
                        {
                            thing.DeSpawn();
                        }
                        
                        Thing placedBuilding = GenSpawn.Spawn(InnerThing, pawn.CurJob.targetB.Cell, pawn.Map);
                        thing.InnerThing = null;
                        thing.Destroy();
                    }

                }
                ReadyForNextToil();

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}