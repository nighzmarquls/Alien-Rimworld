using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed partial class GameComponent_Nemesis
    {
        internal void RegisterGeneticTrial(NemesisGeneticTrial trial, Map map, IEnumerable<Pawn> pawns)
        {
            if (trial == null || !ModsConfig.BiotechActive) return;
            trial.mapId = map.uniqueID;
            foreach (Pawn pawn in pawns.Where(EligibleGeneticMember))
                trial.members.Add(new NemesisGeneticMember { pawn = pawn, pawnId = pawn.ThingID,
                    genes = BioUtility.GetCanonicalPawnGenes(pawn), lastCell = pawn.Position });
            if (trial.members.Count == 0) return;
            NemesisStrain planned = strains.FirstOrDefault(value => value.id == trial.strainId);
            if (planned != null && NemesisStrain.Signature(planned.genes) != NemesisStrain.Signature(trial.members[0].genes)
                && trial.members.All(member => NemesisStrain.Signature(member.genes) == NemesisStrain.Signature(trial.members[0].genes)))
                trial.strainId = RememberStrain(trial.members[0].genes, planned.source, false, donor: planned).id;
            trial.mission.GeneEvaluator.Snapshot(trial, map);
            geneticTrials.Add(trial);
            NemesisLog.Detail("Genetics", "Trial=" + trial.id + " mission=" + trial.mission.defName + " probe=" + trial.probe
                + " strain=" + trial.strainId + " members=" + trial.members.Count);
        }

        private static bool StructurallyValid(Pawn pawn) => XMT_PawnStateUtility.StructurallyValid(pawn);
        internal static bool EligibleGeneticMember(Pawn pawn) => StructurallyValid(pawn) && pawn.ageTracker.Adult && XMTUtility.IsXenomorph(pawn)
            && !XMTUtility.IsQueen(pawn) && pawn.GetComp<CompLarvalGenes>() == null && pawn.GetComp<CompSubverterGenes>() == null;

        internal NemesisGeneticTrial Trial(int id) => geneticTrials.FirstOrDefault(value => value.id == id);
        private IEnumerable<NemesisGeneticTrial> TrialsFor(Pawn pawn) => geneticTrials.Where(trial => !trial.finalized
            && trial.members.Any(member => member.pawn == pawn || member.pawnId == pawn?.ThingID));

        internal void RecordGeneticKill(Pawn killer)
        {
            if (killer == null) return;
            foreach (NemesisGeneticTrial trial in TrialsFor(killer)) trial.members.First(member => member.pawnId == killer.ThingID).kills++;
        }
        internal void RecordGeneticInjury(Pawn pawn, float amount)
        {
            foreach (NemesisGeneticTrial trial in TrialsFor(pawn))
                trial.members.First(member => member.pawnId == pawn.ThingID).injury += Mathf.Max(0f, amount);
        }
        internal void RecordGeneticExtraction(Pawn pawn)
        {
            foreach (NemesisGeneticTrial trial in TrialsFor(pawn)) trial.members.First(member => member.pawnId == pawn.ThingID).extracted++;
        }
        internal void RecordGeneticLoss(int trialId, Pawn pawn, PawnLostCondition condition)
        {
            NemesisGeneticTrial trial = Trial(trialId);
            NemesisGeneticMember member = trial?.members.FirstOrDefault(value => value.pawnId == pawn?.ThingID);
            if (member == null) return;
            member.lastCell = pawn.PositionHeld;
            if (condition == PawnLostCondition.ExitedMap)
            {
                // ExitMap notifies the lord before PassToWorld. Keep identity through that transfer.
                KeepGeneticPawn(pawn);
            }
            else if (condition != PawnLostCondition.Incapped) member.terminal = true;
        }
        internal void ConfirmGeneticEscape(Pawn pawn)
        {
            if (!EligibleGeneticMember(pawn) || pawn.Dead || pawn.Downed || pawn.Spawned || pawn.IsPrisoner || pawn.IsSlave
                || pawn.Faction == Faction.OfPlayer || !Find.WorldPawns.Contains(pawn) || CaravanUtility.IsCaravanMember(pawn)) return;
            bool missionMember = false;
            if (XenoformingUtility.GetXenoforming() > 0f)
            {
                KeepGeneticPawn(pawn);
                RememberStrain(BioUtility.GetCanonicalPawnGenes(pawn), "escaped lineage " + pawn.ThingID);
            }
            foreach (NemesisGeneticTrial trial in TrialsFor(pawn))
            {
                NemesisGeneticMember member = trial.members.First(value => value.pawnId == pawn.ThingID);
                member.escaped = true; member.terminal = true; missionMember = true;
            }
            if (missionMember && HasCapability("XMT_NemesisCapability_InjuryRecovery") && !IsRecovering(pawn))
            {
                recoveringMorphs.Add(new NemesisRecoveryRecord { pawn = pawn, readyTick = Find.TickManager.TicksGame + 60000 });
                Current.Game.GetComponent<GameComponent_XMTWorldState>().Track(pawn, "nemesis-recovery");
                TendUtility.DoTend(null, pawn, null);
            }
        }

        internal bool IsRecovering(Pawn pawn) => recoveringMorphs.Any(value => value.pawn == pawn);
        private void KeepGeneticPawn(Pawn pawn) => Current.Game.GetComponent<GameComponent_XMTWorldState>().Track(pawn, "nemesis-genetics");
        internal bool TrialsForSacrifice(Pawn pawn) => pawn != null && geneticTrials.LastOrDefault(trial =>
            trial.members.Any(member => member.pawnId == pawn.ThingID))?.mission.expectedSacrifice == true;
        internal bool PendingGeneticPrelude => geneticTrials.Any(trial => trial.followUpPending && !trial.followUpReported && !trial.finalized);
        internal void QueueAblativeOutcome(int id)
        {
            NemesisGeneticTrial trial = Trial(id);
            if (trial == null || trial.operationEnded) return;
            EndGeneticOperation(id, false); trial.followUpPending = true;
        }
        internal bool ProtectGeneticPawn(Pawn pawn) => IsRecovering(pawn) || geneticTrials.Any(trial => !trial.finalized
            && trial.members.Any(member => member.pawn == pawn && !member.terminal));

        internal void EndGeneticOperation(int id, bool successful)
        {
            NemesisGeneticTrial trial = Trial(id);
            if (trial == null || trial.operationEnded) return;
            trial.operationEnded = true; trial.successful = successful;
            trial.settleAfterTick = Find.TickManager.TicksGame + 60;
        }

        internal void DebugTickGenetics() => TickGenetics();
        private void TickGenetics()
        {
            InitializeGeneticMigration();
            int tick = Find.TickManager.TicksGame;
            GameComponent_XMTWorldState state = Current.Game.GetComponent<GameComponent_XMTWorldState>();
            foreach (XMT_TrackedPawn record in state.Pawns.Where(r => r.reasons.Contains("nemesis-genetics")).ToList())
                if (!StructurallyValid(record.pawn) || record.pawn.Dead || record.pawn.Spawned)
                    state.Release(record.pawn, "nemesis-genetics");
            foreach (NemesisRecoveryRecord record in recoveringMorphs.ToList())
            {
                Pawn pawn = record.pawn;
                if (!StructurallyValid(pawn) || pawn.Dead || pawn.Spawned || !Find.WorldPawns.Contains(pawn)
                    || CaravanUtility.IsCaravanMember(pawn))
                {
                    state.Release(pawn, "nemesis-recovery"); recoveringMorphs.Remove(record); continue;
                }
                if (tick < record.readyTick) continue;
                foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                    if (injury.TryGetComp<HediffComp_GetsPermanent>()?.IsPermanent != true) injury.Heal(injury.Severity);
                if (HasCapability("XMT_NemesisCapability_LimbRecovery"))
                    foreach (Hediff_MissingPart part in pawn.health.hediffSet.GetMissingPartsCommonAncestors().ToList())
                        pawn.health.RestorePart(part.Part);
                state.Release(pawn, "nemesis-recovery"); recoveringMorphs.Remove(record);
                NemesisLog.Detail("Genetics", "Recovered=" + pawn.ThingID);
            }
            foreach (NemesisGeneticTrial trial in geneticTrials.Where(value => !value.finalized).ToList())
            {
                Map map = Find.Maps.FirstOrDefault(value => value.uniqueID == trial.mapId);
                if (map == null) { trial.finalized = true; continue; }
                foreach (NemesisGeneticMember member in trial.members)
                {
                    Pawn pawn = member.pawn;
                    if (!StructurallyValid(pawn) || pawn.Dead || pawn.IsPrisoner || pawn.IsSlave || pawn.Faction == Faction.OfPlayer)
                        member.terminal = true;
                    else if (pawn.Spawned) member.lastCell = pawn.Position;
                }
                if (!trial.operationEnded || tick < trial.settleAfterTick) continue;
                if (trial.followUpPending && !trial.followUpReported)
                {
                    trial.successful = trial.mission.GeneEvaluator.Evaluate(trial, map) >= 0.2f;
                    trial.followUpReported = true;
                    NotifyMissionEnded(trial.mission, map, trial.successful);
                }
                // Downed members cannot escape during the completed mission; withdrawal still gets time to finish.
                if (trial.members.Any(member => !member.terminal && member.pawn?.Downed != true)
                    && tick - trial.startedTick < 180000) continue;
                FinalizeGeneticTrial(trial, map);
            }
            geneticTrials.RemoveAll(value => value.finalized && tick - value.startedTick > 1200000
                && geneticTrials.Any(other => other != value && other.mission == value.mission && other.finalized && !other.probe && other.startedTick > value.startedTick));
            HashSet<string> retained = new HashSet<string>(geneticTrials.Select(value => value.strainId));
            List<NemesisStrain> disposable = strains.Where(value => !value.acquired && !retained.Contains(value.id)).ToList();
            foreach (NemesisStrain strain in disposable.Take(Math.Max(0, strains.Count(value => !value.acquired) - 64))) strains.Remove(strain);
        }

        private void FinalizeGeneticTrial(NemesisGeneticTrial trial, Map map)
        {
            if (trial.finalized) return;
            trial.genericScore = NemesisGeneEvaluationWorker.GenericFitness(trial);
            trial.specialistScore = Mathf.Clamp01(trial.mission.GeneEvaluator.Evaluate(trial, map));
            trial.finalized = true;
            if (trial.cycle != geneticCycle) return;
            foreach (GeneDef gene in trial.members.SelectMany(member => member.genes).Distinct())
            {
                Fitness(gene, null).Observe(trial.genericScore);
                if (trial.mission.specialGeneConsideration) Fitness(gene, trial.mission).Observe(trial.specialistScore);
            }
            if (trial.probe)
            {
                NemesisMissionDef profile = trial.mission.specialGeneConsideration ? trial.mission : null;
                float score = profile == null ? trial.genericScore : trial.specialistScore;
                float delta = score - trial.baseline;
                foreach (GeneDef gene in new[] { trial.added, trial.removed }.Where(value => value != null))
                {
                    NemesisGeneFitness fitness = Fitness(gene, profile);
                    float directionalDelta = gene == trial.removed ? -delta : delta;
                    fitness.Observe(Mathf.Clamp01(0.5f + directionalDelta));
                    if (delta < -0.05f)
                    {
                        fitness.failedProbes++;
                        fitness.retryAfterTick = Find.TickManager.TicksGame + Math.Min(600000, 60000 * fitness.failedProbes);
                    }
                    else if (delta > 0.05f) fitness.failedProbes = Math.Max(0, fitness.failedProbes - 1);
                }
                if (delta < -0.05f && trial.genericScore <= trial.baselineGeneric && !geneticTrials.Any(other => other != trial
                    && other.strainId == trial.strainId && (!other.finalized || other.successful || other.genericScore > trial.genericScore)))
                    strains.RemoveAll(strain => strain.id == trial.strainId && !strain.acquired);
            }
            NemesisLog.Detail("Genetics", "Finalized=" + trial.id + " generic=" + trial.genericScore.ToString("0.###")
                + " specialist=" + trial.specialistScore.ToString("0.###"));
            RefreshQueenExpression();
        }
    }
}
