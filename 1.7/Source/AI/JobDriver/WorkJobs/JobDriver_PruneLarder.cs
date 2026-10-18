using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    // Keep the class and job names for existing saves.
    public class JobDriver_PruneLarder : JobDriver
    {
        private float workDone;
        private CompHivePrunable Prunable => job.targetA.Thing?.TryGetComp<CompHivePrunable>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workDone, "workDone");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Prunable?.CanPrune != true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil cut = ToilMaker.MakeToil("PruneHiveStructure");
            cut.defaultCompleteMode = ToilCompleteMode.Never;
            cut.tickAction = () =>
            {
                if (!pawn.CanReachImmediate(job.targetA, PathEndMode.Touch))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                workDone += pawn.GetStatValue(StatDefOf.PlantWorkSpeed);
                pawn.skills?.Learn(SkillDefOf.Plants, 0.085f);
                pawn.needs?.joy?.GainJoy(Prunable.Props.nestJoy, InternalDefOf.NestTending);
                if (workDone < Prunable.Props.work)
                    return;
                if (Prunable.Prune(pawn))
                    ReadyForNextToil();
                else
                    EndJobWith(JobCondition.Incompletable);
            };
            cut.WithProgressBar(TargetIndex.A, () => workDone / Prunable.Props.work);
            cut.WithEffect(EffecterDefOf.Surgery, TargetIndex.A);
            yield return cut;
        }
    }
}
