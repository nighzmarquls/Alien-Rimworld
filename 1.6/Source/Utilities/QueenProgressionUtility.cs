using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal static class QueenProgressionUtility
    {
        public static int FeralQueenEvolutionPoints(float xenoforming)
        {
            return 1 + Mathf.Max(0, Mathf.FloorToInt(xenoforming - 10f));
        }

        public static List<RoyalEvolutionDef> PredictedFeralQueenEvolutions(RoyalEvolutionSet set, float xenoforming,
            RoyalEvolutionDef stopAfter = null)
        {
            List<RoyalEvolutionDef> result = new List<RoyalEvolutionDef>();
            int remaining = FeralQueenEvolutionPoints(xenoforming);
            foreach (RoyalEvolutionDef evolution in set?.evolutions ?? Enumerable.Empty<RoyalEvolutionDef>())
            {
                if (evolution == null || remaining < evolution.evoPointCost)
                {
                    break;
                }

                result.Add(evolution);
                remaining -= evolution.evoPointCost;
                if (evolution == stopAfter)
                {
                    break;
                }
            }

            return result;
        }

        public static bool WouldReachEvolutionLineage(RoyalEvolutionSet set, RoyalEvolutionDef required, float xenoforming)
        {
            if (required == null)
            {
                return true;
            }

            foreach (RoyalEvolutionDef evolution in PredictedFeralQueenEvolutions(set, xenoforming))
            {
                if (evolution == required || ReplacesEvolution(evolution, required, new HashSet<RoyalEvolutionDef>()))
                {
                    return true;
                }
            }

            return false;
        }

        public static float MinimumXenoformingToReachEvolutionLineage(RoyalEvolutionSet set, RoyalEvolutionDef required)
        {
            if (required == null)
            {
                return 0f;
            }

            int cumulativeCost = 0;
            foreach (RoyalEvolutionDef evolution in set?.evolutions ?? Enumerable.Empty<RoyalEvolutionDef>())
            {
                if (evolution == null)
                {
                    return -1f;
                }

                cumulativeCost += evolution.evoPointCost;
                if (evolution == required || ReplacesEvolution(evolution, required, new HashSet<RoyalEvolutionDef>()))
                {
                    return cumulativeCost <= 1 ? 0f : cumulativeCost + 9f;
                }
            }

            return -1f;
        }

        private static bool ReplacesEvolution(RoyalEvolutionDef evolution, RoyalEvolutionDef required,
            HashSet<RoyalEvolutionDef> visited)
        {
            if (evolution == null || !visited.Add(evolution) || evolution.replaces.NullOrEmpty())
            {
                return false;
            }

            return evolution.replaces.Any(replaced => replaced == required || ReplacesEvolution(replaced, required, visited));
        }
    }
}
