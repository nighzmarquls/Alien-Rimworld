
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class XMT_ThingDefList : Def
    {
        public List<ThingDef> things;
    }

    public class XMT_BreachReplacementListDef : Def
    {
        public List<XMT_BreachReplacementPair> replacements;
    }

    public class XMT_BreachReplacementPair
    {
        public ThingDef sourceThing;
        public ThingDef replacementThing;
        public ThingDef replacementFloorThing;
    }

    internal static class XMTBreachReplacementUtility
    {
        private static Dictionary<ThingDef, XMT_BreachReplacementPair> cachedReplacements;

        internal static bool TryGetReplacement(ThingDef sourceThing, out XMT_BreachReplacementPair replacement)
        {
            replacement = null;
            if (sourceThing == null)
            {
                return false;
            }

            if (cachedReplacements == null)
            {
                cachedReplacements = new Dictionary<ThingDef, XMT_BreachReplacementPair>();
                foreach (XMT_BreachReplacementListDef listDef in DefDatabase<XMT_BreachReplacementListDef>.AllDefsListForReading)
                {
                    if (listDef.replacements == null)
                    {
                        continue;
                    }

                    foreach (XMT_BreachReplacementPair pair in listDef.replacements.Where(pair => pair?.sourceThing != null))
                    {
                        cachedReplacements[pair.sourceThing] = pair;
                    }
                }
            }

            return cachedReplacements.TryGetValue(sourceThing, out replacement);
        }
    }
}
