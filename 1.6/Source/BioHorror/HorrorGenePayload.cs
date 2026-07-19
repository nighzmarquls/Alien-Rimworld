using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public sealed class HorrorGenePayload
    {
        private readonly List<GeneDef> genes;

        public HorrorGenePayload(IEnumerable<GeneDef> genes, string templateName = "")
        {
            this.genes = genes?
                .Where(gene => gene != null)
                .Distinct()
                .ToList() ?? new List<GeneDef>();
            TemplateName = templateName ?? string.Empty;
        }

        public IReadOnlyList<GeneDef> Genes => genes;

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
