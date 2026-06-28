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

        public void NotifyPathFailure(LocalTargetInfo target, Job job)
        {
            if (Parent == null || Parent.Dead || Parent.MapHeld == null || !target.IsValid || !target.Cell.IsValid)
            {
                return;
            }

            NotifyPathFailure(target.Cell, job?.def, job != null && job.playerForced);
        }

        public void NotifyPathFailure(IntVec3 targetCell, JobDef jobDef, bool playerForced)
        {
            if (Parent == null || Parent.Dead || Parent.MapHeld == null || !targetCell.IsValid)
            {
                return;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[PathRecovery] " + Parent + " NotifyPathFailure tick=" + Find.TickManager.TicksGame + " target=" + targetCell + " job=" + jobDef?.defName + " before=" + state.DebugSummary(Parent.MapHeld));
            }

            state.NotifyFailure(targetCell, jobDef, playerForced, Parent.MapHeld, Parent.PositionHeld);
            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[PathRecovery] " + Parent + " NotifyPathFailure after=" + state.DebugSummary(Parent.MapHeld));
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
                Log.Message("[PathRecovery] " + Parent + " evaluating recovery tick=" + Find.TickManager.TicksGame + " state=" + state.DebugSummary(Parent.MapHeld));
            }

            if (!state.IsForMap(Parent.MapHeld) || !state.TargetCell.InBounds(Parent.MapHeld))
            {

                Clear();
                return false;
            }

            if (ClimbUtility.OriginalCanReach(Parent, state.TargetCell, PathEndMode.Touch, Danger.Deadly))
            {

                Clear();
                return false;
            }

            if (!CanUsePathRecovery())
            {

                return false;
            }

            if (TryGetInfiltrationEscapeRecoveryJob(out job))
            {
                return true;
            }

            if (TryGetDoorwayEscapeRecoveryJob(out job))
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
            }

            if (TryGetUnpoweredDoorRecoveryJob(out job))
            {
                Clear();
                return true;
            }

            if (TryGetLocalPowerSabotageRecoveryJob(out job))
            {
                Clear();
                return true;
            }

            if (TryGetBreachRecoveryJob(out job))
            {
                Clear();
                return true;
            }

            Clear();
            return false;
        }

        private bool CanUsePathRecovery()
        {
            return Parent.Faction == null ||
                   !Parent.Faction.IsPlayer ||
                   state.PlayerForced ||
                   (XMTUtility.NoQueenPresent() && XMTSettings.PlayerSabotage);
        }

        private bool TryGetInfiltrationEscapeRecoveryJob(out Job job)
        {
            job = null;
            return false;
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

        private bool TryGetUnpoweredDoorRecoveryJob(out Job job)
        {
            job = null;
            RecoveryCandidate<Building_Door> doorCandidate = CurrentRoomBoundaryDoors()
                .Where(doorCandidate => IsPathRecoveryDoorCandidate(Parent, doorCandidate))
                .Select(MakeDoorRecoveryCandidate)
                .Where(candidate => candidate.Target != null)
                .OrderByRecoveryScore(Parent, state.TargetCell, candidate => candidate.ScoreCell, candidate => candidate.InteractionCell)
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

        private bool TryGetBreachRecoveryJob(out Job job)
        {
            job = null;
            RecoveryCandidate<Building> blockerCandidate = CurrentRoomBoundaryBuildings()
                .Select(MakeBreachRecoveryCandidate)
                .Where(candidate => candidate.Target != null)
                .OrderByRecoveryScore(Parent, state.TargetCell, candidate => candidate.ScoreCell, candidate => candidate.InteractionCell)
                .FirstOrDefault();

            if (blockerCandidate.Target == null || !blockerCandidate.InteractionCell.IsValid)
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PathRecoveryBreach, blockerCandidate.Target, blockerCandidate.InteractionCell);
            job.targetC = state.TargetCell;
            return true;
        }

        private RecoveryCandidate<Building_Door> MakeDoorRecoveryCandidate(Building_Door door)
        {
            if (!TryFindDoorInteractionCell(Parent, door, state.TargetCell, out IntVec3 interactionCell))
            {
                return RecoveryCandidate<Building_Door>.Invalid;
            }

            IntVec3 scoreCell = door.PositionHeld;
            if (PathRecoveryJobUtility.TryFindPassageDestination(Parent, door.OccupiedRect(), interactionCell, requireSafeExit: false, goalCell: state.TargetCell, out IntVec3 passageDestination))
            {
                scoreCell = passageDestination;
            }

            return new RecoveryCandidate<Building_Door>(door, interactionCell, scoreCell);
        }

        private RecoveryCandidate<Building> MakeBreachRecoveryCandidate(Building blocker)
        {
            if (!IsPathRecoveryBreachCandidate(Parent, blocker, state.TargetCell, out IntVec3 interactionCell))
            {
                return RecoveryCandidate<Building>.Invalid;
            }

            IntVec3 scoreCell = blocker.PositionHeld;
            if (PathRecoveryJobUtility.TryFindPassageDestination(Parent, blocker.OccupiedRect(), interactionCell, requireSafeExit: true, goalCell: state.TargetCell, out IntVec3 passageDestination))
            {
                scoreCell = passageDestination;
            }

            return new RecoveryCandidate<Building>(blocker, interactionCell, scoreCell);
        }

        private IEnumerable<Building> CurrentRoomBoundaryBuildings()
        {
            Room room = Parent.GetRoom();
            if (room == null)
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

        private IEnumerable<Building_Door> CurrentRoomBoundaryDoors()
        {
            return CurrentRoomBoundaryBuildings().OfType<Building_Door>();
        }

        private static bool TryFindDoorInteractionCell(Pawn pawn, Building_Door door, out IntVec3 interactionCell)
        {
            return TryFindDoorInteractionCell(pawn, door, IntVec3.Invalid, out interactionCell);
        }

        private static bool TryFindDoorInteractionCell(Pawn pawn, Building_Door door, IntVec3 goalCell, out IntVec3 interactionCell)
        {
            interactionCell = IntVec3.Invalid;
            Room pawnRoom = pawn.GetRoom();
            if (pawnRoom == null || door?.Map == null)
            {
                return false;
            }

            foreach (IntVec3 cell in door.OccupiedRect().AdjacentCells
                         .Where(cell => cell.InBounds(pawn.Map) &&
                                        cell.GetRoom(pawn.Map) == pawnRoom &&
                                        cell.Standable(pawn.Map) &&
                                        FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                         .OrderByRecoveryScore(pawn, goalCell, cell => cell))
            {
                if (ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    interactionCell = cell;
                    return true;
                }
            }

            return false;
        }

        internal static bool IsPathRecoveryDoorCandidate(Pawn pawn, Building_Door door, bool requireAvailability = true)
        {
            if (pawn?.Map == null || door == null || door.Destroyed || door.Open || door.HoldOpen || door is Building_MultiTileDoor)
            {
                return false;
            }

            CompPowerTrader power = door.TryGetComp<CompPowerTrader>();
            return (power == null || !power.PowerOn) && (!requireAvailability || FeralJobUtility.IsThingAvailableForJobBy(pawn, door));
        }

        internal static bool IsPathRecoveryBreachCandidate(Pawn pawn, Building blocker, out IntVec3 interactionCell, bool requireAvailability = true)
        {
            return IsPathRecoveryBreachCandidate(pawn, blocker, IntVec3.Invalid, out interactionCell, requireAvailability);
        }

        internal static bool IsPathRecoveryBreachCandidate(Pawn pawn, Building blocker, IntVec3 goalCell, out IntVec3 interactionCell, bool requireAvailability = true)
        {
            interactionCell = IntVec3.Invalid;
            if (pawn?.Map == null || blocker == null || blocker.Destroyed || blocker is Building_Door || blocker.def.passability != Traversability.Impassable)
            {
                return false;
            }

            if (requireAvailability && !FeralJobUtility.IsThingAvailableForJobBy(pawn, blocker))
            {
                return false;
            }

            XMTSabotageReplacementUtility.TryGetReplacement(blocker.def, out XMT_SabotageReplacementPair replacement);
            if (!PathRecoveryJobUtility.CanSupportBreachPassage(pawn.Map, blocker.Position, replacement))
            {
                return false;
            }

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
                if (ClimbUtility.CanReachByWalkingOrClimb(pawn, adjacent, PathEndMode.OnCell, Danger.Deadly) &&
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
    }

    public class MatureMorphPathRecoveryState : IExposable
    {
        private IntVec3 targetCell = IntVec3.Invalid;
        private JobDef failedJobDef;
        private int lastFailureTick = -1;
        private int totalAttempts;
        private bool playerForced;
        private int mapId = -1;
        private IntVec3 recoveryRoomCell = IntVec3.Invalid;

        public bool Active => targetCell.IsValid && totalAttempts > 0;

        public IntVec3 TargetCell => targetCell;
        public bool PlayerForced => playerForced;

        public string DebugSummary(Map map)
        {
            return "active=" + Active +
                   " target=" + targetCell +
                   " failedJob=" + failedJobDef?.defName +
                   " attempts=" + totalAttempts +
                   " lastFailureTick=" + lastFailureTick +
                   " mapId=" + mapId +
                   " playerForced=" + playerForced +
                   " recoveryRoom=" + MatureMorphPathRecovery.RoomSummary(GetRecoveryRoom(map));
        }

        public void NotifyFailure(IntVec3 cell, JobDef jobDef, bool wasPlayerForced, Map map, IntVec3 roomCell)
        {
            if (map == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            bool sameRoom = SameRecoveryRoom(map, roomCell);
            bool sameFailure = Active &&
                               mapId == map.uniqueID &&
                               sameRoom;
            if (sameFailure && lastFailureTick == tick)
            {
                playerForced |= wasPlayerForced;
                if (XMTSettings.LogJobGiver)
                {
                    Log.Message("[PathRecovery] same-tick duplicate failure ignored. tick=" + tick + " cell=" + cell + " job=" + jobDef?.defName + " state=" + DebugSummary(map));
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
                recoveryRoomCell = roomCell;
            }

            totalAttempts++;
            playerForced |= wasPlayerForced;
            lastFailureTick = tick;
            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[PathRecovery] failure recorded. tick=" + tick + " sameFailure=" + sameFailure + " cell=" + cell + " job=" + jobDef?.defName + " roomCell=" + roomCell + " state=" + DebugSummary(map));
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
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref recoveryRoomCell, "recoveryRoomCell");
        }
    }
}
