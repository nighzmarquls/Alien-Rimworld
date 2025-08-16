
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
            if (Remap.ContainsKey(raceDef.defName))
            {
                foreach (GeneRemap geneRemap in Remap[raceDef.defName])
                {
                    if (geneRemap.targetGeneDef.defName == targetGeneDef.defName)
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

            foreach (GeneRemap geneRemap in geneRemaps)
            {
                AddRemapping(geneRemap);
            }
        }
    }
}
