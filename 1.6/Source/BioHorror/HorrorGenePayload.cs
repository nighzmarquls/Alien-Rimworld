using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public sealed class HorrorGenePayload
    {
        private readonly List<GeneDef> genes;
        private readonly List<GeneDef> removedDefaults;

        public HorrorGenePayload(IEnumerable<GeneDef> genes, string templateName = "", IEnumerable<GeneDef> removedDefaultGenes = null)
        {
            this.genes = genes?
                .Where(gene => gene != null)
                .Distinct()
                .ToList() ?? new List<GeneDef>();
            TemplateName = templateName ?? string.Empty;
            removedDefaults = removedDefaultGenes?.Where(gene => gene != null).Distinct().ToList() ?? new List<GeneDef>();
            this.genes.RemoveAll(removedDefaults.Contains);
        }

        public IReadOnlyList<GeneDef> Genes => genes;
        public IReadOnlyList<GeneDef> RemovedDefaultGenes => removedDefaults;

        public string TemplateName { get; }

        public bool Empty => genes.Count == 0;

        public GeneSet ToGeneSet()
        {
            GeneSet result = new GeneSet();
            foreach (GeneDef gene in genes)
            {
                result.AddGene(gene);
            }
            return result;
        }
    }
}
