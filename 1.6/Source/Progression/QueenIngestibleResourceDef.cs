using System.Collections.Generic;
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

        public float ValueFor(ThingDef thingDef, bool derived)
        {
            if (entries == null || thingDef == null)
            {
                return 0f;
            }

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
