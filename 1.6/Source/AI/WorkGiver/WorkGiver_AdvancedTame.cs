

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class WorkGiver_AdvancedTame : WorkGiver_InteractAnimal
    {
        protected Pawn GetCryptimorph(Thing potentialPlatform)
        {
            if(potentialPlatform is Pawn pawn)
            {
                if (pawn.GetMorphComp() != null)
                {
                    return pawn;
                }
            }

            if (potentialPlatform is Building_HoldingPlatform { HeldPawn: var heldPawn })
            {
                if (heldPawn == null)
                {
                    return null;
                }

                if(heldPawn.GetMorphComp() == null)
                {
                    return null;
                }

 
                return heldPawn;
            }

            return null;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            Pawn tamable = GetCryptimorph(t);

            if(tamable == null)
            {
                return false;
            }

            if(!tamable.GetMorphComp().AnyTamingDesired)
            {
                return false;
            }

            if (tamable.GetMorphComp().ShouldTameBribe)
            {
                Thing thing = FoodUtility.BestFoodSourceOnMap(pawn, tamable, desperate: false, out var foodDef, FoodPreferability.MealLavish, allowPlant: false, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: false, ignoreReservations: false, calculateWantedStackCount: false, FoodPreferability.Undefined, JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamable) * 2f * 4f);
                if (thing == null)
                {
                    return false;

                }
            }


            return !TameUtility.TriedToTameTooRecently(tamable);
        }
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Pawn candidate in XMTHiveUtility.GetHiveMembersOnMap(pawn.Map))
            {
                if(candidate.IsAdvancedTameable())
                {
                    if (pawn.IsOnHoldingPlatform)
                    {
                        if (candidate.ParentHolder is Thing holder)
                        {
                            yield return holder;
                        }
                    }
                    else
                    {
                        if (candidate.Spawned)
                        {
                            yield return candidate;
                        }
                    }
                }
            }

            foreach(Building_HoldingPlatform holdingPlatform in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>())
            {
                if(holdingPlatform.HeldPawn != null)
                {
                    if(holdingPlatform.HeldPawn.GetMorphComp() != null)
                    {
                        yield return holdingPlatform;
                    }
                }    
            }
        }
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling);
        }
        public bool CanInteractWithTarget(Pawn pawn, Thing target, bool forced = false)
        {
            return pawn.Awake();
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            //Log.Message("checking job for: " + pawn + " on thing " + t);

            Pawn tamable;
            bool notPlatform = true;
            if ((t is Building_HoldingPlatform { HeldPawn: Pawn heldPawn }))
            {
                tamable = heldPawn;
                notPlatform = false;
            }
            else
            {
                tamable = t as Pawn;
            }

            if (tamable == null)
            {
                return null;
            }
            //Log.Message("checking job for: " + pawn + " on " + tamable);
            if (!CanInteractWithTarget(pawn, t, forced) && notPlatform)
            {
                return null;
            }

            if (TameUtility.TriedToTameTooRecently(tamable))
            {
                JobFailReason.Is(WorkGiver_InteractAnimal.AnimalInteractedTooRecentlyTrans);
                return null;
            }

            Job job = null;
            Thing thing = null;
            int count = -1;

            if (tamable.GetMorphComp().ShouldTameBribe)
            {
                if (tamable.RaceProps.EatsFood && tamable.needs?.food != null && !TamingUtility.HasFoodToInteractAnimal(pawn, tamable))
                {
                    thing = FoodUtility.BestFoodSourceOnMap(pawn, tamable, desperate: false, out var foodDef, FoodPreferability.MealLavish , allowPlant: false, allowDrug: false, allowCorpse: false, allowDispenserFull: false, allowDispenserEmpty: false, allowForbidden: false, allowSociallyImproper: false, allowHarvest: false, forceScanWholeMap: false, ignoreReservations: false, calculateWantedStackCount: false, FoodPreferability.Undefined, JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamable) * 2f * 4f);
                    if (thing == null)
                    {
                        JobFailReason.Is("NoFood".Translate());

                    }
                    else
                    {
                        float num = JobDriver_InteractAnimal.RequiredNutritionPerFeed(tamable) * 2f * 4f;
                        float nutrition = FoodUtility.GetNutrition(tamable, thing, foodDef);
                        count = Mathf.CeilToInt(num / nutrition);
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AdvancedTameWithFood, t, null, thing);
                        job.count = count;
                    }
                }
            }
            else if(tamable.GetMorphComp().ShouldTameCondition || tamable.GetMorphComp().ShouldTameHostage)
            {
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AdvancedTameThreat, t, null);
                job.count = 1;

            }
            else
            {
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AdvancedTame, t, null);
                job.count = 1;
                
            }
        
            return job;
        }
    }
}
