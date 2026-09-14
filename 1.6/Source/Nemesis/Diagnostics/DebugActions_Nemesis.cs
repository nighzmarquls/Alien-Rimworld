using LudeonTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Xenomorphtype
{
    internal static class DebugActions_Nemesis
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> NemesisNodes()
        {
            yield return new DebugActionNode("Nemesis", DebugActionType.Action, null)
            {
                category = Category,
                childGetter = delegate
                {
                    return new List<DebugActionNode>
                    {
                        DebugActions_NemesisMission.MakeNode(),
                        Group("Report", new DebugActionNode("Known state", DebugActionType.Action, ReportKnownState),
                            new DebugActionNode("Last census plan", DebugActionType.Action, ReportCensusPlan),
                            new DebugActionNode("Spatial contacts", DebugActionType.Action, ReportSpatialContacts)),
                        Group("Intelligence", new DebugActionNode("Full census", DebugActionType.Action, () => RequestCensus("full", null)),
                            new DebugActionNode("Buildings only", DebugActionType.Action, () => RequestCensus("buildings", new[] { NemesisTraversalCategory.PlayerBuildings })),
                            new DebugActionNode("Pawns only", DebugActionType.Action, () => RequestCensus("pawns", new[] { NemesisTraversalCategory.SpawnedPawns, NemesisTraversalCategory.HeldPawns })),
                            new DebugActionNode("Stockpiles only", DebugActionType.Action, () => RequestCensus("stockpiles", new[] { NemesisTraversalCategory.StoredItems })),
                            new DebugActionNode("World xenotypes only", DebugActionType.Action, () => RequestCensus("world xenotypes", new[] { NemesisTraversalCategory.WorldPawns }))),
                        Group("Evaluate", new DebugActionNode("Preview", DebugActionType.Action, () => Evaluate(false)),
                            new DebugActionNode("Commit", DebugActionType.Action, () => Evaluate(true)),
                            new DebugActionNode("Expire commitment", DebugActionType.Action, ExpireCommitment)),
                        Group("Scenarios",
                            new DebugActionNode("Run state-contract sequence (resets)", DebugActionType.Action, NemesisScenarioHarness.RunStateContractSequence),
                            new DebugActionNode("Run corpse-lifecycle sequence... (resets/replaces)", DebugActionType.Action, NemesisScenarioHarness.BeginCorpseLifecycleSequence),
                            new DebugActionNode("Run queen sleep/wake sequence (resets)", DebugActionType.Action, NemesisScenarioHarness.RunQueenSleepSequence),
                            new DebugActionNode("Run abducted-xenotype delay (resets)", DebugActionType.Action, NemesisScenarioHarness.RunAbductedXenotypeDelaySequence),
                            new DebugActionNode("Spawn and test defense layout... (replaces)", DebugActionType.Action, NemesisScenarioHarness.BeginDefenseLayoutScenario),
                            new DebugActionNode("Reobserve spawned layout", DebugActionType.Action, NemesisScenarioHarness.ReobserveSpawnedLayout),
                            new DebugActionNode("Prepare save/reload persistence (resets)", DebugActionType.Action, NemesisScenarioHarness.PrepareSaveReloadSequence),
                            new DebugActionNode("Verify save/reload persistence", DebugActionType.Action, NemesisScenarioHarness.VerifySaveReloadSequence),
                            new DebugActionNode("Clean spawned fixtures", DebugActionType.Action, NemesisScenarioHarness.CleanupSpawnedFixtures)),
                        MakeEvidenceNode(),
                        Group("Reset", new DebugActionNode("Wipe intelligence only", DebugActionType.Action, ResetIntelligence),
                            Group("CONFIRM wipe all Nemesis state", new DebugActionNode("Confirm wipe", DebugActionType.Action, ResetAll))),
                        new DebugActionNode("Force awakening", DebugActionType.Action, ForceAwakening)
                    };
                }
            };
        }

        private static DebugActionNode Group(string label, params DebugActionNode[] children)
        {
            return new DebugActionNode(label, DebugActionType.Action, null)
            {
                childGetter = () => children.ToList()
            };
        }

        private static DebugActionNode MakeEvidenceNode()
        {
            return new DebugActionNode("Add test evidence", DebugActionType.Action, null)
            {
                childGetter = delegate
                {
                    return DefDatabase<NemesisEvidenceDef>.AllDefsListForReading.OrderBy(def => def.defName).Select(def =>
                    {
                        NemesisEvidenceDef selected = def;
                        return new DebugActionNode(selected.defName + " (+1)", DebugActionType.Action, delegate
                        {
                            GameComponent_Nemesis component = Component;
                            bool accepted = component?.RecordEvidence(selected, 1f, null, null, selected, "developer injection") == true;
                            NemesisAssessment assessment = component?.Evaluate(false);
                            Log.Message("[XMT][Nemesis] Test evidence " + selected.defName + " was "
                                + (accepted ? "accepted" : "REJECTED (system dormant or invalid input)")
                                + ". Preview recommendation: " + (assessment?.recommended?.stance?.defName ?? "none") + ".");
                        });
                    }).ToList();
                }
            };
        }

        private static GameComponent_Nemesis Component => Current.Game?.GetComponent<GameComponent_Nemesis>();

        private static void ReportKnownState()
        {
            GameComponent_Nemesis component = Component;
            if (component == null) { Log.Message("[XMT][Nemesis] No game component is available."); return; }
            int tick = Find.TickManager?.TicksGame ?? 0;
            StringBuilder builder = new StringBuilder("[XMT][Nemesis] Known-state diagnostic\n");
            builder.AppendLine("Awakened: " + component.Awakened + "; awakenedTick=" + component.AwakenedTick
                + "; committed stance: " + (component.CurrentStance?.defName ?? "none") + "; committedTick=" + component.StanceCommittedTick);
            builder.AppendLine("Revisions: evidence=" + component.EvidenceRevision + ", intelligence=" + component.IntelligenceRevision + ", assessment=" + component.AssessmentRevision);
            builder.AppendLine("Observations:");
            foreach (NemesisObservationRecord record in component.Observations.OrderBy(value => value.observation.defName))
                builder.AppendLine("  " + record.observation.defName + "=" + record.value.ToString("0.###") + " confidence="
                    + record.EffectiveConfidenceAt(tick).ToString("0.###") + " observedTick=" + record.observedTick + " source=" + record.source);
            builder.AppendLine("Evidence aggregates:");
            foreach (NemesisEvidenceAggregate aggregate in component.Evidence.OrderBy(value => value.evidence.defName))
                builder.AppendLine("  " + aggregate.evidence.defName + "=" + aggregate.ValueAt(tick).ToString("0.###") + " lastUpdatedTick=" + aggregate.lastUpdatedTick);
            builder.AppendLine("Map evidence:");
            foreach (NemesisMapEvidenceAggregate aggregate in component.MapEvidence.OrderBy(value => value.evidence.defName).ThenBy(value => value.mapId))
                builder.AppendLine("  " + aggregate.evidence.defName + "=" + aggregate.ValueAt(tick).ToString("0.###")
                    + " map=" + aggregate.mapLabel + "#" + aggregate.mapId + " lastUpdatedTick=" + aggregate.lastUpdatedTick);
            builder.AppendLine("Recent event ledger (diagnostic identity only; never spatial targeting):");
            foreach (NemesisEvidenceEvent evidenceEvent in component.RecentEvidence.Skip(Math.Max(0, component.RecentEvidence.Count - 12)))
                builder.AppendLine("  tick=" + evidenceEvent.tick + " " + evidenceEvent.evidence.defName + " amount=" + evidenceEvent.amount.ToString("0.###")
                    + " source=" + (evidenceEvent.sourceDefName ?? "none") + (evidenceEvent.detail.NullOrEmpty() ? string.Empty : " [" + evidenceEvent.detail + "]"));
            builder.AppendLine("Xenotype knowledge (canonical, accumulated samples):");
            foreach (NemesisXenotypeRecord record in component.XenotypeKnowledge.OrderBy(value => value.signature))
                builder.AppendLine("  " + record.signature + " label=" + (record.label ?? "none") + " latestSampleCount=" + record.sampleCount
                    + " abducted=" + record.abductedCount + " lastSampledTick=" + record.lastSampledTick
                    + " origin=" + record.origin + " available=" + record.AvailableAt(tick) + " availableTick=" + record.availableTick);
            if (component.LastCensus != null)
            {
                builder.AppendLine("Current last-census xenotype sample frequencies:");
                foreach (IGrouping<string, NemesisXenotypeRecord> group in component.LastCensus.xenotypeSamples.GroupBy(value => value.signature).OrderBy(value => value.Key))
                    builder.AppendLine("  " + group.Key + "=" + group.Count());
            }
            AppendAssessment(builder, component.Evaluate(false));
            Log.Message(builder.ToString());
        }

        private static void ReportCensusPlan()
        {
            NemesisCensusReport report = Component?.LastCensus;
            Log.Message(report == null ? "[XMT][Nemesis] No census has run." : "[XMT][Nemesis] Last census: " + report.reason + " at " + report.tick + "\n" + report.DescribePlan());
        }

        private static void ReportSpatialContacts()
        {
            GameComponent_Nemesis component = Component;
            if (component == null) { Log.Message("[XMT][Nemesis] No game component is available."); return; }
            int tick = Find.TickManager?.TicksGame ?? 0;
            StringBuilder builder = new StringBuilder("[XMT][Nemesis] Observation-owned spatial contacts\n");
            foreach (NemesisSpatialContact contact in component.SpatialContacts.OrderBy(value => value.observation.defName).ThenBy(value => value.mapId).ThenBy(value => value.subjectId))
                builder.AppendLine("  " + contact.observation.defName + " subject=" + contact.subjectId
                    + (contact.observedThingId != contact.subjectId ? " observedThing=" + contact.observedThingId : string.Empty) + " def=" + contact.defName
                    + " map=" + contact.mapLabel + "#" + contact.mapId + " cell=" + contact.cell + " threat=" + contact.threat.ToString("0.###")
                    + " observedTick=" + contact.observedTick + " ageTicks=" + Math.Max(0, tick - contact.observedTick)
                    + " confidence=" + contact.EffectiveConfidenceAt(tick).ToString("0.###") + " tags=" + string.Join(",", contact.tags));
            builder.AppendLine("Total contacts: " + component.SpatialContacts.Count + ". Evidence events cannot populate this list.");
            Log.Message(builder.ToString());
        }

        private static void RequestCensus(string label, IEnumerable<NemesisTraversalCategory> categories)
        {
            GameComponent_Nemesis component = Component;
            if (component == null) { Log.Message("[XMT][Nemesis] No game component is available."); return; }
            NemesisCensusReport report = component.RequestIntelligence("developer " + label + " intelligence request", null, categories);
            Log.Message("[XMT][Nemesis] " + label + " census complete for " + report.mapCount + " map(s), "
                + report.observations.Count + " observation(s), " + report.xenotypeSamples.Count + " xenotype sample(s).\n" + report.DescribePlan());
        }

        private static void Evaluate(bool commit)
        {
            if (Component == null) { Log.Message("[XMT][Nemesis] No game component is available."); return; }
            StringBuilder builder = new StringBuilder("[XMT][Nemesis] " + (commit ? "Commit request" : "Preview") + "\n");
            AppendAssessment(builder, Component.Evaluate(commit));
            Log.Message(builder.ToString());
        }

        private static void ExpireCommitment()
        {
            Component?.ExpireCommitmentForDiagnostics();
            Log.Message("[XMT][Nemesis] Commitment window expired for diagnostic testing; no stance evaluation was committed.");
        }

        private static void ForceAwakening()
        {
            GameComponent_Nemesis component = Component;
            bool awakened = component?.Awaken("developer forced awakening", true) == true;
            Log.Message(awakened ? "[XMT][Nemesis] System awakened; initial census and evaluation committed."
                : "[XMT][Nemesis] Awakening request had no effect because the system is already awake or unavailable.");
        }

        private static void ResetIntelligence()
        {
            Component?.ResetIntelligence();
            Log.Message("[XMT][Nemesis] Wiped observations, spatial contacts, xenotype knowledge, and transient intelligence reports. Evidence and strategic commitment were retained.");
        }

        private static void ResetAll()
        {
            Component?.ResetAllState();
            Log.Message("[XMT][Nemesis] Wiped all Nemesis state. The map, pawns, buildings, and underlying xenoforming value were not changed.");
        }

        private static void AppendAssessment(StringBuilder builder, NemesisAssessment assessment)
        {
            if (assessment == null) { builder.AppendLine("No assessment available."); return; }
            builder.AppendLine("Signals:");
            foreach (NemesisSignalResult signal in assessment.signals)
            {
                builder.AppendLine("  " + signal.signal.defName + "=" + signal.value.ToString("0.###"));
                foreach (NemesisContribution contribution in signal.contributions)
                    builder.AppendLine("    " + contribution.source + ": raw=" + contribution.rawValue.ToString("0.###") + " confidence="
                        + contribution.confidence.ToString("0.###") + " weight=" + contribution.weight.ToString("0.###") + " contribution=" + contribution.contribution.ToString("0.###"));
            }
            builder.AppendLine("Stances:");
            foreach (NemesisStanceResult stance in assessment.stances)
                builder.AppendLine("  " + stance.stance.defName + " score=" + stance.score.ToString("0.###") + " eligible=" + stance.eligible
                    + " evolutionSet=" + (stance.stance.evolutionSet?.defName ?? "none")
                    + (stance.failureReasons.Count > 0 ? " failed=[" + string.Join("; ", stance.failureReasons) + "]" : string.Empty));
            builder.AppendLine("Recommended: " + (assessment.recommended?.stance?.defName ?? "none"));
            builder.AppendLine(assessment.commitmentExplanation ?? string.Empty);
        }
    }
}
