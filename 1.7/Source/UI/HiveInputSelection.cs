using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public static class HiveInputSelection
    {
        public static bool TryGetProcessors(List<object> selection, bool resin, out List<IHiveInputProcessor> processors)
        {
            processors = new List<IHiveInputProcessor>();
            if (selection == null || selection.Count == 0)
                return false;

            HashSet<QueenIngestibleResourceDef> resources = null;
            foreach (object selected in selection)
            {
                if (selected is not ThingWithComps thing || selection.Count > 1 && thing.Faction != Faction.OfPlayer)
                    return false;
                IHiveInputProcessor processor = resin
                    ? thing.GetComp<CompAcidPit>()
                    : thing.GetComp<CompJellyMaker>();
                if (processor == null)
                    return false;
                if (selection.Count > 1)
                {
                    if (resources == null)
                    {
                        resources = new HashSet<QueenIngestibleResourceDef>(processor.InputResources.Where(def => def != null));
                        if (resources.Count == 0)
                            return false;
                    }
                    else if (!resources.SetEquals(processor.InputResources.Where(def => def != null)))
                        return false;
                }
                processors.Add(processor);
            }
            return true;
        }
    }
}
