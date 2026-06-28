using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class JobGiver_Xenomorph : JobGiver_ExitMapBest
    {
        protected Job GetAbductJob(Pawn pawn, Pawn target, bool allowExpansion = false)
        {
            IntVec3 cell = IntVec3.Invalid;
            XMTHiveUtility.TryGetHiveCocoonCell(pawn, out cell);
            if(!cell.IsValid)
            {
                if (allowExpansion)
                {
                    Job expansionJob = XMTHiveUtility.GetHiveCocoonExpansionJob(pawn);
                    if (expansionJob != null)
                    {
                        return expansionJob;
                    }
                }

                Messages.Message("XMT_NoRoomToCocoon".Translate(), MessageTypeDefOf.NegativeEvent);
                return null;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, target, cell);
            if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, target, PathEndMode.Touch, Danger.Deadly))
            {
                pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(target.Position), job);
                return null;
            }
            
            FeralJobUtility.ReservePlaceForJob(pawn, job, cell);
            FeralJobUtility.ReserveThingForJob(pawn, job, target);
            job.count = 1;
            return job;
        }

        protected bool TryGetFeralAbductionJob(Pawn pawn, CompMatureMorph compMatureMorph, IEnumerable<Pawn> candidates, bool allowExpansion, bool blockedByOvomorphNest, out Job job)
        {
            job = null;
            if (pawn?.Map == null || compMatureMorph == null || candidates == null || blockedByOvomorphNest)
            {
                return false;
            }

            List<Pawn> candidateList = candidates as List<Pawn> ?? candidates.ToList();
            if (!candidateList.Any())
            {
                return false;
            }

            if (!compMatureMorph.ShouldAbductHost())
            {
                return false;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " thinks a host should be abducted");
            }

            if (compMatureMorph.ShouldDoMichief() && XMTMischiefUtility.TryFindRoofBreakMischief(pawn, out Job roofBreakJob, out string roofBreakReason))
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is starting roof breaking mischief during abduction search: " + roofBreakReason);
                }

                job = roofBreakJob;
                return true;
            }

            Pawn target = compMatureMorph.BestAbductCandidate(candidateList);
            if (target == null)
            {
                return false;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " thinks " + target + " should be abducted");
            }

            job = GetAbductJob(pawn, target, allowExpansion);
            return job != null;
        }

        protected Job GetFeralJob(Pawn pawn)
        {
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " is getting Feral Job");
            }

            if(!pawn.ageTracker.Adult)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is fleeing the map due to age");
                }

                Need_Food juvenileFood = pawn.needs.food;
                bool juvenileDesperate = juvenileFood != null && juvenileFood.CurCategory == HungerCategory.Starving;
                if (juvenileFood != null && juvenileFood.CurLevelPercentage < 0.9f)
                {
                    Job juvenileFoodJob = GetFoodJob(pawn, juvenileDesperate, juvenileDesperate ? 1f : 0.45f);
                    if (juvenileFoodJob != null)
                    {
                        return juvenileFoodJob;
                    }
                }

                Job juvenileRestJob = XMTHiveUtility.GetHiveRestJob(pawn);
                if (juvenileRestJob != null)
                {
                    return juvenileRestJob;
                }
            }
            IEnumerable<Pawn> colonyPawns = pawn.Map.PlayerPawnsForStoryteller.Where(x => XMTUtility.TriggersOvomorph(x));
            IEnumerable<Pawn> hostPawns = pawn.Map.mapPawns.AllPawnsSpawned.Where(x => XMTUtility.TriggersOvomorph(x));
            Need_Food food = pawn.needs?.food;
            bool desperate = food != null && food.CurCategory == HungerCategory.Starving;

            CompMatureMorph compMatureMorph = pawn.GetMorphComp();

            bool shouldOvomorph = compMatureMorph.ShouldOvomorphCandidate();
            bool hasCocoonedHosts = XMTHiveUtility.HasCocooned(pawn.Map);
            bool hasNest = hasCocoonedHosts || XMTHiveUtility.HasLocalNest(pawn.Map);

            if (compMatureMorph != null && compMatureMorph.TryGetPriorityFeralFeedJob(out Job priorityFeedJob))
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is prioritizing host feeding.");
                }

                return priorityFeedJob;
            }

            if (food == null || food.CurCategory != HungerCategory.Starving)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is getting fed Job there are " + hostPawns.Count() + " possible hosts");
                }
                if (TryGetFeralAbductionJob(pawn, compMatureMorph, hostPawns, allowExpansion: true, blockedByOvomorphNest: shouldOvomorph && hasCocoonedHosts, out Job abductJob))
                {
                    return abductJob;
                }
            }
            else
            {
               Job job = GetFoodJob(pawn,desperate);
               if(job != null)
               {
                   return job;
               }

               if (TryGetFeralAbductionJob(pawn, compMatureMorph, hostPawns, allowExpansion: true, blockedByOvomorphNest: shouldOvomorph && hasCocoonedHosts, out Job abductJob))
               {
                   if (XMTSettings.LogJobGiver)
                   {
                       Log.Message(pawn + " is abducting a host after failing to find food.");
                   }
                   return abductJob;
               }
            }

            if (compMatureMorph != null)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is doing Feral Activity");
                }

                if (XMTHiveUtility.IsInsideNest(pawn.Position,pawn.Map) && XMTHiveUtility.IsCloseToNest(pawn.Position, pawn.Map))
                {
                    if (shouldOvomorph)
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " thinks a candidate should be Ovomorphed.");
                        }
                        Pawn target = XMTHiveUtility.GetOvomorphingCandidate(pawn.Map);
                        if (target != null)
                        {
                            FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, target);
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is going to Ovomorph " + target);
                            }
                            if (!target.Spawned)
                            {
                                XMTHiveUtility.RemoveHost(target, pawn.Map);
                            }
                            else
                            {
                                return JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyOvomorphing, target);
                            }
                        }
                    }
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is inside nest");
                    }
                    
                    IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, compMatureMorph.abductRange, false).OfType<Pawn>().Where(x => !x.Downed && !XMTUtility.NotPrey(x));
                    if (pawns.Any())
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is defending nest.");
                        }
                    
                        return JobMaker.MakeJob(JobDefOf.AttackMelee, pawns.RandomElement());
                    }
                    else
                    {
                        bool shouldHunt = compMatureMorph.ShouldHunt();
                        IEnumerable<Pawn> UndownedPawns = colonyPawns.Where(x => !x.Downed && x != pawn);
                        if (UndownedPawns.Count() == 1 && shouldHunt)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is abducting the last colony pawn.");
                            }
                           
                            Pawn target = UndownedPawns.First();

                            return GetAbductJob(pawn, target, allowExpansion: true);

                        }
                        else if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.25f)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is napping.");
                            }

                            Job restJob = XMTHiveUtility.GetHiveRestJob(pawn);
                            return restJob;
                        }
                        else if (shouldHunt && UndownedPawns.Any())
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is abducting a random colony pawn.");
                            }
                            
                            Pawn target = UndownedPawns.RandomElement();

                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is going to abduct " + target);
                            }

                            return GetAbductJob(pawn, target, allowExpansion: true);
                    
                        }
                    }

                    if (compMatureMorph.ShouldBeInNest())
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + "should be in the nest.");
                        }

                        return XMTHiveUtility.GetHiveRestJob(pawn);
                    }

                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " has nothing else to do.");
                    }

                    Job hibernateJob = XMTHiveUtility.GetSurplusHibernationJob(pawn);
                    if (hibernateJob != null)
                    {
                        return hibernateJob;
                    }

                    Job wanderRestJob = XMTHiveUtility.GetHiveRestJob(pawn);
                    if (wanderRestJob != null)
                    {
                        return wanderRestJob;
                    }

                    Job hiveWanderJob = XMTHiveUtility.GetHiveWanderJob(pawn);
                    if (hiveWanderJob != null)
                    {
                        return hiveWanderJob;
                    }

                }
                else
                {
                    Job restJob = XMTHiveUtility.GetHiveRestJob(pawn);
                    if (restJob != null)
                    {
                        return restJob;
                    }
                }
                
            }

            if (hasNest && compMatureMorph != null)
            {
                if (compMatureMorph.GetCocoonedFeedJob(out Job feedJob))
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is feeding a cocooned pawn instead of leaving the map.");
                    }
                    return feedJob;
                }

                Job buildJob = XMTHiveUtility.GetHiveCocoonExpansionJob(pawn);
                if (buildJob != null)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is expanding cocoon space instead of leaving the map: " + buildJob);
                    }
                    return buildJob;
                }

                buildJob = XMTHiveUtility.GetNestBuildJob(pawn);
                if (buildJob != null)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is doing hive building work instead of leaving the map.");
                    }
                    return buildJob;
                }

                Job restJob = XMTHiveUtility.GetHiveRestJob(pawn);
                if (restJob != null)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is returning to hive rest instead of leaving the map.");
                    }
                    return restJob;
                }

                Job wanderJob = XMTHiveUtility.GetHiveWanderJob(pawn);
                if (wanderJob != null)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is wandering in the hive instead of leaving the map.");
                    }
                    return wanderJob;
                }
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " has no feral jobs valid.");
            }

            return null;
            
        }

        protected Job GetFoodJob(Pawn pawn, bool desperate, float maximumLight = 1.0f)
        {
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " is getting food Job");
            }

            if (pawn?.Map == null)
            {
                return null;
            }

            bool caresAboutForbidden = ForbidUtility.CaresAboutForbidden(pawn, false);
            Thing foodSource = null;
            ThingDef foodDef = null;
            bool foundFoodSource = false;

            try
            {
                foundFoodSource = FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, desperate,
                    out foodSource, out foodDef,
                    canRefillDispenser: false, canUseInventory: true, canUsePackAnimalInventory: false,
                    allowForbidden: !caresAboutForbidden, allowCorpse: true, allowSociallyImproper: true, allowHarvest: false, forceScanWholeMap: true, ignoreReservations: true, calculateWantedStackCount: false, allowVenerated: true);
            }
            catch (Exception ex)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Warning(pawn + " failed vanilla food source scan: " + ex.Message);
                }
                foodSource = null;
                foodDef = null;
                foundFoodSource = false;
            }

            if (!foundFoodSource)
            {
                int distance = int.MaxValue;
                foreach(Pawn prey in pawn.Map.spawnedThings.OfType<Pawn>())
                {
                    if (prey == null || prey == pawn || !prey.Spawned || prey.Dead || XMTUtility.NotPrey(prey) || XMTUtility.IsInorganic(prey))
                    {
                        continue;
                    }

                    if(caresAboutForbidden)
                    {
                        if(prey.IsForbidden(pawn))
                        {
                            continue;
                        }
                        if(!prey.PositionHeld.InAllowedArea(pawn))
                        {
                            continue;
                        }
                    }

                    int preydistance = prey.PositionHeld.DistanceToSquared(pawn.Position);

                    if(preydistance < distance)
                    {
                        distance = preydistance;
                        foodSource = prey;
                    }
                }

                if(foodSource == null)
                {
                    return null;
                }
            }

            if (foodSource == null)
            {
                return null;
            }

            if (foodSource is Pawn foodPawn)
            {
                if (foodPawn.Dead)
                {
                    foodSource = foodPawn.Corpse;
                    foodDef = foodSource != null ? FoodUtility.GetFinalIngestibleDef(foodSource, false) : null;
                }
                else
                {
                    if (pawn.playerSettings != null)
                    {
                        pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
                    }
                    Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, foodPawn);
                    job.killIncappedTarget = true;

                    if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, foodPawn, PathEndMode.Touch, Danger.Deadly))
                    {
                        pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(foodPawn.PositionHeld), job);
                        return null;
                    }
                    return job;
                }
            }

            if (foodSource == null || foodDef == null || foodDef.ingestible == null)
            {
                return null;
            }

            if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, foodSource))
            {
                return null;
            }

            float nutrition = FoodUtility.GetNutrition(pawn, foodSource, foodDef);
            if (nutrition <= 0f)
            {
                return null;
            }

            Pawn pawn3 = (foodSource.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (pawn3 != null && pawn3 != pawn)
            {
                Job job2 = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, foodSource, pawn3);
                job2.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition);
                return job2;
            }

            if (maximumLight < 1)
            {
                if (foodSource.MapHeld == null || !foodSource.Position.InBounds(foodSource.MapHeld))
                {
                    return null;
                }

                float brightness = foodSource.MapHeld.glowGrid.GroundGlowAt(foodSource.Position);

                if(brightness > maximumLight)
                {
                    return null;
                }
            }

            Job job3 = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);

            if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, foodSource, PathEndMode.Touch, Danger.Deadly))
            {
                pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(foodSource.PositionHeld), job3);
                return null;
            }
            job3.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition);
            return job3;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {


            if(pawn == null)
            {
                return null;
            }

            if(!XMTUtility.IsXenomorph(pawn))
            {
                return null;
            }

            if (pawn.def == InternalDefOf.XMT_Larva)
            {
                CompLarvalGenes compLarvalGenes = pawn.GetComp<CompLarvalGenes>();
                if (compLarvalGenes != null)
                {
                    if (compLarvalGenes.latched)
                    {
                        return null;
                    }
                    if (compLarvalGenes.spent)
                    {
                        return null;
                    }

                    IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, compLarvalGenes.LeapRange, true).OfType<Pawn>()
                        .Where(x => XMTUtility.TriggersOvomorph(x));

                    if (pawns.Any())
                    {
                        foreach (Pawn target in pawns)
                        {
                            return JobMaker.MakeJob(XenoWorkDefOf.XMT_ImplantHunt, target);
                        }
                    }
                }
            }

            CompMatureMorph compMatureMorph = pawn.GetMorphComp();
            if (compMatureMorph != null)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is looking for base xenomorph jobs.");
                }

                if (pawn.MentalState is MentalState_XMT_MurderousRage rage)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is seeking to hunt " + rage.target);
                    }
                    if (rage.target is Pawn living)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, rage.target);
                        job.killIncappedTarget = true;

                        if (living.IsAnimal && pawn.IsPlayerControlled)
                        {
                            pawn.Map.designationManager.RemoveAllDesignationsOn(rage.target);
                            pawn.Map.designationManager.AddDesignation(new Designation(rage.target, DesignationDefOf.Hunt));
                        }
                        return job;
                    }
                    else
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, rage.target);
                        job.killIncappedTarget = true;

                        return job;
                    }
                }

                if (compMatureMorph.TryGetPathRecoveryJob(out Job recoveryJob))
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is performing path recovery job " + recoveryJob);
                    }
                    return recoveryJob;
                }

                if (compMatureMorph.ShouldMature())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is trying to mature.");
                    }

                    if (!XMTUtility.IsSpace(pawn.MapHeld))
                    {

                        if (XMTHiveUtility.IsLightSuitableAt(pawn.PositionHeld, pawn.MapHeld) && pawn.Faction == null && !XMTHiveUtility.HasCocooned(pawn.Map))
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is leaving the map");
                            }
                            return base.TryGiveJob(pawn);
                        }
                    }

                    IntVec3 cell = XMTHiveUtility.GetValidCocoonCell(pawn.Map, pawn);

                    if (cell.IsValid)
                    {
                        
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Mature, cell);
                        FeralJobUtility.ReservePlaceForJob(pawn, job, cell);
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is starting mature job at " + cell);
                        }
                        return job;
                    }
                    else
                    {
                        cell = pawn.PositionHeld;
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Mature, cell);
                        FeralJobUtility.ReservePlaceForJob(pawn, job, cell);
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is starting mature job at " + cell);
                        }
                        return job;
                    }

                    if (pawn.Faction != null && pawn.Faction.IsPlayer)
                    {
                        Messages.Message("XMT_NoRoomToMature".Translate(), MessageTypeDefOf.NegativeEvent);
                    }
                }

                if (compMatureMorph.ShouldGorge())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is gorging");
                    }
                    Job foodJob = !pawn.ageTracker.Adult?  GetFoodJob(pawn, true, 0.45f) : GetFoodJob(pawn, true);

                    if (foodJob != null)
                    {
                        return foodJob;
                    }
                    else if(pawn.Faction == null && !pawn.ageTracker.Adult)
                    {
                        if (XMTUtility.IsSpace(pawn.MapHeld))
                        {
                            foodJob = GetFoodJob(pawn, true);
                            if (foodJob != null)
                            {
                                return foodJob;
                            }
                        }
                        else
                        {
                            if (XMTHiveUtility.HasCocooned(pawn.Map))
                            {
                                Job restJob = XMTHiveUtility.GetHiveRestJob(pawn);
                                if (restJob != null)
                                {
                                    return restJob;
                                }

                                Job wanderJob = XMTHiveUtility.GetHiveWanderJob(pawn);
                                if (wanderJob != null)
                                {
                                    return wanderJob;
                                }

                                return null;
                            }

                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is leaving the map for safe food");
                            }

                            return base.TryGiveJob(pawn);
                        }
                    }

                }

                if (compMatureMorph.ShouldSnuggle())
                {
                    Pawn SnuggleTarget = compMatureMorph.GetSnuggleTarget();
                    if (SnuggleTarget != null)
                    {
                        if (pawn.Faction == null)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is seducing " + SnuggleTarget);
                            }
                            return JobMaker.MakeJob(XenoWorkDefOf.XMT_Seduce, SnuggleTarget);
                        }

                        return JobMaker.MakeJob(XenoWorkDefOf.XMT_Snuggle, SnuggleTarget);
                    }
                }

                if (compMatureMorph.ShouldBeInNest())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " thinks they should be in the nest.");
                    }

                    if (XMTHiveUtility.IsInsideNest(pawn.Position, pawn.Map))
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is inside the nest.");
                        }

                        Thing offensiveThing = XMTHiveUtility.GetMostOffensiveThingInNest(pawn.Position, pawn.Map);
                        if (offensiveThing != null)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is clearing offensive thing " + offensiveThing + " from the nest.");
                            }

                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, offensiveThing);

                            return job;
                        }

                        if (compMatureMorph.GetCocoonedFeedJob(out Job feedJob))
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is feeding a cocooned pawn while staying in the nest.");
                            }

                            return feedJob;
                        }

                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is sleeping in the nest.");
                        }


                        if (compMatureMorph.NestPosition.InSunlight(pawn.Map))
                        {
                            pawn.needs.rest.CurLevel = 0.25f;
                        }

                        return XMTHiveUtility.GetHiveRestJob(pawn);
                    }
                    else
                    {
                        Job job = XMTHiveUtility.GetHiveRestJob(pawn);

                        if(job == null && !pawn.ageTracker.Adult)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " cannot find way to nest.");
                            }
                            float brightness = pawn.MapHeld.glowGrid.GroundGlowAt(pawn.PositionHeld);

                            if (brightness < 0.5)
                            {
                                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Mature, pawn.PositionHeld);
                                FeralJobUtility.ReservePlaceForJob(pawn, job, pawn.PositionHeld);
                                if (XMTSettings.LogJobGiver)
                                {
                                    Log.Message(pawn + " is starting mature job at " + pawn.PositionHeld);
                                }
                                return job;
                            }
                        }

                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is going to the nest.");
                        }

                        return job;
                    }
                }

                if (compMatureMorph.ShouldTendNest())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " thinks the nest needs tending.");
                    }

                    Job TendNestJob = compMatureMorph.GetTendNestJob();

                    if (TendNestJob != null)
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is doing nest tending job.");
                        }

                        return TendNestJob;
                    }
                }

                if (compMatureMorph.ShouldAbductHost())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " thinks they should abduct hosts.");
                    }

                    if (pawn.IsPsychologicallyInvisible())
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is hidden.");
                        }

                        IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, compMatureMorph.abductRange, true).OfType<Pawn>();
                        if (pawns.Any())
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is near some pawns.");
                            }

                            Pawn target = compMatureMorph.BestAbductCandidate(pawns);
                            if (target != null)
                            {
                                if (XMTSettings.LogJobGiver)
                                {
                                    Log.Message(pawn + " is going to abduct " + target);
                                }


                                return GetAbductJob(pawn, target);
                            }
                        }
                    }
                }

                if (compMatureMorph.ShouldDoMichief())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " thinks they should do michief.");
                    }

                    Job michief;
                    if (compMatureMorph.FindMichief(out michief))
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " found some michief to do.");
                        }
                        return michief;
                    }
                }

                if (pawn.Faction == null || !pawn.Faction.IsPlayer)
                {
                    return GetFeralJob(pawn);
                }

                /*
                //TODO: Implement this logic elsewhere and in a more performant way.
                Job surplusHibernateJob = XMTHiveUtility.GetSurplusHibernationJob(pawn);
                if (surplusHibernateJob != null)
                {
                    return surplusHibernateJob;
                }

                Job hiveRestJob = XMTHiveUtility.GetHiveRestJob(pawn);
                if (hiveRestJob != null && pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.25f)
                {
                    return hiveRestJob;
                }
                */
            }
            return null;
        }
    }
}
