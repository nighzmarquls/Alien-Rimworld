using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public class MutationTissueExtension : DefModExtension
    {
        public string summaryLabelKey;
        public string descriptionKey;
        public bool showInTissueTab = true;
        public bool showInTissueBeforeInspection = true;
        public string revealedSummaryLabelKey;
        public string revealedDescriptionKey;
        public string healthLabelKey;
        public string healthDescriptionKey;
        public string revealedHealthLabelKey;
        public string revealedHealthDescriptionKey;
        public string inspectionFindingKey;
        public List<TissueSummaryRule> diagnoses = new List<TissueSummaryRule>();
        public bool useDefaultRecognition;
        public List<ResearchProjectDef> revealResearchProjects = new List<ResearchProjectDef>();
        public List<RoyalEvolutionDef> revealQueenAdvancements = new List<RoyalEvolutionDef>();
        public bool ColonyRecognizes => TissueKnowledge.Recognizes(this);
    }
}
