using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    public enum NemesisRetainedPawnPurpose { AbductedHost, CapturedQueen }

    public sealed class NemesisRetainedPawn : IExposable
    {
        public Pawn pawn;
        public NemesisRetainedPawnPurpose purpose;
        public int retainedTick;
        public int processTick;
        public bool processingComplete;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref purpose, "purpose", NemesisRetainedPawnPurpose.AbductedHost);
            Scribe_Values.Look(ref retainedTick, "retainedTick");
            Scribe_Values.Look(ref processTick, "processTick");
            Scribe_Values.Look(ref processingComplete, "processingComplete");
        }
    }

    public sealed class GameComponent_NemesisWorldPawns : GameComponent
    {
        private const int AbductionProcessingTicks = 600000;
        private List<NemesisRetainedPawn> retainedPawns = new List<NemesisRetainedPawn>();
        public IReadOnlyList<NemesisRetainedPawn> RetainedPawns => retainedPawns;
        public GameComponent_NemesisWorldPawns(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref retainedPawns, "nemesisRetainedPawns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                retainedPawns ??= new List<NemesisRetainedPawn>();
                retainedPawns.RemoveAll(record => record?.pawn == null);
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager.TicksGame % 250 == 0) ProcessRetainedPawns();
        }

        public bool RetainExtractedHost(Pawn carrier, Pawn victim)
        {
            if (victim == null || victim.Dead || carrier?.carryTracker?.CarriedThing != victim || !carrier.Spawned
                || !carrier.Position.OnEdge(carrier.Map) || retainedPawns.Any(record => record.pawn == victim)) return false;
            bool player = victim.Faction == Faction.OfPlayer;
            bool alreadyImplanted = XMTUtility.HasEmbryo(victim);
            victim.PreKidnapped(carrier);
            carrier.carryTracker.innerContainer.Remove(victim);
            Find.WorldPawns.PassToWorld(victim, PawnDiscardDecideMode.KeepForever);
            if (!Find.WorldPawns.Contains(victim)) return false;
            int tick = Find.TickManager.TicksGame;
            retainedPawns.Add(new NemesisRetainedPawn { pawn = victim, purpose = NemesisRetainedPawnPurpose.AbductedHost, retainedTick = tick,
                processTick = tick + AbductionProcessingTicks, processingComplete = alreadyImplanted });
            Find.WorldPawns.ForcefullyKeptPawns.Add(victim);
            NemesisEvidenceReporter.ReportAbduction(victim, carrier);
            if (player)
            {
                PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(victim, null, PawnDiedOrDownedThoughtsKind.Lost);
                BillUtility.Notify_ColonistUnavailable(victim);
                Find.LetterStack.ReceiveLetter("XMT_MissionHostTakenLabel".Translate(),
                    "XMT_MissionHostTaken".Translate(victim.Named("PAWN")), LetterDefOf.NegativeEvent);
            }
            QuestUtility.SendQuestTargetSignals(victim.questTags, "Kidnapped", victim.Named("SUBJECT"), carrier.Named("KIDNAPPER"));
            Find.GameEnder.CheckOrUpdateGameOver();
            NemesisLog.Detail("WorldPawns", "Retained pawn=" + victim + " processTick=" + (tick + AbductionProcessingTicks));
            return true;
        }

        public void ProcessRetainedPawns()
        {
            int tick = Find.TickManager.TicksGame;
            foreach (NemesisRetainedPawn record in retainedPawns.ToList())
            {
                Pawn pawn = record.pawn;
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Spawned || CaravanUtility.IsCaravanMember(pawn)
                    || !Find.WorldPawns.Contains(pawn))
                {
                    if (pawn != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
                    retainedPawns.Remove(record);
                    continue;
                }
                Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
                if (record.purpose == NemesisRetainedPawnPurpose.AbductedHost && !record.processingComplete && tick >= record.processTick)
                {
                    record.processingComplete = true;
                    Current.Game.GetComponent<GameComponent_Xenomorph>().ReleaseEmbryoOnWorld(pawn);
                }
            }
        }

        public int PlaceRetainedHosts(Map map, IEnumerable<IntVec3> candidates)
        {
            if (map == null || candidates == null) return 0;
            Queue<IntVec3> cells = new Queue<IntVec3>(candidates.Distinct().Where(c => c.InBounds(map)
                && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null));
            int placed = 0;
            foreach (NemesisRetainedPawn record in retainedPawns.Where(record => record.purpose == NemesisRetainedPawnPurpose.AbductedHost).ToList())
            {
                Pawn pawn = record.pawn;
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Spawned || CaravanUtility.IsCaravanMember(pawn)
                    || !Find.WorldPawns.Contains(pawn)) continue;
                if (cells.Count == 0) break;
                IntVec3 cell = cells.Dequeue(); CocoonBase cocoon = null; Hediff added = null;
                try
                {
                    GenSpawn.Spawn(pawn, cell, map);
                    if (pawn.Dead) throw new InvalidOperationException("Captive died during world-pawn catch-up.");
                    cocoon = XMTHiveUtility.TryPlaceCocoonBase(cell, pawn) as CocoonBase;
                    if (cocoon == null) throw new InvalidOperationException("Cocoon placement failed.");
                    if (!pawn.health.hediffSet.HasHediff(InternalDefOf.StarbeastCocoon))
                    {
                        added = HediffMaker.MakeHediff(InternalDefOf.StarbeastCocoon, pawn); pawn.health.AddHediff(added);
                    }
                    pawn.jobs.Notify_TuckedIntoBed(cocoon);
                    if (!pawn.Spawned || pawn.Position != cell || cell.GetEdifice(map) != cocoon)
                        throw new InvalidOperationException("Captive placement did not establish the expected cocoon.");
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
                    retainedPawns.Remove(record);
                    placed++;
                }
                catch (Exception exception)
                {
                    if (added != null && pawn.health.hediffSet.hediffs.Contains(added)) pawn.health.RemoveHediff(added);
                    if (cocoon != null && !cocoon.Destroyed) cocoon.Destroy();
                    if (pawn.Spawned) pawn.DeSpawn();
                    if (!pawn.Destroyed && !Find.WorldPawns.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    Log.Warning("[XMT][Nemesis][WorldPawns] Captive placement rolled back: " + exception);
                }
            }
            return placed;
        }
    }
}
