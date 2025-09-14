using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{ 
    public class CompPetrolsump : ThingComp
    {
        Pawn progenitorPawn = null;
        float bodySize = 1;
        float breathingSize = 0;
        public int SporeMinHP => Props.HitpointsPerSpore;
        public int RealMaxHP => parent.MaxHitPoints;
        public Vector2 DisplaySize => Vector2.one* displaySize;
        float interiorTemperature = 0;
        float displaySize =>  (bodySize/2) + breathingSize;

        bool NotFedThisHour = false;
        bool HeatFedThisHour = false;

        int tickCountUp = 0;

        CompPetrolsumpProperties Props => props as CompPetrolsumpProperties;

        float tickBreath => (Props.maxSizeChange) / (Props.breathticks / 2);
        bool inhaling;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref progenitorPawn, "progenitorPawn");
            Scribe_Values.Look(ref bodySize, "bodySize", 1);

            Scribe_Values.Look(ref tickCountUp, "tickCount", 0);
        }

        public void SetProgenitor(Pawn pawn)
        {
            progenitorPawn = pawn;
            bodySize = XMTUtility.GetFinalBodySize(pawn);
            parent.HitPoints = parent.MaxHitPoints;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if(stat == StatDefOf.MaxHitPoints || stat == StatDefOf.Mass || stat == StatDefOf.MarketValue)
            {
                return bodySize*bodySize;
            }
            return base.GetStatFactor(stat);
        }

        public float TotalBodySize()
        { 
            return bodySize;
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.Burn || dinfo.Def == DamageDefOf.Bomb)
            {
                dinfo.SetAmount(0);
                absorbed = true;
                HeatFedThisHour = true;
                return;
            }

            if (parent.HitPoints > Props.HitpointsPerSpore)
            {
                if (dinfo.Amount < Props.HitpointsPerSpore)
                {
                    dinfo.SetAmount(Props.HitpointsPerSpore);
                }

                ReleaseSpore(parent.Position, parent.Map);
            }
            base.PostPreApplyDamage(ref dinfo, out absorbed);
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            List<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, 1.5f, false).ToList();
            cells.Shuffle();
            
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            DropFuel(Mathf.RoundToInt(totalDamageDealt / 2));

            

            int fireCount = 0;
            foreach (IntVec3 cell in cells)
            {
                FireUtility.TryStartFireIn(cell, parent.Map, 0.5f, parent);
                fireCount++;
                if(fireCount >= Props.maxFires)
                {
                    break;
                }
            }
        }

        public void DropFuel(int stackTotal)
        {
            DropFuel(stackTotal, parent.Map);
        }
        public void DropFuel(int stackTotal, Map currentmap)
        {
            ThingDef fuelDef = ThingDefOf.Chemfuel;

            XMTUtility.DropAmountThing(fuelDef, stackTotal, parent.Position, currentmap, ThingDefOf.Filth_Fuel);

            
        }

        public void ReleaseSpore(IntVec3 cell,  Map targetMap)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(Props.sporeKindDef, parent.Faction);
            request.FixedBiologicalAge = 0;
            Pawn spore = PawnGenerator.GeneratePawn(request);

            GenSpawn.Spawn(spore, cell, targetMap);
        }


        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }

            tickCountUp++;

            if (tickCountUp % 240 == 0)
            {
                
                if (interiorTemperature < Props.minTemperature)
                {
                    //Log.Message(parent + " interior temp reported: " + interiorTemperature + " degrees");
                    if (!parent.Position.UsesOutdoorTemperature(parent.Map))
                    {
                        if (NotFedThisHour)
                        {
                            //Log.Message(parent + " not fed this hour");
                            parent.HitPoints -= 1;
                        }
                       
                    }
                }
                else
                {
                    if(interiorTemperature >= 100f)
                    {
                        HeatFedThisHour = true;
                    }
                }
            }

            if (inhaling)
            {
                breathingSize += tickBreath;

                if (breathingSize > Props.maxSizeChange)
                {
                    inhaling = false; 
                }
            }
            else
            {
                breathingSize -= tickBreath;

                if (breathingSize < -Props.maxSizeChange)
                {
                    inhaling = true;
                }

            }

            if (parent.HitPoints > parent.MaxHitPoints)
            {
                parent.HitPoints = parent.MaxHitPoints;
            }

            if (tickCountUp >= 2500)
            {
                //Log.Message(parent + " Reached Hour Check " + interiorTemperature);
                tickCountUp = 0;
                NotFedThisHour = true;
                if ( bodySize < 8f)
                {
                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true);
                    if (cells.Any())
                    {
                        DrainBestCell(cells);
                    }
                    if (HeatFedThisHour)
                    {
                        float growth = 0.1f;
                        if (parent.HitPoints < parent.MaxHitPoints)
                        {
                            parent.HitPoints += Mathf.CeilToInt(parent.MaxHitPoints * growth);
                        }
                        else
                        {
                            bodySize += growth;
                            StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
                            parent.HitPoints = parent.MaxHitPoints;
                        }
                    }
                }

                
                //Log.Message(parent + " Not Fed this Hour: " + NotFedThisHour);
                if (parent.HitPoints < parent.MaxHitPoints)
                {

                    int hitPointsCache = parent.HitPoints;
                    bodySize -= 0.1f;
                    StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
                    parent.HitPoints = hitPointsCache;
                    if (bodySize <= 0)
                    {
                        parent.Kill();
                    }
                }
                HeatFedThisHour = false;
            }

        }

        protected void DrainBestCell(IEnumerable<IntVec3> Cells)
        {
           
            IntVec3 bestCell = Cells.First();

            TerrainDef bestTerrain = bestCell.GetTerrain(parent.Map);

            float growth = 0;
            foreach (IntVec3 cell in Cells)
            {
               
                IEnumerable<Corpse> corpses = parent.Map.thingGrid.ThingsListAt(cell).OfType<Corpse>();

               
                if (corpses.Any())
                {
                    Corpse corpse = corpses.First();

                

                    growth = corpse.GetStatValue(StatDefOf.MeatAmount) / 75;

                    CompRottable compRottable = corpse.TryGetComp<CompRottable>();
                    if (compRottable != null)
                    {
                 
                        if (compRottable.Stage == RotStage.Dessicated)
                        {
                          
                            growth *= 0.25f;
                        }
                    }

                   
                    growth = bodySize > 1 ? growth / bodySize : growth;

                    if (corpse.IsDessicated())
                    {
                        if (corpse.MapHeld is Map map)
                        {
                            map.reservationManager.ReleaseAllForTarget(corpse);
                        }
                    }

                    corpse.Destroy();

                    if (parent.HitPoints < parent.MaxHitPoints)
                    {
                        parent.HitPoints += Mathf.CeilToInt(parent.MaxHitPoints * growth);
                    }
                    else
                    {
                        bodySize += growth;
                        StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
                        parent.HitPoints = parent.MaxHitPoints;
                    }
                    
                    FilthMaker.TryMakeFilth(cell, parent.Map, ThingDefOf.Filth_Fuel);
                    
                    NotFedThisHour = false;
                    return;
                }
               
                IEnumerable<Plant> plants = parent.Map.thingGrid.ThingsListAt(cell).OfType<Plant>();
                if (plants.Any())
                {
                    Plant plant = plants.First();

                    growth = (plant.Growth * plant.GetStatValue(StatDefOf.Nutrition) * 0.5f)/(bodySize > 1? bodySize : 1);
                    plant.Kill();
                    
                    if(parent.HitPoints < parent.MaxHitPoints)
                    {
                        parent.HitPoints += Mathf.CeilToInt(parent.MaxHitPoints * growth);
                    }
                    else
                    {
                        bodySize += growth;
                        StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
                        parent.HitPoints = parent.MaxHitPoints;
                    }

                    FilthMaker.TryMakeFilth(cell, parent.Map, ThingDefOf.Filth_Fuel);

                    NotFedThisHour = false;
                    return;
                }
                TerrainDef foundTerrain = cell.GetTerrain(parent.Map);
                if(foundTerrain.fertility > bestTerrain.fertility)
                {
                    bestCell = cell;
                    bestTerrain = foundTerrain;
                }

            }

            if (XenoformingUtility.CellIsFertile(bestCell,parent.Map))
            {
                growth = (XenoformingUtility.DegradeTerrainOnCell(parent.Map, bestCell, bestTerrain) * 0.1f);

                if (parent.HitPoints < parent.MaxHitPoints)
                {
                    parent.HitPoints += Mathf.CeilToInt(parent.MaxHitPoints * growth);
                }
                else
                {
                    bodySize += growth;
                    StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
                    parent.HitPoints = parent.MaxHitPoints;
                }

                FilthMaker.TryMakeFilth(bestCell, parent.Map, ThingDefOf.Filth_Fuel);
                NotFedThisHour = false;
            }
        }
    }


    public class CompPetrolsumpProperties : CompProperties
    {
        public int maxFires = 3;
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public float minTemperature = 20f;
        public int HitpointsPerSpore = 50;
        public PawnKindDef sporeKindDef;
        public CompPetrolsumpProperties()
        {
            this.compClass = typeof(CompPetrolsump);
        }

        public CompPetrolsumpProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
