using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public static class MutationTissueAssessment
    {
        public static IHorrorPregnancy HorrorPregnancy(Hediff tissue) => (tissue as HediffWithComps)?.comps?
            .OfType<IHorrorPregnancy>().FirstOrDefault(p => p.IsHorror);
        public static bool Inspected(Hediff tissue) => HorrorPregnancy(tissue)?.Discovered ??
            (tissue is Hediff_CryptimorphicExposure exposure ? exposure.discovered :
             tissue.TryGetComp<HediffComp_SurgeryInspectableEmbryo>()?.Identified == true ||
             tissue.TryGetComp<HediffComp_MedicalReveal>()?.revealed == true);
        public static bool Recognized(Hediff tissue) => Inspected(tissue) || tissue.def.GetModExtension<MutationTissueExtension>()?.ColonyRecognizes == true;
        public static bool CanPresent(Hediff tissue)
        {
            var data = tissue.def.GetModExtension<MutationTissueExtension>();
            return data?.showInTissueTab != false && (data?.showInTissueBeforeInspection != false || Recognized(tissue));
        }
        public static string PresentationKey(Hediff tissue, string ordinary, string revealed) => !revealed.NullOrEmpty() && Recognized(tissue) ? revealed : ordinary;
        public static float VisibleInfluence(Pawn pawn, bool requireVisible = true)
        {
            float influence = BioUtility.GetXenomorphInfluence(pawn);
            foreach (Hediff tissue in pawn.health.hediffSet.hediffs)
                if ((requireVisible && !tissue.Visible) || !CanPresent(tissue))
                    foreach (InfluenceHealth contribution in XenoGeneDefOf.XMT_InfluencesSet.influences)
                        if (contribution.hediff == tissue.def) influence -= contribution.Contribution(tissue.Severity);
            return Mathf.Max(0f, influence);
        }

        public static string Label(Hediff tissue)
        {
            var data = tissue.def.GetModExtension<MutationTissueExtension>();
            string key = PresentationKey(tissue, data?.summaryLabelKey, data?.revealedSummaryLabelKey);
            return key.NullOrEmpty() ? tissue.LabelCap.ToString() : key.Translate().ToString();
        }
        public static string HealthLabel(Hediff tissue, string fallback)
        {
            var data = tissue.def.GetModExtension<MutationTissueExtension>();
            string key = PresentationKey(tissue, data?.healthLabelKey, data?.revealedHealthLabelKey);
            return key.NullOrEmpty() ? fallback : key.Translate().ToString();
        }
        public static string HealthDescription(Hediff tissue, string fallback)
        {
            var data = tissue.def.GetModExtension<MutationTissueExtension>();
            string key = PresentationKey(tissue, data?.healthDescriptionKey, data?.revealedHealthDescriptionKey);
            return key.NullOrEmpty() ? fallback : key.Translate().ToString();
        }
    }
}
