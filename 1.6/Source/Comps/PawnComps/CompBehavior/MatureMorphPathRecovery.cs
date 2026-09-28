using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal sealed class MatureMorphPathRecovery
    {
        private readonly CompMatureMorph self;
        private readonly MatureMorphPathRecoveryState state;

        private Pawn Parent => self.Parent;

        public MatureMorphPathRecovery(CompMatureMorph self, MatureMorphPathRecoveryState state)
        {
            this.self = self;
            this.state = state;
        }

        public void NotifyPathFailure(LocalTargetInfo target, Job job, bool confirmedPatherFailure = false,
            PathEndMode pathEndMode = PathEndMode.Touch)
        {
            if (Parent == null || Parent.Dead || Parent.MapHeld == null || !target.IsValid || !target.Cell.IsValid)
            {
                return;
            }

            NotifyPathFailure(target.Cell, job?.def, job != null && job.playerForced, confirmedPatherFailure, pathEndMode);
        }

        public void NotifyPathFailure(IntVec3 targetCell, JobDef jobDef, bool playerForced,
            bool confirmedPatherFailure = false, PathEndMode pathEndMode = PathEndMode.Touch)
        {
            if (Parent == null || Parent.Dead || Parent.MapHeld == null || !targetCell.IsValid)
            {
                return;
            }

            if (jobDef != null &&
                (jobDef == XenoWorkDefOf.XMT_HiveBuilding ||
                 jobDef == XenoWorkDefOf.XMT_HiveRoofing ||
                 jobDef == XenoWorkDefOf.XMT_PathRecoveryOpenDoor ||
                 jobDef == XenoWorkDefOf.XMT_PathRecoveryBreach))
            {
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message("[XMT][JobGiver][PathRecovery] " + Parent + " ignoring path failure from excluded job " + jobDef.defName + " at " + targetCell + ".");
                }
                Clear();
                return;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver][PathRecovery] " + Parent + " NotifyPathFailure tick=" + Find.TickManager.TicksGame + " target=" + targetCell + " job=" + jobDef?.defName + " before=" + state.DebugSummary(Parent.MapHeld));
            }

            state.NotifyFailure(targetCell, jobDef, playerForced, confirmedPatherFailure, pathEndMode,
                Parent.MapHeld, Parent.PositionHeld);
            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver][PathRecovery] " + Parent + " NotifyPathFailure after=" + state.DebugSummary(Parent.MapHeld));
            }
        }

        public void Clear()
        {
            state.Clear();
        }

        public bool TryGetJob(out Job job)
        {
            job = null;
            if (Parent == null || Parent.Dead || Parent.MapHeld == null || !state.Active)
            {
                return false;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver][PathRecovery] " + Parent + " evaluating recovery tick=" + Find.TickManager.TicksGame + " state=" + state.DebugSummary(Parent.MapHeld));
            }

            if (!state.IsForMap(Parent.MapHeld) || !state.TargetCell.InBounds(Parent.MapHeld))
            {

                Clear();
                return false;
            }

            if (ClimbUtility.OriginalCanReach(Parent, state.TargetCell, state.FailedPathEndMode, Danger.Deadly))
            {

                Clear();
                return false;
            }

            if (!CanUsePathRecovery())
            {

                return false;
            }

            if (!TryGetRecoveryContext(out RecoveryContext context))
            {
                Clear();
                return false;
            }

            if (context.SeekerTrapped && TryGetDoorwayEscapeRecoveryJob(out job))
            {
                Clear();
                return true;
            }

            if (!Parent.DevelopmentalStage.Adult())
            {
                if (TryGetLocalMatureRecoveryJob(out job))
                {
                    Clear();
                    return true;
                }

                if (TryGetLocalFoodRecoveryJob(out job))
                {
                    return true;
                }

                Clear();
                return false;
            }

            if (TryGetUnpoweredDoorRecoveryJob(context, out job))
            {
                Clear();
                return true;
            }

            if (TryGetLocalPowerSabotageRecoveryJob(out job))
            {
                Clear();
                return true;
            }

            if (TryGetBreachRecoveryJob(context, out job))
            {
                Clear();
                return true;
            }

            Clear();
            return false;
        }

        private bool CanUsePathRecovery()
        {
            if (Parent.IsPrisoner)
            {
                return false;
            }

            return Parent.Faction == null ||
                   !Parent.Faction.IsPlayer ||
                   state.ConfirmedPatherFailure ||
                   state.PlayerForced ||
                   (XMTUtility.NoQueenPresent() && XMTSettings.PlayerSabotage);
        }

        private bool TryGetDoorwayEscapeRecoveryJob(out Job job)
        {
            job = null;
            if (!PathRecoveryJobUtility.IsInDoorwayOrRoomBorder(Parent))
            {
                return false;
            }

            if (!PathRecoveryJobUtility.TryFindDoorwayEscapeCell(Parent, state.TargetCell, out IntVec3 escapeCell))
            {
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.Goto, escapeCell);
            FeralJobUtility.ReservePlaceForJob(Parent, job, escapeCell);
            return true;
        }

        private bool TryGetLocalMatureRecoveryJob(out Job job)
        {
            job = null;
            if (Parent.needs?.food == null || Parent.needs.food.CurLevelPercentage < 0.9f)
            {
                return false;
            }

            if (!Parent.PositionHeld.InBounds(Parent.MapHeld) || !Parent.PositionHeld.Standable(Parent.MapHeld))
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Mature, Parent.PositionHeld);
            FeralJobUtility.ReservePlaceForJob(Parent, job, Parent.PositionHeld);
            return true;
        }

        private bool TryGetLocalFoodRecoveryJob(out Job job)
        {
            job = null;
            Room room = Parent.GetRoom();
            if (room == null)
            {
                return false;
            }

            Thing bestFood = null;
            float bestScore = float.MinValue;
            foreach (IntVec3 cell in room.Cells)
            {
                if (!cell.InBounds(Parent.MapHeld))
                {
                    continue;
                }

                foreach (Thing thing in cell.GetThingList(Parent.MapHeld))
                {
                    ThingDef foodDef = FoodUtility.GetFinalIngestibleDef(thing, false);
                    if (foodDef?.ingestible == null || !FeralJobUtility.IsThingAvailableForJobBy(Parent, thing))
                    {
                        continue;
                    }

                    if (!ClimbUtility.CanReachByWalkingOrClimb(Parent, thing, PathEndMode.Touch, Danger.Deadly))
                    {
                        continue;
                    }

                    float nutrition = FoodUtility.GetNutrition(Parent, thing, foodDef);
                    if (nutrition <= 0f)
                    {
                        continue;
                    }

                    float localScore = nutrition * 100f;
                    float score = PathRecoveryJobUtility.RecoveryScore(Parent, thing.PositionHeld, state.TargetCell, localScore);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFood = thing;
                    }
                }
            }

            if (bestFood != null)
            {
                ThingDef foodDef = FoodUtility.GetFinalIngestibleDef(bestFood, false);
                job = JobMaker.MakeJob(JobDefOf.Ingest, bestFood);
                job.count = FoodUtility.WillIngestStackCountOf(Parent, foodDef, FoodUtility.GetNutrition(Parent, bestFood, foodDef));
                FeralJobUtility.ReserveThingForJob(Parent, job, bestFood);
                return true;
            }

            Pawn prey = room.Cells
                .Select(cell => cell.GetFirstPawn(Parent.MapHeld))
                .Where(candidate => candidate != null && candidate != Parent && candidate.Spawned && !candidate.Dead && !XMTUtility.NotPrey(candidate) && !XMTUtility.IsInorganic(candidate))
                .OrderByRecoveryScore(Parent, state.TargetCell, candidate => candidate.PositionHeld)
                .FirstOrDefault(candidate => FeralJobUtility.IsThingAvailableForJobBy(Parent, candidate) && ClimbUtility.CanReachByWalkingOrClimb(Parent, candidate, PathEndMode.Touch, Danger.Deadly));

            if (prey == null)
            {
                return false;
            }

            job = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
            job.killIncappedTarget = true;
            FeralJobUtility.ReserveThingForJob(Parent, job, prey);
            return true;
        }

        private bool TryGetUnpoweredDoorRecoveryJob(RecoveryContext context, out Job job)
        {
            job = null;
            RecoveryCandidate<Building_Door> doorCandidate = BoundaryBuildings(context.BoundaryRoom).OfType<Building_Door>()
                .Where(doorCandidate => IsPathRecoveryDoorCandidate(Parent, doorCandidate))
                .Select(door => MakeDoorRecoveryCandidate(door, context))
                .Where(candidate => candidate.Target != null)
                .OrderBy(candidate => candidate.InteractionCell.DistanceToSquared(Parent.PositionHeld))
                .ThenByDescending(candidate => PathRecoveryJobUtility.RecoveryScore(
                    Parent, candidate.ScoreCell, candidate.InteractionCell, state.TargetCell))
                .FirstOrDefault();

            if (doorCandidate.Target == null || !doorCandidate.InteractionCell.IsValid)
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PathRecoveryOpenDoor, doorCandidate.Target, doorCandidate.InteractionCell);
            job.targetC = state.TargetCell;
            return true;
        }

        private bool TryGetLocalPowerSabotageRecoveryJob(out Job job)
        {
            job = null;
            return false;
        }

        private bool TryGetBreachRecoveryJob(RecoveryContext context, out Job job)
        {
            job = null;
            RecoveryCandidate<Building> blockerCandidate = BoundaryBuildings(context.BoundaryRoom)
                .Select(blocker => MakeBreachRecoveryCandidate(blocker, context))
                .Where(candidate => candidate.Target != null)
                .OrderBy(candidate => candidate.InteractionCell.DistanceToSquared(Parent.PositionHeld))
                .ThenByDescending(candidate => PathRecoveryJobUtility.RecoveryScore(
                    Parent, candidate.ScoreCell, candidate.InteractionCell, state.TargetCell))
                .FirstOrDefault();

            if (blockerCandidate.Target == null || !blockerCandidate.InteractionCell.IsValid)
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PathRecoveryBreach, blockerCandidate.Target, blockerCandidate.InteractionCell);
            job.targetC = state.TargetCell;
            return true;
        }

        private RecoveryCandidate<Building_Door> MakeDoorRecoveryCandidate(Building_Door door, RecoveryContext context)
        {
            if (!TryFindBoundaryInteractionCell(Parent, door, context, out IntVec3 interactionCell))
            {
                return RecoveryCandidate<Building_Door>.Invalid;
            }

            if (!PathRecoveryJobUtility.TryFindPassageDestination(Parent, door.OccupiedRect(), interactionCell,
                    requireSafeExit: true, goalCell: state.TargetCell, out IntVec3 passageDestination) ||
                !PassageAdvancesRecovery(passageDestination, context))
            {
                return RecoveryCandidate<Building_Door>.Invalid;
            }

            return new RecoveryCandidate<Building_Door>(door, interactionCell, passageDestination);
        }

        private RecoveryCandidate<Building> MakeBreachRecoveryCandidate(Building blocker, RecoveryContext context)
        {
            if (!TryFindBoundaryInteractionCell(Parent, blocker, context, out IntVec3 interactionCell) ||
                !CanBreachBlocker(Parent, blocker, requireAvailability: true) ||
                !PathRecoveryJobUtility.TryFindPassageDestination(Parent, blocker.OccupiedRect(), interactionCell,
                    requireSafeExit: true, goalCell: state.TargetCell, out IntVec3 passageDestination) ||
                !PassageAdvancesRecovery(passageDestination, context))
            {
                return RecoveryCandidate<Building>.Invalid;
            }

            return new RecoveryCandidate<Building>(blocker, interactionCell, passageDestination);
        }

        private IEnumerable<Building> BoundaryBuildings(Room room)
        {
            if (room?.Map != Parent.MapHeld)
            {
                yield break;
            }

            HashSet<Building> seen = new HashSet<Building>();
            foreach (IntVec3 roomCell in room.Cells)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 borderCell = roomCell + direction;
                    if (!borderCell.InBounds(Parent.MapHeld) || borderCell.GetRoom(Parent.MapHeld) == room)
                    {
                        continue;
                    }

                    Building building = borderCell.GetEdifice(Parent.MapHeld);
                    if (building != null && seen.Add(building))
                    {
                        yield return building;
                    }
                }
            }
        }

        private static bool TryFindBoundaryInteractionCell(Pawn pawn, Building blocker,
            RecoveryContext context, out IntVec3 interactionCell)
        {
            interactionCell = IntVec3.Invalid;
            if (pawn?.Map == null || blocker?.Map != pawn.Map || context.BoundaryRoom == null)
            {
                return false;
            }

            foreach (IntVec3 cell in blocker.OccupiedRect().AdjacentCells
                         .Where(cell => cell.InBounds(pawn.Map) &&
                                        (context.GoalCentered
                                            ? cell.GetRoom(pawn.Map) != context.BoundaryRoom
                                            : cell.GetRoom(pawn.Map) == context.BoundaryRoom) &&
                                        cell.Standable(pawn.Map) &&
                                        FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                         .OrderBy(cell => cell.DistanceToSquared(pawn.PositionHeld))
                         .ThenByDescending(cell => PathRecoveryJobUtility.RecoveryScore(
                             pawn, cell, context.GoalCell)))
            {
                if (ClimbUtility.OriginalCanReach(pawn, cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    interactionCell = cell;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetRecoveryContext(out RecoveryContext context)
        {
            context = default;
            Map map = Parent?.Map;
            if (map == null || !state.TargetCell.InBounds(map))
            {
                return false;
            }

            bool seekerTrapped = InfiltrationUtility.IsCellTrapped(
                Parent.PositionHeld, map, TraverseMode.NoPassClosedDoors, Danger.Deadly);
            List<IntVec3> goalCells = GoalApproachCells().ToList();
            List<IntVec3> trappedGoalCells = goalCells
                .Where(cell => InfiltrationUtility.IsCellTrapped(
                    cell, map, TraverseMode.NoPassClosedDoors, Danger.Deadly))
                .ToList();
            bool goalTrapped = goalCells.Count > 0 && trappedGoalCells.Count == goalCells.Count;
            Room seekerRoom = Parent.GetRoom();
            Room goalRoom = trappedGoalCells.Select(cell => cell.GetRoom(map)).FirstOrDefault(room => room != null);

            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver][PathRecovery] " + Parent +
                    " endpoint classification: seekerTrapped=" + seekerTrapped +
                    " goalTrapped=" + goalTrapped +
                    " seekerRoom=" + RoomSummary(seekerRoom) +
                    " goalRoom=" + RoomSummary(goalRoom) +
                    " pathEndMode=" + state.FailedPathEndMode + ".");
            }

            if (!seekerTrapped && !goalTrapped)
            {
                return false;
            }

            if (seekerTrapped)
            {
                if (seekerRoom == null || goalTrapped && seekerRoom == goalRoom)
                {
                    return false;
                }

                context = new RecoveryContext(seekerRoom, goalCentered: false,
                    seekerTrapped: true, goalTrapped: goalTrapped, goalCell: state.TargetCell);
                return true;
            }

            if (goalRoom == null)
            {
                return false;
            }

            context = new RecoveryContext(goalRoom, goalCentered: true,
                seekerTrapped: false, goalTrapped: true, goalCell: state.TargetCell);
            return true;
        }

        private IEnumerable<IntVec3> GoalApproachCells()
        {
            Map map = Parent?.Map;
            if (map == null || !state.TargetCell.InBounds(map))
            {
                yield break;
            }

            if (state.FailedPathEndMode == PathEndMode.OnCell)
            {
                if (state.TargetCell.Standable(map))
                {
                    yield return state.TargetCell;
                }
                yield break;
            }

            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            foreach (IntVec3 offset in GenAdj.AdjacentCellsAndInside)
            {
                IntVec3 cell = state.TargetCell + offset;
                if (cell.InBounds(map) && cell.Standable(map) && seen.Add(cell))
                {
                    yield return cell;
                }
            }
        }

        private bool PassageAdvancesRecovery(IntVec3 passageDestination, RecoveryContext context)
        {
            Map map = Parent?.Map;
            if (map == null || !passageDestination.InBounds(map) || !passageDestination.Standable(map))
            {
                return false;
            }

            Room passageRoom = passageDestination.GetRoom(map);
            if (context.GoalCentered)
            {
                return passageRoom == context.BoundaryRoom && CanReachGoalFrom(passageDestination);
            }

            if (passageRoom == context.BoundaryRoom)
            {
                return false;
            }

            if (context.GoalTrapped)
            {
                return !InfiltrationUtility.IsCellTrapped(
                    passageDestination, map, TraverseMode.NoPassClosedDoors, Danger.Deadly);
            }

            return CanReachGoalFrom(passageDestination);
        }

        private bool CanReachGoalFrom(IntVec3 start)
        {
            Map map = Parent?.Map;
            return map != null && start.InBounds(map) &&
                map.reachability.CanReach(start, state.TargetCell, state.FailedPathEndMode,
                    TraverseParms.For(Parent, Danger.Deadly, TraverseMode.ByPawn,
                        canBashDoors: false, alwaysUseAvoidGrid: false, canBashFences: false));
        }

        internal static bool IsPathRecoveryDoorCandidate(Pawn pawn, Building_Door door, bool requireAvailability = true)
        {
            if (pawn?.Map == null || door == null || door.Destroyed || door.Open || door.HoldOpen)
            {
                return false;
            }

            return XMTDoorUtility.CanForceOpenConventionally(door) &&
                   (!requireAvailability || FeralJobUtility.IsThingAvailableForJobBy(pawn, door));
        }

        internal static bool IsPathRecoveryBreachCandidate(Pawn pawn, Building blocker, out IntVec3 interactionCell, bool requireAvailability = true)
        {
            return IsPathRecoveryBreachCandidate(pawn, blocker, IntVec3.Invalid, out interactionCell, requireAvailability);
        }

        internal static bool IsPathRecoveryBreachCandidate(Pawn pawn, Building blocker, IntVec3 goalCell, out IntVec3 interactionCell, bool requireAvailability = true)
        {
            interactionCell = IntVec3.Invalid;
            if (!CanBreachBlocker(pawn, blocker, requireAvailability))
            {
                return false;
            }

            XMTSabotageReplacementUtility.TryGetReplacement(blocker.def, out XMT_SabotageReplacementPair replacement);

            Room pawnRoom = pawn.GetRoom();
            if (pawnRoom == null)
            {
                return false;
            }

            List<CellRecoveryCandidate> candidates = new List<CellRecoveryCandidate>();
            foreach (IntVec3 adjacent in blocker.OccupiedRect().AdjacentCells
                         .Where(adjacent => adjacent.InBounds(pawn.Map) &&
                                            adjacent.GetRoom(pawn.Map) == pawnRoom &&
                                            adjacent.Standable(pawn.Map) &&
                                            (!requireAvailability || FeralJobUtility.IsPlaceAvailableForJobBy(pawn, adjacent))))
            {
                if (ClimbUtility.OriginalCanReach(pawn, adjacent, PathEndMode.OnCell, Danger.Deadly) &&
                    PathRecoveryJobUtility.TryFindPassageDestination(pawn, blocker.OccupiedRect(), adjacent, requireSafeExit: true, goalCell: goalCell, out IntVec3 passageDestination))
                {
                    candidates.Add(new CellRecoveryCandidate(adjacent, passageDestination));
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            CellRecoveryCandidate candidate = candidates
                .OrderByRecoveryScore(pawn, goalCell, cellCandidate => cellCandidate.ScoreCell, cellCandidate => cellCandidate.InteractionCell)
                .FirstOrDefault();
            interactionCell = candidate.InteractionCell;
            return true;
        }

        private static bool CanBreachBlocker(Pawn pawn, Building blocker, bool requireAvailability)
        {
            bool poweredDoor = blocker is Building_Door door && XMTDoorUtility.HasPoweredResistance(door);
            if (pawn?.Map == null || blocker == null || blocker.Destroyed ||
                (!poweredDoor && blocker.def.passability != Traversability.Impassable))
            {
                return false;
            }

            if (requireAvailability && !FeralJobUtility.IsThingAvailableForJobBy(pawn, blocker))
            {
                return false;
            }

            XMTSabotageReplacementUtility.TryGetReplacement(blocker.def, out XMT_SabotageReplacementPair replacement);
            return PathRecoveryJobUtility.CanSupportBreachPassage(pawn.Map, blocker.Position, replacement);
        }

        public static string RoomSummary(Room room)
        {
            return room == null ? "null" : room.ToString() + " cells=" + room.CellCount;
        }

        private struct RecoveryCandidate<T> where T : Thing
        {
            public static readonly RecoveryCandidate<T> Invalid = new RecoveryCandidate<T>(null, IntVec3.Invalid, IntVec3.Invalid);

            public readonly T Target;
            public readonly IntVec3 InteractionCell;
            public readonly IntVec3 ScoreCell;

            public RecoveryCandidate(T target, IntVec3 interactionCell, IntVec3 scoreCell)
            {
                Target = target;
                InteractionCell = interactionCell;
                ScoreCell = scoreCell;
            }
        }

        private struct CellRecoveryCandidate
        {
            public readonly IntVec3 InteractionCell;
            public readonly IntVec3 ScoreCell;

            public CellRecoveryCandidate(IntVec3 interactionCell, IntVec3 scoreCell)
            {
                InteractionCell = interactionCell;
                ScoreCell = scoreCell;
            }
        }

        private readonly struct RecoveryContext
        {
            public readonly Room BoundaryRoom;
            public readonly bool GoalCentered;
            public readonly bool SeekerTrapped;
            public readonly bool GoalTrapped;
            public readonly IntVec3 GoalCell;

            public RecoveryContext(Room boundaryRoom, bool goalCentered, bool seekerTrapped,
                bool goalTrapped, IntVec3 goalCell)
            {
                BoundaryRoom = boundaryRoom;
                GoalCentered = goalCentered;
                SeekerTrapped = seekerTrapped;
                GoalTrapped = goalTrapped;
                GoalCell = goalCell;
            }
        }
    }

    public class MatureMorphPathRecoveryState : IExposable
    {
        private IntVec3 targetCell = IntVec3.Invalid;
        private JobDef failedJobDef;
        private int lastFailureTick = -1;
        private int totalAttempts;
        private bool playerForced;
        private bool confirmedPatherFailure;
        private PathEndMode pathEndMode = PathEndMode.Touch;
        private int mapId = -1;
        private IntVec3 recoveryRoomCell = IntVec3.Invalid;

        public bool Active => targetCell.IsValid && totalAttempts > 0;

        public IntVec3 TargetCell => targetCell;
        public bool PlayerForced => playerForced;
        public bool ConfirmedPatherFailure => confirmedPatherFailure;
        public PathEndMode FailedPathEndMode => pathEndMode;

        public string DebugSummary(Map map)
        {
            return "active=" + Active +
                   " target=" + targetCell +
                   " failedJob=" + failedJobDef?.defName +
                   " attempts=" + totalAttempts +
                   " lastFailureTick=" + lastFailureTick +
                   " mapId=" + mapId +
                   " playerForced=" + playerForced +
                   " confirmedPatherFailure=" + confirmedPatherFailure +
                   " pathEndMode=" + pathEndMode +
                   " recoveryRoom=" + MatureMorphPathRecovery.RoomSummary(GetRecoveryRoom(map));
        }

        public void NotifyFailure(IntVec3 cell, JobDef jobDef, bool wasPlayerForced, bool wasConfirmedPatherFailure,
            PathEndMode failedPathEndMode, Map map, IntVec3 roomCell)
        {
            if (map == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            bool sameRoom = SameRecoveryRoom(map, roomCell);
            bool sameFailure = Active &&
                               mapId == map.uniqueID &&
                               sameRoom &&
                               targetCell == cell &&
                               failedJobDef == jobDef &&
                               pathEndMode == failedPathEndMode;
            if (sameFailure && lastFailureTick == tick)
            {
                playerForced |= wasPlayerForced;
                confirmedPatherFailure |= wasConfirmedPatherFailure;
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message("[XMT][JobGiver][PathRecovery] same-tick duplicate failure ignored. tick=" + tick + " cell=" + cell + " job=" + jobDef?.defName + " state=" + DebugSummary(map));
                }
                return;
            }

            if (!sameFailure)
            {
                targetCell = cell;
                failedJobDef = jobDef;
                totalAttempts = 0;
                mapId = map.uniqueID;
                playerForced = false;
                confirmedPatherFailure = false;
                pathEndMode = failedPathEndMode;
                recoveryRoomCell = roomCell;
            }

            totalAttempts++;
            playerForced |= wasPlayerForced;
            confirmedPatherFailure |= wasConfirmedPatherFailure;
            lastFailureTick = tick;
            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver][PathRecovery] failure recorded. tick=" + tick + " sameFailure=" + sameFailure + " cell=" + cell + " job=" + jobDef?.defName + " roomCell=" + roomCell + " state=" + DebugSummary(map));
            }
        }

        private bool SameRecoveryRoom(Map map, IntVec3 roomCell)
        {
            if (map == null || !recoveryRoomCell.IsValid || !roomCell.IsValid || !recoveryRoomCell.InBounds(map) || !roomCell.InBounds(map))
            {
                return false;
            }

            Room recoveryRoom = recoveryRoomCell.GetRoom(map);
            Room currentRoom = roomCell.GetRoom(map);
            return recoveryRoom != null && recoveryRoom == currentRoom;
        }

        public bool IsForMap(Map map)
        {
            return map != null && map.uniqueID == mapId;
        }

        private Room GetRecoveryRoom(Map map)
        {
            return map != null && recoveryRoomCell.IsValid && recoveryRoomCell.InBounds(map) ? recoveryRoomCell.GetRoom(map) : null;
        }

        public void Clear()
        {
            targetCell = IntVec3.Invalid;
            failedJobDef = null;
            lastFailureTick = -1;
            totalAttempts = 0;
            playerForced = false;
            confirmedPatherFailure = false;
            pathEndMode = PathEndMode.Touch;
            mapId = -1;
            recoveryRoomCell = IntVec3.Invalid;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_Defs.Look(ref failedJobDef, "failedJobDef");
            Scribe_Values.Look(ref lastFailureTick, "lastFailureTick", -1);
            Scribe_Values.Look(ref totalAttempts, "totalAttempts", 0);
            Scribe_Values.Look(ref playerForced, "playerForced", false);
            Scribe_Values.Look(ref confirmedPatherFailure, "confirmedPatherFailure", false);
            Scribe_Values.Look(ref pathEndMode, "pathEndMode", PathEndMode.Touch);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref recoveryRoomCell, "recoveryRoomCell");
        }
    }
}
