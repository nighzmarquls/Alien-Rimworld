
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class XMT_ThingDefList : Def
    {
        public List<ThingDef> things;
    }

    public class XMT_SabotageReplacementListDef : Def
    {
        public List<XMT_SabotageReplacementPair> replacements;
    }

    public class XMT_SabotageReplacementPair
    {
        public ThingDef sourceThing;
        public ThingDef replacementThing;
        public ThingDef replacementFloorThing;
    }

    internal static class XMTSabotageReplacementUtility
    {
        private static Dictionary<ThingDef, XMT_SabotageReplacementPair> cachedReplacements;

        internal static bool TryGetReplacement(ThingDef sourceThing, out XMT_SabotageReplacementPair replacement)
        {
            replacement = null;
            if (sourceThing == null)
            {
                return false;
            }

            if (cachedReplacements == null)
            {
                cachedReplacements = new Dictionary<ThingDef, XMT_SabotageReplacementPair>();
                foreach (XMT_SabotageReplacementListDef listDef in DefDatabase<XMT_SabotageReplacementListDef>.AllDefsListForReading)
                {
                    if (listDef.replacements == null)
                    {
                        continue;
                    }

                    foreach (XMT_SabotageReplacementPair pair in listDef.replacements.Where(pair => pair?.sourceThing != null))
                    {
                        cachedReplacements[pair.sourceThing] = pair;
                    }
                }
            }

            return cachedReplacements.TryGetValue(sourceThing, out replacement);
        }
    }
}
