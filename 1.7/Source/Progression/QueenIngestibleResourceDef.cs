using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class QueenIngestibleResourceEntry
    {
        public ThingDef thingDef;
        public ThingCategoryDef thingCategory;
        public float valuePerUnit = 1f;
        public bool countsInDerivedCosts = true;

        public bool AppliesTo(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (thingDef != null)
            {
                return def == thingDef;
            }

            return thingCategory != null && def.IsWithinCategory(thingCategory);
        }
    }

    public class QueenIngestibleResourceCapacityModifier
    {
        public HediffDef hediff;
        public float addCapacityPerSeverity;
        public float capacityFactorPerSeverity;
    }

    public class QueenIngestibleResourceDef : Def
    {
        // Storage is optional: converter resources use the same definition without queen bars/capacity.
        public bool usesStorage = true;
        public ThingDef product;
        public ThingFilter inputFilter;
        public ThingFilter corpseFilter;
        public List<RotStage> corpseRotStages = new List<RotStage> { RotStage.Fresh, RotStage.Rotting };
        public GeneDef requiredGene;
        public bool queenOnly;
        public bool nonQueenOnly;
        public bool massBased;
        public float massFactor = 1f;
        public float nutritionToMass;
        public bool applyJellySettings;
        public float allocationWeight = 1f;
        public float terrainYieldFactor = 1f;
        public List<ResourceTerrainDef> terrainProfiles;
        public ResourceTemperature temperature;
        public RoyalEvolutionDef requiredEvolution;
        public List<QueenAssimilationDef> prerequisiteAssimilations;
        public float baseCapacity = 100f;
        public float bodySizeFactor = 1f;
        public float metabolismFactor;
        public Color barColor = new Color(0.62f, 0.64f, 0.64f);
        public Color barHighlightColor = new Color(0.82f, 0.84f, 0.84f);
        public string iconPath;
        public bool showWhenLocked;
        public bool allowDerivedIngredientValue = true;
        public bool allowDirectAndDerivedValue;
        public float derivedIngredientFactor = 1f;
        public List<QueenIngestibleResourceEntry> entries;
        public List<QueenIngestibleResourceCapacityModifier> capacityModifiers;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            inputFilter?.ResolveReferences();
            corpseFilter?.ResolveReferences();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (massFactor <= 0f || nutritionToMass < 0f || allocationWeight < 0f || terrainYieldFactor < 0f)
                yield return defName + ": resource conversion factors must be positive (nutrition/weight may be zero).";
            if (queenOnly && nonQueenOnly) yield return defName + ": queenOnly and nonQueenOnly conflict.";
            if (temperature?.enabled == true && temperature.celsiusPerInterval == 0f)
                yield return defName + ": enabled temperature conversion requires a signed, nonzero interval.";
            if (temperature?.yieldPerInterval < 0f) yield return defName + ": temperature yield must be nonnegative.";
            if (product != null && product.GetStatValueAbstract(StatDefOf.Mass) <= 0f)
                yield return defName + ": physical resource products require a positive unit mass.";
            foreach (string error in ResourceHelperUtility.TerrainConfigErrors(terrainProfiles)) yield return defName + ": " + error;
        }

        public float ValueFor(ThingDef thingDef, bool derived)
        {
            if (thingDef == null)
            {
                return 0f;
            }

            if (inputFilter?.Allows(thingDef) == true) return 1f;
            if (entries == null) return 0f;
            foreach (QueenIngestibleResourceEntry entry in entries)
            {
                if (entry != null && (!derived || entry.countsInDerivedCosts) && entry.AppliesTo(thingDef))
                {
                    return entry.valuePerUnit;
                }
            }

            return 0f;
        }
    }
}
