
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{ 
    public class JobDriver_CorpseSculpture : JobDriver
    {
        private float TicksFinish => (pawn.CurJob.plantDefToSow != null ? pawn.CurJob.plantDefToSow.statBases.GetStatValueFromList(StatDefOf.WorkToBuild, 250) : 60);
        private ThingDef SculptureDef => pawn.CurJob.plantDefToSow;

        protected Thing Item => job.GetTarget(TargetIndex.A).Thing;
        private float Ticks = 0;
        private float Progress = 0;
        protected float xpPerTick = 0.085f;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Item, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return DoToilSculpting();
        }
        private Toil DoToilSculpting()
        {
            Toil toil = ToilMaker.MakeToil("ToilSculpt");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                if (Item is Corpse corpse)
                {
                    pawn.jobs.curJob.plantDefToSow = ArtUtility.GetSculptureDefForCorpse(corpse);
                }
            };
            toil.tickIntervalAction = delegate (int delta)
            {
                Ticks += pawn.GetStatValue(StatDefOf.AssemblySpeedFactor);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Artistic, xpPerTick);
                }
                Progress = (Ticks / TicksFinish);

                if (!BioUtility.PerformBioconstructionCost(pawn))
                {
                    this.FailOnMentalState(TargetIndex.A);
                    return;
                }

                if (Ticks >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (Progress >= 1)
                {
                    IntVec3 cell = job.GetTarget(TargetIndex.A).Cell;
                    Thing corpse = Item;
                    Thing finishedSculpture = ThingMaker.MakeThing(SculptureDef);
                    finishedSculpture.SetFaction(pawn.Faction);
                    corpse.DeSpawn();

                    CompQuality compQuality = finishedSculpture.TryGetComp<CompQuality>();
                    if (compQuality != null)
                    {
                        QualityCategory q = QualityUtility.GenerateQualityCreatedByPawn(pawn, SkillDefOf.Artistic);
                        compQuality.SetQuality(q, ArtGenerationContext.Colony);
                        QualityUtility.SendCraftNotification(finishedSculpture, pawn);
                    }

                    CompArt compArt = finishedSculpture.TryGetComp<CompArt>();
                    if (compArt != null)
                    {
                        if (compQuality == null)
                        {
                            compArt.InitializeArt(ArtGenerationContext.Colony);
                        }

                        compArt.JustCreatedBy(pawn);
                    }

                    if (SculptureDef.Minifiable)
                    {
                        MinifiedThing minifiedThing = finishedSculpture.MakeMinified();
                        job.SetTarget(TargetIndex.A, GenSpawn.Spawn(minifiedThing, cell, pawn.Map));
                    }
                    else
                    {
                        job.SetTarget(TargetIndex.A, GenSpawn.Spawn(finishedSculpture, cell, pawn.Map));
                    }

                    corpse.Destroy();
                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

    }
}
