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
    public class JobDriver_LarvaImplant : JobDriver
    {
        public Pawn Prey
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing as Pawn;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedOrNull(TargetIndex.A)
                .FailOn(() => Prey == null || pawn == null || pawn.Dead)
                .FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return AttemptEmbrace();
        }

        private Toil AttemptEmbrace()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                Pawn prey = Prey;
                if (prey == null || pawn == null || pawn.Dead)
                {
                    ReadyForNextToil();
                    return;
                }

                CompLarvalGenes LarvalGenes = pawn.GetComp<CompLarvalGenes>();
                if (LarvalGenes != null && !LarvalGenes.latched)
                {
                    LarvalGenes.TryEmbrace(prey);
                    ReadyForNextToil();
                    return;
                }

                CompHostHunter HostHunter = pawn.GetComp<CompHostHunter>();
                if(HostHunter != null)
                {
                    HostHunter.TryAttachToHost(prey);
                    ReadyForNextToil();
                    return;
                }   

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
