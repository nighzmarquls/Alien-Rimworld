using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_ExtractJelly : JobDriver
    {
        private Thing Platform => base.TargetThingA;

        private Pawn InnerPawn => (Platform as Building_HoldingPlatform)?.HeldPawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Platform, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(delegate
            {
                Pawn innerPawn2 = InnerPawn;
                if (innerPawn2 == null || innerPawn2.Destroyed)
                {
                    return true;
                }

                return !BioUtility.PawnHasEnoughForExtraction(InnerPawn);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            int ticks = (int)(1f / pawn.GetStatValue(StatDefOf.MedicalTendSpeed) * 2000f);
            Toil toil = Toils_General.WaitWith(TargetIndex.A, ticks, useProgressBar: true, maintainPosture: false, maintainSleep: false, TargetIndex.A);
            toil.activeSkill = () => SkillDefOf.Medicine;
            toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.ClosestTouch).WithProgressBarToilDelay(TargetIndex.A).PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
            yield return toil;
            yield return Toils_General.Do(delegate
            {
                Pawn innerPawn = InnerPawn;
                BioUtility.ExtractMetabolicCostFromPawn(innerPawn);

                XMTUtility.GiveInteractionMemory(innerPawn, ThoughtDefOf.HarmedMe, pawn);

                Thing thing = ThingMaker.MakeThing(InternalDefOf.Starbeast_Jelly);
                thing.stackCount = 5;
                GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            });
        }

        public override string GetReport()
        {
            return JobUtility.GetResolvedJobReport(job.def.reportString, InnerPawn, job.targetB, job.targetC);
        }
    }
}
