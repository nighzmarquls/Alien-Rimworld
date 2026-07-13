
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class GeneRemap
    {
        public ThingDef raceDef;
        public GeneDef targetGeneDef;
        public GeneDef replacementGeneDef;

    }
    public class XMT_GeneRemapListDef : Def
    {
        public static GeneDef GetRemappedGeneFor(ThingDef raceDef, GeneDef targetGeneDef)
        {
            if (raceDef == null || targetGeneDef == null || Remap == null)
            {
                return targetGeneDef;
            }

            if (Remap.TryGetValue(raceDef.defName, out List<GeneRemap> remaps))
            {
                foreach (GeneRemap geneRemap in remaps)
                {
                    if (geneRemap?.targetGeneDef == targetGeneDef && geneRemap.replacementGeneDef != null)
                    {
                        return geneRemap.replacementGeneDef;
                    }
                }
            }
            return targetGeneDef;
        }
        private static Dictionary<string, List<GeneRemap>> Remap;

        public static void AddRemapping(GeneRemap geneRemap)
        {
            if (geneRemap?.raceDef == null || geneRemap.targetGeneDef == null || geneRemap.replacementGeneDef == null)
            {
                Log.Error("Invalid gene remap entry; race, target gene, and replacement gene are required.");
                return;
            }

            if(Remap == null)
            {
                Remap = new Dictionary<string, List<GeneRemap>>();
            }

            if (Remap.ContainsKey(geneRemap.raceDef.defName))
            {
                Remap[geneRemap.raceDef.defName].Add(geneRemap);
            }
            else
            {
                Remap[geneRemap.raceDef.defName] = new List<GeneRemap> { geneRemap };
            }
        }
        public List<GeneRemap> geneRemaps;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
     
            if(geneRemaps != null)
            {
                Log.Message("Loaded " + defName + " with " + geneRemaps.Count + " Remaps");
            }
            else
            {
                Log.Message("Loaded " + defName );
            }

            if (geneRemaps == null)
            {
                return;
            }

            foreach (GeneRemap geneRemap in geneRemaps)
            {
                AddRemapping(geneRemap);
            }
        }
    }
}
