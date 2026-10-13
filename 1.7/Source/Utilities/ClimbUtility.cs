
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;

namespace Xenomorphtype
{
    internal sealed class ClimbTopologyCache
    {
        public int RoofRevision;
        public int RegionRevision;
        public readonly Dictionary<Room, IntVec3> OpenRoofAnchorByRoom = new Dictionary<Room, IntVec3>();
        public readonly Dictionary<Region, bool> ReachableOpenRoofByRegion = new Dictionary<Region, bool>();

        public void Clear()
        {
            OpenRoofAnchorByRoom.Clear();
            ReachableOpenRoofByRegion.Clear();
            RoofRevision++;
            RegionRevision++;
        }

        public void InvalidateRegions()
        {
            OpenRoofAnchorByRoom.Clear();
            ReachableOpenRoofByRegion.Clear();
            RegionRevision++;
        }

        public void InvalidateRoof(Room room)
        {
            if (room != null)
            {
                OpenRoofAnchorByRoom.Remove(room);
            }
            ReachableOpenRoofByRegion.Clear();
            RoofRevision++;
        }
    }

    public class ClimbUtility
    {
        public enum ClimbDecision
        {
            None,
            Required,
            Preferred
        }

        public const int PreferredClimbDistrictThreshold = 3;
        private const int MaxFallbackRecoveryAttempts = 3;
        private static readonly Dictionary<string, int> fallbackRecoveryAttempts = new Dictionary<string, int>();
        private sealed class ClimbToilRegistration
        {
            public Action WrappedInitAction;
        }

        private static readonly ConditionalWeakTable<Toil, ClimbToilRegistration> climbSupportedToils =
            new ConditionalWeakTable<Toil, ClimbToilRegistration>();

        private static ClimbTopologyCache GetTopologyCache(Map map)
        {
            return TraversalTopologyMapComponent.For(map)?.ClimbCache;
        }

        internal static void NotifyRegionsRoomsChanged(Map map)
        {
            GetTopologyCache(map)?.InvalidateRegions();
        }

        internal static void NotifyRoofChanged(Map map, IntVec3 cell)
        {
            GetTopologyCache(map)?.InvalidateRoof(cell.InBounds(map) ? cell.GetRoom(map) : null);
        }

