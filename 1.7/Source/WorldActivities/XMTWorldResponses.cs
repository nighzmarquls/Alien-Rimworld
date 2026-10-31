using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class XMT_SirenResponseDef : XMT_ResponseDef
    {
        public XMT_SirenResponseDef() { responseWorkerClass = typeof(XMT_SirenResponseWorker); }
        public int maximumSettlements = 4;
        public float distanceFalloff = 30f;
        public int caravanTicksPerTile = 1800;
        public IntRange preparationTicks = new IntRange(15000, 30000);
        public float hostilePointsFactor = 1f;
        public int retryTicks = 2500;
        public int maximumWaitTicks = 120000;
    }
    public class XMT_ReprisalDef : XMT_ResponseDef
    {
        public float minimumThreatFactor = 1f;
        public float maximumThreatFactor = 4f;
        public float successiveWaveFactor = 0.75f;
        public float breachingXenoforming = 50f;
        public IntRange preparationTicks = new IntRange(30000, 60000);
        public XMT_ReprisalDef() { responseWorkerClass = typeof(XMT_ReprisalWorker); }
    }
    public class XMT_QueenDefenseDef : XMT_ResponseDef
    {
        public XMT_QueenDefenseDef() { responseWorkerClass = typeof(XMT_QueenDefenseWorker); }
        public float minimumXenoforming = 10f;
        public float costPerPawn = 0.35f;
        public int cooldownTicks = 60000;
        public int waveIntervalTicks = 1200;
        public int maximumWaves = 5;
        public int maximumActivePawns = 40;
        public int maximumPawnsPerWave = 8;
        public float threatPawnFactor = 0.75f;
    }
    public sealed class XMT_QueenDefenseState : IExposable
    {
        public Pawn queen;
        public XMT_QueenDefenseDef definition;
        public int nextTick = -1, nextWaveTick = -1, wavesRemaining, spawned;
        public bool letterSent, active;
        public QueenAidThreatProfile threats;
        public Faction faction;
        public void ExposeData()
        {
            Scribe_References.Look(ref queen, "queen"); Scribe_Defs.Look(ref definition, "definition");
            Scribe_Values.Look(ref nextTick, "nextTick", -1); Scribe_Values.Look(ref nextWaveTick, "nextWaveTick", -1);
            Scribe_Values.Look(ref wavesRemaining, "wavesRemaining"); Scribe_Values.Look(ref spawned, "spawned");
            Scribe_Values.Look(ref letterSent, "letterSent"); Scribe_Values.Look(ref active, "active");
            Scribe_Deep.Look(ref threats, "threats"); Scribe_References.Look(ref faction, "faction");
        }
    }
    public class XMT_QueenDefenseWorker : XMT_ResponseWorker
    {
        public virtual bool TryStart(Pawn queen, Pawn aggressor, XMT_QueenDefenseDef definition) =>
            Current.Game.GetComponent<GameComponent_Xenomorph>().StartQueenDefense(queen, aggressor, definition);
        public virtual void Tick(XMT_QueenDefenseState response) =>
            Current.Game.GetComponent<GameComponent_Xenomorph>().TickQueenDefense(response);
    }
    public sealed class XMT_PendingArrival : IExposable
    {
        public Map map;
        public Faction faction;
        public XMT_SirenResponseDef response;
        public int dueTick;
        public int expiresTick;
        public float interest;
        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map"); Scribe_References.Look(ref faction, "faction"); Scribe_Defs.Look(ref response, "response");
            Scribe_Values.Look(ref dueTick, "dueTick"); Scribe_Values.Look(ref expiresTick, "expiresTick");
            Scribe_Values.Look(ref interest, "interest");
        }
    }
    public sealed partial class GameComponent_XMTWorldState
    {
        private List<XMT_PendingArrival> arrivals = new List<XMT_PendingArrival>();
        private List<Faction> reprisalFactions = new List<Faction>();
        private float reprisalPoints;
        private int nextReprisalTick;
        private Map reprisalTarget;
        private List<XMT_QueenDefenseState> queenDefenses = new List<XMT_QueenDefenseState>();
        public IReadOnlyList<XMT_QueenDefenseState> QueenDefenses => queenDefenses;
        public XMT_QueenDefenseState QueenDefense(Pawn queen, XMT_QueenDefenseDef definition = null)
        {
            queenDefenses ??= new List<XMT_QueenDefenseState>();
            XMT_QueenDefenseState response = queenDefenses.FirstOrDefault(r => r.queen == queen);
            if (response == null) { response = new XMT_QueenDefenseState { queen = queen }; queenDefenses.Add(response); }
            response.definition = definition ?? response.definition ?? DefDatabase<XMT_QueenDefenseDef>.GetNamed(
                queen.Faction == Faction.OfPlayer ? "XMT_QueenDefense_Default" : "XMT_QueenDefense_NPC");
            return response;
        }
        internal void TickQueenDefenses(XMT_WatcherDef watcher)
        {
            foreach (XMT_QueenDefenseState response in queenDefenses.ToList())
            {
                if (response.queen == null || response.queen.Destroyed || response.queen.Dead) { queenDefenses.Remove(response); continue; }
                if (response.active && response.definition.watcher == watcher)
                    ((XMT_QueenDefenseWorker)response.definition.ResponseWorker).Tick(response);
            }
        }
        private void ExposeBroadcasts()
        {
            Scribe_Collections.Look(ref arrivals, "XMT_pendingArrivals", LookMode.Deep);
            Scribe_Collections.Look(ref reprisalFactions, "XMT_reprisalFactions", LookMode.Reference);
            Scribe_Values.Look(ref reprisalPoints, "XMT_reprisalPoints");
            Scribe_Values.Look(ref nextReprisalTick, "XMT_nextReprisalTick");
            Scribe_References.Look(ref reprisalTarget, "XMT_reprisalTarget");
            Scribe_Collections.Look(ref queenDefenses, "XMT_queenDefenses", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { arrivals ??= new List<XMT_PendingArrival>(); reprisalFactions ??= new List<Faction>(); queenDefenses ??= new List<XMT_QueenDefenseState>(); }
        }
        public void AccumulateReprisal(Faction faction, float points, Map target = null)
        {
            if (faction == null || faction.defeated || faction.temporary || points <= 0f) return;
            if (!reprisalFactions.Contains(faction)) reprisalFactions.Add(faction);
            reprisalPoints += points;
            if (target != null) reprisalTarget = target;
        }
        internal void ClearReprisals() { reprisalFactions.Clear(); reprisalPoints = 0f; reprisalTarget = null; }
        public void ImportReprisal(List<Faction> factions, float points)
        {
            reprisalFactions ??= new List<Faction>();
            foreach (Faction faction in factions ?? new List<Faction>())
                if (faction != null && !reprisalFactions.Contains(faction)) reprisalFactions.Add(faction);
            reprisalPoints += Mathf.Max(0f, points);
        }
        public void RecordSiren(Pawn caster, List<PreceptDef> friendly, List<PreceptDef> hostile, int friendlyGoodwill, int hostileGoodwill)
        {
            if (caster?.Faction != Faction.OfPlayer || caster.Map == null) return;
            List<XMT_SirenResponseDef> choices = DefDatabase<XMT_SirenResponseDef>.AllDefsListForReading.Where(d => d.selectionWeight > 0f).ToList();
            if (choices.Count == 0) return;
            XMT_SirenResponseDef definition = choices.RandomElementByWeight(d => d.selectionWeight);
            ((XMT_SirenResponseWorker)definition.ResponseWorker).Notify(this, definition, caster, friendly, hostile, friendlyGoodwill, hostileGoodwill);
        }
        public void RecordSiren(XMT_SirenResponseDef definition, Pawn caster, List<PreceptDef> friendly, List<PreceptDef> hostile, int friendlyGoodwill, int hostileGoodwill)
        {
            var candidates = SirenCandidates(definition, caster);
            HashSet<Faction> selectedFactions = new HashSet<Faction>();
            int tick = Find.TickManager.TicksGame;
            for (int i = 0; i < definition.maximumSettlements && candidates.Count > 0; i++)
            {
                var selected = candidates.RandomElementByWeight(c => c.interest);
                Faction faction = selected.settlement.Faction;
                candidates.RemoveAll(c => c.settlement.Faction == faction);
                if (!selectedFactions.Add(faction)) continue;
                int score = faction.ideos?.PrimaryIdeo?.PreceptsListForReading.Sum(p =>
                    friendly?.Contains(p.def) == true ? 1 : hostile?.Contains(p.def) == true ? -1 : 0) ?? 0;
                int goodwill = score > 0 ? friendlyGoodwill : hostileGoodwill;
                if (goodwill != 0) Faction.OfPlayer.TryAffectGoodwillWith(faction, goodwill, true, true,
                    score > 0 ? HistoryEventDefOf.RitualDone : HistoryEventDefOf.UsedHarmfulAbility);
                if (faction.HostileTo(Faction.OfPlayer))
                {
                    AccumulateReprisal(faction, StorytellerUtility.DefaultThreatPointsNow(caster.Map)
                        * definition.hostilePointsFactor * selected.interest, caster.Map);
                    continue;
                }
                int due = tick + definition.preparationTicks.RandomInRange + Mathf.RoundToInt(selected.distance * definition.caravanTicksPerTile);
                XMT_PendingArrival existing = arrivals.FirstOrDefault(a => a.map == caster.Map && a.faction == faction && a.response == definition);
                if (existing != null)
                {
                    existing.interest = Mathf.Max(existing.interest, selected.interest); existing.dueTick = Math.Min(existing.dueTick, due);
                    existing.expiresTick = Math.Max(existing.expiresTick, due + definition.maximumWaitTicks); continue;
                }
                arrivals.Add(new XMT_PendingArrival { map = caster.Map, faction = faction, response = definition,
                    interest = selected.interest, dueTick = due, expiresTick = due + definition.maximumWaitTicks });
            }
            Find.LetterStack.ReceiveLetter("XMT_SirenConvergenceLabel".Translate(),
                (selectedFactions.Count > 0 ? "XMT_SirenConvergence" : "XMT_SirenNoResponders").Translate(selectedFactions.Count),
                LetterDefOf.NeutralEvent, caster);
        }
        internal void TickPlayerActions(int tick, XMT_WatcherDef watcher)
        {
            foreach (XMT_PendingArrival request in arrivals.ToList())
            {
                if (request.response != null && request.response.watcher != watcher) continue;
                if (request.map == null || !Find.Maps.Contains(request.map) || request.faction == null || request.faction.defeated
                    || request.response == null || tick > request.expiresTick) { arrivals.Remove(request); continue; }
                if (tick < request.dueTick) continue;
                if (request.faction.HostileTo(Faction.OfPlayer))
                { AccumulateReprisal(request.faction, StorytellerUtility.DefaultThreatPointsNow(request.map) * request.interest, request.map); arrivals.Remove(request); continue; }
                if (((XMT_SirenResponseWorker)request.response.ResponseWorker).Arrive(request)) arrivals.Remove(request);
                else request.dueTick = tick + request.response.retryTicks;
            }
        }
        internal void TickReprisal(int tick, XMT_WatcherDef watcher)
        {
            if (tick < nextReprisalTick || Current.Game.GetComponent<GameComponent_Xenomorph>().conquestStarted) return;
            List<XMT_ReprisalDef> choices = DefDatabase<XMT_ReprisalDef>.AllDefsListForReading.Where(d => d.watcher == watcher && d.selectionWeight > 0f).ToList();
            if (choices.Count == 0) return;
            nextReprisalTick = tick + GenDate.TicksPerDay;
            XMT_ReprisalDef definition = choices.RandomElementByWeight(d => d.selectionWeight);
            Map map = ReprisalMap;
            if (map == null || reprisalPoints < StorytellerUtility.DefaultThreatPointsNow(map) * definition.minimumThreatFactor) return;
            ((XMT_ReprisalWorker)definition.ResponseWorker).Deploy(this, definition, map, reprisalFactions, ref reprisalPoints);
        }
        internal Map ReprisalMap => reprisalTarget != null && Find.Maps.Contains(reprisalTarget) ? reprisalTarget
            : XMTUtility.GetQueen()?.MapHeld ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
        internal List<(Settlement settlement, float distance, float interest)> SirenCandidates(XMT_SirenResponseDef definition, Pawn caster)
        {
            var candidates = new List<(Settlement, float, float)>();
            if (caster?.Map == null) return candidates;
            foreach (Settlement settlement in Find.WorldObjects.Settlements)
            {
                Faction faction = settlement.Faction;
                if (faction == null || faction.IsPlayer || faction.defeated || settlement.Tile.Layer != caster.Map.Tile.Layer) continue;
                // Use native cached world reachability in both execution and read-only diagnostics.
                if (!Find.WorldReachability.CanReach(caster.Map.Tile, settlement.Tile)) continue;
                float distance = Find.WorldGrid.ApproxDistanceInTiles(caster.Map.Tile, settlement.Tile);
                candidates.Add((settlement, distance, 1f / (1f + distance / Mathf.Max(1f, definition.distanceFalloff))));
            }
            return candidates;
        }
    }
    public class XMT_SirenResponseWorker : XMT_ResponseWorker
    {
        public virtual void Notify(GameComponent_XMTWorldState state, XMT_SirenResponseDef definition, Pawn caster,
            List<PreceptDef> friendly, List<PreceptDef> hostile, int friendlyGoodwill, int hostileGoodwill) =>
            state.RecordSiren(definition, caster, friendly, hostile, friendlyGoodwill, hostileGoodwill);
        public virtual bool Arrive(XMT_PendingArrival request)
        {
            TraderKindDef trader = request.faction.def.caravanTraderKinds?.RandomElementWithFallback();
            IncidentDef incident = trader == null ? IncidentDefOf.VisitorGroup : IncidentDefOf.TraderCaravanArrival;
            return incident.Worker.TryExecute(new IncidentParms { target = request.map, faction = request.faction, traderKind = trader,
                forced = true, points = StorytellerUtility.DefaultThreatPointsNow(request.map) * request.interest });
        }
    }
    public class XMT_ReprisalWorker : XMT_ResponseWorker
    {
        public virtual void Deploy(GameComponent_XMTWorldState state, XMT_ReprisalDef definition, Map map, List<Faction> factions, ref float budget)
        {
            List<Faction> eligible = factions.Where(f => f != null && !f.defeated && f.def.canStageAttacks)
                .OrderByDescending(f => f.def.techLevel).ToList();
            if (eligible.Count == 0) return;
            Faction leader = eligible[0];
            float points = Mathf.Min(StorytellerUtility.DefaultThreatPointsNow(map) * definition.maximumThreatFactor, budget);
            int delay = definition.preparationTicks.RandomInRange;
            foreach (Faction faction in eligible)
            {
                if (points <= 0f) break;
                if (XenoformingUtility.GetXenoforming() > definition.breachingXenoforming && !leader.AllyOrNeutralTo(faction) && faction != leader) continue;
                IncidentParms parms = new IncidentParms { target = map, faction = faction, forced = true, points = points, silent = faction != leader };
                if (XenoformingUtility.GetXenoforming() > definition.breachingXenoforming) parms.raidStrategy = ExternalDefOf.ImmediateAttackBreachingSmart;
                Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + delay, parms, 120000);
                budget = Mathf.Max(0f, budget - points);
                points = Mathf.Min(points * definition.successiveWaveFactor, budget);
            }
        }
    }
    public sealed class XMT_Watcher_PlayerAction : XMT_WatcherWorker
    { public override void Tick(GameComponent_XMTWorldState state, int tick) { state.TickPlayerActions(tick, Definition); state.TickQueenDefenses(Definition); } }
    public sealed class XMT_Watcher_Reprisal : XMT_WatcherWorker
    { public override void Tick(GameComponent_XMTWorldState state, int tick) => state.TickReprisal(tick, Definition); }
    public sealed class XMT_Watcher_Nemesis : XMT_WatcherWorker
    { public override void Tick(GameComponent_XMTWorldState state, int tick)
        {
            Current.Game.GetComponent<GameComponent_Nemesis>().TickFromWorldCoordinator(tick);
            Current.Game.GetComponent<GameComponent_NemesisWorldPawns>().ProcessRetainedPawns();
            state.TickQueenDefenses(Definition);
        }
    }
}
