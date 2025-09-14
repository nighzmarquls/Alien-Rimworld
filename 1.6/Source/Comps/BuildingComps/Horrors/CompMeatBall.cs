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
    public class CompMeatBall : ThingComp
    {
        Pawn progenitorPawn = null;
        float bodySize = 1;
        float breathingSize = 0;
        float heatPushCache = 0;
        public int RealMaxHP => parent.MaxHitPoints;
        public Vector2 DisplaySize => Vector2.one* displaySize;
        float interiorTemperature = 0;
        float displaySize =>  (bodySize/2) + breathingSize;

        bool NotFedThisHour = false;

        int tickCountUp = 0;

        CompMeatBallProperties Props => props as CompMeatBallProperties;

        public Color progenitorSkinColor => progenitorPawn == null ? parent.def.graphic.color : XMTUtility.GetSkinColorFrom(progenitorPawn);
        float tickBreath => (Props.maxSizeChange) / (Props.breathticks / 2);
        bool inhaling;
        internal float harvestWork => Props.harvestWork;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref progenitorPawn, "progenitorPawn");
            Scribe_Values.Look(ref bodySize, "bodySize", 1);
            Scribe_Values.Look(ref heatPushCache, "heatPushCache", 0);
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

        public ThingDef GetMeat()
        {
            return progenitorPawn == null ? ThingDefOf.Meat_Human : progenitorPawn.RaceProps.meatDef;
        }

        public ThingDef GetBlood()
        {
            return progenitorPawn == null ? ThingDefOf.Filth_Blood : progenitorPawn.RaceProps.BloodDef;
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            DropMeat(Mathf.RoundToInt(totalDamageDealt / 2));
        }

        public void DropMeat(int stackTotal)
        {
            DropMeat(stackTotal, parent.Map);
        }
        public void DropMeat(int stackTotal, Map currentmap)
        {
            ThingDef meatDef = GetMeat();

            XMTUtility.DropAmountThing(meatDef, stackTotal, parent.Position, currentmap, GetBlood());
        }

        public bool CanBePruned()
        {
            return bodySize > 1 && parent.HitPoints > 75;
        }
        public void PruneMeatBall(Pawn pruner)
        {
            float TotalYieldHarvest = pruner.GetStatValue(StatDefOf.PlantHarvestYield);
            if (bodySize > 1)
            {
                float SquareRoot = Mathf.Sqrt(bodySize);
                TotalYieldHarvest = (SquareRoot*0.25f);
            }
            else
            {
                Log.Warning(parent + " is being pruned when too small.");
                return;
            }

            TotalYieldHarvest = Mathf.Min(TotalYieldHarvest, bodySize - 1);


            ThingDef meatDef = GetMeat();
            int harvestedStack = Mathf.FloorToInt(meatDef.stackLimit * TotalYieldHarvest);

            if (harvestedStack > 0)
            {
                DropMeat(harvestedStack);
                parent.HitPoints -= harvestedStack;
            }
        }

        protected bool CellIsFertile(IntVec3 cell)
        {
            TerrainDef foundTerrain = cell.GetTerrain(parent.Map);
            return foundTerrain.fertility > 0 || (foundTerrain.driesTo != null && foundTerrain.driesTo.fertility > 0);
        }
        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }

            tickCountUp++;
            if (tickCountUp % 60 == 0)
            {
                //Log.Message(parent + " pushing heat: " + heatPushCache + " pushed" + " interior temperature : " + interiorTemperature);
                interiorTemperature = parent.Position.GetTemperature(parent.Map);
                if (heatPushCache > 0)
                {
                    GenTemperature.PushHeat(parent.Position, parent.Map, heatPushCache);
                }
            }

            if (tickCountUp % 240 == 0)
            {
                if (interiorTemperature < Props.IdealTemperature)
                {
                    //Log.Message(parent + " interior temp reported: " + interiorTemperature + " degrees");
                    if (!parent.Position.UsesOutdoorTemperature(parent.Map))
                    {
                        if (NotFedThisHour)
                        {
                            //Log.Message(parent + " not fed this hour");
                            parent.HitPoints -= 1;
                        }
                        heatPushCache = bodySize * 42;
                        //Log.Message(parent + " pushing heat " + heatPushCache + " cached");
                    }
                }
                else
                {
                    heatPushCache = 0;
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
                if ( bodySize < 8f)
                {
                    NotFedThisHour = true;
                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true);
                    if (cells.Any())
                    {
                        DrainBestCell(cells);
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

                

                    growth = corpse.GetStatValue(StatDefOf.MeatAmount) / GetMeat().stackLimit;

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
                    if (ModsConfig.AnomalyActive)
                    {
                        FilthMaker.TryMakeFilth( cell, parent.Map, ThingDefOf.Filth_TwistedFlesh);
                    }
                    else
                    {
                        FilthMaker.TryMakeFilth(cell, parent.Map, ThingDefOf.Filth_CorpseBile);
                    }
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

                    if (ModsConfig.AnomalyActive)
                    {
                        FilthMaker.TryMakeFilth(cell, parent.Map, ThingDefOf.Filth_TwistedFlesh);
                    }
                    else
                    {
                        FilthMaker.TryMakeFilth(cell, parent.Map, ThingDefOf.Filth_CorpseBile);
                    }
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

            if (XenoformingUtility.CellIsFertile(bestCell, parent.Map))
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
                NotFedThisHour = false;
            }
        }
    }


    public class CompMeatBallProperties : CompProperties
    {
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public float harvestWork = 250f;
        public float IdealTemperature = 20f;
        public CompMeatBallProperties()
        {
            this.compClass = typeof(CompMeatBall);
        }

        public CompMeatBallProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
