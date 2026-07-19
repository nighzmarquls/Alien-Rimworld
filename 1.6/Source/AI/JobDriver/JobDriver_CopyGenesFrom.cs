using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_CopyGenesFrom : JobDriver
    {

        private float TicksFinish = 350;
        private float Ticks = 0;
        private float Progress = 0;
        private Thing geneSource;
        private CompHiveGeneHolder geneHolder;
        public Thing Target
        {
            get
            {
                if (job == null)
                {
                    return null;
                }
                return job.GetTarget(TargetIndex.A).Thing;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo target = job.GetTarget(TargetIndex.A);
            return target.IsValid; // && pawn.Reserve(target, job, int.MaxValue, -1, null, errorOnFailed);
        }
        public bool FailAction()
        {
            return pawn == null || pawn.Destroyed || pawn.Downed;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(FailAction);
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return AttemptCopy();
        }

        private Toil AttemptCopy()
        {
            Toil toil = ToilMaker.MakeToil("AttemptCopy");
            toil.initAction = delegate
            {
                geneSource = Target;
                geneHolder = geneSource?.TryGetComp<CompHiveGeneHolder>();
                if (!SourceIsValid())
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            toil.tickAction = delegate
            {
                if (!SourceIsValid())
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Ticks += 1;
                Progress = (Ticks / TicksFinish);
                if (Ticks >= TicksFinish && Progress < float.MaxValue)
                {
                    Progress = float.MaxValue;
                    JobCondition result = TryCopyGenes();
                    EndJobWith(result);
                }

            };
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private bool SourceIsValid()
        {
            Pawn actor = GetActor();
            return actor != null
                && !actor.Destroyed
                && !actor.Downed
                && geneSource != null
                && !geneSource.Destroyed
                && geneHolder != null;
        }

        private JobCondition TryCopyGenes()
        {
            try
            {
                Pawn actor = GetActor();
                if (!SourceIsValid())
                {
                    return JobCondition.Incompletable;
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("finished activating gene copy " + actor);
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("gene holder found in " + geneSource);
                }
                List<GeneDef> originalGenes = BioUtility.GetGeneForExpressionList(actor);
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("got original genes on " + actor);
                }
                List<GeneDef> newGenes = BioUtility.GetGeneForExpressionList(geneSource);
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("got new genes from " + geneSource);
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("applying alter genes on " + actor);
                }
                BioUtility.AlterGenes(ref actor, newGenes, originalGenes, geneHolder.EffectiveTemplateName);
                return JobCondition.Succeeded;
            }
            catch (Exception exception)
            {
                Log.Error("Error applying copied genes from gene ovomorph: " + exception);
                return JobCondition.Errored;
            }
        }
    }
}
