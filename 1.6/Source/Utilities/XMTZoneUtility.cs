using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [Flags]
    internal enum AbductionDestinationWarning
    {
        None = 0,
        InvalidHostRoom = 1,
        HostZoneFallback = 2
    }

    internal static class XMTZoneUtility
    {
        private const int WarningCooldownTicks = 2500;

        private static readonly Dictionary<string, int> nextWarningTick = new Dictionary<string, int>();
        private static readonly HashSet<ThingDef> warnedUnsupportedStorageDefs = new HashSet<ThingDef>();

        internal static bool IsDoorwayCell(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                ThingDef def = thing.def;
                if (IsDoorwayDef(def))
                {
                    return true;
                }

                if (def?.entityDefToBuild is ThingDef buildDef && IsDoorwayDef(buildDef))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDoorwayDef(ThingDef def)
        {
            return def != null &&
                   (def.IsDoor ||
                    def == XenoBuildingDefOf.HiveWebbing ||
                    def == XenoBuildingDefOf.HiveWebbingBuildable);
        }

        internal static void ReconcileDoorwayCells(Map map)
        {
            if (map?.zoneManager?.AllZones == null)
            {
                return;
            }

            List<Zone> zones = map.zoneManager.AllZones
                .Where(zone => zone is Zone_HostPlacement || zone is Zone_OvomorphStorage)
                .ToList();

            foreach (Zone zone in zones)
            {
                List<IntVec3> doorwayCells = zone.Cells.Where(cell => IsDoorwayCell(cell, map)).ToList();
                if (doorwayCells.Count == 0)
                {
                    continue;
                }

                foreach (IntVec3 doorwayCell in doorwayCells)
                {
                    if (zone.ContainsCell(doorwayCell))
                    {
                        zone.RemoveCell(doorwayCell);
                    }
                }

                SplitDisconnectedZone(zone);
            }
        }

        private static void SplitDisconnectedZone(Zone zone)
        {
            if (zone == null || zone.Map == null || zone.CellCount <= 1)
            {
                return;
            }

            List<HashSet<IntVec3>> components = CardinalComponents(zone.Cells);
            if (components.Count <= 1)
            {
                return;
            }

            components.Sort((left, right) => right.Count.CompareTo(left.Count));
            for (int componentIndex = 1; componentIndex < components.Count; componentIndex++)
            {
                HashSet<IntVec3> component = components[componentIndex];
                Zone splitZone = zone is Zone_HostPlacement
                    ? new Zone_HostPlacement(zone.Map.zoneManager)
                    : new Zone_OvomorphStorage(zone.Map.zoneManager);

                foreach (IntVec3 cell in component)
                {
                    zone.RemoveCell(cell);
                    splitZone.AddCell(cell);
                }
            }
        }

        private static List<HashSet<IntVec3>> CardinalComponents(IEnumerable<IntVec3> cells)
        {
            HashSet<IntVec3> remaining = new HashSet<IntVec3>(cells);
            List<HashSet<IntVec3>> components = new List<HashSet<IntVec3>>();

            while (remaining.Count > 0)
            {
                IntVec3 seed = remaining.First();
                HashSet<IntVec3> component = new HashSet<IntVec3>();
                Queue<IntVec3> open = new Queue<IntVec3>();
                open.Enqueue(seed);
                remaining.Remove(seed);

                while (open.Count > 0)
                {
                    IntVec3 cell = open.Dequeue();
                    component.Add(cell);
                    foreach (IntVec3 direction in GenAdj.CardinalDirections)
                    {
                        IntVec3 adjacent = cell + direction;
                        if (remaining.Remove(adjacent))
                        {
                            open.Enqueue(adjacent);
                        }
                    }
                }

                components.Add(component);
            }

            return components;
        }

        internal static bool TryGetAbductionCocoonCell(Pawn pawn, out IntVec3 cell, bool playerOrdered = false)
        {
            bool found = TryGetAbductionCocoonCellQuiet(pawn, out cell, out AbductionDestinationWarning warnings);
            if ((warnings & AbductionDestinationWarning.InvalidHostRoom) != 0)
            {
                Warn(pawn?.Map, "invalidHostRoom", "XMT_HostZoneInvalidRoom", playerOrdered);
            }
            if ((warnings & AbductionDestinationWarning.HostZoneFallback) != 0)
            {
                Warn(pawn?.Map, "hostFallback", "XMT_HostZonesUnavailable", playerOrdered);
            }

            return found;
        }

        internal static bool TryGetAbductionCocoonCellQuiet(
            Pawn pawn,
            out IntVec3 cell,
            out AbductionDestinationWarning warnings)
        {
            cell = IntVec3.Invalid;
            warnings = AbductionDestinationWarning.None;
            if (!CanUsePlayerZones(pawn))
            {
                return XMTHiveUtility.TryGetHiveCocoonCell(pawn, out cell);
            }

            List<Zone_HostPlacement> zones = HostZones(pawn.Map);
            if (zones.Count == 0)
            {
                return XMTHiveUtility.TryGetHiveCocoonCell(pawn, out cell);
            }

            bool foundInvalidRoom = false;
            if (TryGetPreferredHostCell(pawn, zones, out cell, ref foundInvalidRoom))
            {
                if (foundInvalidRoom)
                {
                    warnings |= AbductionDestinationWarning.InvalidHostRoom;
                }

                return true;
            }

            if (foundInvalidRoom)
            {
                warnings |= AbductionDestinationWarning.InvalidHostRoom;
            }

            warnings |= AbductionDestinationWarning.HostZoneFallback;
            bool foundFallback = XMTHiveUtility.TryGetHiveCocoonCell(pawn, out cell);
            XMTSettings.LogStructure(
                "Host-zone placement fell back for " + pawn +
                ": zones=" + zones.Count +
                ", invalidRoomSeen=" + foundInvalidRoom +
                ", fallbackFound=" + foundFallback +
                ", fallbackCell=" + (foundFallback ? cell.ToString() : "none") + ".");
            return foundFallback;
        }

        internal static void MarkPreferredHostDestination(Job job, Map map, IntVec3 cell)
        {
            if (job != null && map?.zoneManager?.ZoneAt(cell) is Zone_HostPlacement)
            {
                job.targetC = cell;
            }
        }

        internal static bool PreferredHostDestinationStillValid(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.Map == null ||
                pawn.Map.zoneManager.ZoneAt(cell) is not Zone_HostPlacement ||
                !IsEnclosedRoom(cell.GetRoom(pawn.Map)) ||
                IsDoorwayCell(cell, pawn.Map) ||
                !XMTHiveUtility.IsCellValidCocoon(cell, pawn.Map) ||
                !XMTHiveUtility.HasAdjacentOpenEggPlacementCell(cell, pawn.Map, pawn, ignorePawns: true))
            {
                return false;
            }

            return IsPlaceAvailableForCurrentOrNewJob(pawn, cell);
        }

        internal static void WarnPreferredHostDestinationLost(Map map)
        {
            Warn(map, "hostDestinationLost", "XMT_HostZonesUnavailable", playerOrdered: false);
        }

        private static bool TryGetPreferredHostCell(
            Pawn pawn,
            List<Zone_HostPlacement> zones,
            out IntVec3 bestCell,
            ref bool foundInvalidRoom)
        {
            bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;

            foreach (Zone_HostPlacement zone in zones)
            {
                Dictionary<Room, List<IntVec3>> roomCells = new Dictionary<Room, List<IntVec3>>();
                foreach (IntVec3 zoneCell in zone.Cells)
                {
                    Room room = zoneCell.GetRoom(pawn.Map);
                    if (!IsEnclosedRoom(room) || IsDoorwayCell(zoneCell, pawn.Map))
                    {
                        foundInvalidRoom = true;
                        continue;
                    }

                    if (!roomCells.TryGetValue(room, out List<IntVec3> cells))
                    {
                        cells = new List<IntVec3>();
                        roomCells.Add(room, cells);
                    }

                    cells.Add(zoneCell);
                }

                foreach (KeyValuePair<Room, List<IntVec3>> roomGroup in roomCells)
                {
                    if (!HostRoomHasEstimatedCapacity(roomGroup.Key, roomGroup.Value, pawn.Map))
                    {
                        XMTSettings.LogStructure(
                            "Host zone " + zone.ID +
                            " rejected room " + roomGroup.Key.ID +
                            " by capacity precheck: zoneCells=" + roomGroup.Value.Count +
                            ", cardinalPerimeterCells=" + roomGroup.Value.Count(candidate =>
                                IsCardinalRoomPerimeterCell(candidate, roomGroup.Key, pawn.Map)) + ".");
                        continue;
                    }

                    foreach (IntVec3 candidate in roomGroup.Value)
                    {
                        if (!IsHostCellAvailableFor(pawn, candidate))
                        {
                            continue;
                        }

                        int score = HostCellScore(candidate, roomGroup.Key, pawn);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestCell = candidate;
                        }
                    }
                }
            }

            return bestCell.IsValid;
        }

        internal static bool IsEnclosedRoomAt(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && IsEnclosedRoom(cell.GetRoom(map));
        }

        private static bool IsEnclosedRoom(Room room)
        {
            return room != null && room.ProperRoom && !room.TouchesMapEdge;
        }

        private static bool HostRoomHasEstimatedCapacity(Room room, List<IntVec3> zoneCells, Map map)
        {
            int cardinalPerimeterCells = zoneCells.Count(cell => IsCardinalRoomPerimeterCell(cell, room, map));
            if (cardinalPerimeterCells == 0)
            {
                return false;
            }

            int estimatedCapacity = Math.Max(1, cardinalPerimeterCells / 2);
            int occupied = zoneCells.Count(cell => cell.GetThingList(map).Any(thing => thing is CocoonBase));
            return occupied < estimatedCapacity;
        }

        private static bool IsCardinalRoomPerimeterCell(IntVec3 cell, Room room, Map map)
        {
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (!adjacent.InBounds(map) || adjacent.GetRoom(map) != room || adjacent.GetEdifice(map) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHostCellAvailableFor(Pawn pawn, IntVec3 cell)
        {
            if (!XMTHiveUtility.IsCellValidCocoon(cell, pawn.Map) ||
                cell.GetFirstPawn(pawn.Map) != null ||
                !FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell) ||
                !XMTHiveUtility.HasAdjacentOpenEggPlacementCell(cell, pawn.Map, pawn))
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly);
        }

        private static int HostCellScore(IntVec3 cell, Room room, Pawn pawn)
        {
            int score = IsCardinalRoomPerimeterCell(cell, room, pawn.Map) ? 100 : 0;
            score += cell.Roofed(pawn.Map) ? 25 : 0;
            score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position));
            return score;
        }

        internal static bool HasOvomorphStorageWork(Pawn pawn)
        {
            return TryFindOvomorphStorageMove(pawn, out _, out _, reportFull: true);
        }

        internal static bool TryMakeOvomorphStorageJob(Pawn pawn, out Job job)
        {
            job = null;
            if (!TryFindOvomorphStorageMove(pawn, out Thing source, out IntVec3 destination, reportFull: true))
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MoveOvomorph, source, destination);
            job.targetC = destination;
            job.count = 1;
            if (!FeralJobUtility.ReservePlaceForJob(pawn, job, destination))
            {
                job = null;
                return false;
            }

            FeralJobUtility.ReserveThingForJob(pawn, job, source);
            return true;
        }

        private static bool TryFindOvomorphStorageMove(
            Pawn pawn,
            out Thing source,
            out IntVec3 destination,
            bool reportFull)
        {
            source = null;
            destination = IntVec3.Invalid;
            if (!CanHaulToOvomorphStorage(pawn))
            {
                return false;
            }

            List<Zone_OvomorphStorage> zones = StorageZones(pawn.Map);
            if (zones.Count == 0)
            {
                return false;
            }

            bool foundEligibleSource = false;
            foreach (Thing candidate in StorageCandidates(pawn.Map).OrderBy(thing => thing.Position.DistanceToSquared(pawn.Position)))
            {
                if (!IsEligibleStorageSource(candidate, pawn, zones))
                {
                    continue;
                }

                foundEligibleSource = true;
                if (TryFindStorageDestination(candidate, pawn, zones, out destination))
                {
                    source = candidate;
                    return true;
                }
            }

            if (foundEligibleSource && reportFull)
            {
                Warn(pawn.Map, "storageFull", "XMT_OvomorphStorageFull", playerOrdered: false);
            }

            return false;
        }

        internal static bool TryFindStorageDestination(Thing thing, Pawn pawn, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            if (thing == null || pawn?.Map == null)
            {
                return false;
            }

            return TryFindStorageDestination(thing, pawn, StorageZones(pawn.Map), out destination);
        }

        private static bool TryFindStorageDestination(
            Thing thing,
            Pawn pawn,
            List<Zone_OvomorphStorage> zones,
            out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            foreach (Zone_OvomorphStorage zone in zones)
            {
                foreach (IntVec3 candidate in zone.Cells.OrderBy(cell => cell.DistanceToSquared(pawn.Position)))
                {
                    if (CanInstallStorageThingAt(thing, candidate, zone, pawn))
                    {
                        destination = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool CanInstallStorageThingAt(Thing thing, IntVec3 cell, Pawn pawn)
        {
            if (thing == null || pawn?.Map == null)
            {
                return false;
            }

            Zone_OvomorphStorage zone = pawn.Map.zoneManager.ZoneAt(cell) as Zone_OvomorphStorage;
            return zone != null && CanInstallStorageThingAt(thing, cell, zone, pawn);
        }

        private static bool CanInstallStorageThingAt(Thing thing, IntVec3 cell, Zone_OvomorphStorage zone, Pawn pawn)
        {
            Map map = pawn.Map;
            if (zone == null || zone.Map != map || !cell.InBounds(map) || IsDoorwayCell(cell, map))
            {
                return false;
            }

            Room room = cell.GetRoom(map);
            if (!IsEnclosedRoom(room))
            {
                return false;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(cell, thing.Rotation, thing.def.Size);
            foreach (IntVec3 occupiedCell in occupiedRect)
            {
                if (!occupiedCell.InBounds(map) ||
                    !zone.ContainsCell(occupiedCell) ||
                    occupiedCell.GetRoom(map) != room ||
                    IsDoorwayCell(occupiedCell, map) ||
                    !occupiedCell.Standable(map))
                {
                    return false;
                }
            }

            if (!IsPlaceAvailableForCurrentOrNewJob(pawn, cell) ||
                !ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly))
            {
                return false;
            }

            return !GenSpawn.WouldWipeAnythingWith(
                cell,
                thing.Rotation,
                thing.def,
                map,
                other => IsHardPlacementBlocker(other));
        }

        private static bool IsPlaceAvailableForCurrentOrNewJob(Pawn pawn, IntVec3 cell)
        {
            if (pawn?.MapHeld == null || !cell.InBounds(pawn.MapHeld))
            {
                return false;
            }

            if (ForbidUtility.CaresAboutForbidden(pawn, false) &&
                (cell.IsForbidden(pawn) || !cell.InAllowedArea(pawn)))
            {
                return false;
            }

            if (pawn.MapHeld.physicalInteractionReservationManager.IsReserved(cell) &&
                !pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, cell))
            {
                return false;
            }

            if (pawn.Faction != null &&
                pawn.MapHeld.reservationManager.IsReserved(cell) &&
                !pawn.MapHeld.reservationManager.ReservedBy(cell, pawn, pawn.CurJob))
            {
                return false;
            }

            return true;
        }

        internal static bool CanInstallMovedThingAt(Thing thing, IntVec3 cell, Pawn pawn)
        {
            if (thing == null || pawn?.Map == null || !cell.InBounds(pawn.Map) || IsDoorwayCell(cell, pawn.Map))
            {
                return false;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(cell, thing.Rotation, thing.def.Size);
            foreach (IntVec3 occupiedCell in occupiedRect)
            {
                if (!occupiedCell.InBounds(pawn.Map) || !occupiedCell.Standable(pawn.Map))
                {
                    return false;
                }
            }

            return !GenSpawn.WouldWipeAnythingWith(
                cell,
                thing.Rotation,
                thing.def,
                pawn.Map,
                other => IsHardPlacementBlocker(other));
        }

        private static bool IsHardPlacementBlocker(Thing thing)
        {
            return thing is Pawn ||
                   thing is Blueprint ||
                   thing is Frame ||
                   thing?.def?.category == ThingCategory.Building;
        }

        internal static void MoveLooseItemsAside(Thing thingToInstall, IntVec3 cell, Map map)
        {
            if (thingToInstall == null || map == null)
            {
                return;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(cell, thingToInstall.Rotation, thingToInstall.def.Size);
            List<Thing> looseItems = occupiedRect
                .SelectMany(occupiedCell => occupiedCell.GetThingList(map))
                .Where(thing => thing.Spawned && thing.def.category == ThingCategory.Item)
                .Distinct()
                .ToList();

            foreach (Thing looseItem in looseItems)
            {
                IntVec3 originalCell = looseItem.Position;
                looseItem.DeSpawn();
                bool placed = GenPlace.TryPlaceThing(
                    looseItem,
                    originalCell,
                    map,
                    ThingPlaceMode.Near,
                    null,
                    candidate => !occupiedRect.Contains(candidate),
                    null,
                    20);
                if (!placed && !looseItem.Destroyed && !looseItem.Spawned)
                {
                    GenSpawn.Spawn(looseItem, originalCell, map, WipeMode.VanishOrMoveAside);
                }
            }
        }

        private static IEnumerable<Thing> StorageCandidates(Map map)
        {
            List<ThingDef> defs = XenoBuildingDefOf.XMT_OvomorphStorageEligible?.things;
            if (defs == null)
            {
                yield break;
            }

            foreach (ThingDef def in defs.Where(def => def != null))
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                {
                    yield return thing;
                }
            }
        }

        private static bool IsEligibleStorageSource(Thing thing, Pawn pawn, List<Zone_OvomorphStorage> zones)
        {
            if (thing == null || !thing.Spawned || thing.Map != pawn.Map || thing is not Building || !thing.def.Minifiable)
            {
                WarnUnsupportedStorageDef(thing?.def);
                return false;
            }

            if (thing is Ovomorph ovomorph && !ovomorph.Unhatched)
            {
                return false;
            }

            if (IsInsideStorageZone(thing, zones) || IsServingViableHost(thing) || !FeralJobUtility.IsThingAvailableForJobBy(pawn, thing))
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, thing, PathEndMode.Touch, Danger.Deadly);
        }

        private static bool IsInsideStorageZone(Thing thing, List<Zone_OvomorphStorage> zones)
        {
            foreach (Zone_OvomorphStorage zone in zones)
            {
                if (GenAdj.CellsOccupiedBy(thing).All(zone.ContainsCell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsServingViableHost(Thing thing)
        {
            return GenRadial.RadialDistinctThingsAround(thing.Position, thing.Map, 1.5f, true)
                .OfType<Pawn>()
                .Any(XMTUtility.IsAcceptableHost);
        }

        private static void WarnUnsupportedStorageDef(ThingDef def)
        {
            if (def != null && warnedUnsupportedStorageDefs.Add(def))
            {
                Log.Warning("[XMT] " + def.defName + " is listed for ovomorph storage but is not an installed minifiable building; it will be skipped.");
            }
        }

        private static bool CanUsePlayerZones(Pawn pawn)
        {
            return pawn?.Map != null && pawn.Faction?.IsPlayer == true && XMTUtility.QueenIsPlayer();
        }

        private static bool CanHaulToOvomorphStorage(Pawn pawn)
        {
            return CanUsePlayerZones(pawn) &&
                   pawn.workSettings != null &&
                   pawn.workSettings.EverWork &&
                   pawn.workSettings.WorkIsActive(XenoWorkDefOf.Hauling);
        }

        private static List<Zone_HostPlacement> HostZones(Map map)
        {
            return map.zoneManager.AllZones.OfType<Zone_HostPlacement>().ToList();
        }

        private static List<Zone_OvomorphStorage> StorageZones(Map map)
        {
            return map.zoneManager.AllZones.OfType<Zone_OvomorphStorage>().ToList();
        }

        private static void Warn(Map map, string reason, string translationKey, bool playerOrdered)
        {
            if (map == null)
            {
                return;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            string warningKey = map.uniqueID + ":" + reason;
            if (!playerOrdered &&
                nextWarningTick.TryGetValue(warningKey, out int allowedTick) &&
                ticksGame < allowedTick)
            {
                return;
            }

            nextWarningTick[warningKey] = ticksGame + WarningCooldownTicks;
            Messages.Message(translationKey.Translate(), MessageTypeDefOf.NegativeEvent, false);
        }
    }
}
