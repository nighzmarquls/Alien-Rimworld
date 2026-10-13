using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal static class GeneLineageUtility
    {
        internal static List<GeneDef> Defaults => BioUtility.NormalizeGenesForStorage(
            InternalDefOf.XMT_Starbeast_AlienRace, BioUtility.GetXenomorphGenes());

        internal static HorrorGenePayload Selection(IEnumerable<GeneDef> genes, string name = "")
        {
            List<GeneDef> selected = genes?.Where(gene => gene != null).Distinct().ToList() ?? new List<GeneDef>();
            return new HorrorGenePayload(selected, name, Defaults.Where(gene => !selected.Contains(gene)));
        }

        internal static void CleanDefaults(Pawn pawn, IEnumerable<GeneDef> removed)
        {
            if (pawn?.genes == null || removed == null) return;
            HashSet<GeneDef> exclusions = new HashSet<GeneDef>(removed);
            foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
                if (exclusions.Contains(XMT_GeneRemapListDef.GetCanonicalGeneFor(pawn.def, gene.def)))
                    pawn.genes.RemoveGene(gene);
        }

        internal static void SetSelection(Thing target, IEnumerable<GeneDef> genes, string name = "")
        {
            BioUtility.TryApplyHorrorGenePayload(target, Selection(genes, name), replaceNativeGenes: true);
        }

        internal static void InitializePioneer(Pawn pawn)
        {
            if (pawn?.story?.Adulthood?.defName != "StarbeastPioneer16" || pawn.inventory == null) return;
            HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(pawn, inheritableOnly: true);
            foreach (Thing item in pawn.inventory.innerContainer.ToList())
            {
                Ovomorph egg = (item is MinifiedThing minified ? minified.InnerThing : item) as Ovomorph;
                CompHatchingEgg holder = egg?.TryGetComp<CompHatchingEgg>();
                if (holder == null || holder.mother != null || !holder.UnHatched) continue;
                holder.mother = pawn;
                holder.ReplaceGenes(payload.Genes, payload.TemplateName, removedDefaults: payload.RemovedDefaultGenes);
                egg.SetFaction(pawn.Faction);
            }
        }
    }
}
