using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class AcidPitEconomy
    {
        private sealed class PriceCache
        {
            public float setting;
            public float margin;
            public float minimum;
            public int defs;
            public int tick;
            public double cost;
        }

        private static readonly Dictionary<CompProperties_AcidPit, PriceCache> prices = new Dictionary<CompProperties_AcidPit, PriceCache>();
        // Mirrors the continuous output path; fractional banking removes batch-size premiums.
        public static double CostPerKg(CompProperties_AcidPit props)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            int defs = DefDatabase<ThingDef>.DefCount;
            if (prices.TryGetValue(props, out PriceCache cached) && cached.setting == XMTSettings.JellyMassEfficiency && cached.margin == props.lossMargin && cached.minimum == props.jellyPerKg && cached.defs == defs && tick >= cached.tick && tick - cached.tick < 2500)
                return cached.cost;
            ThingDef jelly = InternalDefOf.Starbeast_Jelly;
            double maxReturn = 0d;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                foreach (CompJellyMakerProperties maker in def.comps?.OfType<CompJellyMakerProperties>() ?? Enumerable.Empty<CompJellyMakerProperties>())
                    foreach (ResourceConversionEntry entry in maker.resources ?? new List<ResourceConversionEntry>())
                    {
                        QueenIngestibleResourceDef resource = entry.resourceDef;
                        if (resource?.product != jelly || !resource.massBased)
                            continue;
                        double factor = resource.massFactor * (resource.applyJellySettings ? XMTSettings.JellyMassEfficiency / 0.025d : 1d);
                        maxReturn = Math.Max(maxReturn, factor * maker.conversionRate / jelly.GetStatValueAbstract(StatDefOf.Mass));
                    }

            double cost = Math.Max(props.jellyPerKg, maxReturn * Math.Max(1.01f, props.lossMargin));
            prices[props] = new PriceCache
            {
                setting = XMTSettings.JellyMassEfficiency,
                margin = props.lossMargin,
                minimum = props.jellyPerKg,
                defs = defs,
                tick = tick,
                cost = cost
            };
            return cost;
        }

        public static double Capacity(CompProperties_AcidPit props, double costPerKg) => props.baselineMassCapacity * costPerKg;
    }
}
