using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal enum NestBuildStage
    {
        None,
        ClaimFloor,
        EncloseOpenRoom,
        RoofEnclosedRoom
    }

    internal struct NestBuildRequest
    {
        public NestBuildStage stage;
        public IntVec3 cell;
        public ThingDef buildDef;
        public int score;
        public string reason;
    }

    internal static class XMTNestBuildingUtility
    {
        private const int DefaultSightLimit = 5;
        private const float LocalPatchRadius = 10f;
        private const float NestSeedRadius = 8f;
        private const float NestBuildStartRadius = 12f;
        private const int MinimumHiveFloorAreaForEnclosure = 12;
        private const int MaxCandidates = 96;
        private const int MaxQueuedEnclosureRequests = 160;
        private const int MinQueuedWebbingRequests = 2;
        private const int MaxQueuedWebbingRequests = 6;
        private const int MaxQueuedPocketFloorArea = 8;
        private const float MinimumRefogHiveLightSuitability = 0.5f;
        private const float MinimumBuildHiveLightSuitability = 0.45f;

        private class NestEncirclementPlan
        {
            public Map map;
            public IntVec3 nestSeed;
            public List<NestBuildRequest> requests;
        }

        private static List<NestEncirclementPlan> encirclementPlans;
        private static List<NestEncirclementPlan> EncirclementPlans
        {
            get
            {
                if (encirclementPlans == null)
                {
                    encirclementPlans = new List<NestEncirclementPlan>();
                }

                return encirclementPlans;
            }
        }

        private static bool TryFindNestApproachCell(Pawn builder, IntVec3 nestSeed, out IntVec3 approachCell)
        {
            approachCell = IntVec3.Invalid;
            if (builder == null || builder.Map == null || !builder.Spawned || !nestSeed.InBounds(builder.Map))
            {
                return false;
            }

            Map map = builder.Map;
            List<IntVec3> candidates = GenRadial.RadialCellsAround(nestSeed, NestSeedRadius, true)
                .Where(cell => cell.InBounds(map) && cell != builder.Position && cell.Standable(map))
                .OrderBy(cell => cell.DistanceToSquared(nestSeed))
                .ThenBy(cell => cell.DistanceToSquared(builder.Position))
                .Take(MaxCandidates)
                .ToList();

            TraverseParms traverseParms = TraverseParms.For(builder, Danger.Deadly, TraverseMode.ByPawn);
            foreach (IntVec3 cell in candidates)
            {
                if (map.reachability.CanReach(builder.Position, cell, PathEndMode.OnCell, traverseParms))
                {
                    approachCell = cell;
                    return true;
                }
            }

            foreach (IntVec3 cell in candidates)
            {
                if (ClimbUtility.CanReachByWalkingOrExecutableClimb(builder, cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    approachCell = cell;
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldBuildNest(Map map, IntVec3 nestSeed, int sightLimit = DefaultSightLimit)
        {
            if (map == null || !nestSeed.InBounds(map))
            {
                return false;
            }

            return TryFindNestBuildRequest(map, nestSeed, nestSeed, sightLimit, out NestBuildRequest _);
        }

        internal static Job TryGetNestBuildJob(Pawn builder, IntVec3 nestSeed, int sightLimit = DefaultSightLimit)
        {
            if (builder == null || builder.Map == null || !builder.Spawned || builder.Dead)
            {
                return null;
            }

            if (builder.Position.DistanceTo(nestSeed) > NestBuildStartRadius && !HasLocalHiveTerrain(builder.Map, builder.Position, 4f))
            {
                if (TryFindNestApproachCell(builder, nestSeed, out IntVec3 approachCell))
                {
                    Job approachJob = JobMaker.MakeJob(JobDefOf.Goto, approachCell);
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(builder + " is approaching nest build seed before building: " + approachJob);
                    }
                    return approachJob;
                }
            }

            if (!TryFindNestBuildRequest(builder, nestSeed, sightLimit, out NestBuildRequest request))
            {
                return null;
            }

            Job job = request.stage == NestBuildStage.RoofEnclosedRoom
                ? JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveRoofing, request.cell)
                : JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, request.cell);
            job.plantDefToSow = request.buildDef;
            FeralJobUtility.ReservePlaceForJob(builder, job, request.cell);
            return job;
        }

        internal static Job TryGetNestBuildJobFromExpansionSeed(Pawn builder, IntVec3 nestSeed, int sightLimit = DefaultSightLimit)
        {
            if (builder == null || builder.Map == null || !builder.Spawned || builder.Dead || !nestSeed.InBounds(builder.Map))
            {
                return null;
            }

            if (builder.Position.DistanceTo(nestSeed) > NestBuildStartRadius)
            {
                if (TryFindNestApproachCell(builder, nestSeed, out IntVec3 approachCell))
                {
                    Job approachJob = JobMaker.MakeJob(JobDefOf.Goto, approachCell);
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(builder + " is approaching cocoon expansion seed before building: " + approachJob);
                    }
                    return approachJob;
                }
            }

            if (!TryFindNestBuildRequest(builder.Map, nestSeed, nestSeed, sightLimit, out NestBuildRequest request, builder))
            {
                return null;
            }

            Job job = request.stage == NestBuildStage.RoofEnclosedRoom
                ? JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveRoofing, request.cell)
                : JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, request.cell);
            job.plantDefToSow = request.buildDef;
            FeralJobUtility.ReservePlaceForJob(builder, job, request.cell);
            return job;
        }

        internal static Job TryGetHiveRoomExpansionBuildJob(Pawn builder, Room room, IntVec3 anchorCell)
        {
            if (builder == null || builder.Map == null || !builder.Spawned || builder.Dead || room == null || room.Map != builder.Map)
            {
                return null;
            }

            if (!TryFindHiveRoomExpansionRequest(builder.Map, anchorCell, room, anchorCell, builder, out NestBuildRequest request))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, request.cell);
            job.plantDefToSow = request.buildDef;
            FeralJobUtility.ReservePlaceForJob(builder, job, request.cell);
            return job;
        }

        internal static bool TryFindNestBuildRequest(Pawn builder, IntVec3 nestSeed, int sightLimit, out NestBuildRequest request, bool debug = false)
        {
            request = default;
            if (builder == null || builder.Map == null || !builder.Spawned || !nestSeed.InBounds(builder.Map))
            {
                return false;
            }

            bool result = TryFindNestBuildRequest(builder.Map, nestSeed, nestSeed, sightLimit, out request, builder);
            if (debug && result)
            {
                MoteMaker.ThrowText(request.cell.ToVector3Shifted(), builder.Map, request.stage + ": " + (request.buildDef?.defName ?? "roof"), 6f);
            }

            return result;
        }

        internal static void NotifyHiveConstructionCompleted(Map map, IntVec3 cell, ThingDef finalDef)
        {
            if (map == null || finalDef == null || !cell.InBounds(map))
            {
                return;
            }

            if (IsHiveFloorBuildableDef(finalDef))
            {
                NotifyPotentialHiveRoomCompleted(map, cell);
                return;
            }

            if (finalDef != XenoBuildingDefOf.Hivemass && finalDef != XenoBuildingDefOf.HiveWebbing)
            {
                return;
            }

            NotifyPotentialHiveRoomCompleted(map, cell);
        }

        private static void NotifyPotentialHiveRoomCompleted(Map map, IntVec3 cell)
        {
            Room room = cell.GetRoomOrAdjacent(map);
            if (TryNotifyFinishedHiveRoom(room))
            {
                return;
            }

            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                Room adjacentRoom = adjacent.GetRoomOrAdjacent(map);
                if (TryNotifyFinishedHiveRoom(adjacentRoom))
                {
                    return;
                }
            }
        }

        private static bool TryNotifyFinishedHiveRoom(Room room)
        {
            if (!IsFinishedHiveRoom(room))
            {
                return false;
            }

            XMTHiveUtility.NotifyHiveRoomCompleted(room);
            TryRefogHiveRoom(room);
            return true;
        }

        private static bool IsHiveFloorBuildableDef(ThingDef def)
        {
            return def == XenoBuildingDefOf.HiveFloorBuildable ||
                   def == XenoBuildingDefOf.HiveBridgeBuildable;
        }

        internal static void NotifyHiveRoofCompleted(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            Room room = cell.GetRoomOrAdjacent(map);
            TryNotifyFinishedHiveRoom(room);
        }

        private static bool TryFindNestBuildRequest(Map map, IntVec3 locus, IntVec3 nestSeed, int sightLimit, out NestBuildRequest request, Pawn builder = null)
        {
            request = default;
            if (map == null || !locus.InBounds(map) || !nestSeed.InBounds(map))
            {
                return false;
            }

            List<IntVec3> localHivePatch = ConnectedHiveFloorPatch(map, locus, nestSeed);
            bool hasOpenPerimeter = localHivePatch.Count >= MinimumHiveFloorAreaForEnclosure && HasOpenHivePerimeter(map, localHivePatch);
            bool shouldConsiderEnclosure = localHivePatch.Count >= MinimumHiveFloorAreaForEnclosure && (hasOpenPerimeter || HasEdgeLikeCardinalScan(map, locus, sightLimit));

            if (shouldConsiderEnclosure && TryFindStructureClingFloorRequest(map, locus, nestSeed, localHivePatch, builder, out request))
            {
                ClearEncirclementPlan(map, nestSeed);
                return true;
            }

            if (shouldConsiderEnclosure && TryGetQueuedEnclosureRequest(map, locus, nestSeed, localHivePatch, builder, out request))
            {
                return true;
            }

            if (TryFindRoofRequest(map, locus, nestSeed, localHivePatch, builder, out request))
            {
                return true;
            }

            if (!shouldConsiderEnclosure && TryFindClaimFloorRequest(map, locus, nestSeed, localHivePatch, builder, out request))
            {
                return true;
            }

            if (TryGetQueuedEnclosureRequest(map, locus, nestSeed, localHivePatch, builder, out request))
            {
                return true;
            }

            return false;
        }

        private static bool TryFindRoofRequest(Map map, IntVec3 locus, IntVec3 nestSeed, List<IntVec3> localHivePatch, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            IEnumerable<Room> rooms = localHivePatch
                .Select(cell => cell.GetRoomOrAdjacent(map))
                .Concat(new[] { locus.GetRoomOrAdjacent(map), nestSeed.GetRoomOrAdjacent(map) })
                .Where(room => RoomNeedsHiveRoof(room))
                .Distinct();

            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            foreach (Room room in rooms)
            {
                foreach (IntVec3 cell in room.Cells)
                {
                    if (!CanBuildHiveRoofAt(map, cell, builder))
                    {
                        continue;
                    }

                    int roofBatchCount = HiveRoofBatchCells(map, cell, builder).Count;
                    int score = Rand.RangeInclusive(0, 12);
                    score += roofBatchCount * 45;
                    score += IsHiveTerrain(cell, map) ? 60 : 0;
                    score += AdjacentHiveFloorCount(map, cell) * 12;
                    score += AdjacentSolidStructureCount(map, cell) * 8;
                    score -= Mathf.CeilToInt(cell.DistanceTo(locus) * 2f);
                    score -= Mathf.CeilToInt(cell.DistanceTo(nestSeed));

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCell = cell;
                    }
                }
            }

            if (!bestCell.IsValid)
            {
                return false;
            }

            request = new NestBuildRequest
            {
                stage = NestBuildStage.RoofEnclosedRoom,
                cell = bestCell,
                buildDef = null,
                score = bestScore,
                reason = "roof enclosed hive room"
            };
            return true;
        }

        private static bool TryFindClaimFloorRequest(Map map, IntVec3 locus, IntVec3 nestSeed, List<IntVec3> localHivePatch, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            Room room = locus.GetRoomOrAdjacent(map) ?? nestSeed.GetRoomOrAdjacent(map);
            IEnumerable<IntVec3> cells = CandidateCells(map, locus, nestSeed);
            IntVec3 bestCell = IntVec3.Invalid;
            ThingDef bestDef = null;
            int bestScore = int.MinValue;

            foreach (IntVec3 cell in cells)
            {
                if (!CanClaimFloorAt(map, cell, builder, out ThingDef buildDef))
                {
                    continue;
                }

                int score = Rand.RangeInclusive(0, 18);
                score += Mathf.CeilToInt((LocalPatchRadius - Mathf.Min(LocalPatchRadius, cell.DistanceTo(locus))) * 2f);
                score += Mathf.CeilToInt((NestSeedRadius - Mathf.Min(NestSeedRadius, cell.DistanceTo(nestSeed))) * 1.5f);
                score += Mathf.CeilToInt(XMTHiveUtility.HiveLightSuitabilityAt(cell, map) * 35f);

                if (IsWallDoorEnclosedRoom(room) && room.Cells.Contains(cell))
                {
                    score += 45;
                }

                int hiveAdjacency = AdjacentHiveFloorCount(map, cell);
                score += hiveAdjacency * 22;
                if (localHivePatch.Count == 0 && cell == nestSeed)
                {
                    score += 80;
                }

                if (cell == locus)
                {
                    score -= 20;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestDef = buildDef;
                }
            }

            if (!bestCell.IsValid)
            {
                return false;
            }

            request = new NestBuildRequest
            {
                stage = NestBuildStage.ClaimFloor,
                cell = bestCell,
                buildDef = bestDef,
                score = bestScore,
                reason = "claim floor"
            };
            return true;
        }

        private static bool TryFindHiveRoomExpansionRequest(Map map, IntVec3 locus, Room room, IntVec3 anchorCell, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            IntVec3 bestCell = IntVec3.Invalid;
            ThingDef bestDef = null;
            int bestScore = int.MinValue;

            foreach (IntVec3 cell in HiveRoomExpansionCandidateCells(map, room, anchorCell))
            {
                if (!CanClaimFloorAt(map, cell, builder, out ThingDef buildDef))
                {
                    continue;
                }

                if (!ClimbUtility.CanReachByWalkingOrClimb(builder, cell, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                int score = Rand.RangeInclusive(0, 12);
                score += AdjacentSolidStructureCount(map, cell) * 35;
                score += AdjacentHiveFloorCount(map, cell) * 30;
                score += AdjacentHeavyHiveFloorCount(map, cell) * 12;
                score += CardinalOpenHiveFloorAdjacentCount(map, cell) * 20;
                score -= PassableNonHiveOpenAdjacentCount(map, cell) * 3;
                score -= Mathf.CeilToInt(cell.DistanceTo(locus) * 2f);
                if (anchorCell.IsValid)
                {
                    score -= Mathf.CeilToInt(cell.DistanceTo(anchorCell));
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestDef = buildDef;
                }
            }

            if (!bestCell.IsValid)
            {
                return false;
            }

            request = new NestBuildRequest
            {
                stage = NestBuildStage.ClaimFloor,
                cell = bestCell,
                buildDef = bestDef,
                score = bestScore,
                reason = "claim floor for hive room expansion"
            };
            return true;
        }

        private static IEnumerable<IntVec3> HiveRoomExpansionCandidateCells(Map map, Room room, IntVec3 anchorCell)
        {
            HashSet<IntVec3> cells = new HashSet<IntVec3>();

            foreach (IntVec3 borderCell in room.BorderCellsCardinal)
            {
                AddHiveRoomExpansionCandidateCell(map, cells, borderCell);
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    AddHiveRoomExpansionCandidateCell(map, cells, borderCell + direction);
                }
            }

            if (cells.Count == 0 && anchorCell.IsValid && anchorCell.InBounds(map))
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(anchorCell, NestSeedRadius, true))
                {
                    AddHiveRoomExpansionCandidateCell(map, cells, cell);
                }
            }

            List<IntVec3> result = cells.ToList();
            result.Shuffle();
            return result;
        }

        private static void AddHiveRoomExpansionCandidateCell(Map map, HashSet<IntVec3> cells, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map) || cells.Contains(cell) || IsHiveTerrain(cell, map) || cell.GetTerrain(map) == ExternalDefOf.EmptySpace)
            {
                return;
            }

            Building edifice = cell.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return;
            }

            if (AdjacentHiveFloorCount(map, cell) <= 0 && AdjacentSolidStructureCount(map, cell) <= 0)
            {
                return;
            }

            cells.Add(cell);
        }

        private static bool TryFindStructureClingFloorRequest(Map map, IntVec3 locus, IntVec3 nestSeed, List<IntVec3> localHivePatch, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            IntVec3 bestCell = IntVec3.Invalid;
            ThingDef bestDef = null;
            int bestScore = int.MinValue;

            foreach (IntVec3 cell in CandidateCells(map, locus, nestSeed))
            {
                if (!CanClaimFloorAt(map, cell, builder, out ThingDef buildDef) || !IsStructureClingFloorCandidate(map, cell))
                {
                    continue;
                }

                int score = Rand.RangeInclusive(0, 12);
                score += CardinalOpenHiveFloorAdjacentCount(map, cell) * 45;
                score += AdjacentNonHiveSolidStructureCount(map, cell) * 30;
                score += LocalHiveOrStructureContainmentCount(map, cell) * 12;
                score -= PassableNonHiveOpenCardinalAdjacentCount(map, cell) * 10;
                score -= Mathf.CeilToInt(cell.DistanceTo(locus) * 2f);
                score -= Mathf.CeilToInt(cell.DistanceTo(nestSeed));

                if (localHivePatch.Count == 0)
                {
                    score -= 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestDef = buildDef;
                }
            }

            if (!bestCell.IsValid)
            {
                return false;
            }

            request = new NestBuildRequest
            {
                stage = NestBuildStage.ClaimFloor,
                cell = bestCell,
                buildDef = bestDef,
                score = bestScore,
                reason = "claim structure-cling floor"
            };
            return true;
        }

        private static bool TryFindPerimeterPocketFloorRequest(Map map, IntVec3 locus, IntVec3 nestSeed, Pawn builder, NestEncirclementPlan plan, out NestBuildRequest request)
        {
            request = default;
            if (plan?.requests == null || plan.requests.Count == 0)
            {
                return false;
            }

            HashSet<IntVec3> queuedMassCells = new HashSet<IntVec3>(plan.requests
                .Where(queuedRequest => queuedRequest.buildDef == XenoBuildingDefOf.Hivemass)
                .Select(queuedRequest => queuedRequest.cell));

            if (queuedMassCells.Count == 0)
            {
                return false;
            }

            IntVec3 bestCell = IntVec3.Invalid;
            ThingDef bestDef = null;
            int bestScore = int.MinValue;
            HashSet<IntVec3> checkedCells = new HashSet<IntVec3>();

            foreach (IntVec3 cell in CandidateCells(map, locus, nestSeed))
            {
                if (checkedCells.Contains(cell) || !CanClaimFloorAt(map, cell, builder, out ThingDef buildDef))
                {
                    continue;
                }

                if (!TryCollectQueuedPocketRegion(map, cell, queuedMassCells, out List<IntVec3> region))
                {
                    continue;
                }

                foreach (IntVec3 regionCell in region)
                {
                    checkedCells.Add(regionCell);
                }

                if (!RegionLooksLikeQueuedPerimeterPocket(map, region, queuedMassCells))
                {
                    continue;
                }

                int score = Rand.RangeInclusive(0, 12);
                score += (MaxQueuedPocketFloorArea - region.Count) * 18;
                score += region.Sum(regionCell => AdjacentQueuedMassCount(regionCell, queuedMassCells)) * 25;
                score += region.Sum(regionCell => AdjacentNonHiveSolidStructureCount(map, regionCell)) * 12;
                score += region.Sum(regionCell => CardinalOpenHiveFloorAdjacentCount(map, regionCell)) * 20;
                score -= Mathf.CeilToInt(cell.DistanceTo(locus) * 2f);
                score -= Mathf.CeilToInt(cell.DistanceTo(nestSeed));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestDef = buildDef;
                }
            }

            if (!bestCell.IsValid)
            {
                return false;
            }

            request = new NestBuildRequest
            {
                stage = NestBuildStage.ClaimFloor,
                cell = bestCell,
                buildDef = bestDef,
                score = bestScore,
                reason = "claim queued perimeter pocket"
            };
            return true;
        }

        private static bool TryGetQueuedEnclosureRequest(Map map, IntVec3 locus, IntVec3 nestSeed, List<IntVec3> localHivePatch, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            if (localHivePatch.Count < MinimumHiveFloorAreaForEnclosure && !HasDamagedOrOpenHiveRoom(map, locus, nestSeed))
            {
                return false;
            }

            if (IsLocalHiveAreaEnclosed(map, localHivePatch, nestSeed))
            {
                ClearEncirclementPlan(map, nestSeed);
                return false;
            }

            NestEncirclementPlan plan = GetEncirclementPlan(map, nestSeed);
            if (plan != null && TryGetNextQueuedEnclosureRequest(plan, builder, out request))
            {
                return true;
            }

            if (plan == null || plan.requests.Count == 0)
            {
                plan = BuildEncirclementPlan(map, locus, nestSeed, localHivePatch);
                if (TryFindPerimeterPocketFloorRequest(map, locus, nestSeed, builder, plan, out request))
                {
                    ClearEncirclementPlan(map, nestSeed);
                    return true;
                }

                ReplaceEncirclementPlan(plan);
            }

            return plan != null && TryGetNextQueuedEnclosureRequest(plan, builder, out request);
        }

        private static NestEncirclementPlan BuildEncirclementPlan(Map map, IntVec3 locus, IntVec3 nestSeed, List<IntVec3> localHivePatch)
        {
            Dictionary<IntVec3, int> edgeScores = new Dictionary<IntVec3, int>();
            HashSet<IntVec3> hiveCells = new HashSet<IntVec3>(localHivePatch);

            foreach (IntVec3 hiveCell in localHivePatch)
            {
                foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(hiveCell, Rot4.North, new IntVec2(1, 1)))
                {
                    if (!adjacent.InBounds(map) || hiveCells.Contains(adjacent))
                    {
                        continue;
                    }

                    if (!IsInitialEncirclementEdgeCell(map, adjacent))
                    {
                        continue;
                    }

                    if (CanPlaceQueuedPerimeterBuildingAt(map, adjacent, null))
                    {
                        int score = ScoreQueuedEdgeCell(map, adjacent, locus, nestSeed);
                        if (!edgeScores.ContainsKey(adjacent) || score > edgeScores[adjacent])
                        {
                            edgeScores[adjacent] = score;
                        }
                    }
                }
            }

            List<IntVec3> webbingCells = SelectQueuedWebbingCells(map, edgeScores, hiveCells);
            HashSet<IntVec3> webbingSet = new HashSet<IntVec3>(webbingCells);
            List<NestBuildRequest> requests = new List<NestBuildRequest>();
            foreach (IntVec3 webbingCell in webbingCells)
            {
                requests.Add(new NestBuildRequest
                {
                    stage = NestBuildStage.EncloseOpenRoom,
                    cell = webbingCell,
                    buildDef = XenoBuildingDefOf.HiveWebbing,
                    score = edgeScores[webbingCell] + 1000,
                    reason = "queued webbing opening"
                });
            }

            foreach (KeyValuePair<IntVec3, int> candidate in edgeScores.OrderByDescending(pair => pair.Value))
            {
                if (webbingSet.Contains(candidate.Key))
                {
                    continue;
                }

                if (IsDoorAccessPerimeterCell(map, candidate.Key))
                {
                    continue;
                }

                requests.Add(new NestBuildRequest
                {
                    stage = NestBuildStage.EncloseOpenRoom,
                    cell = candidate.Key,
                    buildDef = XenoBuildingDefOf.Hivemass,
                    score = candidate.Value,
                    reason = "queued perimeter mass"
                });

                if (requests.Count >= MaxQueuedEnclosureRequests)
                {
                    break;
                }
            }

            if (requests.Count == 0)
            {
                return null;
            }

            return new NestEncirclementPlan
            {
                map = map,
                nestSeed = nestSeed,
                requests = requests.OrderByDescending(queuedRequest => queuedRequest.score).ToList()
            };
        }

        private static List<IntVec3> SelectQueuedWebbingCells(Map map, Dictionary<IntVec3, int> edgeScores, HashSet<IntVec3> hiveCells)
        {
            List<IntVec3> result = new List<IntVec3>();
            if (edgeScores.Count < 4)
            {
                return result;
            }

            int targetCount = Mathf.Clamp(edgeScores.Count / 18, MinQueuedWebbingRequests, MaxQueuedWebbingRequests);
            Dictionary<IntVec3, int> webbingScores = new Dictionary<IntVec3, int>();
            foreach (IntVec3 cell in edgeScores.Keys)
            {
                webbingScores[cell] = ScoreQueuedWebbingCandidate(map, cell, hiveCells);
            }

            foreach (KeyValuePair<IntVec3, int> candidate in webbingScores.Where(pair => IsDoorAccessPerimeterCell(map, pair.Key) && pair.Value > 0).OrderByDescending(pair => pair.Value))
            {
                if (!result.Contains(candidate.Key))
                {
                    result.Add(candidate.Key);
                }
            }

            foreach (KeyValuePair<IntVec3, int> candidate in webbingScores.Where(pair => pair.Value > 0).OrderByDescending(pair => pair.Value))
            {
                if (result.Contains(candidate.Key))
                {
                    continue;
                }

                if (result.Any(cell => cell.DistanceTo(candidate.Key) < 4f))
                {
                    continue;
                }

                result.Add(candidate.Key);
                if (result.Count >= targetCount)
                {
                    break;
                }
            }

            return result;
        }

        private static int ScoreQueuedWebbingCandidate(Map map, IntVec3 cell, HashSet<IntVec3> hiveCells)
        {
            int score = Rand.RangeInclusive(0, 8);
            int cardinalHive = 0;
            int cardinalOutside = 0;
            int oppositeCrossings = 0;

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 inside = cell + direction;
                IntVec3 outside = cell - direction;

                bool hasHiveSide = inside.InBounds(map) && hiveCells.Contains(inside);
                bool hasOutsideSide = IsPassableExteriorCell(map, outside);

                if (hasHiveSide)
                {
                    cardinalHive++;
                }

                if (IsPassableExteriorCell(map, inside))
                {
                    cardinalOutside++;
                }

                if (hasHiveSide && hasOutsideSide)
                {
                    oppositeCrossings++;
                }
            }

            if (oppositeCrossings == 0)
            {
                return -1000;
            }

            score += oppositeCrossings * 80;
            score += Mathf.Min(cardinalHive, cardinalOutside) * 30;
            score += cardinalHive * 8;
            score += IsDoorAccessPerimeterCell(map, cell) ? 500 : 0;

            if (cardinalHive == 0)
            {
                score -= 100;
            }

            if (cardinalOutside == 0)
            {
                score -= 60;
            }

            score -= AdjacentSolidStructureCount(map, cell) * 8;
            return score;
        }

        private static int ScoreQueuedEdgeCell(Map map, IntVec3 cell, IntVec3 locus, IntVec3 nestSeed)
        {
            int score = Rand.RangeInclusive(0, 12);
            score += AdjacentHiveFloorCount(map, cell) * 30;
            score += AdjacentHeavyHiveFloorCount(map, cell) * 12;
            score += AdjacentSolidStructureCount(map, cell) * 12;
            score -= PassableNonHiveOpenAdjacentCount(map, cell) * 3;
            score -= Mathf.CeilToInt(cell.DistanceTo(locus) * 2f);
            score -= Mathf.CeilToInt(cell.DistanceTo(nestSeed));
            return score;
        }

        private static bool TryGetNextQueuedEnclosureRequest(NestEncirclementPlan plan, Pawn builder, out NestBuildRequest request)
        {
            request = default;
            if (plan?.map == null || plan.requests == null)
            {
                return false;
            }

            for (int i = plan.requests.Count - 1; i >= 0; i--)
            {
                NestBuildRequest candidate = plan.requests[i];
                if (IsQueuedRequestCompleted(plan.map, candidate) || !IsQueuedRequestStillValid(plan, candidate))
                {
                    plan.requests.RemoveAt(i);
                }
            }

            for (int i = 0; i < plan.requests.Count; i++)
            {
                NestBuildRequest candidate = plan.requests[i];
                if (!IsQueuedRequestAvailableForBuilder(plan, candidate, builder))
                {
                    continue;
                }

                request = candidate;
                return true;
            }

            return false;
        }

        private static bool IsQueuedRequestCompleted(Map map, NestBuildRequest request)
        {
            Building edifice = request.cell.GetEdifice(map);
            return edifice != null && edifice.def == request.buildDef;
        }

        private static bool IsQueuedRequestStillValid(NestEncirclementPlan plan, NestBuildRequest request)
        {
            Map map = plan?.map;
            if (request.buildDef == XenoBuildingDefOf.Hivemass)
            {
                return CanPlaceQueuedPerimeterBuildingAt(map, request.cell, null) &&
                       !WouldBlockWebbingAccess(map, request.cell, plan);
            }

            if (request.buildDef == XenoBuildingDefOf.HiveWebbing)
            {
                return CanPlaceQueuedPerimeterBuildingAt(map, request.cell, null);
            }

            return false;
        }

        private static bool IsQueuedRequestAvailableForBuilder(NestEncirclementPlan plan, NestBuildRequest request, Pawn builder)
        {
            Map map = plan?.map;
            if (builder == null)
            {
                return true;
            }

            if (request.buildDef == XenoBuildingDefOf.Hivemass)
            {
                return CanPlaceQueuedPerimeterBuildingAt(map, request.cell, builder) &&
                       !WouldBlockWebbingAccess(map, request.cell, plan);
            }

            if (request.buildDef == XenoBuildingDefOf.HiveWebbing)
            {
                return CanPlaceQueuedPerimeterBuildingAt(map, request.cell, builder);
            }

            return false;
        }

        private static bool WouldBlockWebbingAccess(Map map, IntVec3 massCell, NestEncirclementPlan plan)
        {
            if (map == null || !massCell.InBounds(map))
            {
                return false;
            }

            if (IsDoorAccessPerimeterCell(map, massCell))
            {
                return true;
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 webbingCell = massCell + direction;
                if (!webbingCell.InBounds(map) || !IsWebbingAccessCell(map, webbingCell, plan))
                {
                    continue;
                }

                int currentCrossings = CountWebbingAccessCrossings(map, webbingCell, IntVec3.Invalid);
                if (currentCrossings > 0 && CountWebbingAccessCrossings(map, webbingCell, massCell) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountWebbingAccessCrossings(Map map, IntVec3 webbingCell, IntVec3 blockedCell)
        {
            int crossings = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 firstSide = webbingCell + direction;
                IntVec3 secondSide = webbingCell - direction;
                if ((IsWebbingInteriorAccessCell(map, firstSide, blockedCell) && IsWebbingExteriorAccessCell(map, secondSide, blockedCell)) ||
                    (IsWebbingInteriorAccessCell(map, secondSide, blockedCell) && IsWebbingExteriorAccessCell(map, firstSide, blockedCell)))
                {
                    crossings++;
                }
            }

            return crossings;
        }

        private static bool IsWebbingAccessCell(Map map, IntVec3 cell, NestEncirclementPlan plan)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice != null && IsHiveWebbingDef(edifice.def))
            {
                return true;
            }

            return plan?.requests != null &&
                   plan.requests.Any(request => request.cell == cell && IsHiveWebbingDef(request.buildDef));
        }

        private static bool IsWebbingInteriorAccessCell(Map map, IntVec3 cell, IntVec3 blockedCell)
        {
            if (cell == blockedCell || !cell.InBounds(map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            return IsHiveTerrain(cell, map) && (edifice == null || !IsHiveBorderBarrier(edifice.def));
        }

        private static bool IsWebbingExteriorAccessCell(Map map, IntVec3 cell, IntVec3 blockedCell)
        {
            return cell != blockedCell && IsPassableExteriorCell(map, cell);
        }

        private static NestEncirclementPlan GetEncirclementPlan(Map map, IntVec3 nestSeed)
        {
            EncirclementPlans.RemoveAll(plan => plan == null || plan.map == null || plan.requests == null || plan.requests.Count == 0);
            return EncirclementPlans.FirstOrDefault(plan => plan.map == map && plan.nestSeed == nestSeed);
        }

        private static void ReplaceEncirclementPlan(NestEncirclementPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            ClearEncirclementPlan(plan.map, plan.nestSeed);
            EncirclementPlans.Add(plan);
        }

        private static void ClearEncirclementPlan(Map map, IntVec3 nestSeed)
        {
            EncirclementPlans.RemoveAll(plan => plan.map == map && plan.nestSeed == nestSeed);
        }

        private static IEnumerable<IntVec3> CandidateCells(Map map, IntVec3 locus, IntVec3 nestSeed)
        {
            List<IntVec3> cells = new List<IntVec3>();
            AddCells(cells, map, locus, LocalPatchRadius);
            AddCells(cells, map, nestSeed, NestSeedRadius);
            cells.Shuffle();
            if (cells.Count > MaxCandidates)
            {
                cells.RemoveRange(MaxCandidates, cells.Count - MaxCandidates);
            }

            return cells;
        }

        private static void AddCells(List<IntVec3> cells, Map map, IntVec3 center, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (cell.InBounds(map) && !cells.Contains(cell))
                {
                    cells.Add(cell);
                }
            }
        }

        private static List<IntVec3> ConnectedHiveFloorPatch(Map map, IntVec3 locus, IntVec3 nestSeed)
        {
            IntVec3 start = IntVec3.Invalid;
            if (IsOpenHiveFloorForNestPatch(locus, map))
            {
                start = locus;
            }
            else if (IsOpenHiveFloorForNestPatch(nestSeed, map))
            {
                start = nestSeed;
            }
            else
            {
                foreach (IntVec3 cell in CandidateCells(map, locus, nestSeed))
                {
                    if (IsOpenHiveFloorForNestPatch(cell, map))
                    {
                        start = cell;
                        break;
                    }
                }
            }

            List<IntVec3> result = new List<IntVec3>();
            if (!start.IsValid)
            {
                return result;
            }

            Queue<IntVec3> open = new Queue<IntVec3>();
            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            open.Enqueue(start);
            seen.Add(start);

            while (open.Count > 0 && result.Count < 256)
            {
                IntVec3 current = open.Dequeue();
                result.Add(current);
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = current + direction;
                    if (!adjacent.InBounds(map) || seen.Contains(adjacent) || !IsOpenHiveFloorForNestPatch(adjacent, map))
                    {
                        continue;
                    }

                    if (adjacent.DistanceTo(locus) > LocalPatchRadius + 6f && adjacent.DistanceTo(nestSeed) > NestSeedRadius + 6f)
                    {
                        continue;
                    }

                    seen.Add(adjacent);
                    open.Enqueue(adjacent);
                }
            }

            return result;
        }

        private static bool HasEdgeLikeCardinalScan(Map map, IntVec3 locus, int sightLimit)
        {
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                bool sawNonHiveFloorCandidate = false;
                for (int i = 1; i <= sightLimit; i++)
                {
                    IntVec3 cell = locus + (direction * i);
                    if (!cell.InBounds(map))
                    {
                        break;
                    }

                    if (CanClaimFloorAt(map, cell, null, out ThingDef _))
                    {
                        sawNonHiveFloorCandidate = true;
                        break;
                    }
                }

                if (!sawNonHiveFloorCandidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanClaimFloorAt(Map map, IntVec3 cell, Pawn builder, out ThingDef buildDef)
        {
            buildDef = null;
            if (map == null || !cell.InBounds(map) || IsHiveTerrain(cell, map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            if (IsHiveBorderBarrier(edifice?.def))
            {
                return false;
            }

            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return false;
            }

            if (builder != null && !FeralJobUtility.IsPlaceAvailableForJobBy(builder, cell))
            {
                return false;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null || terrain == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            if (XMTHiveUtility.HiveLightSuitabilityAt(cell, map) < MinimumBuildHiveLightSuitability)
            {
                return false;
            }

            if (terrain.affordances != null && terrain.affordances.Contains(TerrainAffordanceDefOf.Bridgeable) && !terrain.affordances.Contains(TerrainAffordanceDefOf.Light))
            {
                buildDef = XenoBuildingDefOf.HiveBridgeBuildable;
                return buildDef != null;
            }

            if (terrain.affordances != null && terrain.affordances.Contains(TerrainAffordanceDefOf.Light))
            {
                buildDef = XenoBuildingDefOf.HiveFloorBuildable;
                return buildDef != null;
            }

            return false;
        }

        private static bool CanPlaceQueuedPerimeterBuildingAt(Map map, IntVec3 cell, Pawn builder)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            if (IsHiveBorderBarrier(edifice?.def))
            {
                return false;
            }

            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return false;
            }

            if (builder != null && !FeralJobUtility.IsPlaceAvailableForJobBy(builder, cell))
            {
                return false;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            return terrain != null && terrain != ExternalDefOf.EmptySpace;
        }

        private static bool IsInitialEncirclementEdgeCell(Map map, IntVec3 cell)
        {
            return cell.InBounds(map) &&
                   !IsHiveTerrain(cell, map) &&
                   IsPassableNonHiveOpenTerrain(map, cell) &&
                   cell.GetTerrain(map) != ExternalDefOf.EmptySpace;
        }

        private static bool IsPassableExteriorCell(Map map, IntVec3 cell)
        {
            return cell.InBounds(map) &&
                   !IsHiveTerrain(cell, map) &&
                   IsPassableNonHiveOpenTerrain(map, cell) &&
                   cell.GetTerrain(map) != ExternalDefOf.EmptySpace;
        }

        private static bool IsDoorAccessPerimeterCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map) || cell.GetTerrain(map) == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return false;
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                if (adjacent.GetEdifice(map) is Building_Door)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLocalHiveAreaEnclosed(Map map, List<IntVec3> localHivePatch, IntVec3 nestSeed)
        {
            if (localHivePatch.Count == 0)
            {
                return false;
            }

            if (HasOpenHivePerimeter(map, localHivePatch))
            {
                return false;
            }

            foreach (IntVec3 cell in localHivePatch)
            {
                Room room = cell.GetRoomOrAdjacent(map);
                if (!IsEnclosedHiveRoom(room))
                {
                    return false;
                }
            }

            return true;
        }

        internal static List<IntVec3> HiveRoofBatchCells(Map map, IntVec3 center, Pawn builder)
        {
            List<IntVec3> result = new List<IntVec3>();
            if (map == null || !center.InBounds(map))
            {
                return result;
            }

            Room room = center.GetRoomOrAdjacent(map);
            if (!RoomNeedsHiveRoof(room))
            {
                return result;
            }

            foreach (IntVec3 offset in GenAdj.AdjacentCellsAndInside)
            {
                IntVec3 cell = center + offset;
                if (cell.InBounds(map) &&
                    cell.GetRoomOrAdjacent(map) == room &&
                    CanBuildHiveRoofAt(map, cell, builder))
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        private static bool HasDamagedOrOpenHiveRoom(Map map, IntVec3 locus, IntVec3 nestSeed)
        {
            Room room = locus.GetRoomOrAdjacent(map) ?? nestSeed.GetRoomOrAdjacent(map);
            if (room == null)
            {
                return false;
            }

            int hiveCells = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                if (IsHiveTerrain(cell, map) && ++hiveCells >= MinimumHiveFloorAreaForEnclosure)
                {
                    return !IsWallDoorEnclosedRoom(room);
                }
            }

            return false;
        }

        private static bool RoomNeedsHiveRoof(Room room)
        {
            return IsEnclosedHiveRoom(room) &&
                   room.OpenRoofCount > 0;
        }

        internal static bool TryRefogHiveRoom(Room room, bool unfogFromColonists = true)
        {
            if (XMTHiveDebug.DisableHiveRoomRefog)
            {
                return false;
            }

            if (!IsFinishedHiveRoom(room) || !ShouldRefogFinishedHiveRoom(room))
            {
                return false;
            }

            Map map = room.Map;
            bool refoggedAny = false;
            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.InBounds(map) && !cell.Fogged(map))
                {
                    map.fogGrid.Refog(CellRect.CenteredOn(cell, 0));
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.FogOfWar);
                    refoggedAny = true;
                }
            }

            if (!refoggedAny)
            {
                return false;
            }

            if (!unfogFromColonists)
            {
                return true;
            }

            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist != null && colonist.Spawned && !XMTUtility.IsCocooned(colonist))
                {
                    FloodFillerFog.FloodUnfog(colonist.Position, map);
                }
            }

            return true;
        }

        private static bool IsFinishedHiveRoom(Room room)
        {
            return IsEnclosedHiveRoom(room) &&
                   room.OpenRoofCount <= 0;
        }

        private static bool ShouldRefogFinishedHiveRoom(Room room)
        {
            if (PlayerFactionIsCryptimorphNest())
            {
                return false;
            }

            if (PlayerHasCryptimorphPawnOnMap(room.Map))
            {
                return !RoomHasUnsuitableLight(room);
            }

            return true;
        }

        private static bool PlayerFactionIsCryptimorphNest()
        {
            return Faction.OfPlayerSilentFail?.def == InternalDefOf.XMT_PlayerHive;
        }

        private static bool PlayerHasCryptimorphPawnOnMap(Map map)
        {
            if (map == null)
            {
                return false;
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn != null && !pawn.Dead && pawn.Faction != null && pawn.Faction.IsPlayer && XMTUtility.IsXenomorph(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RoomHasUnsuitableLight(Room room)
        {
            Map map = room.Map;
            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.InBounds(map) && XMTHiveUtility.HiveLightSuitabilityAt(cell, map) < MinimumRefogHiveLightSuitability)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool CanBuildHiveRoofAt(Map map, IntVec3 cell, Pawn builder)
        {
            if (map == null || !cell.InBounds(map) || cell.Roofed(map))
            {
                return false;
            }

            if (cell.GetTerrain(map) == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            Thing blocker = RoofUtility.FirstBlockingThing(cell, map);
            if (blocker != null)
            {
                return false;
            }

            if (!RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map, assumeNonNoRoofCellsAreRoofed: true))
            {
                return false;
            }

            if (builder != null)
            {
                if (!FeralJobUtility.IsPlaceAvailableForJobBy(builder, cell))
                {
                    return false;
                }

                if (!ClimbUtility.CanReachByWalkingOrClimb(builder, cell, PathEndMode.Touch, Danger.Deadly))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEnclosedHiveRoom(Room room)
        {
            if (!IsWallDoorEnclosedRoom(room))
            {
                return false;
            }

            int hiveCells = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                if (IsHiveTerrain(cell, room.Map) && ++hiveCells >= MinimumHiveFloorAreaForEnclosure)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWallDoorEnclosedRoom(Room room)
        {
            return room != null && !room.IsDoorway && !room.TouchesMapEdge;
        }

        private static bool HasLocalHiveTerrain(Map map, IntVec3 center, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (cell.InBounds(map) && IsHiveTerrain(cell, map))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOpenHivePerimeter(Map map, List<IntVec3> localHivePatch)
        {
            foreach (IntVec3 hiveCell in localHivePatch)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = hiveCell + direction;
                    if (!adjacent.InBounds(map) || IsHiveTerrain(adjacent, map))
                    {
                        continue;
                    }

                    if (IsPassableNonHiveOpenTerrain(map, adjacent) && adjacent.GetTerrain(map) != ExternalDefOf.EmptySpace)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCollectQueuedPocketRegion(Map map, IntVec3 start, HashSet<IntVec3> queuedMassCells, out List<IntVec3> region)
        {
            region = new List<IntVec3>();
            if (!IsQueuedPocketOpenCell(map, start, queuedMassCells))
            {
                return false;
            }

            Queue<IntVec3> open = new Queue<IntVec3>();
            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            open.Enqueue(start);
            seen.Add(start);

            while (open.Count > 0)
            {
                IntVec3 current = open.Dequeue();
                region.Add(current);
                if (region.Count > MaxQueuedPocketFloorArea)
                {
                    return false;
                }

                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = current + direction;
                    if (seen.Contains(adjacent) || !IsQueuedPocketOpenCell(map, adjacent, queuedMassCells))
                    {
                        continue;
                    }

                    seen.Add(adjacent);
                    open.Enqueue(adjacent);
                }
            }

            return true;
        }

        private static bool RegionLooksLikeQueuedPerimeterPocket(Map map, List<IntVec3> region, HashSet<IntVec3> queuedMassCells)
        {
            if (region == null || region.Count == 0 || region.Count > MaxQueuedPocketFloorArea)
            {
                return false;
            }

            bool touchesQueuedMass = false;
            bool touchesHiveFloor = false;
            bool touchesNonHiveStructure = false;
            foreach (IntVec3 cell in region)
            {
                touchesQueuedMass |= AdjacentQueuedMassCount(cell, queuedMassCells) > 0;
                touchesHiveFloor |= CardinalOpenHiveFloorAdjacentCount(map, cell) > 0;
                touchesNonHiveStructure |= AdjacentNonHiveSolidStructureCount(map, cell) > 0;
            }

            return touchesQueuedMass && touchesHiveFloor && touchesNonHiveStructure;
        }

        private static bool IsQueuedPocketOpenCell(Map map, IntVec3 cell, HashSet<IntVec3> queuedMassCells)
        {
            if (!cell.InBounds(map) || queuedMassCells.Contains(cell) || IsHiveTerrain(cell, map))
            {
                return false;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null || terrain == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            return edifice == null ||
                   (edifice.def.passability != Traversability.Impassable && !IsHiveBorderBarrier(edifice.def));
        }

        private static int AdjacentQueuedMassCount(IntVec3 cell, HashSet<IntVec3> queuedMassCells)
        {
            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                if (queuedMassCells.Contains(cell + direction))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsStructureClingFloorCandidate(Map map, IntVec3 cell)
        {
            if (CardinalOpenHiveFloorAdjacentCount(map, cell) == 0)
            {
                return false;
            }

            if (AdjacentNonHiveSolidStructureCount(map, cell) == 0)
            {
                return false;
            }

            if (LocalHiveOrStructureContainmentCount(map, cell) < 3)
            {
                return false;
            }

            if (PassableNonHiveOpenCardinalAdjacentCount(map, cell) > 2)
            {
                return false;
            }

            return !IsExposedParallelWallFace(map, cell);
        }

        private static int CardinalOpenHiveFloorAdjacentCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (adjacent.InBounds(map) && IsOpenHiveFloorForNestPatch(adjacent, map))
                {
                    count++;
                }
            }

            return count;
        }

        private static int AdjacentNonHiveSolidStructureCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (adjacent.InBounds(map) && IsNonHiveSolidStructure(map, adjacent))
                {
                    count++;
                }
            }

            return count;
        }

        private static int LocalHiveOrStructureContainmentCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                if (IsOpenHiveFloorForNestPatch(adjacent, map) || IsNonHiveSolidStructure(map, adjacent))
                {
                    count++;
                }
            }

            return count;
        }

        private static int PassableNonHiveOpenCardinalAdjacentCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (adjacent.InBounds(map) && IsPassableNonHiveOpenTerrain(map, adjacent))
                {
                    count++;
                }
            }

            return count;
        }

        // Long-wall bleed guard for the structure-cling floor prepass.
        // If this fights legitimate room filling, disable this section by returning false here;
        // the core pocket heuristics above will still require hive support and local containment.
        private static bool IsExposedParallelWallFace(Map map, IntVec3 cell)
        {
            if (CardinalOpenHiveFloorAdjacentCount(map, cell) > 1)
            {
                return false;
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 wallCell = cell + direction;
                if (!wallCell.InBounds(map) || !IsNonHiveSolidStructure(map, wallCell))
                {
                    continue;
                }

                IntVec3 parallel = PerpendicularDirection(direction);
                if (IsPassableExteriorCell(map, cell + parallel) && IsPassableExteriorCell(map, cell - parallel))
                {
                    return true;
                }
            }

            return false;
        }

        private static IntVec3 PerpendicularDirection(IntVec3 direction)
        {
            if (direction.x != 0)
            {
                return new IntVec3(0, 0, 1);
            }

            return new IntVec3(1, 0, 0);
        }

        private static bool IsNonHiveSolidStructure(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            return edifice != null &&
                   edifice.def.passability == Traversability.Impassable &&
                   !IsHiveBorderBarrier(edifice.def);
        }

        private static int AdjacentHiveFloorCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (adjacent.InBounds(map) && IsHiveTerrain(adjacent, map))
                {
                    count++;
                }
            }

            return count;
        }

        private static int AdjacentHeavyHiveFloorCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (adjacent.InBounds(map) && IsHeavyHiveTerrain(adjacent, map))
                {
                    count++;
                }
            }

            return count;
        }

        private static int AdjacentSolidStructureCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                Building edifice = adjacent.GetEdifice(map);
                if (edifice != null && edifice.def.passability == Traversability.Impassable)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsHiveBorderBarrier(ThingDef def)
        {
            return def == XenoBuildingDefOf.Hivemass ||
                   def == XenoBuildingDefOf.HiveWebbing ||
                   def == XenoBuildingDefOf.HiveReinforcement ||
                   def == XenoBuildingDefOf.HiveMassBuildable ||
                   def == XenoBuildingDefOf.HiveWebbingBuildable;
        }

        private static bool IsHiveWebbingDef(ThingDef def)
        {
            return def == XenoBuildingDefOf.HiveWebbing ||
                   def == XenoBuildingDefOf.HiveWebbingBuildable;
        }

        private static int PassableNonHiveOpenAdjacentCount(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (adjacent.InBounds(map) && IsPassableNonHiveOpenTerrain(map, adjacent))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsPassableNonHiveOpenTerrain(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || IsHiveTerrain(cell, map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            return edifice == null || edifice.def.passability != Traversability.Impassable;
        }

        private static bool IsOpenHiveFloorForNestPatch(IntVec3 cell, Map map)
        {
            if (!IsHiveTerrain(cell, map))
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            return edifice == null || !IsHiveBorderBarrier(edifice.def);
        }

        private static bool IsHiveTerrain(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            return terrain == InternalDefOf.HiveFloor || terrain == InternalDefOf.HeavyHiveFloor || terrain == InternalDefOf.SmoothHiveFloor;
        }

        private static bool IsHeavyHiveTerrain(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && cell.GetTerrain(map) == InternalDefOf.HeavyHiveFloor;
        }
    }
}
