using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public static class TissueSummaryBuilder
    {
        public static List<(Hediff tissue, TissueSummaryRule diagnosis)> CollectDiagnoses(IEnumerable<Hediff> entries)
        {
            var matches = new List<(Hediff tissue, TissueSummaryRule diagnosis)>();
            foreach (Hediff tissue in entries)
                foreach (TissueSummaryRule diagnosis in tissue.def.GetModExtension<MutationTissueExtension>()?.diagnoses ?? Enumerable.Empty<TissueSummaryRule>())
                    if (diagnosis.Worker.Matches(tissue, diagnosis)) matches.Add((tissue, diagnosis));
            return matches;
        }
        public static List<KeyValuePair<string, string>> Build(Pawn pawn, TissueAssessmentDef data)
        {
            var entries = pawn.health.hediffSet.hediffs.Where(h => (!data.requireVisible || h.Visible) && MutationTissueAssessment.CanPresent(h)).ToList();
            var matches = CollectDiagnoses(entries);

            var rows = new List<(int priority, int sequence, string text, string detail)>();
            foreach (var group in matches.GroupBy(m => new {
                Key = m.diagnosis.groupKey.NullOrEmpty() ? m.tissue.def.defName : m.diagnosis.groupKey,
                m.diagnosis.workerClass, m.diagnosis.labelKey, m.diagnosis.activityAdviceKey,
                m.diagnosis.restingAdviceKey, Context = m.diagnosis.Worker.Group(m.tissue) }))
            {
                var first = group.First();
                var sources = group.Select(m => m.tissue).Distinct().ToList();
                var diagnosis = first.diagnosis;
                string text = diagnosis.labelKey.Translate(Names(sources, true, data), diagnosis.Worker.Detail(first.tissue)).ToString();
                string advice = diagnosis.Worker.Advice(first.tissue, diagnosis);
                if (!advice.NullOrEmpty()) text += " " + advice.Translate();
                rows.Add((group.Min(m => m.diagnosis.priority), rows.Count, text, Details(sources, data)));
            }
            if (!matches.Any(m => m.diagnosis.activeMutator) && !data.noMutatorsKey.NullOrEmpty())
                rows.Add((data.noMutatorsPriority, rows.Count, data.noMutatorsKey.Translate(), ""));
            if (!matches.Any(m => m.diagnosis.hazard) && !data.noHazardsKey.NullOrEmpty())
                rows.Add((data.noHazardsPriority, rows.Count, data.noHazardsKey.Translate(), ""));
            var consumed = matches.Select(m => m.tissue).ToHashSet();
            var other = entries.Where(h => !consumed.Contains(h) && (h.def.GetModExtension<MutationTissueExtension>() != null ||
                (data.includeInfluenceSources && XenoGeneDefOf.XMT_InfluencesSet.influences.Any(i => i.hediff == h.def)))).ToList();
            if (other.Count > 0 && !data.otherTissueKey.NullOrEmpty())
                rows.Add((data.otherTissuePriority, rows.Count, data.otherTissueKey.Translate(Names(other, true, data)), Details(other, data)));
            return rows.OrderBy(r => r.priority).ThenBy(r => r.sequence)
                .Select(r => new KeyValuePair<string, string>(r.text, r.detail)).ToList();
        }
        public static string Names(IEnumerable<Hediff> conditions, bool compact, TissueAssessmentDef data)
        {
            var names = conditions.GroupBy(h => new { h.def, Label = MutationTissueAssessment.Label(h) }).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Label)
                .Select(g => g.Count() > 1 ? data.countKey.Translate(g.Key.Label, g.Count()).ToString() : g.Key.Label).ToList();
            if (!compact) return string.Join("\n", names);
            int limit = Math.Max(1, data.compactNameLimit);
            string text = string.Join(", ", names.Take(limit));
            return names.Count > limit ? data.moreKey.Translate(text, names.Count - limit).ToString() : text;
        }
        private static string Details(IEnumerable<Hediff> conditions, TissueAssessmentDef data) => string.Join("\n\n", conditions.GroupBy(h => h.def).Select(g =>
        {
            var metadata = g.Key.GetModExtension<MutationTissueExtension>();
            string key = MutationTissueAssessment.PresentationKey(g.First(), metadata?.descriptionKey, metadata?.revealedDescriptionKey);
            return Names(g, false, data) + (key.NullOrEmpty() ? "" : "\n" + key.Translate());
        }));
    }
}
