using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_ExtractPheromone : JobDriver
    {
        private Thing Platform => TargetThingA;
        private Pawn InnerPawn => (Platform as Building_HoldingPlatform)?.HeldPawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Platform, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished != true ||
                InnerPawn?.Info()?.extractPheromone != true || !PheromoneUtility.CanExtractFrom(InnerPawn));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            int ticks = (int)(1f / pawn.GetStatValue(StatDefOf.MedicalTendSpeed) * 2000f);
            Toil wait = Toils_General.WaitWith(TargetIndex.A, ticks, true, false, false, TargetIndex.A);
            wait.activeSkill = () => SkillDefOf.Medicine;
            wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.ClosestTouch)
                .WithProgressBarToilDelay(TargetIndex.A)
                .PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
            yield return wait;

            yield return Toils_General.Do(() =>
            {
                Pawn innerPawn = InnerPawn;
                if (!BioUtility.TryContainedExtraction(innerPawn, pawn))
                {
                    CompPawnInfo info = innerPawn?.Info();
                    if (info != null)
                    {
                        info.extractPheromone = false;
                    }
                    return;
                }

                if (!PheromoneUtility.TryExtractGland(innerPawn, pawn, pawn.Position, pawn.Map))
                {
                    Messages.Message("XMT_MessagePawnHadNotEnoughToProducePheromone".Translate(innerPawn.Named("PAWN")), innerPawn, MessageTypeDefOf.NeutralEvent);
                }
            });
        }

        public override string GetReport()
        {
            return JobUtility.GetResolvedJobReport(job.def.reportString, InnerPawn, job.targetB, job.targetC);
        }
    }
}
