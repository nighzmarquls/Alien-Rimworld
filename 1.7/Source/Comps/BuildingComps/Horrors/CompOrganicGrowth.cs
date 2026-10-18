using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class OrganicFeedingResource
    {
        public QueenIngestibleResourceDef resourceDef;
        public float growthPerUnit = 0.01f;
    }

    public class CompProperties_OrganicGrowth : CompProperties
    {
        public List<OrganicFeedingResource> feedingResources = new List<OrganicFeedingResource>();
        public bool feedOnTerrain = true;
        public int maxItemsPerFeeding = 1;
        public float maxBodySize = 8f;
        public float visualSizePerBodySize = 0.5f;
        public float statExponent = 2f;
        public int feedingInterval = 2500;
        public float shrinkWhenDamaged = 0.1f;
        public CompProperties_OrganicGrowth() { compClass = typeof(CompOrganicGrowth); }
        public CompProperties_OrganicGrowth(Type type) { compClass = type; }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (maxItemsPerFeeding < 1) yield return "Organic feeding maxItemsPerFeeding must be positive.";
            foreach (OrganicFeedingResource food in feedingResources)
                if (food?.resourceDef == null || food.growthPerUnit <= 0f)
                    yield return "Organic feeding resources require a resourceDef and positive growthPerUnit.";
            foreach (string error in ResourceHelperUtility.TerrainConfigErrors(feedingResources.Where(f => f?.resourceDef?.terrainProfiles != null).SelectMany(f => f.resourceDef.terrainProfiles))) yield return error;
        }
    }

    public class CompOrganicGrowth : ThingComp
    {
        protected float bodySize = 1f;
        protected int tickCountUp;
        protected bool NotFedThisHour;
        private Pawn legacyPawn;
        protected CompProperties_OrganicGrowth GrowthProps => (CompProperties_OrganicGrowth)props;
        public float VisualScale => Mathf.Max(0.01f, bodySize * GrowthProps.visualSizePerBodySize);
        public int RealMaxHP => parent.MaxHitPoints;
        public float TotalBodySize() => bodySize;
        protected virtual ThingDef FeedingFilth => InternalDefOf.Starbeast_Filth_Resin;
        protected virtual bool InheritInjuries => false;
        protected BuildingOrigin Origin => (parent as XMTBase_Building)?.Origins.FirstOrDefault();
        protected Pawn Progenitor => parent.GetComp<CompPawnPreservation>()?.Entries.FirstOrDefault()?.Pawn;

        public virtual void SetProgenitor(Pawn pawn)
        {
            bodySize = Mathf.Clamp(XMTUtility.GetFinalBodySize(pawn), 0.01f, GrowthProps.maxBodySize);
            ClearSizeCaches();
            float health = InheritInjuries ? Mathf.Clamp01(1f - PreservedBuildingUtility.InjuryDebt(pawn) / pawn.health.LethalDamageThreshold) : 1f;
            parent.HitPoints = Mathf.Max(1, Mathf.RoundToInt(parent.MaxHitPoints * health));
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            // Old comps deep-saved this pawn. Never deep-save the new owner's pawn twice.
            if (Scribe.mode == LoadSaveMode.LoadingVars) Scribe_Deep.Look(ref legacyPawn, "progenitorPawn");
            Scribe_Values.Look(ref bodySize, "bodySize", 1f);
            Scribe_Values.Look(ref tickCountUp, "tickCount", 0);
            Scribe_Values.Look(ref NotFedThisHour, "notFedThisHour");
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs && legacyPawn != null)
                Scribe_Deep.Look(ref legacyPawn, "progenitorPawn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyPawn != null)
            {
                Scribe_Deep.Look(ref legacyPawn, "progenitorPawn");
                PreservedBuildingUtility.MigrateLegacyPawn(parent, legacyPawn);
                legacyPawn = null;
            }
        }
        public override float GetStatFactor(StatDef stat)
        {
            return stat == StatDefOf.MaxHitPoints || stat == StatDefOf.Mass || stat == StatDefOf.MarketValue
                ? Mathf.Pow(Mathf.Max(0.01f, bodySize), GrowthProps.statExponent) : base.GetStatFactor(stat);
        }
        public void ConsumePruningGrowth(float amount) { bodySize = Mathf.Max(0.01f, bodySize - amount); ClearSizeCaches(); parent.HitPoints = Mathf.Min(parent.HitPoints, parent.MaxHitPoints); }
        protected void ClearSizeCaches()
        {
            StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
            StatDefOf.Mass.Worker.ClearCacheForThing(parent);
            StatDefOf.MarketValue.Worker.ClearCacheForThing(parent);
        }
        protected virtual void TickSpecial() { }
        protected virtual void FeedSpecial() { }
        protected virtual void OnStarved() { }
        public override void CompTick()
        {
            if (!parent.Spawned) return;
            tickCountUp++;
            TickSpecial();
            if (parent.Destroyed || !parent.Spawned) return;
            parent.HitPoints = Mathf.Min(parent.HitPoints, parent.MaxHitPoints);
            if (tickCountUp < Mathf.Max(1, GrowthProps.feedingInterval)) return;
            tickCountUp = 0;
            NotFedThisHour = true;
            if (bodySize < GrowthProps.maxBodySize || parent.HitPoints < parent.MaxHitPoints)
            {
                DrainBestCell(GenRadial.RadialCellsAround(parent.Position, parent.def.specialDisplayRadius, true));
                FeedSpecial();
            }
            if (NotFedThisHour) OnStarved();
            if (parent.Destroyed || !parent.Spawned) return;
            if (parent.HitPoints < parent.MaxHitPoints)
            {
                int hp = parent.HitPoints;
                bodySize = Mathf.Max(0f, bodySize - GrowthProps.shrinkWhenDamaged);
                ClearSizeCaches();
                if (bodySize <= 0f) { parent.Kill(); return; }
                parent.HitPoints = Mathf.Min(hp, parent.MaxHitPoints);
            }
            parent.GetComp<CompPawnPreservation>()?.HealToBuildingHealth();
        }
        protected void ApplyGrowth(float growth)
        {
            if (growth <= 0f) return;
            if (parent.HitPoints < parent.MaxHitPoints)
                parent.HitPoints = Mathf.Min(parent.MaxHitPoints, parent.HitPoints + Mathf.CeilToInt(parent.MaxHitPoints * growth));
            else
            {
                bodySize = Mathf.Min(GrowthProps.maxBodySize, bodySize + growth);
                ClearSizeCaches();
                parent.HitPoints = parent.MaxHitPoints;
            }
            NotFedThisHour = false;
            parent.GetComp<CompPawnPreservation>()?.HealToBuildingHealth();
        }
        protected void DrainBestCell(IEnumerable<IntVec3> cells)
        {
            Map map = parent.Map;
            ResourceHelperUtility.TerrainEvaluation bestTerrain = null;
            IntVec3 bestCell = IntVec3.Invalid;
            float bestValue = 0f;
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map)
                    .OrderBy(thing => thing is Corpse ? 0 : thing is Plant ? 1 : 2).ToList())
                {
                    if (thing == parent || thing is Pawn || thing.Destroyed || !thing.Spawned) continue;
                    int count = Mathf.Min(thing.stackCount, Mathf.Max(1, GrowthProps.maxItemsPerFeeding));
                    float growth = GrowthFromThing(thing, count);
                    if (growth <= 0f || float.IsNaN(growth) || float.IsInfinity(growth)) continue;
                    if (!ResourceHelperUtility.Consume(thing, count)) continue;
                    ApplyGrowth(growth);
                    LeaveFilth(cell);
                    return;
                }
                var terrain = ResourceHelperUtility.EvaluateTerrain(GrowthProps.feedingResources
                    .Where(f => ResourceHelperUtility.Available(f?.resourceDef, parent)).Select(f => f.resourceDef), cell, map, GrowthProps.feedOnTerrain);
                float terrainGrowth = GrowthProps.feedingResources.Sum(f => f?.resourceDef != null && terrain.values.TryGetValue(f.resourceDef, out float value) ? value * f.growthPerUnit : 0f);
                if (terrainGrowth > bestValue) { bestValue = terrainGrowth; bestCell = cell; bestTerrain = terrain; }
            }
            if (bestValue > 0f && ResourceHelperUtility.ApplyTerrain(bestTerrain, bestCell, map))
            {
                ApplyGrowth(bestValue / Mathf.Max(1f, bodySize));
                LeaveFilth(bestCell);
            }
        }
        protected virtual float GrowthFromThing(Thing thing, int count)
        {
            float value = 0f;
            foreach (OrganicFeedingResource food in GrowthProps.feedingResources)
            {
                if (!ResourceHelperUtility.Available(food?.resourceDef, parent)) continue;
                float material = ResourceHelperUtility.ValueFor(thing, food.resourceDef, count);
                // Temperature uses the consumed material as its basis; no separate free unit.
                if (material > 0f) value += material * (1f + ResourceHelperUtility.TemperatureYield(food.resourceDef, thing.PositionHeld, thing.MapHeld)) * food.growthPerUnit;
            }
            return value / Mathf.Max(1f, bodySize);
        }        private void LeaveFilth(IntVec3 cell)
        {
            if (FeedingFilth != null) FilthMaker.TryMakeFilth(cell, parent.Map, FeedingFilth);
        }
    }
}
