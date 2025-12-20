
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngineInternal;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_AdvancedTameWithFood : JobDriver_InteractAnimal
    {
        private float feedNutritionLeft;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref feedNutritionLeft, "feedNutritionLeft", 0f);
        }

        private const TargetIndex FoodIndex = TargetIndex.C;
        protected override bool CanInteractNow => !TameUtility.TriedToTameTooRecently(Target);
        protected Pawn Target => HoldingPlatform == null ? (Pawn)job.targetA.Thing : HoldingPlatform.HeldPawn;
        protected virtual Building_HoldingPlatform HoldingPlatform => job.targetA.Thing as Building_HoldingPlatform;

        protected override bool CanFeedEver => Target?.needs?.food != null;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Thing, job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (job.GetTarget(TargetIndex.A).Thing is Pawn)
            {
                Func<bool> noLongerDesignated = () => !Target.GetMorphComp().ShouldTameBribe;
                if (job.GetTarget(TargetIndex.C).HasThing)
                {
                    yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.C).FailOn(noLongerDesignated);
                    yield return Toils_Haul.TakeToInventory(TargetIndex.C, job.count).FailOn(noLongerDesignated);
                }

                foreach (Toil item in MakePawnToils())
                {
                    item.FailOn(noLongerDesignated);
                    yield return item;
                }
                yield return TamingUtility.TryRecruitPawn(TargetIndex.A);
            }
            else
            {
                Func<bool> noLongerDesignated = () => !Target.GetMorphComp().ShouldTameBribe;
                if (job.GetTarget(TargetIndex.C).HasThing)
                {
                    yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.C).FailOn(noLongerDesignated);
                    yield return Toils_Haul.TakeToInventory(TargetIndex.C, job.count).FailOn(noLongerDesignated);
                }

                foreach (Toil item in MakeHoldingPlatformToils())
                {
                    item.FailOn(noLongerDesignated);
                    yield return item;
                }
                yield return TamingUtility.TryRecruitPawnOnPlatform(TargetIndex.A);
            }

        }

        IEnumerable<Toil> MakePawnToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            if (CanFeedEver)
            {
                foreach (Toil item in PawnFeedToils())
                {
                    yield return item;
                }
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            if (CanFeedEver)
            {
                foreach (Toil item2 in PawnFeedToils())
                {
                    yield return item2;
                }
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !CanInteractNow);
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
            yield return Toils_Interpersonal.GotoInteractablePosition(TargetIndex.A);
        }

        IEnumerable<Toil> MakeHoldingPlatformToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            if (CanFeedEver)
            {
                foreach (Toil item in PlatformFeedToils())
                {
                    yield return item;
                }
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            if (CanFeedEver)
            {
                foreach (Toil item2 in PlatformFeedToils())
                {
                    yield return item2;
                }
            }

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !CanInteractNow);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
        }

        private IEnumerable<Toil> PlatformFeedToils()
        {
            Toil toil = ToilMaker.MakeToil("FeedToils");
            toil.initAction = delegate
            {
                feedNutritionLeft = RequiredNutritionPerFeed(Target);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil;
            Toil gotoAnimal = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return gotoAnimal;
            yield return StartFeedPlatform(TargetIndex.A);
            yield return Toils_Ingest.FinalizeIngest(Target, TargetIndex.B);
            yield return Toils_General.PutCarriedThingInInventory();
            yield return Toils_General.ClearTarget(TargetIndex.B);
            yield return Toils_Jump.JumpIf(gotoAnimal, () => feedNutritionLeft > 0f);
        }

        private IEnumerable<Toil> PawnFeedToils()
        {
            Toil toil = ToilMaker.MakeToil("FeedToils");
            toil.initAction = delegate
            {
                feedNutritionLeft = RequiredNutritionPerFeed(Target);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil;
            Toil gotoAnimal = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return gotoAnimal;
            yield return StartFeedPawn(TargetIndex.A);
            yield return Toils_Ingest.FinalizeIngest(Animal, TargetIndex.B);
            yield return Toils_General.PutCarriedThingInInventory();
            yield return Toils_General.ClearTarget(TargetIndex.B);
            yield return Toils_Jump.JumpIf(gotoAnimal, () => feedNutritionLeft > 0f);
        }

        private Toil StartFeedPlatform(TargetIndex tameeInd)
        {
            Toil toil = ToilMaker.MakeToil("StartFeedAnimal");
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                Pawn target = ((Building_HoldingPlatform)actor.CurJob.GetTarget(tameeInd).Thing).HeldPawn;
                Thing thing = FoodUtility.BestFoodInInventory(actor, target, FoodPreferability.NeverForNutrition, FoodPreferability.MealLavish);
                if (thing == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                else
                {
                    actor.mindState.lastInventoryRawFoodUseTick = Find.TickManager.TicksGame;
                    int num = FoodUtility.StackCountForNutrition(feedNutritionLeft, thing.GetStatValue(StatDefOf.Nutrition));
                    int stackCount = thing.stackCount;
                    Thing thing2 = actor.inventory.innerContainer.Take(thing, Mathf.Min(num, stackCount));
                    actor.carryTracker.TryStartCarry(thing2);
                    actor.CurJob.SetTarget(TargetIndex.B, thing2);
                    float bribeNutrition = (float)thing2.stackCount * thing2.GetStatValue(StatDefOf.Nutrition);
                    ticksLeftThisToil = Mathf.CeilToInt(270f * (bribeNutrition / RequiredNutritionPerFeed(target)));

                    float taming = bribeNutrition * Mathf.Min(actor.skills.GetSkill(SkillDefOf.Animals).Level * 0.01f, actor.skills.GetSkill(SkillDefOf.Social).Level * 0.01f);
                    target.GetMorphComp().tamingBribes += taming;
                    XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.RecievedTrophallaxis, actor);
                    if (num <= stackCount)
                    {
                        feedNutritionLeft = 0f;
                    }
                    else
                    {
                        feedNutritionLeft -= bribeNutrition;
                        if (feedNutritionLeft < 0.001f)
                        {
                            feedNutritionLeft = 0f;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.activeSkill = () => SkillDefOf.Animals;
            return toil;
        }
        private Toil StartFeedPawn(TargetIndex tameeInd)
        {
            Toil toil = ToilMaker.MakeToil("StartFeedAnimal");
            toil.initAction = delegate
            {
                Pawn actor = toil.GetActor();
                Pawn target = (Pawn)(Thing)actor.CurJob.GetTarget(tameeInd);
                PawnUtility.ForceWait(target, 270, actor);
                Thing thing = FoodUtility.BestFoodInInventory(actor, target, FoodPreferability.NeverForNutrition, FoodPreferability.MealLavish);
                if (thing == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
                else
                {
                    actor.mindState.lastInventoryRawFoodUseTick = Find.TickManager.TicksGame;
                    int num = FoodUtility.StackCountForNutrition(feedNutritionLeft, thing.GetStatValue(StatDefOf.Nutrition));
                    int stackCount = thing.stackCount;
                    Thing thing2 = actor.inventory.innerContainer.Take(thing, Mathf.Min(num, stackCount));
                    actor.carryTracker.TryStartCarry(thing2);
                    actor.CurJob.SetTarget(TargetIndex.B, thing2);
                    float bribeNutrition = (float)thing2.stackCount * thing2.GetStatValue(StatDefOf.Nutrition);
                    ticksLeftThisToil = Mathf.CeilToInt(270f * (bribeNutrition / RequiredNutritionPerFeed(target)));
                    float taming = bribeNutrition * Mathf.Min(actor.skills.GetSkill(SkillDefOf.Animals).Level * 0.01f, actor.skills.GetSkill(SkillDefOf.Social).Level * 0.01f);
                    target.GetMorphComp().tamingBribes += taming;
                    XMTUtility.GiveInteractionMemory(target, HorrorMoodDefOf.RecievedTrophallaxis, actor);
                    if (num <= stackCount)
                    {

                        feedNutritionLeft = 0f;
                    }
                    else
                    {
                        feedNutritionLeft -= bribeNutrition;
                        if (feedNutritionLeft < 0.001f)
                        {
                            feedNutritionLeft = 0f;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.activeSkill = () => SkillDefOf.Animals;
            return toil;
        }
    }
}
