using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class HivePruningUtility
    {
        private static List<ThingDef> prunableDefs;
        private static int defCount = -1;

        public static IEnumerable<ThingWithComps> CandidatesOn(Map map)
        {
            if (prunableDefs == null || defCount != DefDatabase<ThingDef>.DefCount)
            {
                defCount = DefDatabase<ThingDef>.DefCount;
                prunableDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(d =>
                    d.comps?.Any(c => c.compClass != null && typeof(CompHivePrunable).IsAssignableFrom(c.compClass)) == true).ToList();
            }

            foreach (ThingDef def in prunableDefs)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                {
                    if (thing is ThingWithComps building)
                    {
                        yield return building;
                    }
                }
            }
        }
    }
}
