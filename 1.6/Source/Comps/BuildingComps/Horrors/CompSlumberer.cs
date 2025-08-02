using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype { 
    internal class CompSlumberer : ThingComp
    {
        Pawn progenitorPawn = null;
        float bodySize = 1;
        float breathingSize = 0;
        public int RealMaxHP => parent.MaxHitPoints;
        public Vector2 DisplaySize => Vector2.one * displaySize;
        float interiorTemperature = 0;
        float displaySize => (bodySize / 2) + breathingSize;

        bool NotFedThisHour = false;

        int tickCountUp = 0;

        const float maxBodySize = 8f;
        CompSlumbererProperties Props => props as CompSlumbererProperties;

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
            float pawnsize = XMTUtility.GetFinalBodySize(pawn);
            bodySize = Mathf.Min(maxBodySize, pawnsize);
            Log.Message(pawn + " had a bodysize of " + bodySize + " calculated Body Size " + bodySize);
            float damageFactor = 0;
            for (int i = 0; i < progenitorPawn.health.hediffSet.hediffs.Count; i++)
            {
                if (progenitorPawn.health.hediffSet.hediffs[i] is Hediff_Injury)
                {
                    damageFactor += progenitorPawn.health.hediffSet.hediffs[i].Severity;
                }
            }
            damageFactor = damageFactor/progenitorPawn.health.LethalDamageThreshold;
            StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
            parent.HitPoints = Mathf.FloorToInt((parent.MaxHitPoints) * (1 - damageFactor));

        }

        public override float GetStatFactor(StatDef stat)
        {
            if (stat == StatDefOf.MaxHitPoints || stat == StatDefOf.Mass || stat == StatDefOf.MarketValue)
            {
                return bodySize * bodySize;
            }
            return base.GetStatFactor(stat);
        }
        public float TotalBodySize()
        {
            return bodySize;
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {

            base.PostPreApplyDamage(ref dinfo, out absorbed);
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            List<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, 1.5f, false).ToList();
            cells.Shuffle();
            if ((parent.MaxHitPoints / 2 ) < parent.HitPoints)
            {
                AwakenSleeper(parent.PositionHeld, parent.MapHeld);
            }
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
        }

        public void AwakenSleeper(IntVec3 cell, Map targetMap)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(Props.slumbererKindDef, parent.Faction);
            request.FixedBiologicalAge = 0;
            Pawn slumberer = PawnGenerator.GeneratePawn(request);
            CompAwakenedSlumberer comp = slumberer.GetComp<CompAwakenedSlumberer>();
            if (comp != null)
            {
                comp.InitializeSleeper(bodySize);
            }

            slumberer = GenSpawn.Spawn(slumberer, cell, targetMap) as Pawn;

            if (slumberer.Spawned)
            {
                if(parent.HitPoints < parent.MaxHitPoints)
                {
                    float percentage = (float) parent.HitPoints / (float)parent.MaxHitPoints;
                    float max = slumberer.health.LethalDamageThreshold;

                    float hitpoint = max * percentage;
                    Log.Message(parent + " percentage:" + percentage + " max:" + max + " hitpoint:" + hitpoint);

                    while (hitpoint < max)
                    {
                        DamageWorker.DamageResult result = slumberer.TakeDamage(new DamageInfo(DamageDefOf.Crush, 2,armorPenetration:100));
                        hitpoint += 10;
                        Log.Message(slumberer + " taking " + result.totalDamageDealt + " damage");
                    }
                }
                slumberer.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, forced: true);
        
                parent.Destroy();
            }
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
                            AwakenSleeper(parent.PositionHeld, parent.MapHeld);
                        }

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
                if (bodySize < maxBodySize)
                {
                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true);
                    if (cells.Any())
                    {
                        DrainBestCell(cells);
                    }

                }

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

                    FilthMaker.TryMakeFilth(cell, parent.Map, InternalDefOf.Starbeast_Filth_Resin);

                    NotFedThisHour = false;
                    return;
                }

                IEnumerable<Plant> plants = parent.Map.thingGrid.ThingsListAt(cell).OfType<Plant>();
                if (plants.Any())
                {
                    Plant plant = plants.First();

                    growth = (plant.Growth * plant.GetStatValue(StatDefOf.Nutrition) * 0.5f) / (bodySize > 1 ? bodySize : 1);
                    plant.Kill();

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

                    FilthMaker.TryMakeFilth(cell, parent.Map, InternalDefOf.Starbeast_Filth_Resin);

                    NotFedThisHour = false;
                    return;
                }
                TerrainDef foundTerrain = cell.GetTerrain(parent.Map);
                if (foundTerrain.fertility > bestTerrain.fertility)
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

                FilthMaker.TryMakeFilth(bestCell, parent.Map, InternalDefOf.Starbeast_Filth_Resin);
                NotFedThisHour = false;
            }
        }
    }


    public class CompSlumbererProperties : CompProperties
    {
        public int maxFires = 3;
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public float minTemperature = 20f;
        public PawnKindDef slumbererKindDef;
        public CompSlumbererProperties()
        {
            this.compClass = typeof(CompSlumberer);
        }

        public CompSlumbererProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
