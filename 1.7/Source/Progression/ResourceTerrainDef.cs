using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class ResourceTemperature
    {
        public bool enabled;
        public float restingCelsius = 20f;
        public float celsiusPerInterval = 10f;
        public float yieldPerInterval = 1f;
    }

    public class ResourceConversionEntry
    {
        public QueenIngestibleResourceDef resourceDef;
        // Negative means use the resource's weight; zero deliberately disables this entry.
        public float allocationWeight = -1f;
        public float Weight => allocationWeight < 0f ? resourceDef?.allocationWeight ?? 0f : allocationWeight;
    }

    public class ResourceTerrainDef : Def
    {
        public List<TerrainDef> terrains;
        public List<TerrainAffordanceDef> requiredAffordances;
        public List<TerrainAffordanceDef> excludedAffordances;
        public float minFertility;
        public float maxFertility = float.MaxValue;
        public bool useDriesToFertility;
        public float yield = 1f;
        public bool yieldFromFertilityLoss;
        public TerrainDef depletedTerrain;
        public bool removeTopLayer;
        public int depletionPriority;

        public bool Matches(TerrainDef terrain)
        {
            if (terrain == null || (terrains != null && !terrains.Contains(terrain))) return false;
            if (requiredAffordances != null)
                foreach (TerrainAffordanceDef affordance in requiredAffordances)
                    if (terrain.affordances?.Contains(affordance) != true) return false;
            if (excludedAffordances != null)
                foreach (TerrainAffordanceDef affordance in excludedAffordances)
                    if (terrain.affordances?.Contains(affordance) == true) return false;
            float fertility = useDriesToFertility && terrain.driesTo != null ? terrain.driesTo.fertility : terrain.fertility;
            return fertility >= minFertility && fertility <= maxFertility;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (removeTopLayer && depletedTerrain != null) yield return defName + ": choose removal or replacement, not both.";
            if (yield < 0f || minFertility > maxFertility) yield return defName + ": invalid terrain yield/fertility range.";
        }
    }
}
