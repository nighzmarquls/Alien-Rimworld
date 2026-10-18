using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public struct QueenResourceGain
    {
        public QueenIngestibleResourceDef resource;
        public float amount;

        public QueenResourceGain(QueenIngestibleResourceDef resource, float amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }

    public static class QueenIngestibleResourceUtility
    {
        private const int MaxFlattenDepth = 4;

        public static float CapacityFor(Pawn pawn, QueenIngestibleResourceDef resource)
        {
            if (pawn == null || resource == null || !resource.usesStorage)
            {
                return 0f;
            }

            float bodySize = Mathf.Max(0.1f, pawn.BodySize);
            float capacity = resource.baseCapacity * Mathf.Pow(bodySize, resource.bodySizeFactor);
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                capacity *= 1f + Mathf.Max(0f, MetabolismFor(pawn)) * resource.metabolismFactor;
            }

            if (resource.capacityModifiers != null)
            {
                foreach (QueenIngestibleResourceCapacityModifier modifier in resource.capacityModifiers)
                {
                    if (modifier?.hediff == null)
                    {
                        continue;
                    }

                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.def == modifier.hediff)
                        {
                            capacity += hediff.Severity * modifier.addCapacityPerSeverity;
                            capacity *= 1f + hediff.Severity * modifier.capacityFactorPerSeverity;
                        }
                    }
                }
            }

            return Mathf.Max(0f, capacity);
        }

        private static int MetabolismFor(Pawn pawn)
        {
            int metabolism = 0;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active)
                {
                    metabolism += gene.def.biostatMet;
                }
            }

            return metabolism;
        }

        public static List<QueenResourceGain> GetResourceGains(Thing thing, Pawn queen, CompQueenAssimilation comp)
        {
            List<QueenResourceGain> gains = new List<QueenResourceGain>();
            if (thing == null || queen == null || comp == null)
            {
                return gains;
            }

            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (!comp.ResourceUnlocked(resource) || comp.GetResourceSpace(resource) <= 0f)
                {
                    continue;
                }

                float amount = ResourceValueFor(thing, resource);
                amount = Mathf.Min(amount, comp.GetResourceSpace(resource));
                if (amount > 0f)
                {
                    gains.Add(new QueenResourceGain(resource, amount));
                }
            }

            return gains;
        }

        public static float ResourceValueFor(Thing thing, QueenIngestibleResourceDef resource)
            => ResourceHelperUtility.ValueFor(thing, resource);
        public static float DirectValue(ThingDef def, QueenIngestibleResourceDef resource, bool derived = false)
            => resource?.ValueFor(def, derived) ?? 0f;
        public static float FlattenedThingDefValue(ThingDef def, QueenIngestibleResourceDef resource)
            => ResourceHelperUtility.DefinitionUnits(def, resource);
        public static float FlattenedRecipeValue(RecipeDef recipe, QueenIngestibleResourceDef resource)
            => ResourceHelperUtility.RecipeUnits(recipe, resource);
        public static string GainsLabel(List<QueenResourceGain> gains)
        {
            if (gains == null || gains.Count == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < gains.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append("+");
                builder.Append(Mathf.FloorToInt(gains[i].amount));
                builder.Append(" ");
                builder.Append(gains[i].resource.LabelCap);
            }

            return builder.ToString();
        }
    }
}
