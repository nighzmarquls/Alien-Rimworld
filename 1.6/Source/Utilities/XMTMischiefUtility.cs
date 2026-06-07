using PipeSystem;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace Xenomorphtype
{
    internal enum XMTMischiefKind
    {
        OffensiveNest,
        DoorOpening,
        CorpseUnearthing,
        RoofBreaking,
        PowerSabotage
    }

    internal class XMTMischiefCandidate
    {
        public XMTMischiefKind Kind;
        public Job Job;
        public float Weight;
        public string Label;
    }

    internal static class XMTMischiefUtility
    {
        private const float MinimumMischiefLightSuitability = 0.55f;
        private const int CorpseDropSearchRadius = 28;
        private const int CorpseDropNearPawnBonusRadius = 9;
        private const int RoofFallbackApproachSearchRadius = 14;

        internal static bool IsDarkEnoughForMischief(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && XMTHiveUtility.HiveLightSuitabilityAt(cell, map) >= MinimumMischiefLightSuitability;
        }

        internal static void NotifyMischiefCompleted(Pawn pawn, float joyGain = 0.1f)
        {
            if (pawn?.needs?.joy != null)
            {
                pawn.needs.joy.GainJoy(joyGain, ExternalDefOf.Gaming_Dexterity);
            }
        }

        internal static bool TryFindDoorMischief(Pawn pawn, out Job job, out string reason)
        {
            job = null;
            reason = "no valid door";
            if (pawn?.Map == null)
            {
                reason = "invalid pawn map";
                return false;
            }

            List<Building_Door> doors = pawn.Map.listerBuildings.allBuildingsColonist
                .OfType<Building_Door>()
                .Where(door => IsValidDoorMischiefTarget(pawn, door) && TryFindDoorMischiefInteractionCell(pawn, door, out IntVec3 _))
                .ToList();

            if (!doors.Any())
            {
                return false;
            }

            Building_Door target = doors.RandomElement();
            if (!TryFindDoorMischiefInteractionCell(pawn, target, out IntVec3 interactionCell))
            {
                reason = "selected door has no dark interaction cell";
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_OpenDoorMischief, target, interactionCell);
            reason = "door " + target.LabelShort + " at " + target.Position + " from dark cell " + interactionCell;
            return true;
        }

        internal static bool TryFindOffensiveNestMischief(Pawn pawn, out Job job, out string reason)
        {
            job = null;
            reason = "no offensive nest object";
            CompMatureMorph comp = pawn?.GetMorphComp();
            if (pawn?.Map == null || comp == null)
            {
                reason = "invalid pawn map or morph comp";
                return false;
            }

            if (!XMTHiveUtility.NestOnMap(pawn.Map))
            {
                reason = "no nest on map";
                return false;
            }

            Thing offensive = XMTHiveUtility.GetMostOffensiveThingInNest(comp.NestPosition, pawn.Map);
            if (offensive == null || !IsDarkEnoughForMischief(offensive.PositionHeld, offensive.MapHeld))
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, offensive);
            reason = "offensive nest object " + offensive.LabelShort + " at " + offensive.PositionHeld;
            return true;
        }

        internal static bool TryFindRoofBreakMischief(Pawn pawn, out Job job, out string reason)
        {
            job = null;
            reason = "no valid roof";
            if (pawn?.Map == null)
            {
                reason = "invalid pawn map";
                return false;
            }

            IEnumerable<Pawn> candidates = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(candidate => XMTUtility.TriggersOvomorph(candidate) && XMTUtility.NotPrey(candidate) == false && IsValidRoofBreakTarget(pawn, candidate, requireDarkApproach: true));

            Pawn target = pawn.GetMorphComp()?.BestAbductCandidate(candidates);
            if (target == null)
            {
                reason = RoofBreakCandidateReport(pawn);
                return false;
            }

            if (!TryFindRoofBreakCellForTarget(pawn, target, out IntVec3 roofCell))
            {
                reason = "selected target has no removable roof";
                return false;
            }

            if (!TryFindRoofBreakApproachCell(pawn, target, roofCell, out IntVec3 approachCell, out string approachReason))
            {
                reason = approachReason;
                return false;
            }

            JobDef roofBreakJobDef = RoofBreakJobDef(out string jobDefReason);
            if (roofBreakJobDef == null)
            {
                reason = jobDefReason;
                return false;
            }

            job = JobMaker.MakeJob(roofBreakJobDef, roofCell, approachCell);
            reason = "roof over blocked target " + target + " at " + roofCell + " from " + approachCell;
            return true;
        }

        internal static bool TryFindPowerSabotageMischief(Pawn pawn, out Job job, out string reason)
        {
            job = null;
            reason = "no valid power sabotage target";
            Thing target = XMTUtility.GetPowerSabotageTarget(pawn);
            if (target == null)
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, target);
            reason = "power target " + target.LabelShort + " at " + target.PositionHeld;
            return true;
        }

        internal static string ReportMischiefCandidates(Pawn pawn)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Mischief candidates for " + pawn);
            AppendCandidateReport(builder, "Offensive nest", TryFindOffensiveNestMischief(pawn, out Job offensiveJob, out string offensiveReason), offensiveJob, offensiveReason);
            AppendCandidateReport(builder, "Door", TryFindDoorMischief(pawn, out Job doorJob, out string doorReason), doorJob, doorReason);
            AppendCandidateReport(builder, "Roof", TryFindRoofBreakMischief(pawn, out Job roofJob, out string roofReason), roofJob, roofReason);
            AppendCandidateReport(builder, "Power", TryFindPowerSabotageMischief(pawn, out Job powerJob, out string powerReason), powerJob, powerReason);
            return builder.ToString();
        }

        internal static bool TryTakeCorpseFromCasket(Building_CorpseCasket casket, Pawn carrier, out Corpse corpse)
        {
            corpse = null;
            if (casket?.Map == null || carrier == null || !casket.HasCorpse)
            {
                return false;
            }

            Corpse heldCorpse = casket.Corpse;
            if (heldCorpse == null)
            {
                return false;
            }

            int transferred = casket.GetDirectlyHeldThings().TryTransferToContainer(heldCorpse, carrier.carryTracker.innerContainer, heldCorpse.stackCount, out Thing carriedThing, canMergeWithExistingStacks: false);
            if (transferred <= 0)
            {
                return false;
            }

            casket.DirtyMapMesh(casket.Map);
            corpse = carriedThing as Corpse;
            return corpse != null;
        }

        private static void AppendCandidateReport(StringBuilder builder, string label, bool found, Job job, string reason)
        {
            builder.AppendLine(label + ": " + (found ? "YES " + job : "NO") + " - " + reason);
        }

        private static JobDef RoofBreakJobDef(out string reason)
        {
            reason = null;
            JobDef defOfJobDef = XenoWorkDefOf.XMT_BreakRoofMischief;
            if (defOfJobDef != null && defOfJobDef.defName == "XMT_BreakRoofMischief")
            {
                return defOfJobDef;
            }

            JobDef namedJobDef = DefDatabase<JobDef>.GetNamedSilentFail("XMT_BreakRoofMischief");
            if (namedJobDef != null)
            {
                Log.Warning("[Alien|Rimworld] XMT_BreakRoofMischief DefOf resolved as " + JobDefName(defOfJobDef) + "; using DefDatabase lookup instead.");
                return namedJobDef;
            }

            reason = "roof break JobDef unresolved; DefOf=" + JobDefName(defOfJobDef);
            return null;
        }

        private static string JobDefName(JobDef jobDef)
        {
            return jobDef == null ? "null" : jobDef.defName;
        }

        private static string RoofBreakCandidateReport(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return "invalid pawn map";
            }

            List<Pawn> hostCandidates = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(candidate => XMTUtility.TriggersOvomorph(candidate) && XMTUtility.NotPrey(candidate) == false)
                .ToList();
            if (!hostCandidates.Any())
            {
                return "no spawned ovamorph-triggering prey hosts";
            }

            int unavailable = 0;
            int noRoof = 0;
            int noApproach = 0;
            int litApproach = 0;
            int valid = 0;
            StringBuilder sample = new StringBuilder();

            foreach (Pawn candidate in hostCandidates)
            {
                string candidateReason = RoofBreakTargetFailureReason(pawn, candidate);
                switch (candidateReason)
                {
                    case null:
                        valid++;
                        break;
                    case "reserved/forbidden/unavailable":
                        unavailable++;
                        break;
                    case "target tile has no removable roof":
                        noRoof++;
                        break;
                    case "no reachable exterior wall approach":
                        noApproach++;
                        break;
                    case "reachable exterior approaches are too lit":
                        litApproach++;
                        break;
                }

                if (sample.Length < 500)
                {
                    sample.Append(candidate.LabelShortCap).Append(": ").Append(candidateReason ?? "valid").Append("; ");
                }
            }

            return "roof break host scan: valid=" + valid +
                   ", unavailable=" + unavailable +
                   ", noRemovableRoof=" + noRoof +
                   ", noExteriorApproach=" + noApproach +
                   ", approachTooLit=" + litApproach +
                   ". sample: " + sample;
        }

        private static bool IsValidDoorMischiefTarget(Pawn pawn, Building_Door door)
        {
            if (door == null || door.Destroyed || door.Open || door.HoldOpen || door is Building_MultiTileDoor)
            {
                return false;
            }

            if (door.TryGetComp<CompPowerTrader>() != null)
            {
                return false;
            }

            if (door.def.defName.Contains("SecurityDoor"))
            {
                return false;
            }

            return FeralJobUtility.IsThingAvailableForJobBy(pawn, door);
        }

        internal static bool TryFindDoorMischiefInteractionCell(Pawn pawn, Building_Door door, out IntVec3 interactionCell)
        {
            interactionCell = IntVec3.Invalid;
            if (pawn?.Map == null || door?.Map == null || pawn.Map != door.Map)
            {
                return false;
            }

            List<IntVec3> cells = door.OccupiedRect()
                .AdjacentCells
                .Where(cell => IsValidDoorMischiefInteractionCell(pawn, door, cell))
                .ToList();

            if (!cells.Any())
            {
                return false;
            }

            interactionCell = cells.RandomElementByWeight(cell => Mathf.Max(1f, XMTHiveUtility.HiveLightSuitabilityAt(cell, pawn.Map) * 10f));
            return true;
        }

        private static bool IsValidDoorMischiefInteractionCell(Pawn pawn, Building_Door door, IntVec3 cell)
        {
            Map map = pawn.Map;
            return cell.InBounds(map) &&
                   cell.Standable(map) &&
                   !cell.Fogged(map) &&
                   IsDarkEnoughForMischief(cell, map) &&
                   FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell) &&
                   ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly, canBashDoors: false, canBashFences: false) &&
                   cell.AdjacentTo8WayOrInside(door.Position);
        }

        private static bool CanReachForAbduction(Pawn pawn, Pawn target)
        {
            if (target == null)
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: false);
        }

        internal static bool IsValidRoofBreakCell(Pawn pawn, IntVec3 cell)
        {
            Map map = pawn.Map;
            return HasRemovableRoofAt(map, cell) && FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell);
        }

        internal static bool HasRemovableRoofAt(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof == null || roof.isThickRoof || roof == RoofDefOf.RoofRockThick)
            {
                return false;
            }

            return true;
        }

        private static bool IsValidRoofBreakTarget(Pawn pawn, Pawn target, bool requireDarkApproach)
        {
            return RoofBreakTargetFailureReason(pawn, target, requireDarkApproach) == null;
        }

        private static string RoofBreakTargetFailureReason(Pawn pawn, Pawn target, bool requireDarkApproach = true)
        {
            if (target == null || !target.Spawned || target.Map != pawn.Map || !FeralJobUtility.IsThingAvailableForJobBy(pawn, target))
            {
                return "reserved/forbidden/unavailable";
            }

            if (!TryFindRoofBreakCellForTarget(pawn, target, out IntVec3 roofCell))
            {
                return "target tile has no removable roof";
            }

            Room roofRoom = target.GetRoom();
            if (!IsValidRoofBreakRoom(roofRoom))
            {
                return "roof cell room is invalid or doorway";
            }

            if (!TryFindRoofBreakApproachCell(pawn, target, roofCell, out IntVec3 _, out string approachReason, requireDarkApproach))
            {
                return approachReason;
            }

            return null;
        }

        internal static bool TryFindRoofBreakApproachCell(Pawn pawn, Pawn target, out IntVec3 approachCell)
        {
            if (!TryFindRoofBreakCellForTarget(pawn, target, out IntVec3 roofCell))
            {
                approachCell = IntVec3.Invalid;
                return false;
            }

            return TryFindRoofBreakApproachCell(pawn, target, roofCell, out approachCell, out string _);
        }

        internal static bool TryFindRoofBreakApproachCell(Pawn pawn, Pawn target, out IntVec3 approachCell, out string reason, bool requireDarkApproach = true)
        {
            if (!TryFindRoofBreakCellForTarget(pawn, target, out IntVec3 roofCell))
            {
                approachCell = IntVec3.Invalid;
                reason = "target tile has no removable roof";
                return false;
            }

            return TryFindRoofBreakApproachCell(pawn, target, roofCell, out approachCell, out reason, requireDarkApproach);
        }

        internal static bool TryFindRoofBreakApproachCell(Pawn pawn, Pawn target, IntVec3 roofCell, out IntVec3 approachCell, out string reason, bool requireDarkApproach = true)
        {
            approachCell = IntVec3.Invalid;
            reason = "invalid pawn or target map";
            if (pawn?.Map == null || target?.Map == null || pawn.Map != target.Map)
            {
                return false;
            }

            Map map = pawn.Map;
            List<IntVec3> cells = new List<IntVec3>();
            int reachableExteriorApproaches = 0;
            Room roofRoom = roofCell.GetRoom(map);
            if (!IsValidRoofBreakRoom(roofRoom))
            {
                reason = "roof cell room is invalid or doorway";
                return false;
            }

            AddRoofBreakApproachCandidatesFromRoom(pawn, roofRoom, requireDarkApproach, cells, ref reachableExteriorApproaches);
            AddFallbackRoofBreakApproachCandidates(pawn, target, roofCell, roofRoom, requireDarkApproach, cells, ref reachableExteriorApproaches);

            cells = cells.Distinct().ToList();
            if (!cells.Any())
            {
                reason = reachableExteriorApproaches > 0 ? "reachable exterior approaches are too lit" : "no reachable exterior wall approach";
                return false;
            }

            approachCell = cells.RandomElementByWeight(cell => Mathf.Max(1f, XMTHiveUtility.HiveLightSuitabilityAt(cell, map) * 10f));
            reason = "approach " + approachCell;
            return true;
        }

        internal static bool TryFindRoofBreakCellForTarget(Pawn pawn, Pawn target, out IntVec3 roofCell)
        {
            roofCell = IntVec3.Invalid;
            if (pawn?.Map == null || target?.Map == null || pawn.Map != target.Map)
            {
                return false;
            }

            Map map = pawn.Map;

            Room room = target.GetRoom();

            if (room == null)
            {
                return false;
            }

            if (room.UsesOutdoorTemperature || room.PsychologicallyOutdoors)
            {
                return false;
            }

            List<IntVec3> interiorBorderCells = new List<IntVec3>();
            foreach(IntVec3 cell in room.BorderCells)
            {
                foreach(IntVec3 adjacent in GenRadial.RadialCellsAround(cell,1,true))
                {
                    if(adjacent.GetRoom(map) == room && adjacent.Standable(map))
                    {
                        interiorBorderCells.AddUnique(adjacent);
                    }
                }
            }
            List<IntVec3> roofCells = interiorBorderCells
                .Where(cell => HasRemovableRoofAt(map, cell) && FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                .Distinct()
                .OrderBy(cell => cell.DistanceToSquared(target.PositionHeld))
                .ToList();

            if (!roofCells.Any())
            {
                return false;
            }

            roofCell = roofCells.First();
            return true;
        }

        internal static bool IsRoofBreakCellStillRelevantToTarget(Pawn target, IntVec3 roofCell)
        {
            if (target == null || !roofCell.IsValid)
            {
                return false;
            }

            Room room = target.GetRoom();

            if (room == null || target.Map == null)
            {
                return false;
            }

            return HasRemovableRoofAt(target.Map, roofCell);
        }

        internal static bool TryFindRoofBreakTargetAtCell(Pawn pawn, IntVec3 roofCell, out Pawn target)
        {
            target = null;
            if (pawn?.Map == null || !roofCell.IsValid)
            {
                return false;
            }

            IEnumerable<Pawn> candidates = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(candidate => XMTUtility.TriggersOvomorph(candidate) &&
                                    XMTUtility.NotPrey(candidate) == false &&
                                    FeralJobUtility.IsThingAvailableForJobBy(pawn, candidate) &&
                                    IsRoofBreakCellStillRelevantToTarget(candidate, roofCell));

            target = pawn.GetMorphComp()?.BestAbductCandidate(candidates);
            return target != null;
        }

        internal static bool TryFindRoofBreakTargetInRoom(Pawn pawn, IntVec3 roofCell, out Pawn target)
        {
            target = null;
            if (pawn?.Map == null || !roofCell.IsValid || !roofCell.InBounds(pawn.Map))
            {
                return false;
            }

            Room room = roofCell.GetRoom(pawn.Map);
            if (!IsValidRoofBreakRoom(room))
            {
                return false;
            }

            IEnumerable<Pawn> candidates = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(candidate => XMTUtility.TriggersOvomorph(candidate) &&
                                    XMTUtility.NotPrey(candidate) == false &&
                                    FeralJobUtility.IsThingAvailableForJobBy(pawn, candidate) &&
                                    room.ContainsCell(candidate.PositionHeld));

            target = pawn.GetMorphComp()?.BestAbductCandidate(candidates);
            return target != null;
        }

        private static bool IsValidRoofBreakRoom(Room room)
        {
            return room != null && !room.IsDoorway;
        }

        private static void AddRoofBreakApproachCandidatesFromRoom(Pawn pawn, Room targetRoom, bool requireDarkApproach, List<IntVec3> cells, ref int reachableExteriorApproaches)
        {
            Map map = pawn.Map;
            foreach (IntVec3 roomCell in targetRoom.Cells)
            {
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 wallCell = roomCell + GenAdj.CardinalDirections[i];
                    if (!IsRoofBreakBoundaryCell(map, wallCell))
                    {
                        continue;
                    }

                    IntVec3 candidate = wallCell + GenAdj.CardinalDirections[i];
                    if (candidate.GetRoom(map) != targetRoom)
                    {
                        AddRoofBreakApproachCandidate(pawn, candidate, requireDarkApproach, cells, ref reachableExteriorApproaches);
                    }
                }
            }
        }

        private static void AddFallbackRoofBreakApproachCandidates(Pawn pawn, Pawn target, IntVec3 roofCell, Room roofRoom, bool requireDarkApproach, List<IntVec3> cells, ref int reachableExteriorApproaches)
        {
            Map map = pawn.Map;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(roofCell, RoofFallbackApproachSearchRadius, true))
            {
                if (!candidate.InBounds(map) ||
                    !candidate.Standable(map) ||
                    candidate == roofCell ||
                    IsRoofBreakCellStillRelevantToTarget(target, candidate) ||
                    candidate.GetRoom(map) == roofRoom)
                {
                    continue;
                }

                if (IsOutsideRoofBreakRoomAcrossBoundary(map, candidate, roofRoom))
                {
                    AddRoofBreakApproachCandidate(pawn, candidate, requireDarkApproach, cells, ref reachableExteriorApproaches);
                }
            }
        }

        private static bool IsOutsideRoofBreakRoomAcrossBoundary(Map map, IntVec3 candidate, Room roofRoom)
        {
            if (!IsValidRoofBreakRoom(roofRoom))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                IntVec3 wallCell = candidate + GenAdj.CardinalDirections[i];
                IntVec3 roomCell = wallCell + GenAdj.CardinalDirections[i];
                if (IsRoofBreakBoundaryCell(map, wallCell) && roomCell.InBounds(map) && roomCell.GetRoom(map) == roofRoom)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRoofBreakApproachCandidate(Pawn pawn, IntVec3 candidate, bool requireDarkApproach, List<IntVec3> cells, ref int reachableExteriorApproaches)
        {
            if (!IsValidRoofBreakApproachCell(pawn, candidate, requireDarkness: false))
            {
                return;
            }

            reachableExteriorApproaches++;
            if (!requireDarkApproach || IsDarkEnoughForMischief(candidate, pawn.Map))
            {
                cells.Add(candidate);
            }
        }

        private static bool IsRoofBreakBoundaryCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def.holdsRoof && edifice.def.passability == Traversability.Impassable;
        }

        private static bool IsValidRoofBreakApproachCell(Pawn pawn, IntVec3 cell, bool requireDarkness)
        {
            Map map = pawn.Map;
            return cell.InBounds(map) &&
                   cell.Standable(map) &&
                   !cell.Fogged(map) &&
                   IsOutdoorRoofBreakApproachCell(map, cell) &&
                   (!requireDarkness || IsDarkEnoughForMischief(cell, map)) &&
                   FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell) &&
                   ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly, canBashDoors: false, canBashFences: false);
        }

        private static bool IsOutdoorRoofBreakApproachCell(Map map, IntVec3 cell)
        {
            Room room = cell.GetRoom(map);
            return room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge || room.UsesOutdoorTemperature;
        }
    }
}
