using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal static class NemesisMissionUtility
    {
        internal static JobDef AbductJob => XenoWorkDefOf.XMT_AbductOffMap;
        internal static bool MapDark(Map map) => map != null && map.skyManager.CurSkyGlow < 0.5f;
        internal static bool ValidHost(Pawn pawn) => pawn != null && pawn.Spawned && !pawn.Dead
            && !XMTUtility.NotPrey(pawn) && XMTUtility.IsAcceptableHost(pawn) && !XMTUtility.IsXenomorphFriendly(pawn)
            && !(pawn.CurrentBed() is CocoonBase);

        internal static Pawn FindHost(Pawn seeker, float radius, bool opportunistic, bool requirePlayer, ISet<Pawn> excluded = null)
        {
            return seeker.Map.mapPawns.AllPawnsSpawned.Where(p => (excluded == null || !excluded.Contains(p))
                && HostRejection(seeker, p, radius, opportunistic, requirePlayer) == null)
                .OrderByDescending(p => !p.Awake() || p.Downed).ThenBy(p => seeker.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        internal static string HostRejection(Pawn seeker, Pawn host, float radius, bool opportunistic, bool requirePlayer)
        {
            if (host == seeker || !ValidHost(host)) return "invalid host";
            if (requirePlayer && host.Faction != Faction.OfPlayer) return "awaiting player-host assignment";
            if (host.Position.DistanceToSquared(seeker.Position) > radius * radius) return "outside search radius";
            if (!GenSight.LineOfSight(seeker.Position, host.Position, seeker.Map)) return "no line of sight";
            if (opportunistic && host.Awake() && !host.Downed
                && (!XMTHiveUtility.IsLightSuitableAt(host.Position, host.Map)
                    || host.Map.mapPawns.AllPawnsSpawned.Any(other => other != host && other != seeker && !XMTUtility.IsXenomorph(other)
                        && other.Awake() && !other.Downed && other.Position.DistanceToSquared(host.Position) < 36f)))
                return "not an isolated dark/sleeping opportunity";
            if (!FeralJobUtility.IsThingAvailableForJobBy(seeker, host)) return "reserved or forbidden";
            if (!ClimbUtility.CanReachByWalkingOrClimb(seeker, host, PathEndMode.Touch, Danger.Deadly)) return "unreachable";
            return null;
        }

        internal static bool ExitCell(Pawn pawn, out IntVec3 cell) => CellFinder.TryFindRandomEdgeCellWith(
            c => c.Standable(pawn.Map) && ClimbUtility.CanReachByWalkingOrClimb(pawn, c, PathEndMode.OnCell, Danger.Deadly), pawn.Map, 0f, out cell);

        internal static Job ExtractionJob(Pawn pawn, Pawn victim)
        {
            if (!ExitCell(pawn, out IntVec3 exit)) return null;
            Job job = JobMaker.MakeJob(AbductJob, victim, exit);
            job.count = 1;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        internal static Job ExitJob(Pawn pawn)
        {
            if (!ExitCell(pawn, out IntVec3 exit)) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, exit);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        internal static List<IntVec3> Routes(IEnumerable<NemesisSpatialContact> contacts, NemesisMissionDef def, Map map)
        {
            List<IntVec3> result = new List<IntVec3>();
            foreach (NemesisSpatialContact contact in contacts)
            {
                if (!contact.cell.InBounds(map) || result.Any(cell => cell.DistanceToSquared(contact.cell) < 64f)) continue;
                result.Add(contact.cell);
                if (result.Count >= def.workerSettings.maximumRoutePoints) break;
            }
            if (result.Count == 0)
            {
                foreach (IntVec3 home in map.areaManager.Home.ActiveCells.InRandomOrder())
                {
                    if (!home.Standable(map) || result.Any(cell => cell.DistanceToSquared(home) < 225f)) continue;
                    result.Add(home);
                    if (result.Count >= def.workerSettings.maximumRoutePoints) break;
                }
                if (result.Count == 0) result.Add(map.Center);
                NemesisLog.Detail("Route", "mission=" + def.defName + " map=" + map.uniqueID
                    + " fallbackSearch=" + string.Join(", ", result));
            }
            return result;
        }

        private static string ContactKey(NemesisSpatialContact contact) => contact.observation.defName + ":" + contact.subjectId;
        private static bool SameSpatialInformation(NemesisSpatialContact a, NemesisSpatialContact b)
            => a.mapId == b.mapId && a.cell == b.cell && a.defName == b.defName && Mathf.Approximately(a.threat, b.threat)
                && new HashSet<string>(a.tags).SetEquals(b.tags);

        internal static List<NemesisSpatialContact> ChangedContacts(IEnumerable<NemesisSpatialContact> previous,
            IEnumerable<NemesisSpatialContact> current, int mapId)
        {
            Dictionary<string, NemesisSpatialContact> before = previous.GroupBy(ContactKey).ToDictionary(g => g.Key, g => g.First());
            Dictionary<string, NemesisSpatialContact> after = current.GroupBy(ContactKey).ToDictionary(g => g.Key, g => g.First());
            List<NemesisSpatialContact> changed = after.Values.Where(c => c.mapId == mapId
                && (!before.TryGetValue(ContactKey(c), out NemesisSpatialContact old) || !SameSpatialInformation(old, c))).ToList();
            changed.AddRange(before.Values.Where(c => c.mapId == mapId && (!after.TryGetValue(ContactKey(c), out NemesisSpatialContact next)
                || next.mapId != mapId || next.cell != c.cell)));
            return changed;
        }

        internal static bool Launch(NemesisMissionRequest request, Map map, bool forceLight, out string reason)
        {
            reason = null;
            if (request?.mission == null || map == null) { reason = "target map or mission is missing"; return false; }
            if (!forceLight && !MapDark(map)) { reason = "waiting for darkness"; return false; }
            GameComponent_Nemesis component = Current.Game.GetComponent<GameComponent_Nemesis>();
            NemesisMissionWorker worker = request.mission.Worker;
            if (!worker.CanTarget(request.mission, component, map))
            { reason = request.mission.defName + " target rejected: " + worker.DescribeTarget(map); return false; }
            int count = worker.PartySize(request.mission, component, map, request.active);
            if (count < request.mission.populationRange.min)
            { reason = request.mission.defName + " deployment below minimum: " + worker.DescribeSizing(request.mission, component, map, request.active); return false; }
            IntVec3 entry = IntVec3.Invalid;
            for (int attempt = 0; attempt < 24; attempt++)
                if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 candidate, map, CellFinder.EdgeRoadChance_Animal)
                    && (forceLight || XMTHiveUtility.IsLightSuitableAt(candidate, map))) { entry = candidate; break; }
            if (!entry.IsValid) { reason = "no suitable entry"; return false; }

            List<IntVec3> route = worker.PrepareRoute(request.mission, component, map);
            LordJob_NemesisMission job = (LordJob_NemesisMission)Activator.CreateInstance(request.mission.lordJobClass);
            job.Initialize(request.mission, request.active, route);
            Lord lord = LordMaker.MakeNewLord(null, job, map);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Pawn pawn = Current.Game.GetComponent<GameComponent_Xenomorph>().GetWorldOrGeneratedCryptimorphForMission();
                    IntVec3 spawnCell = GenRadial.RadialCellsAround(entry, Mathf.Max(4f, count), true)
                        .Where(c => c.InBounds(map) && c.Standable(map) && (forceLight || XMTHiveUtility.IsLightSuitableAt(c, map))
                            && !map.thingGrid.ThingsListAtFast(c).Any(t => t is Pawn)).RandomElementWithFallback(entry);
                    GenSpawn.Spawn(pawn, spawnCell, map);
                    pawn.mindState.mentalStateHandler.Reset();
                    pawn.GetComp<CompStealth>()?.TryHide();
                    lord.AddPawn(pawn);
                }
                job.Begin();
                return true;
            }
            catch (Exception exception)
            {
                job.Withdraw("launch preparation failed");
                Log.Error("[XMT][Nemesis][Mission] Launch failed: " + exception);
                if (lord.ownedPawns.Count > 0) return true;
                map.lordManager.RemoveLord(lord);
                reason = "launch failed before deployment";
                return false;
            }
        }
    }
}
