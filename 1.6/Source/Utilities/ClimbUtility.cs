
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class ClimbUtility
    {
        public struct ClimbParameters
        {
            public bool ClimbCellsRegistered => (ClimbStarts != null && ClimbStarts.Count > 0 && ClimbEnds != null && ClimbEnds.Count > 0
                && ClimbStarts[0].IsValid && ClimbEnds[0].IsValid);

            public IntVec3 FinalGoalCell;
            public bool NoWallToClimb ;
            public bool Tunneling;

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

        private static bool OriginalCanReach(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (!pawn.Spawned)
            {
                return false;
            }

            return pawn.Map.reachability.CanReach(pawn.Position, dest, peMode, TraverseParms.For(pawn, maxDanger, mode, canBashDoors, alwaysUseAvoidGrid: false, canBashFences));

        }

        public static bool CanReachByInfiltration(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (InfiltrationUtility.GetInfiltrationEntry(pawn.Map, pawn.Position, dest.Cell, out Building entry))
            {
                if (InfiltrationUtility.GetInfiltrationExit(entry, dest.Cell, out Building exit))
                {
                    return true;
                }
            }
            return false;
        }
        public static bool CanReachByClimb(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors = false, bool canBashFences = false, TraverseMode mode = TraverseMode.ByPawn)
        {
            if (!pawn.Spawned)
            {
                return false;
            }

            if(pawn.Map == null)
            {
                return false;
            }

            bool normalReach = OriginalCanReach(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode);

            if (normalReach)
            {
                return normalReach;
            }

            Room destRoom = dest.Cell.GetRoom(pawn.Map);

            Room pawnRoom = pawn.GetRoom();

            if (destRoom == null || pawnRoom == null)
            {
                return false;
            }

            if(!(destRoom.PsychologicallyOutdoors && pawnRoom.PsychologicallyOutdoors))
            {
                if(CanReachByInfiltration(pawn,dest, peMode, maxDanger,canBashDoors,canBashFences,mode))
                {
                    return true;
                }

                bool noOpening = true;
                foreach(IntVec3 roomCell in destRoom.Cells)
                {
                    if(XMTRoofUtility.RoofIsBreakable(roomCell.GetRoof(pawn.Map)))
                    {
                        noOpening = false;
                        break;
                    }
                }

                if(noOpening)
                {
                    return false;
                }

                noOpening = true;

                foreach (IntVec3 roomCell in pawnRoom.Cells)
                {
                    if (XMTRoofUtility.RoofIsBreakable(roomCell.GetRoof(pawn.Map)))
                    {
                        noOpening = false;
                        break;
                    }
                }

                if (noOpening)
                {
                    return false;
                }
            }
            
            return true;
        }
        protected static bool ShouldClimbTo(Pawn actor, IntVec3 cell)
        {
            if (actor == null)
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
    

            bool isSelected = Find.Selector.IsSelected(actor);

            climber.pawnClimber = climber.climbParameters.Tunneling ? PawnClimber.MakeClimber(InternalDefOf.PawnClimbUnder, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.StartClimbCell.ToVector3(), null, climber.EndClimbCell)
            : PawnClimber.MakeClimber(InternalDefOf.PawnClimber, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.StartClimbCell.ToVector3(), null, climber.EndClimbCell);

            if (climber.pawnClimber != null)
            {
                climber.pawnClimber.underground = climber.climbParameters.Tunneling;
                GenSpawn.Spawn(climber.pawnClimber, cell, map);

                climber.startedClimb = true;
                if (isSelected)
                {
                    Find.Selector.Select(actor, playSound: false, forceDesignatorDeselect: false);
                }

                if (!climber.climbParameters.Tunneling)
                {
                    RoofDef roof = climber.StartClimbCell.GetRoof(map);
                    if(roof == RoofDefOf.RoofRockThick)
                    {
                        map.roofGrid.SetRoof(climber.StartClimbCell, RoofDefOf.RoofRockThin);
                    }
                    RoofCollapserImmediate.DropRoofInCells(climber.StartClimbCell, map);
                }
            }
        }

        protected static void InitClimbAction(Pawn actor, ref CompClimber climber, Toil toil, PathEndMode peMode = PathEndMode.OnCell)
        {
            if (actor.Position == climber.StartClimbCell)
            {
                BeginClimb(actor, ref climber, toil);
                return;
            }

            actor.pather.StartPath(climber.StartClimbCell, peMode);
        }

        protected static void TickClimbIntervalAction(int interval, Pawn actor, ref CompClimber climber, Toil toil, PathEndMode peMode = PathEndMode.OnCell)
        {
            if (!actor.Spawned)
            {
                return;
            }

            if (climber.climbing)
            {
                return;
            }

            if (actor.pather.curPath == null || actor.pather.curPath.Finished)
            {
                if (climber.startedClimb && !climber.finishedClimb)
                {
                    climber.finishedClimb = true;

                    if (climber.lastClimb)
                    {
                        //Log.Message(actor + " pathing too " + climber.climbParameters.FinalGoalCell);
                        actor.pather.StartPath(climber.climbParameters.FinalGoalCell, peMode);
                    }
                    else
                    {
                        climber.startedClimb = false;
                        climber.finishedClimb = false;
                        //Log.Message(actor + " pathing too " + climber.StartClimbCell);
                        actor.pather.StartPath(climber.StartClimbCell, peMode);
                    }
                    return;
                }
            
                if (climber.lastClimb)
                {
                    if (climber.climbParameters.FinalGoalCell.AdjacentTo8WayOrInside(actor.Position))
                    {
                        climber.ClearClimberData();
                        actor.jobs.curDriver.ReadyForNextToil();
                    }
                }
                else if(!climber.startedClimb)
                {
                    //Log.Message(actor + " is checking if they are at " + climber.StartClimbCell);
                    if (climber.StartClimbCell.AdjacentTo8WayOrInside(actor.Position))
                    {
                        BeginClimb(actor, ref climber, toil);
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

        public static Toil GotoCell( TargetIndex ind, PathEndMode peMode)
        {
            Toil toil = ToilMaker.MakeToil("ClimbToCell");
            toil.AddFailCondition(() => { return FailClimbToil(toil); });
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                LocalTargetInfo target = actor.jobs.curJob.GetTarget(ind);
                if (actor.Position == target.Cell)
                {
                    actor.pather.StopDead();
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                CompClimber climber = actor.GetClimberComp();
                if (climber == null || !GetClimbParameters(actor, target.Cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(target, peMode);
                    return;
                }
                toil.defaultCompleteMode = ToilCompleteMode.Never;
                InitClimbAction(actor, ref climber, toil, peMode);
            };

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });

            return toil;
        }
        public static Toil GotoCell( IntVec3 cell, PathEndMode peMode)
        {
            Toil toil = ToilMaker.MakeToil("ClimbToCell");
            toil.AddFailCondition(() => { return FailClimbToil(toil); });
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                if (actor.Position == cell)
                {
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                CompClimber climber = actor.GetClimberComp();
                if (climber == null || !GetClimbParameters(actor, cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(cell, peMode);
                    return;
                }
                toil.defaultCompleteMode = ToilCompleteMode.Never;
                InitClimbAction(actor, ref climber, toil, peMode);
            };

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });


            return toil;
        }

        public static Toil GotoThing( TargetIndex ind, IntVec3 exactCell)
        {

            Toil toil = ToilMaker.MakeToil("ClimbToThing");
            toil.AddFailCondition(() => { return FailClimbToil(toil); });
            toil.initAction = delegate
            {
                
                Pawn actor = toil.actor;
                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;
                
                CompClimber climber = actor.GetClimberComp();
                if (climber == null || !GetClimbParameters(actor, dest.Cell, ref climber))
                {
                    //Log.Message(actor + " pathing too " + thing);
                    toil.actor.pather.StartPath(exactCell, PathEndMode.OnCell);
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    return;
                }
                toil.defaultCompleteMode = ToilCompleteMode.Never;
                InitClimbAction(actor, ref climber, toil);
            };
            
            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });

            toil.FailOnDespawnedOrNull(ind);
            

            return toil;
        }


        public static Toil GotoThing(TargetIndex ind, PathEndMode peMode, bool canGotoSpawnedParent = false)
        {

            Toil toil = ToilMaker.MakeToil("ClimbToThing");
            toil.AddFailCondition(() => { return FailClimbToil(toil); });
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;
                CompClimber climber = actor.GetClimberComp();

                if (climber == null || !GetClimbParameters(actor, dest.Cell, ref climber))
                {
                    if (thing != null && canGotoSpawnedParent)
                    {
                        dest = thing.SpawnedParentOrMe;
                    }
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(dest, peMode);
                    return;
                }
                toil.defaultCompleteMode = ToilCompleteMode.Never;
                InitClimbAction(actor, ref climber, toil, peMode);
            };
            
            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });

            if (canGotoSpawnedParent)
            {
                toil.FailOnSelfAndParentsDespawnedOrNull(ind);
            }
            else
            {
                toil.FailOnDespawnedOrNull(ind);
            }

            return toil;
        }

        public static Toil CarryHauledThingToCell(TargetIndex ind, PathEndMode peMode = PathEndMode.ClosestTouch)
        {
            Toil toil = ToilMaker.MakeToil("ClimbToHaulToCell");
            toil.AddFailCondition(() => { return FailClimbToil(toil); });
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                LocalTargetInfo target = actor.jobs.curJob.GetTarget(ind);
                if (actor.Position == target.Cell)
                {
                    actor.pather.StopDead();
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                CompClimber climber = actor.GetClimberComp();
                if (climber == null || !GetClimbParameters(actor, target.Cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(target, peMode);
                    return;
                }

                toil.defaultCompleteMode = ToilCompleteMode.Never;
                InitClimbAction(actor, ref climber, toil, peMode);
            };

            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                Pawn actor = toil.actor;
                CompClimber climber = toil.actor.GetClimberComp();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });

            toil.AddEndCondition(delegate
            {
                Pawn actor2 = toil.actor;
                IntVec3 cell2 = actor2.jobs.curJob.GetTarget(ind).Cell;
                CompPushable compPushable2 = actor2.carryTracker.CarriedThing.TryGetComp<CompPushable>();
                if (compPushable2 != null)
                {
                    Vector3 v = actor2.Position.ToVector3() + compPushable2.drawPos;
                    if (new IntVec3(v) == cell2)
                    {
                        return JobCondition.Succeeded;
                    }
                }

                return JobCondition.Ongoing;
            });
            toil.AddFailCondition(delegate
            {
                Pawn actor = toil.actor;
                IntVec3 cell = actor.jobs.curJob.GetTarget(ind).Cell;
                if (actor.carryTracker.CarriedThing == null)
                {
                    return true;
                }

                if (actor.jobs.curJob.haulMode == HaulMode.ToCellStorage && !cell.IsValidStorageFor(actor.Map, actor.carryTracker.CarriedThing))
                {
                    return true;
                }

                CompPushable compPushable = actor.carryTracker.CarriedThing.TryGetComp<CompPushable>();
                return (compPushable != null && !compPushable.canBePushed) ? true : false;
            });

            return toil;
        }


        protected static bool GetClimbCells(Pawn pawn, IntVec3 goal, out List<IntVec3> climbStart, out List<IntVec3> climbEnd)
        {
            climbStart = new List<IntVec3>();
            climbEnd = new List<IntVec3>();

            IntVec3 start = pawn.Position;
            Map map = pawn.Map;

            List<IntVec3> line = GenSight.PointsOnLineOfSight(goal, start).ToList();

            bool breakInto = start.Roofed(map) && goal.Roofed(map);
            bool wasBlocked = false;
            IntVec3 lastClear = IntVec3.Invalid;
            int i = 0;
            foreach (IntVec3 point in line)
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message(pawn + " is checking " + point + ":" + i);
                }
                if (climbEnd.Count == 0)
                {
                    if(point.Roofed(map))
                    {
                        if (XMTSettings.LogClimbing)
                        {
                            Log.Message(pawn + " has found a end point" + point + ":" + i);
                        }
                        climbEnd.Add(point);
                    }
                    else
                    {
                        IEnumerable<IntVec3> adjacent = GenRadial.RadialCellsAround(point, 1.5f, false);
                        foreach(IntVec3 adj in adjacent)
                        {
                            if (adj.Roofed(map) || (!adj.Walkable(map) && !adj.CanBeSeenOverFast(map)))
                            {
                                if (XMTSettings.LogClimbing)
                                {
                                    Log.Message(pawn + " has found a end point" + point + ":" + i);
                                }
                                climbEnd.Add(point);
                                break;
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
                    if (point.CanBeSeenOverFast(map))
                    {
                        if (wasBlocked)
                        {
                            noLongerBlocked = true;
                        }
                    }
                    else if(!wasBlocked)
                    {
                        climbEnd.Add(lastClear); 
                        wasBlocked = true;
                    }
                }

                if (noLongerBlocked && climbEnd.Count > climbStart.Count)
                {
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message(pawn + " has found a start point" + point + ":" + i);
                    }
                    climbStart.Add(point);
                    wasBlocked = false;

                    if (OriginalCanReach(pawn, point, PathEndMode.OnCell, Danger.Some))
                    {
                        break;
                    }
                }

                i++;
            }

            if (climbStart.Count == 0 && breakInto)
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message(pawn + "found no good starts using" + start);
                }
                climbStart.Add(start);

                climbEnd.Clear();
                climbEnd.Add(goal);
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message(climbEnd.Count + " ends and " + climbStart.Count + "starts");
            }

            climbStart.Reverse();
            climbEnd.Reverse();
            return climbStart.Count > 0 && climbEnd.Count > 0 && climbStart.Count == climbEnd.Count;
        }
        protected static bool GetClimbParameters(Pawn pawn, IntVec3 FinalGoalCell, ref CompClimber climber)
        {
            if (XMTSettings.LogClimbing)
            {
                Log.Message(pawn + " gathering climb parameters");
            }
            climber.ClearClimberData();

            climber.climbParameters.FinalGoalCell = FinalGoalCell;

            if (!FinalGoalCell.IsValid)
            {
                return false;
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message(pawn + " final goal valid");
            }

            if (!ShouldClimbTo(pawn, FinalGoalCell))
            {
                return false;
            }

            bool ClimbOver = true;
            IntVec3 Start = pawn.Position;

            if (XMTSettings.LogClimbing)
            {
                Log.Message(pawn + " Trying infiltrate from Position: " + Start + " to " + FinalGoalCell);
            }

            if (InfiltrationUtility.GetInfiltrationEntry(pawn.Map, Start, FinalGoalCell, out Building entry))
            {
                IntVec3 entrypoint = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(Start, entry);
                climber.climbParameters.ClimbStarts.Add(entrypoint);
                if (XMTSettings.LogClimbing)
                {
                    Log.Message(pawn + " Found Infiltration Target Entry: " + entry + " at: " + entrypoint);
                }

                if (InfiltrationUtility.GetInfiltrationExit(entry, FinalGoalCell, out Building exit))
                {
                    IntVec3 exitpoint = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(FinalGoalCell, exit);
                    climber.climbParameters.ClimbEnds.Add(exitpoint);
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message(pawn + " Found Infiltration Target Exit: " + exit + " at: " + exitpoint);
                    }

                    climber.climbParameters.NoWallToClimb = false;
                    ClimbOver = false;
                    climber.climbParameters.Tunneling = true;
                }
            }

            if(XMTUtility.IsSpace(pawn.Map))
            {
                climber.climbParameters.Tunneling = true;
            }

            if (ClimbOver)
            {
                GetClimbCells(pawn, FinalGoalCell, out climber.climbParameters.ClimbStarts, out climber.climbParameters.ClimbEnds);
            
            }

            if (climber.climbParameters.ClimbCellsRegistered)
            {
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("Climb cells registered for " + pawn);
                }
                return true;
            }

            if (XMTSettings.LogClimbing)
            {
                Log.Message("Climb cells failed to register for " + pawn);
            }
            return false;
        }
    }
}
