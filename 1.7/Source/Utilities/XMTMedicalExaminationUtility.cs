using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTMedicalExaminationUtility
    {
        private const int MutationResearchProgress = 10;

        internal static void NotifyConditionDiscovered(Pawn surgeon, int researchProgress)
        {
            if (researchProgress > 0)
            {
                ResearchUtility.ProgressCryptobioTech(researchProgress, surgeon);
            }
        }

        internal static List<Hediff> HiddenMutations(Pawn patient)
        {
            if (patient?.health?.hediffSet?.hediffs == null)
            {
                return new List<Hediff>();
            }

            HashSet<HediffDef> mutationDefs = DefDatabase<XMT_MutationsHealthSet>.AllDefsListForReading
                .Where(set => set?.mutations != null)
                .SelectMany(BioUtility.AllMutationsForSet)
                .Where(mutation => mutation?.horror != null)
                .Select(mutation => mutation.horror)
                .ToHashSet();

            return patient.health.hediffSet.hediffs
                .Where(hediff => hediff != null && !hediff.Visible && mutationDefs.Contains(hediff.def))
                .ToList();
        }

        internal static string RevealMutations(Pawn patient, Pawn surgeon, IEnumerable<Hediff> candidates)
        {
            if (patient?.health?.hediffSet?.hediffs == null || candidates == null)
            {
                return string.Empty;
            }

            StringBuilder findings = new StringBuilder();
            foreach (Hediff hediff in candidates.Distinct())
            {
                if (hediff == null || hediff.Visible || !patient.health.hediffSet.hediffs.Contains(hediff))
                {
                    continue;
                }

                hediff.SetVisible();
                NotifyConditionDiscovered(surgeon, MutationResearchProgress);
                AppendFinding(findings, "XMT_MedicalExaminationMutationFinding".Translate(
                    patient.Named("PAWN"), hediff.LabelCap.Named("MUTATION")));
            }
            return findings.ToString();
        }

        internal static string ExamineWithoutAnomaly(Pawn patient, Pawn surgeon)
        {
            if (patient?.health?.hediffSet?.hediffs == null)
            {
                return string.Empty;
            }

            StringBuilder findings = new StringBuilder();
            List<Hediff> hediffs = patient.health.hediffSet.hediffs.ToList();
            foreach (Hediff hediff in hediffs)
            {
                if (hediff == null || hediff.Visible ||
                    hediff.TryGetComp<HediffComp_SurgeryInspectableEmbryo>() is not HediffComp_SurgeryInspectableEmbryo embryo)
                {
                    continue;
                }

                if (embryo.DoSurgicalInspection(surgeon) != SurgicalInspectionOutcome.Nothing)
                {
                    hediff.SetVisible();
                    TaggedString description = embryo.Props.surgicalDetectionDesc.Formatted(
                        patient.Named("PAWN"), surgeon.Named("SURGEON"));
                    AppendFinding(findings, description);
                }
            }

            AppendFinding(findings, RevealMutations(patient, surgeon, HiddenMutations(patient)));
            return findings.ToString();
        }

        private static void AppendFinding(StringBuilder builder, string finding)
        {
            if (finding.NullOrEmpty())
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
            builder.Append(finding);
        }
    }
}
