using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_ConsumeGenes : JobDriver
    {
        public Thing Target
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing;
            }
        }

        float workDone;
        CompOvamorphLayer ovamorphLayer;
        List<GeneDef> consumedGenes = new List<GeneDef>();

        private float ChewDurationMultiplier
        {
            get
            {
                Thing ingestibleSource = Target;
                if (ingestibleSource.def.ingestible != null && !ingestibleSource.def.ingestible.useEatingSpeedStat)
                {
                    return 1f;
                }

                return 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected Toil GeneDigestion()
        {
            ovamorphLayer = pawn.GetComp<CompOvamorphLayer>();

            consumedGenes = BioUtility.GetGeneForForConsumptionList(Target);
            Toil toil = ToilMaker.MakeToil("DigestGenesToOvamorph");
            toil.atomicWithPrevious = true;
            toil.handlingFacing = true;
            toil.initAction = delegate
            {
                pawn.Rotation = ovamorphLayer.GetLayingFacing(pawn.Position);
            };
            toil.tickAction = delegate
            {
                pawn.Rotation = ovamorphLayer.GetLayingFacing(pawn.Position);
                workDone += 0.0017f;
                if (workDone > 1)
                {
                    ovamorphLayer.SetNextOvamorphAsGene();  
                    Thing ovamorph = ovamorphLayer.LayOvamorph(pawn.Position);

                    CompHiveGeneHolder geneHolder = ovamorph.TryGetComp<CompHiveGeneHolder>();
                    if (geneHolder != null && consumedGenes != null && consumedGenes.Count() > 0)
                    {
                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message(ovamorph + " getting " + consumedGenes.Count() + " genes!");
                        }
                        geneHolder.genes = new GeneSet();
                        BioUtility.ExtractGenesToGeneset(ref geneHolder.genes, consumedGenes);

                        if(Target.Spawned)
                        {
                            Corpse corpse = Target as Corpse;
                            if (corpse != null)
                            {
                                
                            }
                        }
                        Find.WindowStack.Add(new Dialogue_GeneExpression(ovamorph));


                    }

                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            return toil;
        }
        protected Toil GetNonIngestibleConsume()
        {
            Toil toil = ToilMaker.MakeToil("ChewNonIngestible");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
  
                toil.actor.pather.StopDead();
                actor.jobs.curDriver.ticksLeftThisToil = Mathf.RoundToInt(60 * ChewDurationMultiplier);
                if (Target.Spawned)
                {
                    Target.Map.physicalInteractionReservationManager.Reserve(pawn, actor.CurJob, Target);
                }
                
            };
            toil.tickAction = delegate
            { 
                if (Target != null && Target.Spawned)
                {
                    toil.actor.rotationTracker.FaceCell(Target.Position);
                }

                
                toil.actor.GainComfortFromCellIfPossible(1);
            };
            toil.WithProgressBar(TargetIndex.A, delegate
            {
                Thing thing2 = toil.actor.CurJob.GetTarget(TargetIndex.A).Thing;
                return (thing2 == null) ? 1f : (1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / Mathf.Round(60 * ChewDurationMultiplier));
            });
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOnDestroyedOrNull(TargetIndex.A);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.AddFinishAction(delegate
            {
                if (Target != null && pawn.Map.physicalInteractionReservationManager.IsReservedBy(pawn, Target))
                {
                    pawn.Map.physicalInteractionReservationManager.Release(pawn, toil.actor.CurJob, Target);
                }
                Target.Destroy();
            });
            toil.handlingFacing = true;
            return toil;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil chew = Toils_Ingest.ChewIngestible(pawn, ChewDurationMultiplier, TargetIndex.A, TargetIndex.B).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            Toil DigestGenes = GeneDigestion();
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            if (Target.def.ingestible == null)
            {
                yield return GetNonIngestibleConsume();

            }
            else
            {
                yield return chew;
                yield return Toils_Ingest.FinalizeIngest(pawn, TargetIndex.A);
            }
            yield return DigestGenes;
        }
    }
}
