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
    public class JobDriver_ApplyOvamorph : JobDriver
    {
        private float TicksFinish = 350;
        private float Ticks = 0;
        private float Progress = 0;
        public Pawn Prey
        {
            get
            {
                return (Pawn)job.GetTarget(TargetIndex.A).Thing;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        public bool IsNoLongerValidTarget()
        {
            return XMTUtility.HasEmbryo(Prey);
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
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
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
                    Pawn prey = Prey;
                    CompMatureMorph matureMorph = pawn.GetMorphComp();
                    if (matureMorph != null)
                    {
                        matureMorph.TryOvamorphing(prey);
                    }
                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(EffecterDefOf.Surgery, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
