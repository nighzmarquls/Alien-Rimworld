using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class CompJellyMaker : ThingComp, IHiveInputProcessor
    {
        private HiveInputSettings inputs;
        private Dictionary<ThingDef, double> outputRemainders = new Dictionary<ThingDef, double>();
        private string remainderSettings;
        public bool LastConversionSucceeded { get; private set; }
        public HiveInputSettings Inputs => inputs ??= new HiveInputSettings(CanPotentiallyConvert);
        public IEnumerable<QueenIngestibleResourceDef> InputResources => Resources.Where(entry => entry?.resourceDef != null).Select(entry => entry.resourceDef);
        private bool CanPotentiallyConvert(ThingDef def) => HiveInputSettings.PotentiallyConvertible(def, Resources, parent);
        public override void PostExposeData()
        {
            base.PostExposeData();
            Inputs.ExposeData();
            Scribe_Collections.Look(ref outputRemainders, "outputRemainders", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref remainderSettings, "remainderSettings");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) outputRemainders ??= new Dictionary<ThingDef, double>();
        }
        private const float MinimumFeralJellyLightSuitability = 0.55f;
        public float WorkPerJelly => Props.processingWork;
        // Retained API: this is the fraction of the source consumed per building cycle.
        public float Efficiency => Props.conversionRate;
        private CompJellyMakerProperties Props => (CompJellyMakerProperties)props;
        public List<ResourceConversionEntry> Resources => Props.resources;
        private PipeSystem.CompResource Network => parent.GetComp<PipeSystem.CompResource>();

        public bool CanMakeIntoJelly(Thing thing) => Inputs.Allows(thing) && Evaluate(thing, 1f).Count > 0;
        public bool CanMakeIntoJelly(ThingDef def) => Inputs.GetStoreSettings().filter.Allows(def) && CanPotentiallyConvert(def);
        public bool CanConvertCell(IntVec3 cell)
            => parent.Map != null && cell.InBounds(parent.Map) && (FindIngredient(cell, parent.Map) != null
                || TerrainCellEmpty(cell, parent.Map) && ResourceHelperUtility.AllocateTerrain(Resources, parent, cell, parent.Map, out _).Count > 0);
        public static bool TerrainCellEmpty(IntVec3 cell, Map map) => map != null && cell.InBounds(map)
            && !cell.GetThingList(map).Any(t => t.def.category == ThingCategory.Item || t is Plant || t is Pawn);
        private Thing FindIngredient(IntVec3 cell, Map map)
            => cell.GetThingList(map).FirstOrDefault(t => t != parent && t.Spawned && !t.Destroyed && t.def.category == ThingCategory.Item && CanMakeIntoJelly(t));

        public bool GetJellyMakingJob(out Job job, bool requireLightLevel = false)
        {
            job = null;
            Region region = parent.GetRegion();
            if (region == null || !(parent is Pawn pawn)) return false;
            if (XMTUtility.QueenIsPlayer() && GetJellyProduct() == InternalDefOf.Starbeast_Jelly)
                foreach (Designation designation in region.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_MakeJelly))
                {
                    IntVec3 cell = designation.target.Cell;
                    if (CanConvertCell(cell) && (!requireLightLevel || IsProperLightLevelForFeralJelly(cell, region.Map))
                        && FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                    {
                        job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ProduceJelly, cell);
                        FeralJobUtility.ReservePlaceForJob(pawn, job, cell);
                        return true;
                    }
                }
            Thing ingredient = XMTUtility.SearchRegionsForJellyMakable(region, pawn, this, requireLightLevel);
            if (ingredient == null) return false;
            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ProduceJelly, ingredient);
            FeralJobUtility.ReserveThingForJob(pawn, job, ingredient);
            return true;
        }
        internal static bool IsProperLightLevelForFeralJelly(IntVec3 cell, Map map)
            => map != null && cell.InBounds(map) && XMTHiveUtility.HiveLightSuitabilityAt(cell, map) >= MinimumFeralJellyLightSuitability;

        private List<ResourceHelperUtility.Allocation> Evaluate(Thing thing, float fraction)
            => ResourceHelperUtility.PlanConversion(thing, Resources, parent, fraction).Allocations;
        // Compatibility API: the returned quantity is now a shared mass-equivalent value.
        public float GetNutritionFromThing(Thing thing, float efficiency = 1f) => Evaluate(thing, efficiency).Sum(a => a.mass);
        public int JellyFromThing(Thing thing, float efficiency = 1f) => FinalCounts(Evaluate(thing, efficiency)).Sum(p => p.count);
        public int JellyFromCell(IntVec3 cell, float efficiency = 1f)
        {
            Map map = parent.Map;
            if (map == null || !cell.InBounds(map)) return 0;
            Thing ingredient = FindIngredient(cell, map);
            if (ingredient != null) return JellyFromThing(ingredient, efficiency);
            if (!TerrainCellEmpty(cell, map)) return 0;
            return FinalCounts(ResourceHelperUtility.AllocateTerrain(Resources, parent, cell, map, out _), efficiency).Sum(p => p.count);
        }
        public int ConvertToJelly(IntVec3 cell, float efficiency = 1f)
        {
            LastConversionSucceeded = false;
            Map map = parent.Map;
            if (map == null || !cell.InBounds(map) || efficiency <= 0f) return 0;
            Thing ingredient = FindIngredient(cell, map);
            return ingredient != null ? ConvertToJelly(ingredient, efficiency) : ConvertTerrainToJelly(cell, map, efficiency);
        }
        public int ConvertTerrainToJelly(IntVec3 cell, Map map, float efficiency = 1f)
        {
            LastConversionSucceeded = false;
            if (efficiency <= 0f || !TerrainCellEmpty(cell, map)) return 0;
            var allocations = ResourceHelperUtility.AllocateTerrain(Resources, parent, cell, map, out var terrain);
            if (allocations.Count == 0 || !ResourceHelperUtility.ApplyTerrain(terrain, cell, map)) return 0;
            List<ThingDefCountClass> products = FinalCounts(allocations, efficiency, true);
            LastConversionSucceeded = true;
            RemoveDesignation(cell, map);
            return Deliver(products, cell, map);
        }
        public int ConvertToJelly(Thing ingredient, float efficiency = 1f)
        {
            LastConversionSucceeded = false;
            if (ingredient == null || !ingredient.Spawned || efficiency <= 0f || !CanMakeIntoJelly(ingredient)) return 0;
            Map map = ingredient.Map;
            IntVec3 cell = ingredient.Position;
            ResourceHelperUtility.ConversionPlan plan = ResourceHelperUtility.PlanConversion(ingredient, Resources, parent, efficiency);
            if (!plan.TryConsume()) return 0;
            List<ThingDefCountClass> products = FinalCounts(plan.Allocations, 1f, true);
            LastConversionSucceeded = true;
            RemoveDesignation(cell, map);
            return Deliver(products, cell, map);
        }
        private static void RemoveDesignation(IntVec3 cell, Map map)
        {
            Designation designation = map.designationManager.DesignationAt(cell, XenoWorkDefOf.XMT_MakeJelly);
            if (designation != null) map.designationManager.RemoveDesignation(designation);
        }
        // Bank output units, not unpriced material. Preview never modifies the saved balance.
        private List<ThingDefCountClass> FinalCounts(List<ResourceHelperUtility.Allocation> allocations, float fraction = 1f, bool commit = false)
        {
            string signature = XMTSettings.JellyMassEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                XMTSettings.JellyNutritionEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + Props.conversionRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            bool compatible = remainderSettings == signature;
            if (commit && !compatible) { outputRemainders.Clear(); remainderSettings = signature; }
            var products = new List<ThingDefCountClass>();
            foreach (var group in allocations.Where(a => a.mass > 0f && a.resource.product != null).GroupBy(a => a.resource.product))
            {
                decimal exact = group.Sum(a => (decimal)a.mass * (decimal)fraction * (decimal)Props.conversionRate / (decimal)Math.Max(0.000001f, a.resource.product.GetStatValueAbstract(StatDefOf.Mass)));
                if (compatible && outputRemainders.TryGetValue(group.Key, out double saved)) exact += (decimal)saved;
                int count = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0m, exact)));
                if (commit) outputRemainders[group.Key] = (double)(exact - count);
                if (count > 0) products.Add(new ThingDefCountClass(group.Key, count));
            }
            return products;
        }
        private int Deliver(List<ThingDefCountClass> products, IntVec3 cell, Map map)
        {
            int total = products.Sum(p => p.count);
            foreach (ThingDefCountClass product in products)
            {
                int remaining = product.count;
                // A pipe net represents the primary product only; side products remain physical items.
                if (product.thingDef == GetJellyProduct() && Network?.PipeNet != null && Network.PipeNet.AvailableCapacity > 0f)
                {
                    int toStore = Mathf.Min(remaining, Mathf.FloorToInt(Network.PipeNet.AvailableCapacity));
                    if (toStore > 0)
                    {
                        Network.PipeNet.DistributeAmongStorage(toStore, out float stored);
                        remaining -= Mathf.Min(remaining, Mathf.RoundToInt(stored));
                    }
                }
                if (remaining > 0) XMTUtility.DropAmountThing(product.thingDef, remaining, cell, map, InternalDefOf.Starbeast_Filth_Resin);
            }
            if (total > 0) ResearchUtility.ProgressEvolutionTech(total, parent);
            return total;
        }
        public ThingDef GetJellyProduct() => Resources.FirstOrDefault(e => ResourceHelperUtility.Available(e?.resourceDef, parent))?.resourceDef.product;
        public int MakeJellyByDef(int count, ThingDef thingDef, IntVec3 position, Map map, float efficiency = 1f)
        {
            if (map == null || efficiency <= 0f) return 0;
            var allocations = ResourceHelperUtility.AllocateDefinition(thingDef, count, Resources, parent);
            return Deliver(FinalCounts(allocations, efficiency, true), position, map);
        }
    }

    public class CompJellyMakerProperties : CompProperties
    {
        public List<ResourceConversionEntry> resources = new List<ResourceConversionEntry>();
        public float conversionRate = 0.5f;
        public float processingWork = 20f;
        public bool CanConvert(ThingDef def, Thing consumer = null)
            => ResourceHelperUtility.CanConvertDefinition(def, resources, consumer);
        public bool CanConvert(Thing thing, Thing consumer = null) => ResourceHelperUtility.Allocate(thing, resources, consumer).Count > 0;
        public bool CanConvertTerrain(IntVec3 cell, Map map)
            => ResourceHelperUtility.AllocateTerrain(resources, null, cell, map, out _).Count > 0;
        public CompJellyMakerProperties() { compClass = typeof(CompJellyMaker); }
        public CompJellyMakerProperties(Type compClass) : base(compClass) { }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (resources.NullOrEmpty()) yield return "Jelly makers require resource definitions.";
            if (resources != null)
            {
                foreach (ResourceConversionEntry entry in resources)
                    if (entry?.resourceDef?.product == null) yield return "Jelly maker resources require an output product.";
                foreach (string error in ResourceHelperUtility.TerrainConfigErrors(resources.Where(e => e?.resourceDef?.terrainProfiles != null).SelectMany(e => e.resourceDef.terrainProfiles))) yield return error;
            }
            if (conversionRate <= 0f || processingWork <= 0f) yield return "Jelly maker conversion/work factors must be positive.";
        }
    }
}
