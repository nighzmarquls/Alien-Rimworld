using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public sealed partial class GameComponent_Nemesis : GameComponent
    {
        private const string SettingsDefName = "XMT_NemesisSettings";
        private static readonly NemesisSettingsDef FallbackSettings = new NemesisSettingsDef { defName = SettingsDefName };

        private bool awakened;
        private int awakenedTick = -1;
        private int evidenceRevision;
        private int intelligenceRevision;
        private int assessmentRevision;
        private int lastAssessmentTick = -1;
        private NemesisStanceDef currentStance;
        private int stanceCommittedTick = -1;
        private List<NemesisEvidenceAggregate> evidence = new List<NemesisEvidenceAggregate>();
        private List<NemesisMapEvidenceAggregate> mapEvidence = new List<NemesisMapEvidenceAggregate>();
        private List<NemesisEvidenceEvent> recentEvidence = new List<NemesisEvidenceEvent>();
        private List<NemesisObservationRecord> observations = new List<NemesisObservationRecord>();
        private List<NemesisSpatialContact> spatialContacts = new List<NemesisSpatialContact>();
        private List<NemesisXenotypeRecord> xenotypeKnowledge = new List<NemesisXenotypeRecord>();

        private NemesisAssessment lastAssessment;
        private NemesisCensusReport lastCensus;

        public static NemesisSettingsDef Settings => DefDatabase<NemesisSettingsDef>.GetNamedSilentFail(SettingsDefName) ?? FallbackSettings;
        public bool Awakened => awakened;
        public int AwakenedTick => awakenedTick;
        public int EvidenceRevision => evidenceRevision;
        public int IntelligenceRevision => intelligenceRevision;
        public int AssessmentRevision => assessmentRevision;
        public NemesisStanceDef CurrentStance => currentStance;
        public int StanceCommittedTick => stanceCommittedTick;
        public IReadOnlyList<NemesisEvidenceEvent> RecentEvidence => recentEvidence;
        public IReadOnlyList<NemesisObservationRecord> Observations => observations;
        public IReadOnlyList<NemesisEvidenceAggregate> Evidence => evidence;
        public IReadOnlyList<NemesisMapEvidenceAggregate> MapEvidence => mapEvidence;
        public IReadOnlyList<NemesisSpatialContact> SpatialContacts => spatialContacts;
        public IReadOnlyList<NemesisXenotypeRecord> XenotypeKnowledge => xenotypeKnowledge;
        public IEnumerable<NemesisXenotypeRecord> AvailableXenotypes => xenotypeKnowledge.Where(record => record.AvailableAt(Find.TickManager?.TicksGame ?? 0));
        public NemesisAssessment LastAssessment => lastAssessment;
        public NemesisCensusReport LastCensus => lastCensus;

        public GameComponent_Nemesis(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            ExposeMissions();
            Scribe_Values.Look(ref awakened, "awakened", false);
            Scribe_Values.Look(ref awakenedTick, "awakenedTick", -1);
            Scribe_Values.Look(ref evidenceRevision, "evidenceRevision", 0);
            Scribe_Values.Look(ref intelligenceRevision, "intelligenceRevision", 0);
            Scribe_Values.Look(ref assessmentRevision, "assessmentRevision", 0);
            Scribe_Values.Look(ref lastAssessmentTick, "lastAssessmentTick", -1);
            Scribe_Defs.Look(ref currentStance, "currentStance");
            Scribe_Values.Look(ref stanceCommittedTick, "stanceCommittedTick", -1);
            Scribe_Collections.Look(ref evidence, "evidence", LookMode.Deep);
            Scribe_Collections.Look(ref mapEvidence, "mapEvidence", LookMode.Deep);
            Scribe_Collections.Look(ref recentEvidence, "recentEvidence", LookMode.Deep);
            Scribe_Collections.Look(ref observations, "observations", LookMode.Deep);
            Scribe_Collections.Look(ref spatialContacts, "spatialContacts", LookMode.Deep);
            Scribe_Collections.Look(ref xenotypeKnowledge, "xenotypeKnowledge", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                evidence ??= new List<NemesisEvidenceAggregate>();
                mapEvidence ??= new List<NemesisMapEvidenceAggregate>();
                recentEvidence ??= new List<NemesisEvidenceEvent>();
                observations ??= new List<NemesisObservationRecord>();
                spatialContacts ??= new List<NemesisSpatialContact>();
                xenotypeKnowledge ??= new List<NemesisXenotypeRecord>();
                evidence.RemoveAll(value => value?.evidence == null);
                mapEvidence.RemoveAll(value => value?.evidence == null || value.mapId < 0);
                recentEvidence.RemoveAll(value => value?.evidence == null);
                observations.RemoveAll(value => value?.observation == null);
                spatialContacts.RemoveAll(value => value?.observation == null || value.subjectId.NullOrEmpty());
                xenotypeKnowledge.RemoveAll(value => value == null || value.signature.NullOrEmpty());
                NormalizeXenotypeKnowledge();
            }
        }

        private void NormalizeXenotypeKnowledge()
        {
            List<NemesisXenotypeRecord> normalized = new List<NemesisXenotypeRecord>();
            foreach (NemesisXenotypeRecord record in xenotypeKnowledge)
            {
                if (record.xenotype != null)
                {
                    record.signature = "def:" + record.xenotype.defName;
                }
                NemesisXenotypeRecord existing = normalized.FirstOrDefault(value => value.signature == record.signature);
                if (existing == null)
                {
                    normalized.Add(record);
                    continue;
                }

                existing.sampleCount = Mathf.Max(existing.sampleCount, record.sampleCount);
                existing.abductedCount += Mathf.Max(0, record.abductedCount);
                existing.lastSampledTick = Mathf.Max(existing.lastSampledTick, record.lastSampledTick);
                if (record.availableTick < existing.availableTick)
                {
                    existing.availableTick = record.availableTick;
                    existing.acquiredTick = record.acquiredTick;
                    existing.origin = record.origin;
                }
            }
            xenotypeKnowledge = normalized;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            float xenoforming = Current.Game?.GetComponent<GameComponent_Xenomorph>()?.Xenoforming ?? 0f;
            if (!awakened && xenoforming >= Settings.activationXenoforming)
            {
                Awaken("existing save above activation threshold", runCensus: true);
            }
        }

        public void NotifyXenoformingChanged(float previous, float current)
        {
            if (!awakened && previous < Settings.activationXenoforming && current >= Settings.activationXenoforming)
            {
                Awaken("xenoforming activation threshold crossed", runCensus: true);
            }

            if (!awakened || Mathf.Approximately(previous, current))
            {
                return;
            }

            NemesisObservationDef observation = DefDatabase<NemesisObservationDef>.GetNamedSilentFail("XMT_NemesisObs_Xenoforming");
            SetImmediateObservation(observation, current, 1f, "xenoforming change");
        }

        public bool Awaken(string reason, bool runCensus)
        {
            if (awakened)
            {
                return false;
            }

            awakened = true;
            awakenedTick = Find.TickManager?.TicksGame ?? 0;

            if (runCensus)
            {
                RequestIntelligence(reason, Settings.initialCensusConfidence);
                Evaluate(commit: true);
            }
            return true;
        }

        public NemesisCensusReport RequestIntelligence(string reason, float? confidenceOverride = null,
            IEnumerable<NemesisTraversalCategory> requestedCategories = null)
        {
            lastCensus = NemesisCensus.Run(reason, requestedCategories);
            foreach (NemesisObservationResult result in lastCensus.observations)
            {
                float storedConfidence = confidenceOverride ?? result.confidence;
                StoreObservation(result.observation, result.value, storedConfidence,
                    result.coverage, lastCensus.tick, reason);
                ReplaceSpatialContacts(result.observation, result.contacts, storedConfidence);
            }
            foreach (IGrouping<string, NemesisXenotypeRecord> group in lastCensus.xenotypeSamples.GroupBy(sample => sample.signature))
            {
                NemesisXenotypeRecord sample = group.First();
                sample.sampleCount = group.Count();
                RememberXenotype(sample);
            }
            intelligenceRevision++;
            NemesisLog.Detail("Intelligence", "Census reason=" + reason
                + " tick=" + lastCensus.tick + " maps=" + lastCensus.mapCount + " observations=" + lastCensus.observations.Count
                + " spatialContacts=" + spatialContacts.Count + " revision=" + intelligenceRevision);
            return lastCensus;
        }

        private void ReplaceSpatialContacts(NemesisObservationDef observation, IEnumerable<NemesisSpatialContact> replacements,
            float storedConfidence)
        {
            if (observation?.recordsSpatialContacts != true)
            {
                return;
            }

            spatialContacts.RemoveAll(contact => contact.observation == observation);
            if (replacements != null)
            {
                List<NemesisSpatialContact> candidates = replacements.Where(contact => contact != null).ToList();
                int limit = Mathf.Max(1, Settings.maxSpatialContactsPerObservation);
                float step = candidates.Count > limit ? candidates.Count / (float)limit : 1f;
                int count = Mathf.Min(candidates.Count, limit);
                for (int i = 0; i < count; i++)
                {
                    NemesisSpatialContact contact = candidates[Mathf.Min(candidates.Count - 1, Mathf.FloorToInt(i * step))];
                    contact.confidence = Mathf.Clamp01(storedConfidence);
                    spatialContacts.Add(contact);
                }
            }
        }

        public bool SetImmediateObservation(NemesisObservationDef def, float value, float confidence, string source, float coverage = 1f)
        {
            if (!awakened || def == null
                || (def.updateMode != NemesisObservationUpdateMode.EventImmediate && def.updateMode != NemesisObservationUpdateMode.Hybrid))
            {
                return false;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            StoreObservation(def, value, confidence, coverage, tick, source);
            intelligenceRevision++;
            return true;
        }

        public void AdjustImmediateObservation(NemesisObservationDef def, float delta, float confidence, string source, float coverage = 1f)
        {
            float current = GetObservation(def)?.value ?? 0f;
            SetImmediateObservation(def, Mathf.Max(0f, current + delta), confidence, source, coverage);
        }

        private void StoreObservation(NemesisObservationDef def, float value, float confidence, float coverage, int tick, string source)
        {
            if (def == null)
            {
                return;
            }

            NemesisObservationRecord record = GetObservation(def);
            if (record == null)
            {
                record = new NemesisObservationRecord { observation = def };
                observations.Add(record);
            }

            record.value = value;
            record.observedTick = tick;
            record.confidence = Mathf.Clamp01(confidence);
            record.coverage = Mathf.Clamp01(coverage);
            record.source = source;
        }

        public void QueueAbductedXenotype(Pawn pawn)
        {
            if (!awakened)
            {
                return;
            }
            int tick = Find.TickManager?.TicksGame ?? 0;
            int delay = Mathf.RoundToInt(Settings.abductedXenotypeProcessingDays * 60000f);
            NemesisXenotypeRecord record = NemesisXenotypeRecord.FromPawn(pawn, "abducted pawn", tick, tick + Mathf.Max(0, delay));
            if (record != null)
            {
                record.abductedCount = 1;
                record.sampleCount = 0;
                RememberXenotype(record);
                intelligenceRevision++;
            }
        }

        private void RememberXenotype(NemesisXenotypeRecord candidate)
        {
            if (candidate == null || candidate.signature.NullOrEmpty())
            {
                return;
            }

            NemesisXenotypeRecord existing = xenotypeKnowledge.FirstOrDefault(record => record.signature == candidate.signature);
            if (existing == null)
            {
                xenotypeKnowledge.Add(candidate);
                return;
            }

            if (candidate.lastSampledTick >= existing.lastSampledTick && candidate.sampleCount > 0)
            {
                existing.sampleCount = candidate.sampleCount;
            }
            existing.abductedCount += Mathf.Max(0, candidate.abductedCount);
            existing.lastSampledTick = Mathf.Max(existing.lastSampledTick, candidate.lastSampledTick);

            if (candidate.availableTick < existing.availableTick)
            {
                existing.availableTick = candidate.availableTick;
                existing.origin = candidate.origin;
                existing.acquiredTick = candidate.acquiredTick;
            }
        }

        public bool RecordEvidence(NemesisEvidenceDef def, float amount, Thing actor, Thing subject, Def source, string detail = null)
        {
            if (!awakened || def == null || amount <= 0f)
            {
                return false;
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            NemesisEvidenceAggregate aggregate = evidence.FirstOrDefault(value => value.evidence == def);
            if (aggregate == null)
            {
                aggregate = new NemesisEvidenceAggregate { evidence = def };
                evidence.Add(aggregate);
            }
            aggregate.Add(amount, tick);

            recentEvidence.Add(new NemesisEvidenceEvent
            {
                evidence = def,
                tick = tick,
                amount = amount,
                actorId = actor?.GetUniqueLoadID(),
                subjectId = subject?.GetUniqueLoadID(),
                sourceDefName = source?.defName,
                detail = detail
            });
            int excess = recentEvidence.Count - Mathf.Max(1, Settings.recentEvidenceCapacity);
            if (excess > 0)
            {
                recentEvidence.RemoveRange(0, excess);
            }
            evidenceRevision++;
            NemesisLog.Detail("Evidence", "Accepted=" + def.defName + " amount=" + amount
                + " actor=" + actor?.GetUniqueLoadID() + " subject=" + subject?.GetUniqueLoadID()
                + " source=" + source?.defName + " detail=" + detail + " tick=" + tick);
            return true;
        }

        public bool RecordMapEvidence(NemesisEvidenceDef def, float amount, Map map, Def source, string detail = null)
        {
            if (def == null || amount <= 0f || map == null) return false;
            int tick = Find.TickManager?.TicksGame ?? 0;
            NemesisMapEvidenceAggregate aggregate = mapEvidence.FirstOrDefault(value => value.evidence == def && value.mapId == map.uniqueID);
            if (aggregate == null)
            {
                aggregate = new NemesisMapEvidenceAggregate { evidence = def, mapId = map.uniqueID };
                mapEvidence.Add(aggregate);
            }
            aggregate.mapLabel = map.Parent?.LabelCap;
            aggregate.Add(amount, tick);
            evidenceRevision++;
            NemesisLog.Detail("Evidence", "Accepted map evidence=" + def.defName + " amount=" + amount
                + " map=" + aggregate.mapLabel + "#" + aggregate.mapId + " source=" + source?.defName
                + " detail=" + detail + " tick=" + tick);
            return true;
        }

        public float MapEvidenceValue(NemesisEvidenceDef def, Map map)
        {
            if (def == null || map == null) return 0f;
            NemesisMapEvidenceAggregate aggregate = mapEvidence.FirstOrDefault(value => value.evidence == def && value.mapId == map.uniqueID);
            return aggregate?.ValueAt(Find.TickManager?.TicksGame ?? 0) ?? 0f;
        }

        public void ResetIntelligence()
        {
            observations.Clear();
            spatialContacts.Clear();
            xenotypeKnowledge.Clear();
            lastCensus = null;
            lastAssessment = null;
            intelligenceRevision++;
        }

        public void ResetAllState()
        {
            CancelPendingMission();
            awakened = false;
            awakenedTick = -1;
            evidence.Clear();
            mapEvidence.Clear();
            recentEvidence.Clear();
            observations.Clear();
            spatialContacts.Clear();
            xenotypeKnowledge.Clear();
            currentStance = null;
            stanceCommittedTick = -1;
            lastAssessmentTick = -1;
            lastAssessment = null;
            lastCensus = null;
            evidenceRevision = 0;
            intelligenceRevision = 0;
            assessmentRevision = 0;
        }

        public void ExpireCommitmentForDiagnostics()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            stanceCommittedTick = tick - Mathf.RoundToInt(Settings.stanceCommitmentDays * 60000f) - 1;
        }

        public bool NotifyConfirmedQueenDeath(Pawn queen)
        {
            if (!awakened || queen == null || queen.Faction == Faction.OfPlayer
                || queen.GetComp<CompQueen>() == null)
            {
                return false;
            }

            float xenoforming = Current.Game?.GetComponent<GameComponent_Xenomorph>()?.Xenoforming ?? 0f;
            if (xenoforming >= Settings.activationXenoforming)
            {
                return false;
            }

            awakened = false;
            stanceCommittedTick = -1;
            lastAssessment = null;
            return true;
        }

        public float EvidenceValue(NemesisEvidenceDef def, int tick)
        {
            return evidence.FirstOrDefault(value => value.evidence == def)?.ValueAt(tick) ?? 0f;
        }

        public NemesisObservationRecord GetObservation(NemesisObservationDef def)
        {
            return observations.FirstOrDefault(value => value.observation == def);
        }

        public NemesisAssessment Evaluate(bool commit)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            NemesisAssessment assessment = new NemesisAssessment { tick = tick };
            Dictionary<NemesisSignalDef, NemesisSignalResult> signalLookup = new Dictionary<NemesisSignalDef, NemesisSignalResult>();

            foreach (NemesisSignalDef def in DefDatabase<NemesisSignalDef>.AllDefsListForReading.OrderBy(value => value.defName))
            {
                NemesisSignalResult result = NemesisSignalEvaluator.Evaluate(def, this, tick);
                assessment.signals.Add(result);
                signalLookup[def] = result;
            }

            foreach (NemesisStanceDef def in DefDatabase<NemesisStanceDef>.AllDefsListForReading.OrderBy(value => value.defName))
            {
                assessment.stances.Add(NemesisStanceEvaluator.Evaluate(def, signalLookup));
            }

            assessment.stances.Sort((left, right) => right.score.CompareTo(left.score));
            assessment.recommended = assessment.stances.FirstOrDefault(value => value.eligible)
                ?? assessment.stances.FirstOrDefault(value => value.stance.isFallback);

            if (commit && awakened && assessment.recommended != null)
            {
                ApplyCommitment(assessment, tick);
                lastAssessment = assessment;
                lastAssessmentTick = tick;
                assessmentRevision++;
            }
            else
            {
                assessment.commitmentExplanation = commit && !awakened
                    ? "Commit rejected because the Nemesis system is dormant."
                    : "Preview only; strategic state was not changed.";
            }
            NemesisLog.Detail("Stance", "Evaluation tick=" + tick + " commit=" + commit
                + " recommended=" + assessment.recommended?.stance?.defName + " current=" + currentStance?.defName
                + " reason=" + assessment.commitmentExplanation);
            return assessment;
        }

        private void ApplyCommitment(NemesisAssessment assessment, int tick)
        {
            if (currentStance == null)
            {
                currentStance = assessment.recommended.stance;
                stanceCommittedTick = tick;
                assessment.commitmentExplanation = "Committed initial stance " + currentStance.defName + ".";
                return;
            }

            if (assessment.recommended.stance == currentStance)
            {
                assessment.commitmentExplanation = "Retained " + currentStance.defName + " because it remains the highest-ranked eligible stance.";
                return;
            }

            int minimumTicks = Mathf.RoundToInt(Settings.stanceCommitmentDays * 60000f);
            if (stanceCommittedTick >= 0 && tick - stanceCommittedTick < minimumTicks)
            {
                assessment.commitmentExplanation = "Retained " + currentStance.defName + " during its commitment window.";
                return;
            }

            float currentScore = assessment.stances.FirstOrDefault(value => value.stance == currentStance)?.score ?? 0f;
            if (assessment.recommended.score < currentScore + Settings.challengerMargin)
            {
                assessment.commitmentExplanation = "Retained " + currentStance.defName + "; challenger did not clear the replacement margin.";
                return;
            }

            currentStance = assessment.recommended.stance;
            stanceCommittedTick = tick;
            assessment.commitmentExplanation = "Changed committed stance to " + currentStance.defName + ".";
        }
    }
}
