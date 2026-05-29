
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{ 
    public class JobDriver_CorpseSculpture : JobDriver
    {
        private float TicksFinish => WorkRequired();
        private ThingDef SculptureDef => pawn.CurJob.plantDefToSow;
        private ThingDef StuffDef => (Item as IdeologyResinArtFrame)?.StuffDef;
        private GameComponent_CorpseArtProgress ProgressTracker => Current.Game.GetComponent<GameComponent_CorpseArtProgress>();

        protected Thing Item => job.GetTarget(TargetIndex.A).Thing;
        private float WorkDone = 0;
        private float Progress => WorkDone / TicksFinish;
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
                else if (Item is IdeologyResinArtFrame artFrame)
                {
                    pawn.jobs.curJob.plantDefToSow = artFrame.TargetThingDef;
                }

                WorkDone = ProgressTracker.WorkDone(Item);
            };
            toil.tickIntervalAction = delegate (int delta)
            {
                WorkDone += pawn.GetStatValue(StatDefOf.AssemblySpeedFactor);
                ProgressTracker.SetWorkDone(Item, WorkDone);

                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Artistic, xpPerTick);
                }

                if (!BioUtility.PerformBioconstructionCost(pawn))
                {
                    this.FailOnMentalState(TargetIndex.A);
                    return;
                }

                if (WorkDone >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (Item == null || Item.Destroyed)
                {
                    ProgressTracker.Clear(Item);
                    return;
                }

                if (Progress >= 1)
                {
                    IntVec3 cell = job.GetTarget(TargetIndex.A).Cell;
                    Thing material = Item;
                    Thing finishedSculpture = ThingMaker.MakeThing(SculptureDef, StuffDef);
                    finishedSculpture.SetFaction(pawn.Faction);
                    ProgressTracker.Clear(material);
                    material.Map?.designationManager.RemoveAllDesignationsOn(material);
                    material.DeSpawn();

                    if (material is IdeologyResinArtFrame artFrame && finishedSculpture is ThingWithComps thingWithComps)
                    {
                        thingWithComps.StyleDef = artFrame.TargetStyleDef;
                    }

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

                    if (material is IdeologyResinArtFrame)
                    {
                        job.SetTarget(TargetIndex.A, GenSpawn.Spawn(finishedSculpture, cell, pawn.Map, material.Rotation));
                    }
                    else if (SculptureDef.Minifiable)
                    {
                        MinifiedThing minifiedThing = finishedSculpture.MakeMinified();
                        job.SetTarget(TargetIndex.A, GenSpawn.Spawn(minifiedThing, cell, pawn.Map));
                    }
                    else
                    {
                        job.SetTarget(TargetIndex.A, GenSpawn.Spawn(finishedSculpture, cell, pawn.Map));
                    }

                    material.Destroy();
                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private float WorkRequired()
        {
            if (Item is IdeologyResinArtFrame artFrame && artFrame.TargetThingDef != null)
            {
                float work = artFrame.def.GetStatValueAbstract(StatDefOf.WorkToBuild);
                return work > 0 ? work : 250f;
            }

            return pawn.CurJob.plantDefToSow != null ? pawn.CurJob.plantDefToSow.statBases.GetStatValueFromList(StatDefOf.WorkToBuild, 250) : 60;
        }
    }
}
