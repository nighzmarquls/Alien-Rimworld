using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;

namespace Xenomorphtype
{ 
    internal class JobDriver_MergeIntoJellyWell : JobDriver
    {

        private float TicksFinish = 1000;
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
        public bool FailAction()
        {
            return  pawn.DestroyedOrNull() || pawn.Downed;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(FailAction);
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return Merge();
        }

        private Toil Merge()
        {
            Toil toil = ToilMaker.MakeToil("AttemptCopy");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                Ticks += 1;
                Progress = (Ticks / TicksFinish);
                if (Ticks >= TicksFinish && Progress < float.MaxValue)
                {
                    Progress = float.MaxValue;
                    Pawn actor = GetActor();
                    if (XMTUtility.TransformThingIntoThing(Target, XenoBuildingDefOf.XMT_JellyWell, out Thing result, actor))
                    {
                        CompMatureMorph morph = actor.GetMorphComp();
                        if (morph != null)
                        {
                            morph.DelayedDestroy(DestroyMode.Vanish);
                        }

                    }
                    ReadyForNextToil();
                }

            };
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(EffecterDefOf.Surgery, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
