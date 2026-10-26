using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public class TissueAssessmentDef : Def
    {
        public static TissueAssessmentDef Current => DefDatabase<TissueAssessmentDef>.GetNamed("XMT_TissueAssessment");
        public string tabLabelKey, saturationKey, countKey, moreKey;
        public int compactNameLimit = 3;
        public List<ResearchProjectDef> researchUnlocks = new List<ResearchProjectDef>();
        public List<RoyalEvolutionDef> queenUnlocks = new List<RoyalEvolutionDef>();
        public bool allowGodMode = true, excludeMatureMorphs = true, requireVisible = true, includeInfluenceSources = true;
        public string noMutatorsKey, noHazardsKey, otherTissueKey;
        public int noMutatorsPriority, noHazardsPriority = 50, otherTissuePriority = 100;
        public string defaultInspectionFindingKey;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefsListForReading)
                foreach (var rule in hediff.GetModExtension<MutationTissueExtension>()?.diagnoses ?? Enumerable.Empty<TissueSummaryRule>())
                    if (rule.workerClass == null || !typeof(TissueSummaryWorker).IsAssignableFrom(rule.workerClass))
                        yield return "Invalid tissue diagnosis worker on " + hediff.defName + ": " + rule.labelKey;
        }
    }
}
