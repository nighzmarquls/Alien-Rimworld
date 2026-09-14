using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal static class NemesisScenarioHarness
    {
        private const string LogPrefix = "[XMT][Nemesis][Scenario] ";
        private const string SaveReloadMarker = "nemesis-scenario-save-reload-v1";
        private static readonly List<Thing> SpawnedFixtures = new List<Thing>();
        private static int runNumber;

        private sealed class ScenarioResult
        {
            private readonly int run;
            private readonly string label;
            private readonly List<string> checks = new List<string>();
            private int failures;

            public ScenarioResult(string label)
            {
                run = ++runNumber;
                this.label = label;
                Log.Message(LogPrefix + "BEGIN #" + run + " " + label);
            }

            public void Check(string check, bool passed, object expected, object actual)
            {
                if (!passed)
                {
                    failures++;
                }
                checks.Add((passed ? "PASS " : "FAIL ") + check + " expected=" + expected + " actual=" + actual);
            }

            public void Note(string note)
            {
                checks.Add("INFO " + note);
            }

            public void Finish(Thing lookTarget = null)
            {
                foreach (string check in checks)
                {
                    Log.Message(LogPrefix + "#" + run + " " + check);
                }

                int passed = checks.Count(check => check.StartsWith("PASS "));
                int asserted = passed + failures;
                Log.Message(LogPrefix + "END #" + run + " " + label + " passed=" + passed + "/" + asserted
                    + " failures=" + failures);
                Messages.Message("Nemesis scenario " + label + " " + (failures == 0 ? "PASS" : "FAIL")
                    + ": " + passed + "/" + asserted + ". See log for expected/actual values.",
                    lookTarget, failures == 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            }

            public void Fail(Exception exception)
            {
                failures++;
                checks.Add("FAIL unhandled exception expected=none actual=" + exception);
            }
        }

        internal static void RunStateContractSequence()
        {
            ScenarioResult result = new ScenarioResult("state-contract sequence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            if (component == null || Find.CurrentMap == null)
            {
                result.Check("active game and map", false, true, false);
                result.Finish();
                return;
            }

            try
            {
                component.ResetAllState();
                component.Awaken("developer state-contract scenario", runCensus: false);
                result.Check("explicit awakening", component.Awakened, true, component.Awakened);

                int evidenceBeforeCensus = component.EvidenceRevision;
                NemesisCensusReport first = component.RequestIntelligence("developer state-contract initial census");
                result.Check("full census produced observations", first.observations.Count > 0, ">0", first.observations.Count);
                result.Check("census is evidence-pure", component.EvidenceRevision == evidenceBeforeCensus,
                    evidenceBeforeCensus, component.EvidenceRevision);

                Dictionary<string, int> firstXenotypes = component.XenotypeKnowledge
                    .ToDictionary(record => record.signature, record => record.sampleCount);
                int contactsAfterFirst = component.SpatialContacts.Count;
                int evidenceBeforeRepeat = component.EvidenceRevision;
                component.RequestIntelligence("developer state-contract repeated census");
                Dictionary<string, int> repeatedXenotypes = component.XenotypeKnowledge
                    .ToDictionary(record => record.signature, record => record.sampleCount);
                bool stableXenotypes = firstXenotypes.Count == repeatedXenotypes.Count
                    && firstXenotypes.All(pair => repeatedXenotypes.TryGetValue(pair.Key, out int count) && count == pair.Value);
                result.Check("repeated census does not inflate xenotype samples", stableXenotypes,
                    DescribeCounts(firstXenotypes), DescribeCounts(repeatedXenotypes));
                result.Check("repeated census remains evidence-pure", component.EvidenceRevision == evidenceBeforeRepeat,
                    evidenceBeforeRepeat, component.EvidenceRevision);
                result.Note("spatial contacts first=" + contactsAfterFirst + " repeated=" + component.SpatialContacts.Count
                    + " (replacement is observation-owned; equality is map-state dependent)");

                NemesisCensusReport partial = component.RequestIntelligence("developer state-contract buildings-only census", null,
                    new[] { NemesisTraversalCategory.PlayerBuildings });
                bool onlyBuildings = partial.requestedTraversals.Count == 1
                    && partial.requestedTraversals[0] == NemesisTraversalCategory.PlayerBuildings;
                result.Check("partial request runs only requested traversal", onlyBuildings,
                    NemesisTraversalCategory.PlayerBuildings, string.Join(",", partial.requestedTraversals));
                result.Check("unused traversals are explicitly skipped",
                    partial.skippedTraversals.Count == Enum.GetValues(typeof(NemesisTraversalCategory)).Length - 1,
                    Enum.GetValues(typeof(NemesisTraversalCategory)).Length - 1, partial.skippedTraversals.Count);

                NemesisEvidenceDef testEvidence = DefDatabase<NemesisEvidenceDef>.GetNamedSilentFail("XMT_Nemesis_CryptimorphInjured");
                int intelligenceBeforeEvent = component.IntelligenceRevision;
                int contactsBeforeEvent = component.SpatialContacts.Count;
                int evidenceBeforeEvent = component.EvidenceRevision;
                bool accepted = component.RecordEvidence(testEvidence, 0.5f, null, null, testEvidence,
                    "developer state-contract synthetic event");
                result.Check("immediate evidence accepted while awake", accepted, true, accepted);
                result.Check("immediate evidence increments only evidence revision",
                    component.EvidenceRevision == evidenceBeforeEvent + 1
                    && component.IntelligenceRevision == intelligenceBeforeEvent,
                    "evidence+1,intelligence unchanged",
                    "evidence=" + component.EvidenceRevision + ",intelligence=" + component.IntelligenceRevision);
                result.Check("event evidence cannot create spatial contacts", component.SpatialContacts.Count == contactsBeforeEvent,
                    contactsBeforeEvent, component.SpatialContacts.Count);

                int assessmentBeforePreview = component.AssessmentRevision;
                NemesisAssessment preview = component.Evaluate(commit: false);
                result.Check("preview recommends a stance", preview?.recommended?.stance != null, "non-null",
                    preview?.recommended?.stance?.defName ?? "none");
                result.Check("preview does not commit assessment state", component.AssessmentRevision == assessmentBeforePreview,
                    assessmentBeforePreview, component.AssessmentRevision);
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }

            result.Finish();
        }

        internal static void BeginCorpseLifecycleSequence()
        {
            BeginCellTargeting("Select an open area for the Nemesis corpse lifecycle scenario.", RunCorpseLifecycleSequence);
        }

        private static void RunCorpseLifecycleSequence(IntVec3 selectedCell, Map map)
        {
            ScenarioResult result = new ScenarioResult("corpse-lifecycle sequence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            StarbeastCorpse fatalCorpse = null;
            if (component == null || map == null)
            {
                result.Check("active component and map", false, true, false);
                result.Finish();
                return;
            }

            try
            {
                component.ResetAllState();
                component.Awaken("developer corpse-lifecycle scenario", runCensus: false);

                IntVec3 playDeadCell = selectedCell;
                IntVec3 fatalCell = selectedCell + new IntVec3(3, 0, 0);
                HashSet<IntVec3> footprint = new HashSet<IntVec3> { playDeadCell, fatalCell };
                if (!footprint.All(cell => cell.InBounds(map)))
                {
                    throw new InvalidOperationException("The selected corpse-scenario footprint extends out of bounds.");
                }
                ClearScenarioFootprint(map, footprint);
                Pawn playDeadPawn = SpawnFeralCryptimorph(map, playDeadCell);
                string playDeadSubject = playDeadPawn.GetUniqueLoadID();
                playDeadPawn.Kill(new DamageInfo(DamageDefOf.Cut, 1f));
                StarbeastCorpse livingCorpse = playDeadPawn.Corpse as StarbeastCorpse;
                if (livingCorpse != null)
                {
                    SpawnedFixtures.Add(livingCorpse);
                }

                result.Check("healthy forced death creates custom corpse", livingCorpse != null,
                    nameof(StarbeastCorpse), livingCorpse?.GetType().Name ?? "none");
                result.Check("healthy corpse is classified as play-dead",
                    CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", playDeadSubject) == 1,
                    1, CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", playDeadSubject));
                result.Check("play-dead transition is not initially counted as confirmed death",
                    CountEvents(component, "XMT_Nemesis_CryptimorphKilled", playDeadSubject) == 0,
                    0, CountEvents(component, "XMT_Nemesis_CryptimorphKilled", playDeadSubject));

                int evidenceBeforeObservation = component.EvidenceRevision;
                component.RequestIntelligence("developer corpse-lifecycle corpse census", null,
                    new[] { NemesisTraversalCategory.CryptimorphCorpses });
                float livingCount = ObservationValue(component, "XMT_NemesisObs_LivingCryptimorphCorpses");
                bool hasContact = component.SpatialContacts.Any(contact => contact.subjectId == playDeadSubject
                    && contact.observation?.defName == "XMT_NemesisObs_LivingCryptimorphCorpses");
                result.Check("living corpse is observed", livingCount >= 1f, ">=1", livingCount);
                result.Check("living corpse creates subject-addressable contact", hasContact, true, hasContact);
                result.Check("corpse census does not create event evidence",
                    component.EvidenceRevision == evidenceBeforeObservation, evidenceBeforeObservation, component.EvidenceRevision);

                if (livingCorpse != null && !livingCorpse.Destroyed)
                {
                    livingCorpse.TakeDamage(new DamageInfo(DamageDefOf.Bomb,
                        Math.Max(2f, livingCorpse.MaxHitPoints * 2f), 999f));
                }
                result.Check("destroyed living corpse produces one later confirmed death",
                    CountEvents(component, "XMT_Nemesis_CryptimorphKilled", playDeadSubject) == 1,
                    1, CountEvents(component, "XMT_Nemesis_CryptimorphKilled", playDeadSubject));
                result.Check("corpse destruction does not duplicate play-dead event",
                    CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", playDeadSubject) == 1,
                    1, CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", playDeadSubject));

                Pawn fatalPawn = SpawnFeralCryptimorph(map, fatalCell);
                string fatalSubject = fatalPawn.GetUniqueLoadID();
                BodyPartRecord fatalPart = fatalPawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(part => part.def == InternalDefOf.StarbeastBrain)
                    ?? fatalPawn.health.hediffSet.GetBrain()
                    ?? fatalPawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.def == BodyPartDefOf.Torso);
                DamageInfo fatalDamage = new DamageInfo(DamageDefOf.Cut, 10000f, 999f, -1f, null, fatalPart);
                fatalPawn.TakeDamage(fatalDamage);
                if (!fatalPawn.Dead)
                {
                    fatalPawn.Kill(fatalDamage);
                }
                fatalCorpse = fatalPawn.Corpse as StarbeastCorpse;
                if (fatalCorpse != null)
                {
                    SpawnedFixtures.Add(fatalCorpse);
                }

                result.Check("fatal organ damage produces one confirmed death",
                    CountEvents(component, "XMT_Nemesis_CryptimorphKilled", fatalSubject) == 1,
                    1, CountEvents(component, "XMT_Nemesis_CryptimorphKilled", fatalSubject));
                result.Check("fatal organ damage never enters play-dead",
                    CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", fatalSubject) == 0,
                    0, CountEvents(component, "XMT_Nemesis_CryptimorphPlayedDead", fatalSubject));
                result.Check("fatal corpse is not a living-corpse opportunity",
                    fatalCorpse == null || !fatalCorpse.IsViableLivingCorpseForObservation,
                    false, fatalCorpse?.IsViableLivingCorpseForObservation ?? false);
                result.Note("Scenario deliberately leaves its fatal corpse for inspection; use Clean spawned fixtures afterward.");
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }

            result.Finish(fatalCorpse);
        }

        internal static void BeginDefenseLayoutScenario()
        {
            BeginCellTargeting("Select an empty area for the Nemesis defense-observer layout.", RunDefenseLayoutScenario);
        }

        internal static void RunQueenSleepSequence()
        {
            ScenarioResult result = new ScenarioResult("queen sleep/wake sequence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            float originalXenoforming = XenoformingUtility.GetXenoforming();
            Pawn belowThresholdQueen = null;
            Pawn aboveThresholdQueen = null;
            if (component == null)
            {
                result.Check("active component", false, true, false);
                result.Finish();
                return;
            }

            try
            {
                component.ResetAllState();
                XenoformingUtility.SetXenoforming(0f);
                component.Awaken("developer queen sleep scenario below threshold", runCensus: false);
                belowThresholdQueen = XenoformingUtility.GenerateFeralQueen();
                NemesisEvidenceReporter.ReportCryptimorphPlayedDead(belowThresholdQueen, null);
                result.Check("play-dead queen does not put Nemesis to sleep", component.Awakened, true, component.Awakened);
                NemesisEvidenceReporter.ReportCryptimorphConfirmedDeath(belowThresholdQueen, null, "developer confirmed queen death");
                result.Check("confirmed queen death below threshold sleeps Nemesis", !component.Awakened, false, component.Awakened);

                XenoformingUtility.SetXenoforming(GameComponent_Nemesis.Settings.activationXenoforming + 1f);
                component.ResetAllState();
                component.Awaken("developer queen sleep scenario above threshold", runCensus: false);
                aboveThresholdQueen = XenoformingUtility.GenerateFeralQueen();
                NemesisEvidenceReporter.ReportCryptimorphConfirmedDeath(aboveThresholdQueen, null,
                    "developer confirmed queen death above threshold");
                result.Check("confirmed queen death at sustained xenoforming keeps Nemesis awake",
                    component.Awakened, true, component.Awakened);
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
            finally
            {
                CleanupUnspawnedPawn(belowThresholdQueen);
                CleanupUnspawnedPawn(aboveThresholdQueen);
                component.ResetAllState();
                XenoformingUtility.SetXenoforming(originalXenoforming);
                if (originalXenoforming >= GameComponent_Nemesis.Settings.activationXenoforming)
                {
                    component.Awaken("restored after developer queen sleep scenario", runCensus: true);
                }
            }

            result.Note("Underlying xenoforming was restored to " + originalXenoforming.ToString("0.###")
                + "; Nemesis strategic history was reset by this scenario.");
            result.Finish();
        }

        internal static void RunAbductedXenotypeDelaySequence()
        {
            ScenarioResult result = new ScenarioResult("abducted-xenotype delay sequence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            Pawn pawn = Find.CurrentMap?.mapPawns.AllPawnsSpawned
                .FirstOrDefault(candidate => candidate?.Faction == Faction.OfPlayer && candidate.genes != null);
            if (component == null || pawn == null)
            {
                result.Check("player pawn with genes is available", false, true, pawn != null);
                result.Finish();
                return;
            }

            try
            {
                component.ResetAllState();
                component.Awaken("developer abducted-xenotype scenario", runCensus: false);
                int tick = Find.TickManager?.TicksGame ?? 0;
                NemesisXenotypeRecord expected = NemesisXenotypeRecord.FromPawn(pawn, "scenario", tick, tick);
                component.QueueAbductedXenotype(pawn);
                NemesisXenotypeRecord actual = component.XenotypeKnowledge
                    .FirstOrDefault(record => record.signature == expected?.signature);
                int expectedDelay = Mathf.RoundToInt(GameComponent_Nemesis.Settings.abductedXenotypeProcessingDays * 60000f);
                result.Check("abducted xenotype enters canonical pool", actual != null,
                    expected?.signature ?? "valid signature", actual?.signature ?? "none");
                result.Check("abducted sample is counted separately", actual?.abductedCount == 1 && actual.sampleCount == 0,
                    "abducted=1,samples=0", actual == null ? "missing" : "abducted=" + actual.abductedCount + ",samples=" + actual.sampleCount);
                result.Check("abducted xenotype begins unavailable", actual != null && !actual.AvailableAt(tick),
                    false, actual?.AvailableAt(tick) ?? true);
                result.Check("availability delay matches XML setting", actual != null && actual.availableTick - tick == expectedDelay,
                    expectedDelay, actual == null ? -1 : actual.availableTick - tick);
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
            result.Finish(pawn);
        }

        internal static void PrepareSaveReloadSequence()
        {
            ScenarioResult result = new ScenarioResult("prepare save/reload persistence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            if (component == null || Find.CurrentMap == null)
            {
                result.Check("active component and map", false, true, false);
                result.Finish();
                return;
            }

            try
            {
                component.ResetAllState();
                component.Awaken("developer save/reload scenario", runCensus: true);
                NemesisEvidenceDef markerDef = DefDatabase<NemesisEvidenceDef>.GetNamedSilentFail("XMT_Nemesis_CryptimorphInjured");
                Pawn xenotypePawn = Find.CurrentMap.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(pawn => pawn?.Faction == Faction.OfPlayer && pawn.genes != null);
                if (xenotypePawn != null)
                {
                    component.QueueAbductedXenotype(xenotypePawn);
                }
                component.Evaluate(commit: true);
                string stance = component.CurrentStance?.defName ?? "none";
                string detail = SaveReloadMarker
                    + ";observations=" + component.Observations.Count
                    + ";contacts=" + component.SpatialContacts.Count
                    + ";xenotypes=" + component.XenotypeKnowledge.Count
                    + ";abducted=" + component.XenotypeKnowledge.Sum(record => record.abductedCount)
                    + ";stance=" + stance;
                bool accepted = component.RecordEvidence(markerDef, 0.375f, null, null, markerDef, detail);
                result.Check("persistence marker evidence accepted", accepted, true, accepted);
                result.Check("stance committed before save", component.CurrentStance != null, "non-null", stance);
                result.Note("Save the game, reload it, then run Verify save/reload persistence. Marker: " + detail);
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
            result.Finish();
        }

        internal static void VerifySaveReloadSequence()
        {
            ScenarioResult result = new ScenarioResult("verify save/reload persistence");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            NemesisEvidenceEvent marker = component?.RecentEvidence.LastOrDefault(evidenceEvent =>
                evidenceEvent.detail?.StartsWith(SaveReloadMarker) == true);
            if (component == null || marker == null)
            {
                result.Check("prepared marker survived reload", false, SaveReloadMarker, marker?.detail ?? "missing");
                result.Finish();
                return;
            }

            try
            {
                Dictionary<string, string> expected = ParseMarker(marker.detail);
                result.Check("prepared marker survived reload", true, SaveReloadMarker, SaveReloadMarker);
                result.Check("system remains awake", component.Awakened, true, component.Awakened);
                result.Check("observations survived reload", component.Observations.Count == MarkerInt(expected, "observations"),
                    MarkerInt(expected, "observations"), component.Observations.Count);
                result.Check("spatial contacts survived reload", component.SpatialContacts.Count == MarkerInt(expected, "contacts"),
                    MarkerInt(expected, "contacts"), component.SpatialContacts.Count);
                result.Check("xenotype knowledge survived reload", component.XenotypeKnowledge.Count == MarkerInt(expected, "xenotypes"),
                    MarkerInt(expected, "xenotypes"), component.XenotypeKnowledge.Count);
                result.Check("abducted xenotype count survived reload",
                    component.XenotypeKnowledge.Sum(record => record.abductedCount) == MarkerInt(expected, "abducted"),
                    MarkerInt(expected, "abducted"), component.XenotypeKnowledge.Sum(record => record.abductedCount));
                string expectedStance = expected.TryGetValue("stance", out string stance) ? stance : "none";
                result.Check("committed stance survived reload", component.CurrentStance?.defName == expectedStance,
                    expectedStance, component.CurrentStance?.defName ?? "none");
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
            result.Finish();
        }

        private static void RunDefenseLayoutScenario(IntVec3 center, Map map)
        {
            ScenarioResult result = new ScenarioResult("defense-observer layout");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            if (component == null || map == null)
            {
                result.Check("active component and map", false, true, false);
                result.Finish();
                return;
            }

            List<Tuple<ThingDef, IntVec3, Rot4>> plan = BuildDefenseLayoutPlan(center);
            try
            {
                if (!CanSpawnLayout(map, plan, out string blockedReason))
                {
                    result.Check("layout footprint is in bounds", false, "in bounds", blockedReason);
                    result.Finish();
                    return;
                }

                HashSet<IntVec3> footprint = LayoutFootprint(plan);
                ClearScenarioFootprint(map, footprint);

                if (!component.Awakened)
                {
                    component.Awaken("developer defense-layout scenario", runCensus: false);
                }

                component.RequestIntelligence("developer defense-layout baseline", null,
                    new[] { NemesisTraversalCategory.PlayerBuildings });
                float wallsBefore = ObservationValue(component, "XMT_NemesisObs_Walls");
                float turretsBefore = ObservationValue(component, "XMT_NemesisObs_Turrets");
                float trapsBefore = ObservationValue(component, "XMT_NemesisObs_DeployedTrapThreat");
                float catastrophicBefore = ObservationValue(component, "XMT_NemesisObs_CatastrophicStaticDefense");
                float indirectBefore = ObservationValue(component, "XMT_NemesisObs_IndirectFirepower");

                foreach (Tuple<ThingDef, IntVec3, Rot4> entry in plan)
                {
                    ThingDef stuff = entry.Item1.MadeFromStuff
                        ? (entry.Item1 == ThingDefOf.Wall ? ThingDefOf.BlocksGranite : ThingDefOf.Steel)
                        : null;
                    Thing thing = ThingMaker.MakeThing(entry.Item1, stuff);
                    thing.SetFactionDirect(Faction.OfPlayer);
                    Thing spawned = GenSpawn.Spawn(thing, entry.Item2, map, entry.Item3, WipeMode.VanishOrMoveAside);
                    SpawnedFixtures.Add(spawned);
                }

                component.RequestIntelligence("developer defense-layout observed", null,
                    new[] { NemesisTraversalCategory.PlayerBuildings });
                float wallsAfter = ObservationValue(component, "XMT_NemesisObs_Walls");
                float turretsAfter = ObservationValue(component, "XMT_NemesisObs_Turrets");
                float trapsAfter = ObservationValue(component, "XMT_NemesisObs_DeployedTrapThreat");
                float catastrophicAfter = ObservationValue(component, "XMT_NemesisObs_CatastrophicStaticDefense");
                float indirectAfter = ObservationValue(component, "XMT_NemesisObs_IndirectFirepower");

                int plannedWalls = plan.Count(entry => entry.Item1 == ThingDefOf.Wall);
                result.Check("wall observer counts every fixture", Approximately(wallsAfter - wallsBefore, plannedWalls),
                    plannedWalls, wallsAfter - wallsBefore);
                result.Check("turret observer recognizes mini-turret and mortar", Approximately(turretsAfter - turretsBefore, 2f),
                    2, turretsAfter - turretsBefore);
                result.Check("trap observer gains bounded strategic pressure", trapsAfter > trapsBefore,
                    ">" + trapsBefore.ToString("0.###"), trapsAfter.ToString("0.###"));
                result.Check("antigrain fixture reaches bounded catastrophic ceiling",
                    catastrophicAfter - catastrophicBefore >= 9.99f && catastrophicAfter - catastrophicBefore <= 10.01f,
                    10, catastrophicAfter - catastrophicBefore);
                result.Check("mortar contributes indirect-fire pressure", indirectAfter > indirectBefore,
                    ">" + indirectBefore.ToString("0.###"), indirectAfter.ToString("0.###"));
                result.Note("Layout remains spawned for visual inspection and stale-intelligence experiments.");
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }

            result.Finish(SpawnedFixtures.LastOrDefault(thing => thing != null && !thing.Destroyed));
        }

        internal static void ReobserveSpawnedLayout()
        {
            ScenarioResult result = new ScenarioResult("reobserve spawned layout");
            GameComponent_Nemesis component = Current.Game?.GetComponent<GameComponent_Nemesis>();
            try
            {
                int liveFixtures = SpawnedFixtures.Count(thing => thing != null && !thing.Destroyed && thing.Spawned);
                result.Check("live scenario fixtures exist", liveFixtures > 0, ">0", liveFixtures);
                NemesisCensusReport report = component?.RequestIntelligence("developer scenario layout reobservation", null,
                    new[] { NemesisTraversalCategory.PlayerBuildings, NemesisTraversalCategory.CryptimorphCorpses });
                result.Check("targeted reobservation completed", report != null, "non-null", report == null ? "null" : "complete");
                result.Note("walls=" + ObservationValue(component, "XMT_NemesisObs_Walls").ToString("0.###")
                    + " turrets=" + ObservationValue(component, "XMT_NemesisObs_Turrets").ToString("0.###")
                    + " traps=" + ObservationValue(component, "XMT_NemesisObs_DeployedTrapThreat").ToString("0.###")
                    + " catastrophic=" + ObservationValue(component, "XMT_NemesisObs_CatastrophicStaticDefense").ToString("0.###")
                    + " livingCorpses=" + ObservationValue(component, "XMT_NemesisObs_LivingCryptimorphCorpses").ToString("0.###"));
            }
            catch (Exception exception)
            {
                result.Fail(exception);
            }
            result.Finish();
        }

        internal static void CleanupSpawnedFixtures()
        {
            int removed = 0;
            for (int i = SpawnedFixtures.Count - 1; i >= 0; i--)
            {
                Thing thing = SpawnedFixtures[i];
                if (thing != null && !thing.Destroyed && thing.Spawned)
                {
                    thing.Destroy(DestroyMode.Vanish);
                    removed++;
                }
            }
            SpawnedFixtures.Clear();
            Log.Message(LogPrefix + "Cleaned " + removed + " spawned fixture(s). Stored observations were intentionally left stale.");
            Messages.Message("Cleaned " + removed + " Nemesis scenario fixture(s). Stored observations remain stale until another census.",
                MessageTypeDefOf.TaskCompletion, false);
        }

        private static Pawn SpawnFeralCryptimorph(Map map, IntVec3 cell)
        {
            Pawn pawn = XenoformingUtility.GenerateFeralXenomorph();
            if (pawn == null)
            {
                throw new InvalidOperationException("Could not generate a feral cryptimorph fixture.");
            }
            GenSpawn.Spawn(pawn, cell, map, WipeMode.VanishOrMoveAside);
            SpawnedFixtures.Add(pawn);
            return pawn;
        }

        private static int CountEvents(GameComponent_Nemesis component, string defName, string subjectId)
        {
            return component?.RecentEvidence.Count(evidenceEvent => evidenceEvent.evidence?.defName == defName
                && evidenceEvent.subjectId == subjectId) ?? 0;
        }

        private static float ObservationValue(GameComponent_Nemesis component, string defName)
        {
            return component?.Observations.FirstOrDefault(record => record.observation?.defName == defName)?.value ?? 0f;
        }

        private static string DescribeCounts(Dictionary<string, int> counts)
        {
            return string.Join(",", counts.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
        }

        private static Dictionary<string, string> ParseMarker(string detail)
        {
            return detail.Split(';').Skip(1).Select(part => part.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2).ToDictionary(parts => parts[0], parts => parts[1]);
        }

        private static int MarkerInt(Dictionary<string, string> marker, string key)
        {
            return marker.TryGetValue(key, out string value) && int.TryParse(value, out int parsed) ? parsed : -1;
        }

        private static void CleanupUnspawnedPawn(Pawn pawn)
        {
            if (pawn != null && !pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) < 0.01f;
        }

        private static List<Tuple<ThingDef, IntVec3, Rot4>> BuildDefenseLayoutPlan(IntVec3 center)
        {
            ThingDef miniTurret = DefDatabase<ThingDef>.GetNamed("Turret_MiniTurret");
            ThingDef mortar = DefDatabase<ThingDef>.GetNamed("Turret_Mortar");
            ThingDef highExplosive = DefDatabase<ThingDef>.GetNamed("TrapIED_HighExplosive");
            ThingDef antigrain = DefDatabase<ThingDef>.GetNamed("TrapIED_AntigrainWarhead");
            List<Tuple<ThingDef, IntVec3, Rot4>> plan = new List<Tuple<ThingDef, IntVec3, Rot4>>();
            for (int x = -6; x <= 6; x++)
            {
                plan.Add(Tuple.Create(ThingDefOf.Wall, center + new IntVec3(x, 0, -4), Rot4.North));
            }
            plan.Add(Tuple.Create(miniTurret, center + new IntVec3(-4, 0, 0), Rot4.North));
            plan.Add(Tuple.Create(mortar, center + new IntVec3(3, 0, 0), Rot4.North));
            plan.Add(Tuple.Create(highExplosive, center + new IntVec3(-2, 0, 3), Rot4.North));
            plan.Add(Tuple.Create(antigrain, center + new IntVec3(2, 0, 3), Rot4.North));
            return plan;
        }

        private static bool CanSpawnLayout(Map map, List<Tuple<ThingDef, IntVec3, Rot4>> plan, out string reason)
        {
            foreach (Tuple<ThingDef, IntVec3, Rot4> entry in plan)
            {
                CellRect occupied = GenAdj.OccupiedRect(entry.Item2, entry.Item3, entry.Item1.size);
                foreach (IntVec3 cell in occupied)
                {
                    if (!cell.InBounds(map))
                    {
                        reason = entry.Item1.defName + " footprint is out of bounds at " + cell;
                        return false;
                    }
                }
            }
            reason = null;
            return true;
        }

        private static HashSet<IntVec3> LayoutFootprint(List<Tuple<ThingDef, IntVec3, Rot4>> plan)
        {
            HashSet<IntVec3> footprint = new HashSet<IntVec3>();
            foreach (Tuple<ThingDef, IntVec3, Rot4> entry in plan)
            {
                foreach (IntVec3 cell in GenAdj.OccupiedRect(entry.Item2, entry.Item3, entry.Item1.size))
                {
                    footprint.Add(cell);
                }
            }
            return footprint;
        }

        private static void ClearScenarioFootprint(Map map, HashSet<IntVec3> footprint)
        {
            List<Thing> things = footprint.SelectMany(cell => cell.GetThingList(map)).Distinct().ToList();
            foreach (Thing thing in things)
            {
                if (!thing.Destroyed && thing.Spawned
                    && (thing is Pawn
                        || thing.def.category == ThingCategory.Building
                        || thing.def.category == ThingCategory.Item
                        || thing.def.category == ThingCategory.Plant))
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static void BeginCellTargeting(string prompt, Action<IntVec3, Map> action)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => Find.CurrentMap != null && target.Cell.InBounds(Find.CurrentMap)
            };
            Find.Targeter.BeginTargeting(parameters, delegate(LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("No current map is available for the Nemesis scenario.",
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
                action(target.Cell, map);
            });
        }
    }
}
