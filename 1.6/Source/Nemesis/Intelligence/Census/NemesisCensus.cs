using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public sealed class NemesisCensusContext
    {
        public readonly int tick;
        public readonly string reason;
        public readonly IReadOnlyList<Map> maps;
        public readonly HashSet<NemesisTraversalCategory> requestedCategories;
        public readonly bool fullCensus;
        public readonly List<NemesisXenotypeRecord> xenotypeSamples = new List<NemesisXenotypeRecord>();

        public NemesisCensusContext(int tick, string reason, IReadOnlyList<Map> maps,
            IEnumerable<NemesisTraversalCategory> requestedCategories)
        {
            this.tick = tick;
            this.reason = reason;
            this.maps = maps;
            fullCensus = requestedCategories == null;
            this.requestedCategories = requestedCategories != null
                ? new HashSet<NemesisTraversalCategory>(requestedCategories)
                : new HashSet<NemesisTraversalCategory>(Enum.GetValues(typeof(NemesisTraversalCategory)).Cast<NemesisTraversalCategory>());
        }

        public void RememberXenotype(Pawn pawn, string origin)
        {
            NemesisXenotypeRecord record = NemesisXenotypeRecord.FromPawn(pawn, origin, tick, tick);
            if (record != null)
            {
                xenotypeSamples.Add(record);
            }
        }
    }

    public sealed class NemesisObservationResult
    {
        public NemesisObservationDef observation;
        public float value;
        public float confidence;
        public float coverage = 1f;
        public readonly List<NemesisSpatialContact> contacts = new List<NemesisSpatialContact>();
    }

    public abstract class NemesisObservationWorker
    {
        protected NemesisObservationDef def;

        internal void Initialize(NemesisObservationDef observationDef)
        {
            def = observationDef;
        }

        public virtual bool WantsUpdate(NemesisCensusContext context)
        {
            return def.updateMode != NemesisObservationUpdateMode.EventImmediate;
        }

        public virtual void Begin(NemesisCensusContext context)
        {
        }

        public virtual void VisitBuilding(Building building, NemesisCensusContext context)
        {
        }

        public virtual void VisitItem(Thing thing, NemesisCensusContext context)
        {
        }

        public virtual void VisitCorpse(StarbeastCorpse corpse, NemesisCensusContext context)
        {
        }

        public virtual void VisitPawn(Pawn pawn, bool held, NemesisCensusContext context)
        {
        }

        public virtual NemesisObservationResult Complete(NemesisCensusContext context)
        {
            return new NemesisObservationResult
            {
                observation = def,
                confidence = def.baseConfidence,
                coverage = 1f
            };
        }
    }

    internal sealed class NemesisCensusSession
    {
        internal NemesisObservationDef Def;
        internal NemesisObservationWorker Worker;
    }

    public sealed class NemesisCensusReport
    {
        public int tick;
        public string reason;
        public int mapCount;
        public readonly List<NemesisTraversalCategory> requestedTraversals = new List<NemesisTraversalCategory>();
        public readonly List<NemesisTraversalCategory> skippedTraversals = new List<NemesisTraversalCategory>();
        public readonly Dictionary<NemesisTraversalCategory, List<string>> requesters = new Dictionary<NemesisTraversalCategory, List<string>>();
        public readonly List<NemesisObservationResult> observations = new List<NemesisObservationResult>();
        public readonly List<NemesisXenotypeRecord> xenotypeSamples = new List<NemesisXenotypeRecord>();

        public string DescribePlan()
        {
            IEnumerable<string> lines = Enum.GetValues(typeof(NemesisTraversalCategory))
                .Cast<NemesisTraversalCategory>()
                .Select(category => requestedTraversals.Contains(category)
                    ? category + ": requested by " + string.Join(", ", requesters[category])
                    : category + ": skipped - no accepted worker requested traversal");
            return string.Join("\n", lines);
        }
    }

    public static class NemesisCensus
    {
        public static NemesisCensusReport Run(string reason, IEnumerable<NemesisTraversalCategory> requestedCategories = null)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            List<Map> maps = Find.Maps.Where(map => map != null && map.IsPlayerHome).ToList();
            if (maps.Count == 0 && Find.CurrentMap != null)
            {
                maps.Add(Find.CurrentMap);
            }

            NemesisCensusContext context = new NemesisCensusContext(tick, reason, maps, requestedCategories);
            NemesisCensusReport report = new NemesisCensusReport
            {
                tick = tick,
                reason = reason,
                mapCount = maps.Count
            };

            List<NemesisCensusSession> sessions = BuildSessions(context, report);
            foreach (NemesisCensusSession session in sessions)
            {
                session.Worker.Begin(context);
            }

            DispatchBuildings(context, sessions, NemesisTraversalCategory.PlayerBuildings);
            DispatchItems(context, sessions, NemesisTraversalCategory.StoredItems);
            DispatchCryptimorphCorpses(context, sessions);
            DispatchPawns(context, sessions, NemesisTraversalCategory.SpawnedPawns, held: false);
            DispatchPawns(context, sessions, NemesisTraversalCategory.HeldPawns, held: true);
            DispatchWorldPawns(context, sessions);

            foreach (NemesisCensusSession session in sessions)
            {
                NemesisObservationResult result = session.Worker.Complete(context);
                if (result != null)
                {
                    result.observation ??= session.Def;
                    report.observations.Add(result);
                }
            }

            report.xenotypeSamples.AddRange(context.xenotypeSamples);

            return report;
        }

        private static List<NemesisCensusSession> BuildSessions(NemesisCensusContext context, NemesisCensusReport report)
        {
            List<NemesisCensusSession> sessions = new List<NemesisCensusSession>();
            foreach (NemesisObservationDef def in DefDatabase<NemesisObservationDef>.AllDefsListForReading.OrderBy(value => value.defName))
            {
                NemesisObservationWorker worker;
                try
                {
                    worker = Activator.CreateInstance(def.workerClass) as NemesisObservationWorker;
                }
                catch (Exception exception)
                {
                    Log.Error("[XMT][Nemesis] Could not construct observation worker for " + def.defName + ": " + exception);
                    continue;
                }

                if (worker == null)
                {
                    continue;
                }

                worker.Initialize(def);
                if (!worker.WantsUpdate(context))
                {
                    continue;
                }

                List<NemesisTraversalCategory> traversals = def.traversalCategories ?? new List<NemesisTraversalCategory>();
                if ((!context.fullCensus && traversals.Count == 0)
                    || (traversals.Count > 0 && !traversals.Any(context.requestedCategories.Contains)))
                {
                    continue;
                }

                sessions.Add(new NemesisCensusSession { Def = def, Worker = worker });
                foreach (NemesisTraversalCategory category in def.traversalCategories ?? Enumerable.Empty<NemesisTraversalCategory>())
                {
                    if (!report.requestedTraversals.Contains(category))
                    {
                        report.requestedTraversals.Add(category);
                        report.requesters.Add(category, new List<string>());
                    }
                    report.requesters[category].Add(def.defName);
                }
            }

            foreach (NemesisTraversalCategory category in Enum.GetValues(typeof(NemesisTraversalCategory)))
            {
                if (!report.requestedTraversals.Contains(category))
                {
                    report.skippedTraversals.Add(category);
                }
            }
            return sessions;
        }

        private static void DispatchItems(NemesisCensusContext context, List<NemesisCensusSession> sessions,
            NemesisTraversalCategory category)
        {
            List<NemesisCensusSession> interested = sessions.Where(session => session.Def.traversalCategories.Contains(category)).ToList();
            if (interested.Count == 0)
            {
                return;
            }

            foreach (Map map in context.maps)
            {
                List<Thing> things = map.listerThings.AllThings;
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing == null || thing.Destroyed || thing is Pawn || thing is Building || !IsStoredPlayerItem(thing))
                    {
                        continue;
                    }

                    for (int j = 0; j < interested.Count; j++)
                    {
                        interested[j].Worker.VisitItem(thing, context);
                    }
                }
            }
        }

        private static bool IsStoredPlayerItem(Thing thing)
        {
            if (thing.Faction == Faction.OfPlayer)
            {
                return true;
            }

            SlotGroup slotGroup = thing.MapHeld?.haulDestinationManager?.SlotGroupAt(thing.PositionHeld);
            return slotGroup != null && slotGroup.Settings.AllowedToAccept(thing);
        }

        private static void DispatchCryptimorphCorpses(NemesisCensusContext context, List<NemesisCensusSession> sessions)
        {
            NemesisTraversalCategory category = NemesisTraversalCategory.CryptimorphCorpses;
            List<NemesisCensusSession> interested = sessions.Where(session => session.Def.traversalCategories.Contains(category)).ToList();
            if (interested.Count == 0)
            {
                return;
            }

            foreach (Map map in context.maps)
            {
                List<StarbeastCorpse> corpses = map.listerThings.GetThingsOfType<StarbeastCorpse>().ToList();
                for (int i = 0; i < corpses.Count; i++)
                {
                    StarbeastCorpse corpse = corpses[i];
                    for (int j = 0; j < interested.Count; j++)
                    {
                        interested[j].Worker.VisitCorpse(corpse, context);
                    }
                }
            }
        }

        private static void DispatchBuildings(NemesisCensusContext context, List<NemesisCensusSession> sessions, NemesisTraversalCategory category)
        {
            List<NemesisCensusSession> interested = sessions.Where(session => session.Def.traversalCategories.Contains(category)).ToList();
            if (interested.Count == 0)
            {
                return;
            }

            foreach (Map map in context.maps)
            {
                IReadOnlyList<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    for (int j = 0; j < interested.Count; j++)
                    {
                        interested[j].Worker.VisitBuilding(building, context);
                    }
                }
            }
        }

        private static void DispatchPawns(NemesisCensusContext context, List<NemesisCensusSession> sessions, NemesisTraversalCategory category, bool held)
        {
            List<NemesisCensusSession> interested = sessions.Where(session => session.Def.traversalCategories.Contains(category)).ToList();
            if (interested.Count == 0)
            {
                return;
            }

            foreach (Map map in context.maps)
            {
                IReadOnlyList<Pawn> pawns = held ? map.mapPawns.AllPawnsUnspawned : map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    for (int j = 0; j < interested.Count; j++)
                    {
                        interested[j].Worker.VisitPawn(pawn, held, context);
                    }
                }
            }
        }

        private static void DispatchWorldPawns(NemesisCensusContext context, List<NemesisCensusSession> sessions)
        {
            NemesisTraversalCategory category = NemesisTraversalCategory.WorldPawns;
            List<NemesisCensusSession> interested = sessions.Where(session => session.Def.traversalCategories.Contains(category)).ToList();
            if (interested.Count == 0 || Find.WorldPawns == null)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns = Find.WorldPawns.AllPawnsAliveOrDead;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                for (int j = 0; j < interested.Count; j++)
                {
                    interested[j].Worker.VisitPawn(pawn, held: true, context);
                }
            }
        }
    }
}