        internal static bool TryGetOpenRoofAnchor(Map map, Region region, out IntVec3 anchor)
        {
            anchor = IntVec3.Invalid;
            Room room = region?.Room;
            if (map == null || room == null || room.OpenRoofCount <= 0)
            {
                return false;
            }

            ClimbTopologyCache cache = GetTopologyCache(map);
            if (cache?.OpenRoofAnchorByRoom.TryGetValue(room, out anchor) == true)
            {
                if (!anchor.IsValid)
                {
                    return false;
                }
                if (anchor.InBounds(map) && anchor.Standable(map) && !anchor.Roofed(map))
                {
                    return true;
                }
                cache.OpenRoofAnchorByRoom.Remove(room);
            }

            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.InBounds(map) && cell.Standable(map) && !cell.Roofed(map))
                {
                    anchor = cell;
                    break;
                }
            }
            if (!anchor.IsValid)
            {
                if (cache != null)
                {
                    cache.OpenRoofAnchorByRoom[room] = IntVec3.Invalid;
                }
                return false;
            }
            if (cache != null)
            {
                cache.OpenRoofAnchorByRoom[room] = anchor;
            }
            return true;
        }

        internal static bool RegionCanReachOpenRoof(Map map, Region region)
        {
            if (map == null || region == null)
            {
                return false;
            }
            ClimbTopologyCache cache = GetTopologyCache(map);
            if (cache == null)
            {
                return false;
            }
            if (cache.ReachableOpenRoofByRegion.TryGetValue(region, out bool cached))
            {
                return cached;
            }

            TraverseParms permissiveParms = TraverseParms.For(TraverseMode.PassDoors);
            HashSet<Region> visited = new HashSet<Region>();
            bool reachedOpenRoof = false;
            RegionEntryPredicate entryCondition = (Region from, Region next) => next.Allows(permissiveParms, isDestination: false);
            RegionProcessor processor = delegate (Region current)
            {
                visited.Add(current);
                reachedOpenRoof = TryGetOpenRoofAnchor(map, current, out _);
                return reachedOpenRoof;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, processor, 99999);
            foreach (Region visitedRegion in visited)
            {
                cache.ReachableOpenRoofByRegion[visitedRegion] = reachedOpenRoof;
            }
            return reachedOpenRoof;
        }

        internal static void WarmTopologyFor(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Spawned || pawn.Map.regionDirtyer.AnyDirty)
            {
                return;
            }
            Region region = pawn.Position.GetRegion(pawn.Map, RegionType.Set_Passable);
            if (region != null)
            {
                RegionCanReachOpenRoof(pawn.Map, region);
            }
        }

        internal static string TopologyCacheReport(Map map)
        {
            ClimbTopologyCache cache = GetTopologyCache(map);
            return cache == null ? "no climb topology cache" :
                "roofRevision=" + cache.RoofRevision + ", regionRevision=" + cache.RegionRevision +
                ", cachedRooms=" + cache.OpenRoofAnchorByRoom.Count +
                ", cachedRegionResults=" + cache.ReachableOpenRoofByRegion.Count;
        }

        public static bool HasClimbSupport(Toil toil)
        {
            return toil != null &&
                climbSupportedToils.TryGetValue(toil, out ClimbToilRegistration registration) &&
                ReferenceEquals(toil.initAction, registration.WrappedInitAction);
        }

        private static bool RegisterClimbSupport(Toil toil)
        {
            if (toil == null)
            {
                return false;
            }

            if (climbSupportedToils.TryGetValue(toil, out ClimbToilRegistration registration))
            {
                if (ReferenceEquals(toil.initAction, registration.WrappedInitAction))
                {
                    return false;
                }

                // Toils are pooled. A marker from an earlier use must not prevent the
                // newly assigned vanilla init action from receiving climb support.
                climbSupportedToils.Remove(toil);
            }

            climbSupportedToils.Add(toil, new ClimbToilRegistration());
            return true;
        }

        private static void FinishClimbSupportRegistration(Toil toil)
        {
            if (toil != null && climbSupportedToils.TryGetValue(toil, out ClimbToilRegistration registration))
            {
                registration.WrappedInitAction = toil.initAction;
            }
        }

        internal static void NotifyToilCleared(Toil toil)
        {
            if (toil != null)
            {
                climbSupportedToils.Remove(toil);
            }
        }

        public struct ClimbParameters
        {
            public bool ClimbCellsRegistered => TraversalLegs != null && TraversalLegs.Count > 0 &&
                TraversalLegs.All(leg => leg != null && leg.start.IsValid && leg.end.IsValid);

            public IntVec3 FinalGoalCell;
            public LocalTargetInfo FinalGoalTarget;
            public bool NoWallToClimb ;
            public bool Tunneling;

            public List<TraversalLeg> TraversalLegs;
            // Retained as save migration fields for climb routes created before typed legs.
            public List<IntVec3> ClimbStarts;
            public List<IntVec3> ClimbEnds;
        }

        protected static bool ShouldClimbTo(Pawn actor, TargetIndex ind)
        {
            if (actor == null)
            {
                return false;
            }

            LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
            if (actor.Map == null || !dest.IsValid || !dest.Cell.InBounds(actor.Map))
            {
                return false;
            }

            if (dest.Cell.GetRoomOrAdjacent(actor.Map) == actor.GetRoom())
            {
                return false;
            }

            bool canReach = actor.Map.reachability.CanReach(actor.Position, dest.Cell, PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.PassDoors));

            if (!canReach)
            {
                return true;
            }

            return false;
        }

        public static bool OriginalCanReach(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (!pawn.Spawned)
            {
                return false;
            }


            return pawn.Map.reachability.CanReach(pawn.Position, dest, peMode, TraverseParms.For(pawn, maxDanger, mode, canBashDoors, alwaysUseAvoidGrid: false, canBashFences));

        }

        private static bool CanReachFrom(Pawn pawn, IntVec3 start, LocalTargetInfo dest, PathEndMode peMode)
        {
            if (pawn?.Map == null || !start.IsValid || !start.InBounds(pawn.Map) || !dest.IsValid || !dest.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            return pawn.Map.reachability.CanReach(start, dest, peMode,
                TraverseParms.For(pawn, pawn.NormalMaxDanger(), TraverseMode.ByPawn, canBashDoors: false, alwaysUseAvoidGrid: false, canBashFences: false));
        }

        private static bool ValidateClimbRoute(Pawn pawn, CompClimber climber, PathEndMode finalPathEndMode)
        {
            if (pawn?.Map == null || climber == null)
            {
                return false;
            }

            List<TraversalLeg> legs = climber.climbParameters.TraversalLegs;
            if (legs == null || legs.Count == 0)
            {
                return false;
            }

            IntVec3 approachFrom = pawn.Position;
            for (int i = 0; i < legs.Count; i++)
            {
                TraversalLeg leg = legs[i];
                IntVec3 start = leg.start;
                IntVec3 end = leg.end;
                bool validCells = start.IsValid && end.IsValid && start.InBounds(pawn.Map) && end.InBounds(pawn.Map);
                bool startWalkable = validCells && start.Walkable(pawn.Map);
                bool endWalkable = validCells && end.Walkable(pawn.Map);
                bool endpointsValid = leg.IsInfiltration
                    ? InfiltrationUtility.ValidateTraversalLeg(pawn, leg)
                    : validCells && !start.Roofed(pawn.Map) && !end.Roofed(pawn.Map);
                bool approachReachable = validCells && CanReachFrom(pawn, approachFrom, start, PathEndMode.OnCell);
                if (!validCells || !startWalkable || !endWalkable || !endpointsValid || !approachReachable)
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + pawn + " rejected traversal route step " + (i + 1) + "/" + legs.Count + " " + leg.type +
                            ": approach=" + approachFrom + ", start=" + start + ", end=" + end +
                            ", validCells=" + validCells + ", startWalkable=" + startWalkable +
                            ", endWalkable=" + endWalkable + ", endpointsValid=" + endpointsValid +
                            ", approachReachable=" + approachReachable);
                    }
                    return false;
                }

                approachFrom = end;
            }

            bool finalReachable = CanReachFrom(pawn, approachFrom, climber.climbParameters.FinalGoalTarget, finalPathEndMode);
            if (!finalReachable && XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " rejected climb route because final landing " + approachFrom +
                    " cannot reach " + climber.climbParameters.FinalGoalTarget + " with " + finalPathEndMode);
            }
            return finalReachable;
        }

        private static void LogClimbRoute(Pawn pawn, CompClimber climber, PathEndMode finalPathEndMode)
        {
            if (!XMTSettings.LogClimbing || pawn?.Map == null || climber == null)
            {
                return;
            }

            List<TraversalLeg> legs = climber.climbParameters.TraversalLegs;
            if (legs == null)
            {
                Log.Message("[XMT][Climbing] " + pawn + " generated an invalid traversal route list.");
                return;
            }

            Log.Message("[XMT][Climbing] " + pawn + " generated " + legs.Count + " traversal step(s) from " + pawn.Position + " to " + climber.climbParameters.FinalGoalTarget + " with final mode " + finalPathEndMode + " for " + pawn.jobs?.curJob);
            for (int i = 0; i < legs.Count; i++)
            {
                TraversalLeg leg = legs[i];
                IntVec3 approachFrom = i == 0 ? pawn.Position : legs[i - 1].end;
                LocalTargetInfo downstreamTarget = i == legs.Count - 1
                    ? climber.climbParameters.FinalGoalTarget
                    : new LocalTargetInfo(legs[i + 1].start);
                PathEndMode downstreamMode = i == legs.Count - 1 ? finalPathEndMode : PathEndMode.OnCell;
                bool approachReachable = CanReachFrom(pawn, approachFrom, leg.start, PathEndMode.OnCell);
                bool downstreamReachable = CanReachFrom(pawn, leg.end, downstreamTarget, downstreamMode);
                bool plannedLandingWalkable = leg.end.InBounds(pawn.Map) && leg.end.Walkable(pawn.Map);

                Log.Message("[XMT][Climbing] " + pawn + " traversal route step " + (i + 1) + "/" + legs.Count + " " + leg.type +
                    ": approach " + approachFrom + " -> " + leg.start + " OnCell reachable=" + approachReachable +
                    "; traverse " + leg.start + " -> " + leg.end + " landingWalkable=" + plannedLandingWalkable +
                    "; downstream " + leg.end + " -> " + downstreamTarget + " with " + downstreamMode + " reachable=" + downstreamReachable);
            }
        }

        private static bool CanAttemptWallClimb(Pawn pawn, LocalTargetInfo dest)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.GetClimberComp() == null ||
                !dest.IsValid || !dest.Cell.InBounds(pawn.Map) || pawn.jobs?.curDriver is JobDriver_EnterPortal)
            {
                return false;
            }

            Room destRoom = dest.Cell.GetRoom(pawn.Map);
            Room pawnRoom = pawn.GetRoom();
            if (pawnRoom == destRoom)
            {
                return pawnRoom != null && pawnRoom.OpenRoofCount > 0;
            }

            if (destRoom == null || pawnRoom == null)
            {
                return false;
            }

            return destRoom.OpenRoofCount > 0 && pawnRoom.OpenRoofCount > 0;
        }

        private static void AddTargetDistricts(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms, HashSet<District> targetDistricts)
        {
            TargetInfo resolvedTarget = GenPath.ResolvePathMode(pawn, dest.ToTargetInfo(pawn.Map), ref peMode);
            LocalTargetInfo resolvedLocalTarget = resolvedTarget.HasThing
                ? new LocalTargetInfo(resolvedTarget.Thing)
                : new LocalTargetInfo(resolvedTarget.Cell);

            List<Region> targetRegions = new List<Region>();
            if (peMode == PathEndMode.OnCell)
            {
                Region region = resolvedLocalTarget.Cell.GetRegion(pawn.Map, RegionType.Set_Passable);
                if (region != null && region.Allows(traverseParms, isDestination: true))
                {
                    targetRegions.Add(region);
                }
            }
            else
            {
                TouchPathEndModeUtility.AddAllowedAdjacentRegions(resolvedLocalTarget, traverseParms, pawn.Map, targetRegions);
            }

            foreach (Region region in targetRegions)
            {
                if (region?.District != null && region.District.Passable)
                {
                    targetDistricts.Add(region.District);
                }
            }
        }

        private static bool TryGetDistrictDistance(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParms, out int districtDistance)
        {
            districtDistance = -1;

            District startDistrict = pawn.GetRegion()?.District;
            if (startDistrict == null || !startDistrict.Passable)
            {
                return false;
            }

            HashSet<District> targetDistricts = new HashSet<District>();
            AddTargetDistricts(pawn, dest, peMode, traverseParms, targetDistricts);
            if (targetDistricts.Count == 0)
            {
                return false;
            }

            Queue<District> open = new Queue<District>();
            Dictionary<District, int> distances = new Dictionary<District, int>();
            open.Enqueue(startDistrict);
            distances.Add(startDistrict, 0);

            while (open.Count > 0)
            {
                District current = open.Dequeue();
                int currentDistance = distances[current];
                if (targetDistricts.Contains(current))
                {
                    districtDistance = currentDistance;
                    return true;
                }

                foreach (District neighbor in current.Neighbors)
                {
                    if (neighbor == null)
                    {
                        continue;
                    }

                    if (!neighbor.Passable)
                    {
                        continue;
                    }

                    if (!distances.ContainsKey(neighbor))
                    {
                        distances.Add(neighbor, currentDistance + 1);
                        open.Enqueue(neighbor);
                    }
                }
            }

            return false;
        }

        public static ClimbDecision GetClimbDecision(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (!CanAttemptWallClimb(pawn, dest))
            {
                return ClimbDecision.None;
            }

            bool normalReach = OriginalCanReach(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode);
            if (!normalReach)
            {
                return ClimbDecision.Required;
            }

            TraverseParms traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors, alwaysUseAvoidGrid: false, canBashFences);
            bool hasDistrictRoute = TryGetDistrictDistance(pawn, dest, peMode, traverseParms, out int districtDistance);
            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " climb preference check for " + dest + " with " + peMode +
                    ": districtDistance=" + (hasDistrictRoute ? districtDistance.ToString() : "unavailable") +
                    ", threshold=" + PreferredClimbDistrictThreshold);
            }
            if (hasDistrictRoute && districtDistance >= PreferredClimbDistrictThreshold)
            {
                return ClimbDecision.Preferred;
            }

            return ClimbDecision.None;
        }

        public static string GetClimbDecisionReport(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !dest.IsValid || !dest.Cell.InBounds(pawn.Map))
            {
                return "invalid pawn or target";
            }

            Danger maxDanger = pawn.NormalMaxDanger();
            ClimbDecision decision = GetClimbDecision(pawn, dest, peMode, maxDanger);
            TraverseParms traverseParms = TraverseParms.For(pawn, maxDanger, TraverseMode.ByPawn, canBashDoors: false, alwaysUseAvoidGrid: false, canBashFences: false);
            bool hasDistrictRoute = TryGetDistrictDistance(pawn, dest, peMode, traverseParms, out int districtDistance);
            return "decision=" + decision +
                ", districtDistance=" + (hasDistrictRoute ? districtDistance.ToString() : "unavailable") +
                ", threshold=" + PreferredClimbDistrictThreshold +
                ", normalReach=" + OriginalCanReach(pawn, dest, peMode, maxDanger);
        }

        public static bool CanReachByClimb(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            return GetClimbDecision(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode) == ClimbDecision.Required;
        }

        public static bool CanReachByWalkingOrClimb(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !dest.IsValid || !dest.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            return OriginalCanReach(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode) ||
                   CanReachByClimb(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode) ||
                   TraversalRouteUtility.CanReach(pawn, dest, peMode, maxDanger, mode, allowWallClimb: true);
        }

        internal static bool CanReachBySpecialTraversal(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode,
            Danger maxDanger, TraverseMode mode = TraverseMode.ByPawn)
        {
            return GetClimbDecision(pawn, destination, pathEndMode, maxDanger, mode: mode) != ClimbDecision.None ||
                TraversalRouteUtility.CanReach(pawn, destination, pathEndMode, maxDanger, mode, allowWallClimb: true);
        }

        internal static bool TryBuildTraversalRoute(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode,
            Danger maxDanger, out List<TraversalLeg> legs)
        {
            if (TryBuildDirectWallRoute(pawn, destination, pathEndMode, maxDanger, out legs, out _))
            {
                return true;
            }
            return TraversalRouteUtility.TryBuildRoute(pawn, destination, pathEndMode, maxDanger, allowWallClimb: true, out legs);
        }

        private static bool TryBuildDirectWallRoute(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode,
            Danger maxDanger, out List<TraversalLeg> legs, out ClimbDecision decision)
        {
            legs = new List<TraversalLeg>();
            decision = GetClimbDecision(pawn, destination, pathEndMode, maxDanger);
            if (decision == ClimbDecision.None)
            {
                return false;
            }

            PathEndMode resolvedPathEndMode = pathEndMode;
            TargetInfo resolvedTarget = GenPath.ResolvePathMode(pawn, destination.ToTargetInfo(pawn.Map), ref resolvedPathEndMode);
            GetClimbCells(pawn, resolvedTarget.Cell, decision == ClimbDecision.Required, out List<IntVec3> starts, out List<IntVec3> ends);
            for (int i = 0; i < Math.Min(starts.Count, ends.Count); i++)
            {
                legs.Add(TraversalLeg.WallClimb(starts[i], ends[i]));
            }
            return legs.Count > 0;
        }

        public static bool CanReachByWalkingOrExecutableClimb(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !dest.IsValid || !dest.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            if (OriginalCanReach(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode))
            {
                return true;
            }

            CompClimber climber = pawn.GetClimberComp();
            if (climber == null)
            {
                return false;
            }

            bool canExecute = GetClimbParameters(pawn, dest, peMode, ref climber);
            climber.ClearClimberData();
            return canExecute;
        }

        protected static bool ShouldClimbTo(Pawn actor, IntVec3 cell)
        {
            if (actor == null)
            {
                return false;
            }

            if (actor.Map == null || !cell.InBounds(actor.Map))
            {
                return false;
            }

            if(actor.jobs.curDriver is JobDriver_EnterPortal)
            {
                return false;
            }

            if (cell.GetRoomOrAdjacent(actor.Map) == actor.GetRoom())
            {
                return false;
            }

            bool canReach = OriginalCanReach(actor, cell, PathEndMode.ClosestTouch, Danger.Some); 

            if (!canReach)
            {
                return true;
            }

            return false;
        }

        protected static void BeginClimb(Pawn actor, ref CompClimber climber, Toil toil)
        {
            IntVec3 position = actor.Position;
            IntVec3 cell = climber.EndClimbCell;
            Map map = actor.Map;
            TraversalLeg leg = climber.CurrentTraversalLeg;
            bool infiltration = leg?.IsInfiltration ?? false;

            if (infiltration && (position != climber.StartClimbCell || !InfiltrationUtility.ValidateTraversalLeg(actor, leg)))
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + actor + " rejected stale infiltration step " + leg + " from actual position " + position + ".");
                }
                actor.pather.StopDead();
                climber.ClearClimberData();
                actor.GetMorphComp()?.NotifyPathFailure(cell, actor.jobs?.curJob);
                actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }

            bool tunneling = infiltration || climber.climbParameters.Tunneling;

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + actor + " beginning climb from actual position " + position +
                    "; planned start=" + climber.StartClimbCell + "; planned end=" + cell +
                    "; start matched=" + (position == climber.StartClimbCell) +
                    "; end walkable=" + (cell.InBounds(map) && cell.Walkable(map)) +
                    "; final target=" + climber.climbParameters.FinalGoalTarget +
                    "; traversalType=" + leg?.type + "; tunneling=" + tunneling + "; job=" + actor.jobs?.curJob);
            }

            bool isSelected = Find.Selector.IsSelected(actor);

            climber.pawnClimber = tunneling ? PawnClimber.MakeClimber(InternalDefOf.PawnClimbUnder, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.StartClimbCell.ToVector3(), null, climber.EndClimbCell)
            : PawnClimber.MakeClimber(InternalDefOf.PawnClimber, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.StartClimbCell.ToVector3(), null, climber.EndClimbCell);

            if (climber.pawnClimber != null)
            {
                climber.pawnClimber.underground = tunneling;
                climber.pawnClimber.strictDestination = infiltration;
                GenSpawn.Spawn(climber.pawnClimber, cell, map);

                climber.startedClimb = true;
                if (isSelected)
                {
                    Find.Selector.Select(actor, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }

        protected static void InitClimbAction(Pawn actor, ref CompClimber climber, Toil toil)
        {
            ClearFallbackRecovery(actor);

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + actor + " preparing climb approach from " + actor.Position + " to " + climber.StartClimbCell +
                    " with OnCell reachable=" + CanReachFrom(actor, actor.Position, climber.StartClimbCell, PathEndMode.OnCell) +
                    "; planned climb end=" + climber.EndClimbCell + "; final target=" + climber.climbParameters.FinalGoalTarget +
                    " for " + actor.jobs?.curJob);
            }

            if (actor.Position == climber.StartClimbCell)
            {
                BeginClimb(actor, ref climber, toil);
                return;
            }

            actor.pather.StartPath(climber.StartClimbCell, PathEndMode.OnCell);
        }

        private static string FallbackRecoveryKey(Pawn actor, LocalTargetInfo target, PathEndMode peMode)
        {
            return actor.thingIDNumber + "|" + (actor.jobs?.curJob?.GetHashCode() ?? 0) + "|" + target.Cell + "|" + peMode;
        }

        private static void ClearFallbackRecovery(Pawn actor)
        {
            if (actor == null || fallbackRecoveryAttempts.Count == 0)
            {
                return;
            }

            string prefix = actor.thingIDNumber + "|";
            foreach (string key in fallbackRecoveryAttempts.Keys.Where(key => key.StartsWith(prefix)).ToList())
            {
                fallbackRecoveryAttempts.Remove(key);
            }
        }

        protected static void TickClimbIntervalAction(int interval, Pawn actor, ref CompClimber climber, Toil toil, PathEndMode peMode = PathEndMode.OnCell)
        {
            if (!IsCurrentOwnedClimbToil(toil, actor, climber))
            {
                return;
            }

            if (!actor.Spawned)
            {
                return;
            }

            if (climber.climbing)
            {
                return;
            }

            if (!actor.pather.MovingNow &&
                (actor.pather.curPath == null || actor.pather.curPath.Finished))
            {
                if (climber.startedClimb && !climber.finishedClimb)
                {
                    IntVec3 plannedClimbEnd = climber.EndClimbCell;
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " restored after climb at " + actor.Position + "; planned landing=" + plannedClimbEnd +
                            "; planned landing matched=" + (actor.Position == plannedClimbEnd) + "; current job=" + actor.jobs?.curJob);
                    }

                    climber.finishedClimb = true;

                    if (climber.lastClimb)
                    {
                        if (XMTSettings.LogClimbing)
                        {
                            Log.Message("[XMT][Climbing] " + actor + " requesting final post-climb path from " + actor.Position + " to " + climber.climbParameters.FinalGoalTarget +
                                " with " + peMode + " reachable=" + OriginalCanReach(actor, climber.climbParameters.FinalGoalTarget, peMode, actor.NormalMaxDanger()) +
                                " for " + actor.jobs?.curJob);
                        }
                        actor.pather.StartPath(climber.climbParameters.FinalGoalTarget, peMode);
                    }
                    else
                    {
                        climber.startedClimb = false;
                        climber.finishedClimb = false;
                        if (XMTSettings.LogClimbing)
                        {
                            Log.Message("[XMT][Climbing] " + actor + " requesting next climb approach from " + actor.Position + " to " + climber.StartClimbCell +
                                " with OnCell reachable=" + CanReachFrom(actor, actor.Position, climber.StartClimbCell, PathEndMode.OnCell) +
                                " for " + actor.jobs?.curJob);
                        }
                        actor.pather.StartPath(climber.StartClimbCell, PathEndMode.OnCell);
                    }
                    return;
                }
            
                if (climber.lastClimb)
                {
                    if (HasArrived(actor, climber.climbParameters.FinalGoalTarget, peMode))
                    {
                        TryAdvanceOwnedClimbToil(toil, actor, climber);
                    }
                    else
                    {
                        TryStartFallbackPathOrEndJob(actor, climber.climbParameters.FinalGoalTarget, peMode);
                    }
                }
                else if(!climber.startedClimb)
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " is checking climb approach arrival; position=" + actor.Position + " start=" + climber.StartClimbCell +
                            " moving=" + actor.pather.Moving + " movingNow=" + actor.pather.MovingNow +
                            " curPathNull=" + (actor.pather.curPath == null) + " curPathFinished=" + (actor.pather.curPath?.Finished ?? true) +
                            " for " + actor.jobs?.curJob);
                    }
                   
                    bool arrivedAtTraversalStart = actor.Position == climber.StartClimbCell;
                    if (arrivedAtTraversalStart)
                    {
                        BeginClimb(actor, ref climber, toil);
                    }
                    else
                    {
                        TryStartFallbackPathOrEndJob(actor, climber.StartClimbCell, PathEndMode.OnCell);
                    }
                }
            }

        }

        protected static bool FailClimbToil(Toil toil)
        {
            return false;
            if(toil.actor == null)
            {
                return true;
            }

            CompClimber climber = toil.actor.GetClimberComp();

            if (climber == null)
            {
                return false;
            }

            if (climber.climbParameters.ClimbCellsRegistered)
            {
                if (climber.EndClimbCell.IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasArrived(Pawn actor, IntVec3 cell, PathEndMode peMode)
        {
            return HasArrived(actor, new LocalTargetInfo(cell), peMode);
        }

        private static bool HasArrived(Pawn actor, LocalTargetInfo target, PathEndMode peMode)
        {
            if (actor == null || !target.IsValid)
            {
                return false;
            }

            return actor.Spawned && actor.CanReachImmediate(target, peMode);
        }

        private static bool IsCurrentOwnedClimbToil(Toil toil, Pawn actor, CompClimber climber)
        {
            Job job = actor?.jobs?.curJob;
            JobDriver driver = actor?.jobs?.curDriver;
            return toil != null && actor != null && climber != null && job != null && driver != null &&
                climber.HasActiveClimbToilFor(job, toil);
        }

        private static void ClaimClimbToil(Toil toil, Pawn actor, CompClimber climber)
        {
            if (toil == null || actor?.jobs?.curJob == null || climber == null)
            {
                return;
            }

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            climber.MarkClimbToilActive(actor.jobs.curJob, toil);
        }

        private static void ReturnToVanillaToil(Toil toil, Action vanillaInitAction, ToilCompleteMode vanillaCompleteMode,
            Pawn actor, CompClimber climber)
        {
            climber?.ClearClimberData();
            ClearFallbackRecovery(actor);
            if (toil != null)
            {
                toil.defaultCompleteMode = vanillaCompleteMode;
            }

            if (XMTSettings.LogClimbing && actor != null)
            {
                Log.Message("[XMT][Climbing] " + actor + " returned wrapped movement toil " +
                    (toil?.debugName ?? "<null>") + " to vanilla completion; job=" + actor.jobs?.curJob + ".");
            }

            vanillaInitAction?.Invoke();
        }

        private static bool TryAdvanceOwnedClimbToil(Toil toil, Pawn actor, CompClimber climber)
        {
            if (!IsCurrentOwnedClimbToil(toil, actor, climber))
            {
                return false;
            }

            Job job = actor.jobs.curJob;
            JobDriver driver = actor.jobs.curDriver;
            climber.ClearClimberData();
            ClearFallbackRecovery(actor);

            if (actor.jobs?.curJob != job || actor.jobs?.curDriver != driver)
            {
                return false;
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + actor + " safely advancing owned traversal toil " + toil.debugName +
                    " for " + job + ".");
            }
            driver.ReadyForNextToil();
            return true;
        }

        private static void TickManualPatherArrival(Toil toil, LocalTargetInfo target, PathEndMode peMode)
        {
            Pawn actor = toil.actor;
            if (!IsCurrentOwnedClimbToil(toil, actor, actor?.GetClimberComp()))
            {
                return;
            }

            CompClimber climber = actor.GetClimberComp();
            if (climber != null && climber.climbParameters.ClimbCellsRegistered)
            {
                return;
            }

            if (HasArrived(actor, target, peMode))
            {
                TryAdvanceOwnedClimbToil(toil, actor, climber);
                return;
            }

            if (!actor.pather.MovingNow && (actor.pather.curPath == null || actor.pather.curPath.Finished))
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + actor + " reports as having not arrived at " + target + " with " + peMode + " for " + actor.jobs?.curJob);
                }
                TryStartFallbackPathOrEndJob(actor, target, peMode);
            }
        }

        private static void TickManualPatherArrival(Toil toil, IntVec3 targetCell, PathEndMode peMode)
        {
            TickManualPatherArrival(toil, new LocalTargetInfo(targetCell), peMode);
        }

        private static bool TryStartFallbackPathOrEndJob(Pawn actor, LocalTargetInfo target, PathEndMode peMode)
        {
            if (actor == null)
            {
                return false;
            }

            if (OriginalCanReach(actor, target, peMode, actor.NormalMaxDanger()))
            {
                string key = FallbackRecoveryKey(actor, target, peMode);
                fallbackRecoveryAttempts.TryGetValue(key, out int attempts);
                attempts++;
                fallbackRecoveryAttempts[key] = attempts;

                if (attempts > MaxFallbackRecoveryAttempts)
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " ending climb fallback as incompletable after repeated idle recovery attempts to " + target + " with " + peMode + " for " + actor.jobs?.curJob);
                    }
                    actor.pather.StopDead();
                    actor.GetMorphComp()?.NotifyPathFailure(target, actor.jobs?.curJob);
                    actor.GetClimberComp()?.ClearClimberData();
                    ClearFallbackRecovery(actor);
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return false;
                }

                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + actor + " requesting climb fallback path to " + target + " with " + peMode + " after the pather became idle for " + actor.jobs?.curJob + " attempt " + attempts);
                }

                actor.pather.StartPath(target, peMode);
                return true;
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + actor + " ending climb fallback as incompletable; cannot reach " + target + " with " + peMode + " for " + actor.jobs?.curJob);
            }
            actor.pather.StopDead();
            actor.GetMorphComp()?.NotifyPathFailure(target, actor.jobs?.curJob);
            actor.GetClimberComp()?.ClearClimberData();
            ClearFallbackRecovery(actor);
            actor.jobs.EndCurrentJob(JobCondition.Incompletable);
            return false;
        }

        private static bool TryBeginClimberToil(Toil toil, Action vanillaInitAction, out Pawn actor, out CompClimber climber)
        {
            actor = toil?.actor;
            climber = actor?.GetClimberComp();
            if (climber == null)
            {
                if (XMTSettings.LogClimbing && actor != null && XMTUtility.IsXenomorph(actor))
                {
                    Log.Warning("[XMT][Climbing] " + actor + " entered climb-supported toil " +
                        (toil.debugName ?? "<null>") + " without a climber component; using vanilla movement for " +
                        actor.jobs?.curJob + ".");
                }
                vanillaInitAction?.Invoke();
                return false;
            }

            return true;
        }

        public static void AddClimbSupport(Toil toil, TargetIndex ind, PathEndMode peMode)
        {
            if (!RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                LocalTargetInfo target = actor.jobs.curJob.GetTarget(ind);
                if (HasArrived(actor, target, peMode))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " already arrived at  " + target);
                    }
                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }
                if (!GetClimbParameters(actor, target, peMode, ref climber))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " could not get climb parameters in TargetIndex GotoCell Init Action");
                    }

                    if (HasArrived(actor, target, peMode))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }

                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, target, peMode);
                    return;
                }
                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    TickManualPatherArrival(toil, toil.actor.jobs.curJob.GetTarget(ind), peMode);
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil, peMode);
            });
        }

        public static void AddClimbSupport(Toil toil, Func<Pawn, LocalTargetInfo> targetResolver, PathEndMode peMode)
        {
            if (targetResolver == null || !RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                LocalTargetInfo target = targetResolver(actor);
                if (!target.IsValid)
                {
                    climber.ClearClimberData();
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                if (HasArrived(actor, target, peMode))
                {
                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }
                if (!GetClimbParameters(actor, target, peMode, ref climber))
                {
                    if (HasArrived(actor, target, peMode))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }
                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, target, peMode);
                    return;
                }
                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = actor?.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }

                LocalTargetInfo target = targetResolver(actor);
                if (!target.IsValid)
                {
                    climber.ClearClimberData();
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    TickManualPatherArrival(toil, target, peMode);
                    return;
                }
                TickClimbIntervalAction(interval, actor, ref climber, toil, peMode);
            });
        }

        public static void AddClimbSupport(Toil toil, IntVec3 cell, PathEndMode peMode)
        {
            if (!RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                if (HasArrived(actor, cell, peMode))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " already arrived at  " + cell);
                    }
                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }

                if (!GetClimbParameters(actor, cell, peMode, ref climber))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " could not get climb parameters in cell GotoCell Init Action");
                    }
                    if (HasArrived(actor, cell, peMode))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }
                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, cell, peMode);
                    return;
                }
                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    TickManualPatherArrival(toil, cell, peMode);
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil, peMode);
            });
        }

        public static void AddClimbSupport(Toil toil, TargetIndex ind, IntVec3 exactCell)
        {
            if (!RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;

                if (HasArrived(actor, exactCell, PathEndMode.OnCell))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " already arrived at  " + thing);
                    }

                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }

                if (!GetClimbParameters(actor, exactCell, PathEndMode.OnCell, ref climber))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " could not get climb parameters in dest.cell GotoCell Init Action");
                    }
                    if (HasArrived(actor, exactCell, PathEndMode.OnCell))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }
                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, exactCell, PathEndMode.OnCell);
                    return;
                }
                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);
            
            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    TickManualPatherArrival(toil, exactCell, PathEndMode.OnCell);
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil, PathEndMode.OnCell);
            });
        }

        public static void AddClimbSupport(Toil toil, TargetIndex ind, PathEndMode peMode, bool canGotoSpawnedParent)
        {
            if (!RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;

                if (thing != null && canGotoSpawnedParent)
                {
                    dest = thing.SpawnedParentOrMe;
                }

                if (HasArrived(actor, dest, peMode))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " already arrived at  " + thing);
                    }
                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }

                if (!GetClimbParameters(actor, dest, peMode, ref climber))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " could not get climb parameters in dest.cell GotoThing Init Action");
                    }
                    if (HasArrived(actor, dest, peMode))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }
                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, dest, peMode);
                    return;
                }
                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);
            
            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                    if (dest.Thing != null && canGotoSpawnedParent)
                    {
                        dest = dest.Thing.SpawnedParentOrMe;
                    }
                    TickManualPatherArrival(toil, dest, peMode);
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil, peMode);
            });
        }

        public static void AddCarryClimbSupport(Toil toil, TargetIndex ind, PathEndMode peMode)
        {
            if (!RegisterClimbSupport(toil))
            {
                return;
            }

            Action vanillaInitAction = toil.initAction;
            ToilCompleteMode vanillaCompleteMode = toil.defaultCompleteMode;
            toil.initAction = delegate
            {
                if (!TryBeginClimberToil(toil, vanillaInitAction, out Pawn actor, out CompClimber climber))
                {
                    return;
                }

                LocalTargetInfo target = actor.jobs.curJob.GetTarget(ind);
                if (HasArrived(actor, target, peMode))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " already arrived at  " + target);
                    }
                    actor.pather.StopDead();
                    ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                    return;
                }
                if (!GetClimbParameters(actor, target, peMode, ref climber))
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + actor + " could not get climb parameters in target.Cell CarryHauledThingToCell Init Action");
                    }
                    if (HasArrived(actor, target, peMode))
                    {
                        ReturnToVanillaToil(toil, vanillaInitAction, vanillaCompleteMode, actor, climber);
                        return;
                    }
                    ClaimClimbToil(toil, actor, climber);
                    TryStartFallbackPathOrEndJob(actor, target, peMode);
                    return;
                }

                ClaimClimbToil(toil, actor, climber);
                InitClimbAction(actor, ref climber, toil);
            };
            FinishClimbSupportRegistration(toil);

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null)
                {
                    return;
                }
                if (!IsCurrentOwnedClimbToil(toil, actor, climber))
                {
                    return;
                }
                if (!climber.climbParameters.ClimbCellsRegistered)
                {
                    TickManualPatherArrival(toil, toil.actor.jobs.curJob.GetTarget(ind), peMode);
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil, peMode);
            });
        }


        protected static bool GetClimbCells(Pawn pawn, IntVec3 goal, bool stopAtFirstReachableApproach, out List<IntVec3> climbStart, out List<IntVec3> climbEnd)
        {
            return GetClimbCells(pawn, pawn.Position, goal, stopAtFirstReachableApproach, out climbStart, out climbEnd);
        }

        private static bool GetClimbCells(Pawn pawn, IntVec3 start, IntVec3 goal, bool stopAtFirstReachableApproach, out List<IntVec3> climbStart, out List<IntVec3> climbEnd)
        {
            climbStart = new List<IntVec3>();
            climbEnd = new List<IntVec3>();

            Map map = pawn.Map;

            if (map == null || !start.InBounds(map) || !goal.InBounds(map))
            {
                return false;
            }

            List<IntVec3> line = GenSight.PointsOnLineOfSight(goal, start).ToList();

            bool wasBlocked = false;
            IntVec3 lastClear = IntVec3.Invalid;
            int i = 0;
            foreach (IntVec3 point in line)
            {
                if (!point.InBounds(map))
                {
                    i++;
                    continue;
                }

                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + pawn + " is checking " + point + ":" + i);
                }
                if (climbEnd.Count == 0)
                {
                    if(point.Roofed(map))
                    {
                       if( point.GetRoom(map) is Room destinationRoom)
                       {
                            if (XMTSettings.LogClimbing)
                            {
                                Log.Message("[XMT][Climbing] " + destinationRoom + " is the room of " + point + ":" + i);
                            }
                            if (destinationRoom.OpenRoofCount != 0)
                            {
                                if (XMTSettings.LogClimbing)
                                {
                                    Log.Message("[XMT][Climbing] " + destinationRoom + " room has open roof tiles " + point + ":" + i);
                                }

                                foreach (IntVec3 roomCell in destinationRoom.Cells)
                                {
                                    if (!roomCell.InBounds(map))
                                    {
                                        continue;
                                    }

                                    if (!roomCell.Roofed(map))
                                    {
                                        if (XMTSettings.LogClimbing)
                                        {
                                        Log.Message("[XMT][Climbing] " + pawn + " selected enclosed-goal climb end " + roomCell + " from " + destinationRoom + " at line index " + i);
                                        }
                                        climbEnd.Add(roomCell);
                                        wasBlocked = true;
                                        break;
                                    }
                                }
                            }
                       }
                    }
                }

                bool noLongerBlocked = false;
                bool walkable = point.Walkable(map);
               
                if (walkable && !point.Roofed(map))
                {
                    lastClear = point;
                    if (wasBlocked)
                    {
                        noLongerBlocked = true;
                    }
                }
                else if(!walkable)
                {
                    if (!point.CanBeSeenOverFast(map) && !wasBlocked)
                    {
                        if (lastClear.IsValid && lastClear.InBounds(map))
                        {
                            climbEnd.Add(lastClear);
                            if (XMTSettings.LogClimbing)
                            {
                                Log.Message("[XMT][Climbing] " + pawn + " selected line-side climb end " + lastClear + " before blocked cell " + point + " at line index " + i);
                            }
                            wasBlocked = true;
                        }
                    }
                }

                if (noLongerBlocked && climbEnd.Count > climbStart.Count)
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + pawn + " has found a start point" + point + ":" + i);
                    }
                    climbStart.Add(point);
                    wasBlocked = false;

                    if (stopAtFirstReachableApproach && CanReachFrom(pawn, start, point, PathEndMode.OnCell))
                    {
                        break;
                    }
                }

                i++;
            }

            if (climbEnd.Count > climbStart.Count && wasBlocked && start.Roofed(map))
            {
                if (start.GetRoom(map) is Room departureRoom)
                {
                    if (departureRoom.OpenRoofCount == 0)
                    {
                        return false;
                    }

                    foreach (IntVec3 roomCell in departureRoom.Cells)
                    {
                        if (!roomCell.InBounds(map))
                        {
                            continue;
                        }

                        if (!roomCell.Roofed(map))
                        {
                            if (XMTSettings.LogClimbing)
                            {
                                Log.Message("[XMT][Climbing] " + pawn + " selected enclosed-departure climb start " + roomCell + " from " + departureRoom + " at line index " + i);
                            }
                            climbStart.Add(roomCell);
                            break;
                        }
                    }
                }
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + climbEnd.Count + " ends and " + climbStart.Count + "starts");
            }

            climbStart.Reverse();
            climbEnd.Reverse();
            return climbStart.Count > 0 && climbEnd.Count > 0 && climbStart.Count == climbEnd.Count;
        }

        internal static bool TryBuildWallTraversalLegs(Pawn pawn, IntVec3 start, IntVec3 goal, out List<TraversalLeg> legs)
        {
            legs = new List<TraversalLeg>();
            if (pawn?.Map == null || !start.IsValid || !goal.IsValid || !start.InBounds(pawn.Map) || !goal.InBounds(pawn.Map) ||
                !start.Standable(pawn.Map) || !goal.Standable(pawn.Map) || start.Roofed(pawn.Map) || goal.Roofed(pawn.Map))
            {
                return false;
            }

            if (GetClimbCells(pawn, start, goal, stopAtFirstReachableApproach: false, out List<IntVec3> starts, out List<IntVec3> ends))
            {
                for (int i = 0; i < starts.Count; i++)
                {
                    legs.Add(TraversalLeg.WallClimb(starts[i], ends[i]));
                }
            }

            // Open-roof areas are deliberately treated as one climb-connected space. The
            // directional wall scanner normally supplies the concrete crossings; this is
            // the axiom-preserving fallback when it finds no intervening wall segment.
            if (legs.Count == 0 && start != goal)
            {
                legs.Add(TraversalLeg.WallClimb(start, goal));
            }
            return legs.Count > 0;
        }

        private static void AssignTraversalLegs(CompClimber climber, List<TraversalLeg> legs)
        {
            climber.climbParameters.TraversalLegs = legs ?? new List<TraversalLeg>();
            climber.climbParameters.NoWallToClimb = climber.climbParameters.TraversalLegs.Count == 0;
            climber.climbParameters.ClimbStarts = climber.climbParameters.TraversalLegs.Select(leg => leg.start).ToList();
            climber.climbParameters.ClimbEnds = climber.climbParameters.TraversalLegs.Select(leg => leg.end).ToList();
        }
        protected static bool GetClimbParameters(Pawn pawn, IntVec3 FinalGoalCell, PathEndMode finalPathEndMode, ref CompClimber climber)
        {
            return GetClimbParameters(pawn, new LocalTargetInfo(FinalGoalCell), finalPathEndMode, ref climber);
        }

        protected static bool GetClimbParameters(Pawn pawn, LocalTargetInfo finalGoal, PathEndMode finalPathEndMode, ref CompClimber climber)
        {
            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " gathering climb parameters");
            }
            climber.ClearClimberData();
            climber.MarkClimbToilActive(pawn.jobs?.curJob);

            climber.climbParameters.FinalGoalTarget = finalGoal;
            climber.climbParameters.FinalGoalCell = finalGoal.Cell;

            if (!finalGoal.IsValid || !finalGoal.Cell.IsValid)
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + pawn + " final goal invalid");
                }
                return false;
            }

            if (pawn?.Map == null || !pawn.Position.InBounds(pawn.Map) || !finalGoal.Cell.InBounds(pawn.Map))
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + pawn + " final goal invalid");
                }
                return false;
            }

            if(XMTUtility.IsSpace(pawn.Map))
            {
                climber.climbParameters.Tunneling = true;
            }

            // Prefer an available infiltration network over damaging a wall. This is
            // especially important for prison vents and explicit climb-to orders.
            if (InfiltrationUtility.TryBuildInfiltrationRoute(pawn, finalGoal, finalPathEndMode,
                pawn.NormalMaxDanger(), out List<TraversalLeg> pureInfiltrationLegs))
            {
                AssignTraversalLegs(climber, pureInfiltrationLegs);
                if (climber.climbParameters.ClimbCellsRegistered && ValidateClimbRoute(pawn, climber, finalPathEndMode))
                {
                    LogClimbRoute(pawn, climber, finalPathEndMode);
                    return true;
                }
            }

            if (TryBuildDirectWallRoute(pawn, finalGoal, finalPathEndMode, pawn.NormalMaxDanger(),
                out List<TraversalLeg> wallLegs, out ClimbDecision decision))
            {
                AssignTraversalLegs(climber, wallLegs);

                if (climber.climbParameters.ClimbCellsRegistered && ValidateClimbRoute(pawn, climber, finalPathEndMode))
                {
                    LogClimbRoute(pawn, climber, finalPathEndMode);
                    return true;
                }
            }

            bool normallyReachable = OriginalCanReach(pawn, finalGoal, finalPathEndMode, pawn.NormalMaxDanger());
            if (!normallyReachable && TraversalRouteUtility.TryBuildRoute(pawn, finalGoal, finalPathEndMode, pawn.NormalMaxDanger(),
                allowWallClimb: true, out List<TraversalLeg> combinedLegs))
            {
                AssignTraversalLegs(climber, combinedLegs);
                if (climber.climbParameters.ClimbCellsRegistered && ValidateClimbRoute(pawn, climber, finalPathEndMode))
                {
                    LogClimbRoute(pawn, climber, finalPathEndMode);
                    return true;
                }
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] Traversal cells failed to register for " + pawn + "; climbDecision=" + decision + "; normallyReachable=" + normallyReachable);
            }
            climber.ClearClimberData();
            return false;
        }
    }
}
