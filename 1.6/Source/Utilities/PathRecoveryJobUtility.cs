using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class PathRecoveryJobUtility
    {
        private const float GoalProgressWeight = 32f;
        private const float GoalClosenessWeight = 3f;
        private const float GoalAlignmentWeight = 12f;
        private const float ApproachCostWeight = 1.25f;

        internal static float GoalBiasScore(Pawn pawn, IntVec3 candidateCell, IntVec3 goalCell, float weight = 1f)
        {
            return GoalProgressScore(pawn, candidateCell, goalCell) * weight;
        }

        internal static float RecoveryScore(Pawn pawn, IntVec3 candidateCell, IntVec3 goalCell, float localScore = 0f, float goalBiasWeight = 1f)
        {
            return RecoveryScore(pawn, candidateCell, candidateCell, goalCell, localScore);
        }

        internal static float RecoveryScore(Pawn pawn, IntVec3 recoveryCell, IntVec3 approachCell, IntVec3 goalCell, float localScore = 0f)
        {
            Map map = pawn?.Map;
            if (map == null ||
                !pawn.PositionHeld.IsValid ||
                !recoveryCell.IsValid ||
                !approachCell.IsValid ||
                !goalCell.IsValid ||
                !recoveryCell.InBounds(map) ||
                !approachCell.InBounds(map) ||
                !goalCell.InBounds(map))
            {
                return localScore;
            }

            float currentGoalDistance = pawn.PositionHeld.DistanceTo(goalCell);
            float recoveryGoalDistance = recoveryCell.DistanceTo(goalCell);
            float progressScore = (currentGoalDistance - recoveryGoalDistance) * GoalProgressWeight;
            float closenessScore = -recoveryGoalDistance * GoalClosenessWeight;
            float alignmentScore = DirectionAlignmentScore(pawn.PositionHeld, recoveryCell, goalCell) * GoalAlignmentWeight;
            float approachCost = pawn.PositionHeld.DistanceTo(approachCell) * ApproachCostWeight;
            return localScore + progressScore + closenessScore + alignmentScore - approachCost;
        }

        internal static IOrderedEnumerable<T> OrderByRecoveryScore<T>(this IEnumerable<T> candidates, Pawn pawn, IntVec3 goalCell, Func<T, IntVec3> candidateCellGetter, Func<T, float> localScoreGetter = null, float goalBiasWeight = 1f)
        {
            return candidates.OrderByRecoveryScore(
                pawn,
                goalCell,
                candidateCellGetter,
                candidateCellGetter,
                localScoreGetter);
        }

        internal static IOrderedEnumerable<T> OrderByRecoveryScore<T>(this IEnumerable<T> candidates, Pawn pawn, IntVec3 goalCell, Func<T, IntVec3> recoveryCellGetter, Func<T, IntVec3> approachCellGetter, Func<T, float> localScoreGetter = null)
        {
            return candidates.OrderByDescending(candidate => RecoveryScore(
                pawn,
                recoveryCellGetter(candidate),
                approachCellGetter(candidate),
                goalCell,
                localScoreGetter != null ? localScoreGetter(candidate) : 0f));
        }

        private static float GoalProgressScore(Pawn pawn, IntVec3 candidateCell, IntVec3 goalCell)
        {
            Map map = pawn?.Map;
            if (map == null ||
                !pawn.PositionHeld.IsValid ||
                !candidateCell.IsValid ||
                !goalCell.IsValid ||
                !candidateCell.InBounds(map) ||
                !goalCell.InBounds(map))
            {
                return 0f;
            }

            return pawn.PositionHeld.DistanceTo(goalCell) - candidateCell.DistanceTo(goalCell);
        }

        private static float DirectionAlignmentScore(IntVec3 origin, IntVec3 candidateCell, IntVec3 goalCell)
        {
            float goalX = goalCell.x - origin.x;
            float goalZ = goalCell.z - origin.z;
            float candidateX = candidateCell.x - origin.x;
            float candidateZ = candidateCell.z - origin.z;
            float goalMagnitude = (float)Math.Sqrt(goalX * goalX + goalZ * goalZ);
            float candidateMagnitude = (float)Math.Sqrt(candidateX * candidateX + candidateZ * candidateZ);
            if (goalMagnitude <= 0.01f || candidateMagnitude <= 0.01f)
            {
                return 0f;
            }

            return (goalX * candidateX + goalZ * candidateZ) / (goalMagnitude * candidateMagnitude);
        }

        internal static bool TryFindPassageDestination(Pawn pawn, CellRect passageRect, IntVec3 interactionCell, out IntVec3 destination)
        {
            return TryFindPassageDestination(pawn, passageRect, interactionCell, requireSafeExit: false, out destination);
        }

        internal static bool TryFindPassageDestination(Pawn pawn, CellRect passageRect, IntVec3 interactionCell, bool requireSafeExit, out IntVec3 destination)
        {
            return TryFindPassageDestination(pawn, passageRect, interactionCell, requireSafeExit, IntVec3.Invalid, out destination);
        }

        internal static bool TryFindPassageDestination(Pawn pawn, CellRect passageRect, IntVec3 interactionCell, bool requireSafeExit, IntVec3 goalCell, out IntVec3 destination)
        {
            destination = IntVec3.Invalid;
            Map map = pawn?.Map;
            if (map == null || !interactionCell.IsValid || !interactionCell.InBounds(map))
            {
                return false;
            }

            Room interactionRoom = interactionCell.GetRoom(map);
            IntVec3 passageCell = IntVec3.Invalid;
            foreach (IntVec3 cell in passageRect.Cells.Where(cell => cell.InBounds(map)).OrderBy(cell => cell.DistanceToSquared(interactionCell)))
            {
                passageCell = cell;
                break;
            }

            if (passageCell.IsValid && passageCell.AdjacentToCardinal(interactionCell))
            {
                IntVec3 directDestination = passageCell + (passageCell - interactionCell);
                if (IsUsableDestination(pawn, directDestination))
                {
                    destination = directDestination;
                    return true;
                }
            }

            foreach (IntVec3 adjacentDestination in passageRect.AdjacentCells
                         .Where(cell => cell != interactionCell &&
                                        IsUsableDestination(pawn, cell) &&
                                        (!requireSafeExit || interactionRoom == null || cell.GetRoom(map) != interactionRoom))
                         .OrderByRecoveryScore(pawn, goalCell, cell => cell, cell => interactionCell))
            {
                destination = adjacentDestination;
                return true;
            }

            if (requireSafeExit)
            {
                return false;
            }

            foreach (IntVec3 passageDestination in passageRect.Cells
                         .Where(cell => IsUsableDestination(pawn, cell))
                         .OrderByRecoveryScore(pawn, goalCell, cell => cell, cell => interactionCell))
            {
                destination = passageDestination;
                return true;
            }

            return false;
        }

        internal static bool CanSupportBreachPassage(Map map, IntVec3 cell, XMT_SabotageReplacementPair replacement)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            if (replacement?.replacementFloorThing != null)
            {
                return true;
            }

            return !IsUnsafeBreachDestination(map, cell);
        }

        internal static bool IsInDoorwayOrRoomBorder(Pawn pawn)
        {
            Map map = pawn?.Map;
            if (map == null || !pawn.PositionHeld.IsValid || !pawn.PositionHeld.InBounds(map))
            {
                return false;
            }

            Room room = pawn.GetRoom();
            if (room != null && room.IsDoorway)
            {
                return true;
            }

            return pawn.PositionHeld.GetEdifice(map) is Building_Door;
        }

        internal static bool TryFindDoorwayEscapeCell(Pawn pawn, out IntVec3 escapeCell)
        {
            return TryFindDoorwayEscapeCell(pawn, IntVec3.Invalid, out escapeCell);
        }

        internal static bool TryFindDoorwayEscapeCell(Pawn pawn, IntVec3 goalCell, out IntVec3 escapeCell)
        {
            escapeCell = IntVec3.Invalid;
            Map map = pawn?.Map;
            if (map == null || !pawn.PositionHeld.IsValid || !pawn.PositionHeld.InBounds(map))
            {
                return false;
            }

            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(pawn)
                         .Where(cell => IsUsableDestination(pawn, cell) && !IsDoorwayCell(map, cell))
                         .OrderByRecoveryScore(pawn, goalCell, cell => cell, cell => -cell.DistanceToSquared(pawn.PositionHeld)))
            {
                if (ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    escapeCell = cell;
                    return true;
                }
            }

            return false;
        }

        internal static bool IsUnsafeBreachDestination(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return true;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null ||
                terrain == ExternalDefOf.EmptySpace ||
                terrain == TerrainDefOf.Space ||
                terrain.exposesToVacuum)
            {
                return true;
            }

            if (cell.GetVacuum(map) >= VacuumUtility.MinVacuumForDamage)
            {
                return true;
            }

            if (XMTUtility.IsSpace(map))
            {
                Room room = cell.GetRoom(map);
                return !cell.Roofed(map) ||
                       room == null ||
                       room.PsychologicallyOutdoors ||
                       room.UsesOutdoorTemperature ||
                       room.TouchesMapEdge;
            }

            return false;
        }

        internal static Toil GotoCellIfValid(Func<IntVec3> cellGetter)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                IntVec3 destination = cellGetter();
                if (!IsUsableDestination(actor, destination) ||
                    actor.Position == destination ||
                    !actor.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
                {
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                actor.pather.StartPath(destination, PathEndMode.OnCell);
            };
            toil.tickAction = delegate
            {
                Pawn actor = toil.actor;
                if (!actor.pather.Moving)
                {
                    actor.jobs.curDriver.ReadyForNextToil();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private static bool IsUsableDestination(Pawn pawn, IntVec3 cell)
        {
            Map map = pawn?.Map;
            return map != null &&
                   cell.IsValid &&
                   cell.InBounds(map) &&
                   cell.Standable(map) &&
                   !IsUnsafeBreachDestination(map, cell) &&
                   !cell.Fogged(map) &&
                   FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell);
        }

        private static bool IsDoorwayCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            Room room = cell.GetRoom(map);
            return (room != null && room.IsDoorway) || cell.GetEdifice(map) is Building_Door;
        }
    }
}
