using System;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    // On-demand dev tools only. Reports do not resolve/create pawns, roll selections, or issue jobs.
    public sealed partial class GameComponent_XMTWorldState
    {
        private static string When(int due) => due < 0 ? "unscheduled" : due + " (delta " + (due - Find.TickManager.TicksGame) + " ticks)";

        internal string CompanionGate(XMT_CompanionLink link)
        {
            if (!link.Active) return "ended or missing owner/definition";
            string invalid = XMT_PawnStateUtility.StructuralInvalidity(link.owner);
            if (invalid != null) return "invalid owner: " + invalid;
            if (link.companion?.Dead == true) return "companion died; condition will end";
            if (link.owner.Dead && (link.owner.Corpse == null || link.owner.Corpse.Destroyed)) return "owner corpse destroyed; condition will end";
            if (link.operationId != null) return Find.Maps.Any(m => m.GetComponent<MapComponent_XMTWorldOperations>().Contains(link.operationId))
                ? "deployment in progress" : "stale deployment claim; watcher will clear it";
            bool recovery = XMT_PawnStateUtility.NeedsCompanionRecovery(link);
            bool friendly = !link.owner.Dead && link.owner.Faction == Faction.OfPlayer && !recovery;
            Map map = link.Target?.MapHeld;
            if (link.companion?.Spawned == true)
            {
                if (link.companion.Map != map) return "companion busy on another map";
                if (!recovery && (!friendly || link.companion.Faction == Faction.OfPlayer)) return "already together";
            }
            if (!friendly && !recovery) return "no current player-held recovery/friendly arrival target; normal group attachment remains available";
            if (link.satisfied || link.dueTick < 0 || link.recovering != recovery) return "watcher will initialize " + (recovery ? "recovery" : "friendly reunion") + " delay";
            if (Find.TickManager.TicksGame < link.dueTick) return "waiting for response timer";
            if (link.definition.retryCap >= 0 && link.attempts >= link.definition.retryCap) return "retry cap reached";
            Caravan caravan = link.Target?.GetCaravan();
            if (caravan == null && !XMT_PawnStateUtility.PlayerHeldMap(map)) return "target has no eligible player-held map";
            if (link.companion == null) return "ready to resolve/retrieve/create companion, then validate deployment";
            string unavailable = XMT_PawnStateUtility.AvailabilityReason(link.companion, this, caravan == null ? map : null);
            if (unavailable != null) return "companion unavailable: " + unavailable;
            if (caravan != null) return link.caravanRefused
                ? "caravan refusal recorded; requires night (20:00-05:00) and an encounter-map-capable tile; hour=" + GenLocalDate.HourOfDay(caravan.Tile)
                : "ready to present caravan demand";
            return "ready to execute " + (friendly ? link.definition.friendlyResponse?.defName : link.definition.recoveryResponse?.defName);
        }

        internal string DebugCompanions(Pawn owner = null)
        {
            var report = new StringBuilder("[XMT][Companions] tick=" + Find.TickManager.TicksGame + "\n");
            var links = Companions.Where(c => owner == null || c.owner == owner).ToList();
            report.AppendLine("Matching relationships: " + links.Count);
            foreach (XMT_CompanionLink link in links)
            {
                report.AppendLine("\n" + link.definition?.defName + " owner=" + link.owner + " companion=" + link.companion);
                report.AppendLine("Decision: " + CompanionGate(link));
                report.AppendLine("priority=" + link.priority + " due=" + When(link.dueTick) + " attempts=" + link.attempts
                    + " recovering=" + link.recovering + " satisfied=" + link.satisfied + " ended=" + link.ended + " operation=" + link.operationId);
                report.AppendLine("target=" + link.Target + " map=" + link.Target?.MapHeld?.uniqueID + " caravan=" + link.Target?.GetCaravan()
                    + " prisoner=" + link.owner?.IsPrisoner + " slave=" + link.owner?.IsSlave);
                report.AppendLine("companion holder=" + link.companion?.ParentHolder + " lord=" + link.companion?.GetLord()?.LordJob?.GetType().Name
                    + " job=" + link.companion?.CurJob);
                if (link.companion?.GetLord()?.LordJob is LordJob_XMTRecovery mission) report.AppendLine(mission.DebugDescription);
            }
            foreach (XMT_TrackedPawn record in Pawns.Where(p => owner == null || p.pawn == owner || links.Any(l => l.companion == p.pawn)))
                report.AppendLine("Tracked " + record.pawn + " captor=" + record.captor + " reasons=" + string.Join(",", record.reasons));
            return report.ToString();
        }

        internal string DebugCompanionDefinitions()
        {
            var report = new StringBuilder("[XMT][Companions] Loaded definitions\n");
            foreach (XMT_CompanionDef definition in DefDatabase<XMT_CompanionDef>.AllDefsListForReading)
            {
                report.AppendLine(definition.defName + " role=" + definition.role + " source=" + definition.source?.GetType().Name
                    + " bio=" + (definition.source as XMT_CompanionSource_Bio)?.bio?.defName + " kind=" + definition.source?.pawnKind?.defName);
                report.AppendLine("  watcher=" + definition.watcher?.defName + " friendly=" + definition.friendlyResponse?.defName
                    + " recovery=" + definition.recoveryResponse?.defName + " workers=" + definition.friendlyResponse?.responseWorkerClass?.Name
                    + "/" + definition.recoveryResponse?.responseWorkerClass?.Name);
                report.AppendLine("  delays=" + definition.FriendlyDelay + "/" + definition.RecoveryDelay + " withdrawal=" + definition.retryAfterWithdrawalDays
                    + " retryCap=" + definition.retryCap + " dropPods=" + definition.allowDropPods + " support=" + definition.supportWorkerClass?.Name);
                foreach (string error in definition.ConfigErrors()) report.AppendLine("  CONFIG ERROR: " + error);
            }
            foreach (XMT_BackstoryCompanionDef association in DefDatabase<XMT_BackstoryCompanionDef>.AllDefsListForReading)
                report.AppendLine("Association " + association.backstory?.defName + " -> " + association.companion?.defName);
            return report.ToString();
        }

        internal void DebugMakeCompanionsDue(Pawn owner)
        {
            foreach (XMT_CompanionLink link in Companions.Where(l => l.owner == owner && l.Active))
            {
                link.recovering = XMT_PawnStateUtility.NeedsCompanionRecovery(link);
                link.satisfied = false;
                link.dueTick = Find.TickManager.TicksGame;
            }
        }

        internal string DebugReprisals()
        {
            Map target = ReprisalMap;
            var report = new StringBuilder("[XMT][Reprisal] budget=" + reprisalPoints + " nextCheck=" + When(nextReprisalTick)
                + " target=" + target?.uniqueID + " conquest=" + Current.Game.GetComponent<GameComponent_Xenomorph>().conquestStarted + "\n");
            if (target == null) report.AppendLine("BLOCKED: no explicit target, queen map, or player home map.");
            foreach (XMT_ReprisalDef definition in DefDatabase<XMT_ReprisalDef>.AllDefsListForReading)
            {
                float threshold = target == null ? 0f : StorytellerUtility.DefaultThreatPointsNow(target) * definition.minimumThreatFactor;
                report.AppendLine(definition.defName + " watcher=" + definition.watcher?.defName + " worker=" + definition.responseWorkerClass?.Name
                    + " weight=" + definition.selectionWeight + " threshold=" + threshold + " enoughBudget=" + (reprisalPoints >= threshold)
                    + " maxFactor=" + definition.maximumThreatFactor + " preparation=" + definition.preparationTicks);
            }
            report.AppendLine("Cached factions: " + reprisalFactions.Count);
            foreach (Faction faction in reprisalFactions)
                report.AppendLine("  " + faction + " eligible=" + (faction != null && !faction.defeated && faction.def.canStageAttacks)
                    + " tech=" + faction?.def.techLevel + " defeated=" + faction?.defeated + " hostile=" + faction?.HostileTo(Faction.OfPlayer));
            report.AppendLine("Native incident queue (includes other systems):\n" + Find.Storyteller.incidentQueue.DebugQueueReadout);
            return report.ToString();
        }

        internal void DebugReprisalDue() => nextReprisalTick = Find.TickManager.TicksGame;

        internal string DebugPlayerActions()
        {
            var report = new StringBuilder("[XMT][PlayerAction] Pending arrivals=" + arrivals.Count + "\n");
            foreach (XMT_PendingArrival request in arrivals)
            {
                string gate = request.map == null || !Find.Maps.Contains(request.map) ? "map missing"
                    : request.faction == null || request.faction.defeated ? "faction missing/defeated"
                    : request.response == null ? "response missing" : Find.TickManager.TicksGame > request.expiresTick ? "expired; will discard"
                    : Find.TickManager.TicksGame < request.dueTick ? "waiting" : request.faction.HostileTo(Faction.OfPlayer) ? "will transfer to reprisal" : "ready for arrival worker";
                report.AppendLine(request.response?.defName + " faction=" + request.faction + " map=" + request.map?.uniqueID
                    + " interest=" + request.interest + " due=" + When(request.dueTick) + " expires=" + When(request.expiresTick) + " decision=" + gate);
            }
            foreach (XMT_QueenDefenseState response in queenDefenses)
                report.AppendLine("Queen=" + response.queen + " response=" + response.definition?.defName + " watcher=" + response.definition?.watcher?.defName
                    + " active=" + response.active + " waves=" + response.wavesRemaining + " spawned=" + response.spawned
                    + " nextWave=" + When(response.nextWaveTick) + " cooldown=" + When(response.nextTick));
            foreach (XMT_WatcherSchedule schedule in schedules)
                report.AppendLine("Watcher=" + schedule.definition?.defName + " next=" + When(schedule.nextTick));
            report.AppendLine("Native incident queue (includes other systems):\n" + Find.Storyteller.incidentQueue.DebugQueueReadout);
            return report.ToString();
        }

        internal string DebugPreviewSiren(Pawn caster, XMT_SirenResponseDef definition)
        {
            var report = new StringBuilder("[XMT][PlayerAction] Siren preview " + definition.defName + " caster=" + caster + "\n");
            var candidates = SirenCandidates(definition, caster);
            report.AppendLine("Eligible reachable settlements=" + candidates.Count + "; at most " + definition.maximumSettlements
                + " distinct factions selected by interest weight. Preview does not roll selection or change diplomacy.");
            foreach (var candidate in candidates.OrderBy(c => c.distance))
                report.AppendLine(candidate.settlement.Label + " faction=" + candidate.settlement.Faction + " distance=" + candidate.distance.ToString("0.0")
                    + " interest=" + candidate.interest.ToString("0.000") + " travelTicks=" + (int)(candidate.distance * definition.caravanTicksPerTile));
            return report.ToString();
        }

        internal void DebugQueueArrival(Faction faction, Map map, XMT_SirenResponseDef definition)
        {
            if (arrivals.Any(a => a.faction == faction && a.map == map && a.response == definition)) return;
            int tick = Find.TickManager.TicksGame;
            arrivals.Add(new XMT_PendingArrival { faction = faction, map = map, response = definition,
                interest = 1f, dueTick = tick, expiresTick = tick + definition.maximumWaitTicks });
        }

        internal void DebugArrivalsDue()
        {
            int tick = Find.TickManager.TicksGame;
            foreach (XMT_PendingArrival request in arrivals.Where(a => a.response != null))
            { request.dueTick = tick; request.expiresTick = tick + request.response.maximumWaitTicks; }
        }
    }
}
