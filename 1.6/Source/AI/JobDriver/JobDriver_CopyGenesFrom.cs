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
        public Thing Target
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        public bool FailAction()
        {
            return pawn.Downed;
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
                
                if (Progress >= 1 && Progress < float.MaxValue)
                {
                    Progress = float.MaxValue;
                    Pawn actor = GetActor();

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message(toil.finishActions.Count + " total finishActions on toil");
                        Log.Message("finished activating gene copy " + actor);
                    }

                    CompHiveGeneHolder geneHolder = Target.TryGetComp<CompHiveGeneHolder>();
                    if (geneHolder != null)
                    {
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message("gene holder found in " + Target);
                        }
                        List<GeneDef> originalGenes = BioUtility.GetGeneForExpressionList(actor);
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message("got original genes on " + actor);
                        }
                        List<GeneDef> newGenes = BioUtility.GetGeneForExpressionList(Target);
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message("got new genes from " + Target);
                        }

                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message("applying alter genes on " + actor);
                        }
                        BioUtility.AlterGenes(ref actor, newGenes, originalGenes, geneHolder.templateName);
                        
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
