using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Xenomorphtype
{
    public class CompHiveGeneHolder : ThingComp
    {
        private const int CurrentStorageVersion = 2;

        public GeneSet genes;
        public string templateName;
        private GeneSet legacyGenes;
        private string legacyTemplateName;
        private int storageVersion = CurrentStorageVersion;

        public int StorageVersion => storageVersion;

        public bool UsesNativeGeneTracker => parent is Pawn pawn && pawn.genes != null;

        public IReadOnlyList<GeneDef> GenesListForReading
        {
            get
            {
                if (parent is Pawn pawn && pawn.genes != null)
                {
                    return BioUtility.GetCanonicalPawnGenes(pawn);
                }

                return genes?.GenesListForReading ?? new List<GeneDef>();
            }
        }

        public string EffectiveTemplateName
        {
            get
            {
                if (parent is Pawn pawn && pawn.genes != null)
                {
                    return pawn.genes.xenotypeName ?? string.Empty;
                }

                return templateName ?? string.Empty;
            }
        }

        protected virtual ThingDef LegacyExpressionRace => InternalDefOf.XMT_Starbeast_AlienRace;

        public GeneSet CaptureGeneSet()
        {
            return new HorrorGenePayload(GenesListForReading, EffectiveTemplateName).ToGeneSet();
        }

        public void ReplaceGenes(IEnumerable<GeneDef> canonicalGenes, string newTemplateName = "", bool replaceNativeGenes = true)
        {
            List<GeneDef> geneList = canonicalGenes?.Where(gene => gene != null).Distinct().ToList() ?? new List<GeneDef>();
            if (parent is Pawn pawn && pawn.genes != null)
            {
                if (replaceNativeGenes)
                {
                    foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
                    {
                        pawn.genes.RemoveGene(gene);
                    }
                }

                GeneSet incomingGenes = new HorrorGenePayload(geneList).ToGeneSet();
                BioUtility.InsertGenesetToPawn(incomingGenes, ref pawn);
                if (!newTemplateName.NullOrEmpty())
                {
                    pawn.genes.xenotypeName = newTemplateName;
                }
                genes = null;
                templateName = string.Empty;
                storageVersion = CurrentStorageVersion;
                return;
            }

            genes = new HorrorGenePayload(geneList).ToGeneSet();
            templateName = newTemplateName ?? string.Empty;
            storageVersion = CurrentStorageVersion;
        }

        public void AddGenes(IEnumerable<GeneDef> canonicalGenes, string newTemplateName = null)
        {
            List<GeneDef> combinedGenes = GenesListForReading.ToList();
            if (canonicalGenes != null)
            {
                foreach (GeneDef gene in canonicalGenes)
                {
                    if (gene != null)
                    {
                        combinedGenes.AddDistinct(gene);
                    }
                }
            }

            ReplaceGenes(combinedGenes, newTemplateName ?? EffectiveTemplateName, replaceNativeGenes: false);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref templateName, "xmtHiveTemplateName", "");
            Scribe_Deep.Look(ref genes, "xmtHiveGenes");
            Scribe_Values.Look(ref storageVersion, "xmtHiveGeneStorageVersion", 0);

            if (ShouldReadLegacyStorage())
            {
                Scribe_Values.Look(ref legacyTemplateName, "templateName", "");
                Scribe_Deep.Look(ref legacyGenes, "genes");
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                MigrateLegacyStorage();
            }
        }

        private bool ShouldReadLegacyStorage()
        {
            if (Scribe.mode == LoadSaveMode.Saving || storageVersion >= 1)
            {
                return false;
            }

            return !(parent is Pawn) || this is CompLarvalGenes || this is CompSubverterGenes;
        }

        private void MigrateLegacyStorage()
        {
            if (storageVersion < 1)
            {
                GeneSet sourceGenes = genes ?? legacyGenes;
                string sourceTemplateName = !templateName.NullOrEmpty() ? templateName : legacyTemplateName;
                List<GeneDef> canonicalGenes = BioUtility.NormalizeGenesForStorage(
                    LegacyExpressionRace,
                    sourceGenes?.GenesListForReading);
                ReplaceGenes(canonicalGenes, sourceTemplateName, replaceNativeGenes: false);
                ClearLegacyStorage();
                return;
            }

            if (UsesNativeGeneTracker && genes != null && genes.GenesListForReading.Count > 0)
            {
                List<GeneDef> redundantGenes = genes.GenesListForReading.ToList();
                genes = null;
                AddGenes(redundantGenes, templateName);
            }

            storageVersion = CurrentStorageVersion;
            ClearLegacyStorage();
        }

        private void ClearLegacyStorage()
        {
            legacyGenes = null;
            legacyTemplateName = string.Empty;
        }

        public bool DebugMarkLegacy(ThingDef expressionRace)
        {
            if (UsesNativeGeneTracker)
            {
                return false;
            }

            genes = new HorrorGenePayload(BioUtility.GetGenesForExpression(expressionRace, GenesListForReading)).ToGeneSet();
            storageVersion = 0;
            return true;
        }

        public override string CompInspectStringExtra()
        {
            if (!DebugSettings.godMode || UsesNativeGeneTracker)
            {
                return null;
            }

            IReadOnlyList<GeneDef> storedGenes = GenesListForReading;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Gene storage: canonical v" + storageVersion);
            builder.AppendLine("Template: " + (EffectiveTemplateName.NullOrEmpty() ? "none" : EffectiveTemplateName));
            if (storedGenes.Count == 0)
            {
                builder.Append("Stored genes: none");
                return builder.ToString();
            }

            builder.AppendLine("Stored genes: " + storedGenes.Count + " (complexity " + BioUtility.GeneComplexityTotal(storedGenes) + ")");
            foreach (GeneDef gene in storedGenes)
            {
                builder.AppendLine("- " + gene.LabelCap + " [" + gene.defName + "]");
            }

            return builder.ToString().TrimEndNewlines();
        }
    }
 
}
