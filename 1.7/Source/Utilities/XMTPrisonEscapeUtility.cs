using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal enum PrisonEscapePlanKind
    {
        None,
        Traverse,
        ForceUnpoweredDoor,
        BerserkerBreach
    }

    internal readonly struct PrisonEscapePlan
    {
        public static readonly PrisonEscapePlan None = new PrisonEscapePlan(PrisonEscapePlanKind.None, IntVec3.Invalid, null, IntVec3.Invalid, 0f);

        public readonly PrisonEscapePlanKind Kind;
        public readonly IntVec3 Destination;
        public readonly Building_Door Door;
        public readonly IntVec3 InteractionCell;
        public readonly float PoweredDoorWeakness;

        public PrisonEscapePlan(PrisonEscapePlanKind kind, IntVec3 destination, Building_Door door,
            IntVec3 interactionCell, float poweredDoorWeakness)
        {
            Kind = kind;
            Destination = destination;
            Door = door;
            InteractionCell = interactionCell;
            PoweredDoorWeakness = poweredDoorWeakness;
        }
    }

    internal static class XMTPrisonEscapeUtility
    {
        internal static bool HasFreePlayerCryptimorph(Pawn prisoner)
        {
            if (prisoner?.Map == null)
            {
                return false;
            }

            return prisoner.Map.mapPawns.AllPawnsSpawned.Any(candidate =>
                candidate != prisoner && !candidate.Dead && !candidate.IsPrisoner &&
                candidate.Faction?.IsPlayer == true && XMTUtility.IsXenomorph(candidate));
        }

        internal static bool IsSupportedPlayerPrisoner(Pawn prisoner)
        {
            return prisoner?.Faction?.IsPlayer == true && HasFreePlayerCryptimorph(prisoner);
        }

        internal static float RageChance(Pawn prisoner, PrisonEscapePlan plan)
        {
            float baseChance = Mathf.Clamp01(1f - (prisoner.GetMorphComp()?.Taming ?? 0f));
            return Mathf.Lerp(baseChance, 1f, Mathf.Clamp01(plan.PoweredDoorWeakness));
        }

        internal static bool TryMakeEscapeJob(Pawn prisoner, PrisonEscapePlan plan, out Job job)
        {
            job = null;
            if (prisoner?.Map == null || !prisoner.IsPrisoner || !plan.Destination.IsValid)
            {
                return false;
            }

            if (plan.Kind == PrisonEscapePlanKind.Traverse)
            {
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PrisonEscape, plan.Destination);
                return true;
            }

            if (plan.Kind == PrisonEscapePlanKind.ForceUnpoweredDoor && plan.Door != null && plan.InteractionCell.IsValid)
            {
                job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PrisonEscape, plan.Destination, plan.Door, plan.InteractionCell);
                return true;
            }

            return false;
        }

        internal static PrisonEscapePlan Evaluate(Pawn prisoner)
        {
            if (prisoner?.Map == null || !prisoner.Spawned || !prisoner.IsPrisoner)
            {
                return PrisonEscapePlan.None;
            }

            Room room = prisoner.GetRoom();
            if (room == null)
            {
                return PrisonEscapePlan.None;
            }

            if (TryFindTraversalDestination(prisoner, room, out IntVec3 traversalDestination))
            {
                return new PrisonEscapePlan(PrisonEscapePlanKind.Traverse, traversalDestination, null, IntVec3.Invalid, 0f);
            }

            List<Building_Door> boundaryDoors = GetBoundaryBuildings(prisoner, room).OfType<Building_Door>().ToList();
            foreach (Building_Door door in boundaryDoors
                         .Where(XMTDoorUtility.CanForceOpenConventionally)
                         .OrderBy(door => door.Position.DistanceToSquared(prisoner.Position)))
            {
                if (TryFindDoorPassage(prisoner, room, door, out IntVec3 interactionCell, out IntVec3 destination))
                {
                    return new PrisonEscapePlan(PrisonEscapePlanKind.ForceUnpoweredDoor, destination, door, interactionCell, 0f);
                }
            }

            float poweredWeakness = boundaryDoors.Count == 0
                ? 0f
                : boundaryDoors.Max(XMTDoorUtility.PoweredWeakness);
            return new PrisonEscapePlan(PrisonEscapePlanKind.BerserkerBreach, IntVec3.Invalid, null,
                IntVec3.Invalid, poweredWeakness);
        }

        internal static IEnumerable<Building> GetBoundaryBuildings(Pawn pawn, Room room = null)
        {
            room ??= pawn?.GetRoom();
            if (pawn?.Map == null || room == null)
            {
                yield break;
            }

            HashSet<Building> seen = new HashSet<Building>();
            foreach (IntVec3 roomCell in room.Cells)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 borderCell = roomCell + direction;
                    if (!borderCell.InBounds(pawn.Map) || borderCell.GetRoom(pawn.Map) == room)
                    {
                        continue;
                    }

                    Building building = borderCell.GetEdifice(pawn.Map);
                    if (building != null && seen.Add(building))
                    {
                        yield return building;
                    }
                }
            }
        }

        private static bool TryFindTraversalDestination(Pawn pawn, Room room, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            HashSet<IntVec3> candidates = new HashSet<IntVec3>();
            foreach (IntVec3 roomCell in room.Cells)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 candidate = roomCell + direction;
                    if (candidate.InBounds(pawn.Map) && candidate.Standable(pawn.Map) && candidate.GetRoom(pawn.Map) != room)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            foreach (Building boundary in GetBoundaryBuildings(pawn, room))
            {
                foreach (IntVec3 candidate in boundary.OccupiedRect().AdjacentCells)
                {
                    if (candidate.InBounds(pawn.Map) && candidate.Standable(pawn.Map) && candidate.GetRoom(pawn.Map) != room)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            foreach (InfiltrationNetworkComponent component in InfiltrationUtility.GetAllComponents(pawn.Map))
            {
                foreach (InfiltrationPort port in InfiltrationUtility.GetPorts(pawn.Map, component))
                {
                    if (port.AccessCell.InBounds(pawn.Map) && port.AccessCell.GetRoom(pawn.Map) != room)
                    {
                        candidates.Add(port.AccessCell);
                    }
                }
            }

            foreach (IntVec3 candidate in candidates.OrderBy(cell => cell.DistanceToSquared(pawn.Position)))
            {
                if (ClimbUtility.CanReachByClimb(pawn, candidate, PathEndMode.OnCell, Danger.Deadly) ||
                    (InfiltrationUtility.TryBuildInfiltrationRoute(pawn, candidate, PathEndMode.OnCell, Danger.Deadly,
                         out List<TraversalLeg> legs) && legs.Any(leg => leg.IsInfiltration &&
                             leg.start.GetRoom(pawn.Map) == room && leg.end.GetRoom(pawn.Map) != room)))
                {
                    destination = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindDoorPassage(Pawn pawn, Room room, Building_Door door,
            out IntVec3 interactionCell, out IntVec3 destination)
        {
            interactionCell = IntVec3.Invalid;
            destination = IntVec3.Invalid;
            foreach (IntVec3 candidate in door.OccupiedRect().AdjacentCells
                         .Where(cell => cell.InBounds(pawn.Map) && cell.GetRoom(pawn.Map) == room && cell.Standable(pawn.Map))
                         .OrderBy(cell => cell.DistanceToSquared(pawn.Position)))
            {
                if (!ClimbUtility.OriginalCanReach(pawn, candidate, PathEndMode.OnCell, Danger.Deadly) ||
                    !PathRecoveryJobUtility.TryFindPassageDestination(pawn, door.OccupiedRect(), candidate,
                        requireSafeExit: false, goalCell: IntVec3.Invalid, out IntVec3 passageDestination))
                {
                    continue;
                }

                interactionCell = candidate;
                destination = passageDestination;
                return true;
            }

            return false;
        }
    }
}
