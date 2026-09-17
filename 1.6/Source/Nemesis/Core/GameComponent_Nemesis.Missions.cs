using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public sealed partial class GameComponent_Nemesis
    {
        private int nextMissionTick = -1;
        private NemesisMissionRequest pendingMission;
        private string pendingMissionReason;

        public NemesisMissionRequest PendingMission => pendingMission;
        public int NextMissionTick => nextMissionTick;
        public string PendingMissionReason => pendingMissionReason;
        private int MissionInterval => Mathf.Max(60000, Settings.missionIntervalTicks);
        public float CurrentMissionOpportunityChance
        {
            get
            {
                float xenoforming = Current.Game?.GetComponent<GameComponent_Xenomorph>()?.Xenoforming ?? 0f;
                float progress = Mathf.Clamp01(xenoforming / Mathf.Max(0.01f, Settings.activationXenoforming));
                float curvedProgress = Mathf.Pow(progress, Settings.missionOpportunityChanceExponent);
                return Mathf.Lerp(Settings.missionOpportunityChance, Settings.missionOpportunityChanceAtActivation, curvedProgress);
            }
        }

        private void ExposeMissions()
        {
            Scribe_Values.Look(ref nextMissionTick, "nextMissionTick", -1);
            Scribe_Deep.Look(ref pendingMission, "pendingMission");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingMission?.mission == null) pendingMission = null;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int tick = Find.TickManager.TicksGame;
            if (tick % 250 != 0) return;
            if (pendingMission != null) { RecheckPendingMission(); return; }
            if (AnyMissionLive()) return;
            if (nextMissionTick < 0) nextMissionTick = tick + MissionInterval;
            if (tick >= nextMissionTick) EvaluateMissionOpportunity();
        }

        internal static bool MapHasMission(Map map) => map?.lordManager?.lords.Any(l => l.LordJob is LordJob_NemesisMission mission
            && mission.BlocksNewMissions) == true;
        internal static bool AnyMissionLive() => Find.Maps.Any(MapHasMission);

        public float MissionWeight(NemesisMissionDef def, Map map, bool active)
        {
            if (AnyMissionLive()) return 0f;
            return MissionCandidateWeight(def, map, active);
        }

        private float MissionCandidateWeight(NemesisMissionDef def, Map map, bool active)
        {
            if (def == null || map == null || !def.StrategicRequirementsMet(this, active)) return 0f;
            NemesisMissionWorker worker = def.Worker;
            if (!worker.CanTarget(def, this, map) || worker.PartySize(def, this, map, active) < def.populationRange.min) return 0f;
            return Mathf.Max(0f, def.baseWeight * def.StanceWeight(this, active) * worker.Weight(def, this, map));
        }

        public void EvaluateMissionOpportunity(bool forceOpportunity = false, bool logEvaluation = false)
        {
            bool detailed = logEvaluation || XMTSettings.LogNemesis;
            if (detailed) NemesisLog.Detail("Selection", "Evaluate tick=" + Find.TickManager.TicksGame
                + " forcedOpportunity=" + forceOpportunity + " awakened=" + Awakened
                + " xenoforming=" + (Current.Game?.GetComponent<GameComponent_Xenomorph>()?.Xenoforming ?? 0f).ToString("0.###")
                + " opportunityChance=" + CurrentMissionOpportunityChance.ToString("0.###"));
            if (pendingMission != null || AnyMissionLive())
            {
                if (detailed) NemesisLog.Detail("Selection", "Evaluation skipped: mission is pending or live.");
                return;
            }
            nextMissionTick = Find.TickManager.TicksGame + MissionInterval;
            if (!forceOpportunity)
            {
                float opportunityRoll = Rand.Value;
                float opportunityChance = CurrentMissionOpportunityChance;
                if (opportunityRoll >= opportunityChance)
                {
                    if (detailed) NemesisLog.Detail("Selection", "No mission opportunity: roll="
                        + opportunityRoll.ToString("0.###") + " requiredBelow="
                        + opportunityChance.ToString("0.###") + " nextOpportunity=" + nextMissionTick);
                    return;
                }
                if (detailed) NemesisLog.Detail("Selection", "Mission opportunity passed: roll="
                    + opportunityRoll.ToString("0.###") + " requiredBelow="
                    + opportunityChance.ToString("0.###"));
            }
            if (Awakened) Evaluate(commit: true);

            List<(NemesisMissionDef def, Map map, float weight)> candidates = new List<(NemesisMissionDef, Map, float)>();
            foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
                foreach (NemesisMissionDef def in DefDatabase<NemesisMissionDef>.AllDefsListForReading)
                {
                    float weight = MissionWeight(def, map, Awakened);
                    if (detailed) NemesisLog.Detail("Selection", "candidate=" + def.defName + " map=" + map.uniqueID
                        + " weight=" + weight + " " + def.Worker.DescribeTarget(map) + " "
                        + def.Worker.DescribeSizing(def, this, map, Awakened));
                    if (weight > 0f) candidates.Add((def, map, weight));
                }
            if (candidates.Count == 0)
            {
                if (detailed) NemesisLog.Detail("Selection", "No eligible mission; nextOpportunity=" + nextMissionTick);
                return;
            }
            var selected = candidates.RandomElementByWeight(value => value.weight);
            pendingMission = new NemesisMissionRequest { mission = selected.def, mapId = selected.map.uniqueID,
                active = Awakened, selectedTick = Find.TickManager.TicksGame };
            RecheckPendingMission();
        }

        public void RecheckPendingMission()
        {
            if (pendingMission == null) return;
            if (AnyMissionLive()) { pendingMissionReason = "another mission is live"; return; }
            if (Find.TickManager.TicksGame < pendingMission.launchAfterTick)
            {
                pendingMissionReason = "waiting for follow-up deployment window";
                return;
            }
            if (pendingMission.active != Awakened || !pendingMission.mission.StrategicRequirementsMet(this, Awakened)
                )
            { CancelPendingMission(); return; }
            Map target = Find.Maps.FirstOrDefault(m => m.uniqueID == pendingMission.mapId && m.IsPlayerHome);
            if (target == null) { CancelPendingMission(); return; }
            if (!pendingMission.mission.Worker.CanTarget(pendingMission.mission, this, target))
            { CancelPendingMission(); return; }
            string previous = pendingMissionReason;
            if (NemesisMissionUtility.Launch(pendingMission, target, false, out pendingMissionReason))
            {
                NemesisLog.Detail("Selection", "Deployed " + pendingMission.mission.defName + " map=" + target.uniqueID);
                pendingMission = null;
                nextMissionTick = Find.TickManager.TicksGame + MissionInterval;
            }
            else if (previous != pendingMissionReason)
                NemesisLog.Detail("Selection", "Pending " + pendingMission.mission.defName + ": " + pendingMissionReason);
        }

        public bool ForceMission(NemesisMissionDef def, Map map, bool active, out string reason)
        {
            if (AnyMissionLive()) { reason = "another mission is already live"; return false; }
            bool launched = NemesisMissionUtility.Launch(new NemesisMissionRequest { mission = def, active = active,
                mapId = map.uniqueID, selectedTick = Find.TickManager.TicksGame }, map, true, out reason);
            if (launched) nextMissionTick = Find.TickManager.TicksGame + MissionInterval;
            return launched;
        }

        internal void NotifyMissionEnded(NemesisMissionDef completedMission, Map map, bool successful)
        {
            if (successful && TryQueueFollowUp(completedMission, map)) return;
            nextMissionTick = Find.TickManager.TicksGame + MissionInterval;
            NemesisLog.Detail("Selection", "Mission ended; nextOpportunity=" + nextMissionTick);
        }

        private bool TryQueueFollowUp(NemesisMissionDef completedMission, Map map)
        {
            if (completedMission?.followUpMissions.NullOrEmpty() != false || map == null) return false;
            float xenoforming = XenoformingUtility.GetXenoforming();
            if (xenoforming < Settings.followUpMinimumXenoforming) return false;
            if (!completedMission.Worker.TimingValid(completedMission, this, map,
                NemesisMissionTimingPhase.FollowUp, out string timingReason))
            {
                NemesisLog.Detail("Selection", "Follow-up rejected by prelude timing: " + timingReason);
                return false;
            }
            float chance = Mathf.Clamp01(Settings.followUpChanceByXenoforming.Evaluate(xenoforming));
            if (Rand.Value >= chance) return false;

            List<(NemesisMissionDef mission, float weight)> candidates = completedMission.followUpMissions
                .Where(entry => entry?.mission != null)
                .Select(entry => (entry.mission, entry.weight * MissionCandidateWeight(entry.mission, map, Awakened)))
                .Where(candidate => candidate.Item2 > 0f).ToList();
            if (candidates.Count == 0) return false;
            var selected = candidates.RandomElementByWeight(candidate => candidate.weight);
            int tick = Find.TickManager.TicksGame;
            pendingMission = new NemesisMissionRequest
            {
                mission = selected.mission,
                mapId = map.uniqueID,
                active = Awakened,
                selectedTick = tick,
                launchAfterTick = tick + Settings.followUpDelayTicks.RandomInRange,
                followUp = true
            };
            pendingMissionReason = "follow-up committed";
            NemesisLog.Detail("Selection", "Queued follow-up " + selected.mission.defName + " after "
                + completedMission.defName + "; chance=" + chance.ToString("0.###"));
            return true;
        }

        public void CancelPendingMission()
        {
            pendingMission = null;
            pendingMissionReason = null;
            nextMissionTick = Find.TickManager.TicksGame + MissionInterval;
        }
    }
}
