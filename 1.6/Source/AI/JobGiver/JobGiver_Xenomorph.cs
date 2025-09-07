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
    public class JobGiver_Xenomorph : ThinkNode_JobGiver
    {
        protected Job GetFeralJob(Pawn pawn)
        {
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " is getting Feral Job");
            }
            IEnumerable<Pawn> colonyPawns = pawn.Map.PlayerPawnsForStoryteller.Where(x => !XMTUtility.IsMorphing(x) && !XMTUtility.HasEmbryo(x));
            IEnumerable<Pawn> hostPawns = pawn.Map.mapPawns.AllPawnsSpawned.Where(x => XMTUtility.IsHost(x));
            Need_Food food = pawn.needs.food;
            bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;

            CompMatureMorph compMatureMorph = pawn.GetMorphComp();

            bool shouldOvamorph = compMatureMorph.ShouldOvamorphCandidate();
            bool hasNest = HiveUtility.HasCocooned(pawn.Map);

            if (food.CurCategory == HungerCategory.Fed)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is getting fed Job there are " + hostPawns.Count() + " possible hosts");
                }
                if (hostPawns.Any())
                {
                    if (compMatureMorph != null)
                    {
                        if (compMatureMorph.ShouldAbductHost() && !(shouldOvamorph && hasNest) )
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " thinks a host should be abducted");
                            }
                            Pawn target = compMatureMorph.BestAbductCandidate(hostPawns);
                            if (target != null)
                            {
                                if (XMTSettings.LogJobGiver)
                                {
                                    Log.Message(pawn + " thinks " + target + " should be abducted");
                                }
                                
                               
                                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, target, compMatureMorph.NestPosition);
                                job.count = 1;
                                pawn.Reserve(target, job);
                                return job;
                                

                            }
                        }
                    }
                }
            }
            else
            {
               Job job = GetFoodJob(pawn,desperate);
               if(job != null)
               {
                   return job;
               }
            }

            if (compMatureMorph != null)
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(pawn + " is doing Feral Activity");
                }

                if (HiveUtility.IsInsideNest(pawn.Position,pawn.Map) && HiveUtility.IsCloseToNest(pawn.Position, pawn.Map))
                {
                    if (shouldOvamorph)
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " thinks a candidate should be ovamorphed.");
                        }
                        Pawn target = HiveUtility.GetOvamorphingCandidate(pawn.Map);
                        if (target != null)
                        {
                            pawn.Map.reservationManager.ReleaseAllForTarget(target);
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is going to ovamorph " + target);
                            }
                            if (!target.Spawned)
                            {
                                HiveUtility.RemoveHost(target, pawn.Map);
                            }
                            else
                            {
                                return JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyOvamorphing, target);
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
                                Log.Message(pawn + " is hunting the last colony pawn.");
                            }
                           
                            Pawn target = UndownedPawns.First();

                            return JobMaker.MakeJob(XenoWorkDefOf.XMT_StealthHunt, target);

                        }
                        else if (pawn.needs.rest.CurLevelPercentage < 0.25f)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is napping.");
                            }
                           
                            return JobMaker.MakeJob(JobDefOf.Wait_Asleep);
                        }
                        else if (shouldHunt && UndownedPawns.Any())
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is hunting a random colony pawn.");
                            }
                            
                            Pawn target = UndownedPawns.RandomElement();

                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is hunting " + target);
                            }

                            return JobMaker.MakeJob(XenoWorkDefOf.XMT_StealthHunt, target);
                    
                        }
                    }

                    if (compMatureMorph.ShouldBeInNest())
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + "should be in the nest.");
                        }

                        return JobMaker.MakeJob(JobDefOf.Goto, compMatureMorph.NestPosition);
                    }

                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " has nothing else to do.");
                    }
                    //TODO: Do something here.

                }
                else
                {
                    return JobMaker.MakeJob(JobDefOf.Goto, compMatureMorph.NestPosition);
                }
                
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " has no feral jobs valid.");
            }
           
            if(hasNest)
            {
                return JobMaker.MakeJob(JobDefOf.Goto, compMatureMorph.NestPosition);
            }
            return null;
            
        }

        protected Job GetFoodJob(Pawn pawn, bool desperate)
        {
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " is getting food Job");
            }
            bool caresAboutForbidden = ForbidUtility.CaresAboutForbidden(pawn, false);
            if (!FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, desperate,
                out Thing foodSource, out var foodDef,
                canRefillDispenser: false, canUseInventory: true, canUsePackAnimalInventory: false,
                allowForbidden: !caresAboutForbidden, allowCorpse: true, allowSociallyImproper: true, allowHarvest: false, forceScanWholeMap: true, ignoreReservations: true, calculateWantedStackCount: false, allowVenerated: true))
            {
                IEnumerable<Pawn> SuitablePrey = pawn.Map.spawnedThings.OfType<Pawn>().Where(p => !XMTUtility.NotPrey(p) && !XMTUtility.IsInorganic(p));
                if (SuitablePrey.Any())
                {
                    int distance = int.MaxValue;
                    foreach(Pawn prey in SuitablePrey)
                    {
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
                            preydistance = distance;
                            foodSource = prey;
                        }

                    }
                }
                if(foodSource == null)
                {
                    return null;
                }
            }

            if (foodSource is Pawn foodPawn)
            {
                if (foodPawn.Dead)
                {
                    foodSource = foodPawn.Corpse;
                }
                else
                {
                    pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
                    Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, foodPawn);
                    job.killIncappedTarget = true;
                    return job;
                }
            }

            float nutrition = FoodUtility.GetNutrition(pawn, foodSource, foodDef);
            Pawn pawn3 = (foodSource.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (pawn3 != null && pawn3 != pawn)
            {
                Job job2 = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, foodSource, pawn3);
                job2.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition);
                return job2;
            }

            Job job3 = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
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
                    Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, rage.target);
                    job.killIncappedTarget = true;
                    pawn.Map.designationManager.RemoveAllDesignationsOn(rage.target);
                    pawn.Map.designationManager.AddDesignation(new Designation(rage.target, DesignationDefOf.Hunt));
                    return job;
                }

               

                if (compMatureMorph.ShouldMature())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is trying to mature.");
                    }
                    return JobMaker.MakeJob(XenoWorkDefOf.XMT_Mature, compMatureMorph.NestPosition);
                }

                if (compMatureMorph.ShouldGorge())
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is gorging");
                    }
                    Job foodJob = GetFoodJob(pawn, true);

                    if (foodJob != null)
                    {
                        return foodJob;
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

                    if (HiveUtility.IsInsideNest(pawn.Position, pawn.Map))
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is inside the nest.");
                        }

                        Thing offensiveThing = HiveUtility.GetMostOffensiveThingInNest(pawn.Position, pawn.Map);
                        if (offensiveThing != null)
                        {
                            if (XMTSettings.LogJobGiver)
                            {
                                Log.Message(pawn + " is clearing offensive thing " + offensiveThing + " from the nest.");
                            }

                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, offensiveThing);

                            return job;
                        }

                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is sleeping in the nest.");
                        }


                        if (compMatureMorph.NestPosition.InSunlight(pawn.Map))
                        {
                            pawn.needs.rest.CurLevel = 0.25f;
                            if (pawn.InBed())
                            {
                                return JobMaker.MakeJob(JobDefOf.Wait_Asleep);
                            }
                            else
                            {
                                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(compMatureMorph.NestPosition, 5, true);
                                foreach (IntVec3 cell in cells)
                                {
                                    if (!cell.Standable(pawn.Map))
                                    {
                                        continue;
                                    }

                                    if (cell.TryGetFirstThing(pawn.Map, out CocoonBase thing))
                                    {
                                        continue;
                                    }

                                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HideInSpot, cell);
                                    pawn.Map.reservationManager.Reserve(pawn, job, cell);
                                    return job;
                                }
                            }
                        }
                        else
                        {
                            return JobMaker.MakeJob(JobDefOf.Wait_Asleep, compMatureMorph.NestPosition);
                        }
                    }
                    else
                    {
                        if (XMTSettings.LogJobGiver)
                        {
                            Log.Message(pawn + " is going to the nest.");
                        }

                        return JobMaker.MakeJob(JobDefOf.Goto, compMatureMorph.NestPosition);
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
                                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, target, compMatureMorph.NestPosition);
                                job.count = 1;

                                pawn.Reserve(target, job);
                                return job;
                            }
                        }
                    }
                }
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
                        .Where(x => XMTUtility.IsHost(x));

                    if (pawns.Any())
                    {
                        foreach (Pawn target in pawns)
                        {
                            return JobMaker.MakeJob(XenoWorkDefOf.XMT_ImplantHunt, target);
                        }
                    }
                }
            }
            return null;
        }
    }
}
