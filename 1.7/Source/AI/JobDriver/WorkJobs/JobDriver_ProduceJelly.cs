using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_ProduceJelly : JobDriver_ClimbToPosition
    {
        private float ticks;
        private float work;
        private bool converted;
        protected float xpPerTick = 0.085f;
        public IntVec3 target => job.GetTarget(TargetIndex.A).Cell;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticks, "jellyTicks");
            Scribe_Values.Look(ref work, "jellyWork");
            Scribe_Values.Look(ref converted, "jellyConverted");
        }
        public bool IsNoLongerValidTarget()
        {
            if (converted) return false;
            CompJellyMaker maker = pawn.GetComp<CompJellyMaker>();
            Thing ingredient = job.GetTarget(TargetIndex.A).Thing;
            if (ingredient != null) return !ingredient.Spawned || ingredient.Map != pawn.Map || maker?.CanMakeIntoJelly(ingredient) != true;
            return maker?.CanConvertCell(target) != true;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(IsNoLongerValidTarget);
            AddFailCondition(() => pawn.Faction == null && !CompJellyMaker.IsProperLightLevelForFeralJelly(target, pawn.Map));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil toil = ToilMaker.MakeToil("AttemptJellyMaking");
            toil.atomicWithPrevious = true;
            toil.initAction = () =>
            {
                if (work > 0f || converted) return;
                CompJellyMaker maker = pawn.GetComp<CompJellyMaker>();
                Thing ingredient = job.GetTarget(TargetIndex.A).Thing;
                work = Mathf.Max(1f, (ingredient != null ? maker.JellyFromThing(ingredient) : maker.JellyFromCell(target)) * maker.WorkPerJelly);
            };
            toil.tickAction = () =>
            {
                if (converted) { ReadyForNextToil(); return; }
                ticks += pawn.GetStatValue(ExternalDefOf.CookSpeed);
                pawn.skills?.Learn(SkillDefOf.Cooking, xpPerTick);
                pawn.needs?.joy?.GainJoy(0.001f, InternalDefOf.NestTending);
                if (ticks < work) return;
                if (IsNoLongerValidTarget()) { EndJobWith(JobCondition.Incompletable); return; }
                CompJellyMaker maker = pawn.GetComp<CompJellyMaker>();
                Thing ingredient = job.GetTarget(TargetIndex.A).Thing;
                int amount = ingredient != null ? maker.ConvertToJelly(ingredient) : maker.ConvertToJelly(target);
                converted = maker.LastConversionSucceeded;
                if (converted) ReadyForNextToil();
                else EndJobWith(JobCondition.Incompletable);
            };
            toil.WithProgressBar(TargetIndex.A, () => Mathf.Clamp01(ticks / Mathf.Max(1f, work)));
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}