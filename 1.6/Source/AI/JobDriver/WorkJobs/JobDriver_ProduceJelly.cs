using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobDriver_ProduceJelly : JobDriver_ClimbToPosition
    {
        private float Ticks = 0;
        private float Progress = 0;
        private float TicksFinish = 300;
        protected float xpPerTick = 0.085f;
        public Thing target
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

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return BeginProducingJelly();
        }

        private Toil BeginProducingJelly()
        {
            CompJellyMaker jellyMaker = pawn.GetComp<CompJellyMaker>();
            if (jellyMaker != null)
            {
                TicksFinish = jellyMaker.JellyFromThing(target)*jellyMaker.WorkPerJelly;
            }
            Toil toil = ToilMaker.MakeToil("AttemptJellyMaking");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                Ticks += pawn.GetStatValue(ExternalDefOf.CookSpeed);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Cooking, xpPerTick);
                }
                if (pawn?.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.001f, InternalDefOf.NestTending);
                }
                Progress = (Ticks / TicksFinish);
                if (Ticks >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                Thing ingredient = target;
                
                CompJellyMaker jellyMaker = pawn.GetComp<CompJellyMaker>();
                if (jellyMaker != null)
                {
                    jellyMaker.ConvertToJelly(ingredient, Progress);
                    
                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
