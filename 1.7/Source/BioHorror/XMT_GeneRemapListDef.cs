
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public class GeneRemap
    {
        public ThingDef raceDef;
        public GeneDef targetGeneDef;
        public GeneDef replacementGeneDef;
        public GeneDef canonicalGeneDef;

    }
    public class XMT_GeneRemapListDef : Def
    {
        public static GeneDef GetRemappedGeneFor(ThingDef raceDef, GeneDef targetGeneDef)
        {
            if (raceDef == null || targetGeneDef == null)
            {
                return targetGeneDef;
            }

            if (ExpressionByCanonical != null &&
                ExpressionByCanonical.TryGetValue(raceDef.defName, out Dictionary<GeneDef, GeneDef> expressions) &&
                expressions.TryGetValue(targetGeneDef, out GeneDef canonicalExpression))
            {
                return canonicalExpression;
            }

            if (Remap != null && Remap.TryGetValue(raceDef.defName, out List<GeneRemap> remaps))
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

        public static GeneDef GetCanonicalGeneFor(ThingDef raceDef, GeneDef expressedGeneDef)
        {
            if (raceDef == null || expressedGeneDef == null || CanonicalByExpression == null)
            {
                return expressedGeneDef;
            }

            if (CanonicalByExpression.TryGetValue(raceDef.defName, out Dictionary<GeneDef, GeneDef> canonicalGenes) &&
                canonicalGenes.TryGetValue(expressedGeneDef, out GeneDef canonicalGene))
            {
                return canonicalGene;
            }

            return expressedGeneDef;
        }

        public static IEnumerable<string> ValidateMappings()
        {
            foreach (IGrouping<string, GeneRemap> raceGroup in DefDatabase<XMT_GeneRemapListDef>.AllDefsListForReading
                .Where(def => def.geneRemaps != null)
                .SelectMany(def => def.geneRemaps)
                .Where(remap => remap?.raceDef != null)
                .GroupBy(remap => remap.raceDef.defName))
            {
                foreach (GeneRemap remap in raceGroup)
                {
                    if (remap.targetGeneDef == null || remap.replacementGeneDef == null || remap.canonicalGeneDef == null)
                    {
                        yield return raceGroup.Key + ": remap is missing target, replacement, or canonical gene.";
                    }
                }

                foreach (IGrouping<GeneDef, GeneRemap> canonicalGroup in raceGroup
                    .Where(remap => remap?.canonicalGeneDef != null && remap.replacementGeneDef != null)
                    .GroupBy(remap => remap.canonicalGeneDef))
                {
                    List<GeneDef> replacements = canonicalGroup.Select(remap => remap.replacementGeneDef).Distinct().ToList();
                    if (replacements.Count > 1)
                    {
                        yield return raceGroup.Key + ": canonical gene " + canonicalGroup.Key.defName +
                            " maps to multiple expressions: " + string.Join(", ", replacements.Select(gene => gene.defName)) + ".";
                    }
                }

                foreach (IGrouping<GeneDef, GeneRemap> expressionGroup in raceGroup
                    .Where(remap => remap?.canonicalGeneDef != null && remap.replacementGeneDef != null)
                    .GroupBy(remap => remap.replacementGeneDef))
                {
                    List<GeneDef> canonicalGenes = expressionGroup.Select(remap => remap.canonicalGeneDef).Distinct().ToList();
                    if (canonicalGenes.Count > 1)
                    {
                        yield return raceGroup.Key + ": expression gene " + expressionGroup.Key.defName +
                            " resolves to multiple canonical genes: " + string.Join(", ", canonicalGenes.Select(gene => gene.defName)) + ".";
                    }
                }
            }
        }

        private static Dictionary<string, List<GeneRemap>> Remap;
        private static Dictionary<string, Dictionary<GeneDef, GeneDef>> ExpressionByCanonical;
        private static Dictionary<string, Dictionary<GeneDef, GeneDef>> CanonicalByExpression;

        public static void AddRemapping(GeneRemap geneRemap)
        {
            if (geneRemap?.raceDef == null || geneRemap.targetGeneDef == null || geneRemap.replacementGeneDef == null || geneRemap.canonicalGeneDef == null)
            {
                Log.Error("Invalid gene remap entry; race, target gene, replacement gene, and canonical gene are required.");
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

            ExpressionByCanonical ??= new Dictionary<string, Dictionary<GeneDef, GeneDef>>();
            CanonicalByExpression ??= new Dictionary<string, Dictionary<GeneDef, GeneDef>>();

            if (!ExpressionByCanonical.TryGetValue(geneRemap.raceDef.defName, out Dictionary<GeneDef, GeneDef> expressions))
            {
                expressions = new Dictionary<GeneDef, GeneDef>();
                ExpressionByCanonical.Add(geneRemap.raceDef.defName, expressions);
            }

            if (expressions.TryGetValue(geneRemap.canonicalGeneDef, out GeneDef existingExpression) && existingExpression != geneRemap.replacementGeneDef)
            {
                Log.Error("Conflicting gene expressions for " + geneRemap.raceDef.defName + "/" + geneRemap.canonicalGeneDef.defName + ".");
            }
            else
            {
                expressions[geneRemap.canonicalGeneDef] = geneRemap.replacementGeneDef;
            }

            if (!CanonicalByExpression.TryGetValue(geneRemap.raceDef.defName, out Dictionary<GeneDef, GeneDef> canonicalGenes))
            {
                canonicalGenes = new Dictionary<GeneDef, GeneDef>();
                CanonicalByExpression.Add(geneRemap.raceDef.defName, canonicalGenes);
            }

            if (canonicalGenes.TryGetValue(geneRemap.replacementGeneDef, out GeneDef existingCanonical) && existingCanonical != geneRemap.canonicalGeneDef)
            {
                Log.Error("Ambiguous canonical gene for " + geneRemap.raceDef.defName + "/" + geneRemap.replacementGeneDef.defName + ".");
            }
            else
            {
                canonicalGenes[geneRemap.replacementGeneDef] = geneRemap.canonicalGeneDef;
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
