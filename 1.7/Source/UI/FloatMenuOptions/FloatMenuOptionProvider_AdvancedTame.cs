using RimWorld;
using UnityEngine;
using VEF.Graphics;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    /*internal class FloatMenuOptionProvider_AdvancedTame : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn tamable = clickedThing as Pawn;

            if( tamable != null && tamable.GuestStatus != GuestStatus.Prisoner)
            {
                tamable = null;
            }

            if (clickedThing is Building_HoldingPlatform platform)
            {
                tamable = platform.HeldPawn;
            }
            
            if(XMTUtility.IsXenomorph(tamable))
            {
                FloatMenuOption AdvancedTameOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Tame".Translate(), delegate
                {
                    Job job = null;
                    Thing thing = null;
                    int count = -1;

                    if (tamable.GetMorphComp().ShouldTameBribe && !TamingUtility.HasFoodToInteractAnimal(context.FirstSelectedPawn, tamable))
                    {
                        if (tamable.RaceProps.EatsFood && tamable.needs?.food != null)
                        {
                            thing = FoodUtility.BestFoodSourceOnMap(context.FirstSelectedPawn, tamable, desperate: false, out var foodDef, FoodPreferability.MealLavish, allowPlant: false, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: false, ignoreReservations: false, calculateWantedStackCount: false, FoodPreferability.Undefined, JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamable) * 2f * 4f);
                            if (thing == null)
                            {
                                JobFailReason.Is("NoFood".Translate());

                            }
                            else
                            {
                                float num = JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamable) * 2f * 4f;
                                float nutrition = FoodUtility.GetNutrition(tamable, thing, foodDef);
                                count = Mathf.CeilToInt(num / nutrition);
                                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AdvancedTameWithFood, tamable, null, thing);
                                job.count = count;
                            }
                        }
                    }
                    else
                    {
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AdvancedTame, tamable, null);
                        job.count = 1;

                    }

                    context.FirstSelectedPawn.jobs.StartJob(job, JobCondition.InterruptForced);


                }, priority: MenuOptionPriority.Default), context.FirstSelectedPawn, tamable);


                if(!tamable.GetMorphComp().ShouldTame)
                {
                    AdvancedTameOption.Disabled = true;
                    AdvancedTameOption.tooltip = "XMT_FMO_TameNotEnabled".Translate();
                }
                return AdvancedTameOption;
            }
            

            return null;
        }
    }*/
}
