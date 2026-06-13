
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static Xenomorphtype.CompPawnInfo;

namespace Xenomorphtype
{
    internal class JobDriver_MutateTarget : JobDriver
    {

        private float TicksFinish = 350;
        private float Ticks = 0;
        private float Progress = 0;
        public Thing Target
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }
        public bool IsNoLongerValidTarget()
        {
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(IsNoLongerValidTarget);
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return AttemptInjection();
        }

        private Toil AttemptInjection()
        {
            Toil toil = ToilMaker.MakeToil("AttemptInjection");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                Pawn target = (Pawn)(Thing)actor.CurJob.GetTarget(TargetIndex.A);
                PawnUtility.ForceWait(target, Mathf.FloorToInt(TicksFinish), actor);
            };
            toil.tickAction = delegate
            {
                Ticks += 1;
                Progress = (Ticks / TicksFinish);
                if (Ticks >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (Progress >= 1)
                {
                    if(Target is Pawn prey)
                    {
                        CompGeneManipulator manipulator = pawn.GetComp<CompGeneManipulator>();
                        bool foundOrder = false;
                        if (manipulator != null && manipulator.TryExecuteMutationOrder(prey, out foundOrder))
                        {
                            return;
                        }

                        if (foundOrder)
                        {
                            return;
                        }

                        BioUtility.TryMutatingPawn(ref prey, BioUtility.GetFallbackMutationSet(prey), 1);
                    }

                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
