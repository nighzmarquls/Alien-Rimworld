
using RimWorld;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.AI;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;

namespace Xenomorphtype
{
    public class ClimbUtility
    {
        public struct ClimbParameters
        {
            public bool ClimbCellsRegistered => (StartClimbCell.IsValid && EndClimbCell.IsValid);

            public IntVec3 StartClimbCell;
            public IntVec3 EndClimbCell;
            public IntVec3 FinalGoalCell;
            public bool NoWallToClimb ;
            public bool Tunneling;
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

        protected static bool ShouldClimbTo(Pawn actor, IntVec3 cell)
        {
            if (actor == null)
            {
                return false;
            }

            if (cell.GetRoomOrAdjacent(actor.Map) == actor.GetRoom())
            {
                return false;
            }

            bool canReach = actor.Map.reachability.CanReach(actor.Position, cell, PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.PassDoors));

            if (!canReach)
            {
                return true;
            }

            return false;
        }

        protected static void BeginClimb(Pawn actor, ref CompClimber climber, Toil toil)
        {
            IntVec3 position = actor.Position;
            IntVec3 cell = climber.climbParameters.EndClimbCell;
            Map map = actor.Map;
    

            bool isSelected = Find.Selector.IsSelected(actor);

            climber.pawnClimber = climber.climbParameters.Tunneling ? PawnClimber.MakeClimber(InternalDefOf.PawnClimbUnder, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.climbParameters.StartClimbCell.ToVector3(), null, climber.climbParameters.EndClimbCell)
            : PawnClimber.MakeClimber(InternalDefOf.PawnClimber, actor, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, climber.climbParameters.StartClimbCell.ToVector3(), null, climber.climbParameters.EndClimbCell);

            if (climber.pawnClimber != null)
            {
                climber.pawnClimber.underground = climber.climbParameters.Tunneling;
                GenSpawn.Spawn(climber.pawnClimber, cell, map);
                Log.Message(actor + " started climbing.");
                climber.startedClimb = true;
                if (isSelected)
                {
                    Find.Selector.Select(actor, playSound: false, forceDesignatorDeselect: false);
                }

                if (!climber.climbParameters.Tunneling)
                {
                    RoofCollapserImmediate.DropRoofInCells(climber.climbParameters.StartClimbCell, map);
                }
            }
        }

        protected static void InitClimbAction(Pawn actor, ref CompClimber climber, Toil toil, PathEndMode peMode = PathEndMode.OnCell)
        {
            toil.AddPreTickIntervalAction(delegate (int interval)
            {
                CompClimber climber = toil.actor.GetComp<CompClimber>();
                if (climber == null || !climber.climbParameters.ClimbCellsRegistered)
                {
                    return;
                }
                TickClimbIntervalAction(interval, toil.actor, ref climber, toil);
            });

            toil.defaultCompleteMode = ToilCompleteMode.Never;
            if (actor.Position == climber.climbParameters.StartClimbCell)
            {
                BeginClimb(actor, ref climber, toil);
                return;
            }

            actor.pather.StartPath(climber.climbParameters.StartClimbCell, peMode);
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

            if (climber.startedClimb && !climber.finishedClimb)
            {
                actor.pather.StartPath(climber.climbParameters.FinalGoalCell, peMode);
                climber.finishedClimb = true;
                return;
            }

            if(actor.pather.curPath == null || actor.pather.curPath.Finished)
            {
                if (climber.finishedClimb && climber.startedClimb)
                {
                    if (climber.climbParameters.FinalGoalCell == actor.Position)
                    {
                        climber.ClearClimberData();
                        actor.jobs.curDriver.ReadyForNextToil();
                    }
                }
                else if(!climber.startedClimb)
                {
                    if (climber.climbParameters.StartClimbCell == actor.Position)
                    {
                        BeginClimb(actor, ref climber, toil);
                    }
                }
            }

        }

        public static Toil GotoCell( TargetIndex ind, PathEndMode peMode)
        {
            Toil toil = ToilMaker.MakeToil("ClimbToCell");
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
                CompClimber climber = actor.GetComp<CompClimber>();
                if (climber == null || !GetClimbParameters(actor, target.Cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(target, peMode);
                    return;
                }

                InitClimbAction(actor, ref climber, toil, peMode);
            };

            return toil;
        }
        public static Toil GotoCell( IntVec3 cell, PathEndMode peMode)
        {
            Toil toil = ToilMaker.MakeToil("ClimbToCell");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                if (actor.Position == cell)
                {
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                CompClimber climber = actor.GetComp<CompClimber>();
                if (climber == null || !GetClimbParameters(actor, cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(cell, peMode);
                    return;
                }

                InitClimbAction(actor, ref climber, toil, peMode);
            };

            

            return toil;
        }

        public static Toil GotoThing( TargetIndex ind, IntVec3 exactCell)
        {

            Toil toil = ToilMaker.MakeToil("ClimbToThing");
            toil.initAction = delegate
            {
                
                Pawn actor = toil.actor;
                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;
                
                CompClimber climber = actor.GetComp<CompClimber>();
                if (climber == null || !GetClimbParameters(actor, dest.Cell, ref climber))
                {
                    Log.Message(actor + " pathing too " + thing);
                    toil.actor.pather.StartPath(exactCell, PathEndMode.OnCell);
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    return;
                }

                InitClimbAction(actor, ref climber, toil);
            };
          
            toil.FailOnDespawnedOrNull(ind);
            

            return toil;
        }


        public static Toil GotoThing(TargetIndex ind, PathEndMode peMode, bool canGotoSpawnedParent = false)
        {

            Toil toil = ToilMaker.MakeToil("ClimbToThing");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                LocalTargetInfo dest = actor.jobs.curJob.GetTarget(ind);
                Thing thing = dest.Thing;
                CompClimber climber = actor.GetComp<CompClimber>();

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

                InitClimbAction(actor, ref climber, toil, peMode);
            };

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
                CompClimber climber = actor.GetComp<CompClimber>();
                if (climber == null || !GetClimbParameters(actor, target.Cell, ref climber))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                    actor.pather.StartPath(target, peMode);
                    return;
                }

                InitClimbAction(actor, ref climber, toil, peMode);
            };
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


        protected static bool GetClimbParameters(Pawn pawn, IntVec3 FinalGoalCell, ref CompClimber climber)
        {
            climber.ClearClimberData();

            climber.climbParameters.FinalGoalCell = FinalGoalCell;

            if (!FinalGoalCell.IsValid)
            {
                return false;
            }

            if (!ShouldClimbTo(pawn, FinalGoalCell))
            {
                return false;
            }


            bool ClimbOver = true;
            IntVec3 Start = pawn.Position;


            //Log.Message("Trying infiltrate from Position: " + Start + " to " + FinalGoalCell);
            if (InfiltrationUtility.GetInfiltrationEntry(pawn.Map, Start, FinalGoalCell, out Building entry))
            {
                climber.climbParameters.StartClimbCell = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(Start, entry);
                //Log.Message("Found Infiltration Target Entry: " + entry + " at " + climber.climbParameters.StartClimbCell);
                if (InfiltrationUtility.GetInfiltrationExit(entry, FinalGoalCell, out Building exit))
                {

                    climber.climbParameters.EndClimbCell = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(FinalGoalCell, exit);
                    //Log.Message("Found Infiltration Target Exit: " + exit + " at " + climber.climbParameters.EndClimbCell);
                    climber.climbParameters.NoWallToClimb = false;
                    ClimbOver = false;
                    climber.climbParameters.Tunneling = true;
                }
            }

            if (ClimbOver)
            {
                TraverseMode climbTraverseMode = pawn.Faction == Faction.OfPlayer ? TraverseMode.PassDoors : TraverseMode.NoPassClosedDoors;
                Danger climbDanger = pawn.Faction == Faction.OfPlayer ? Danger.Deadly : Danger.None;

                if (InfiltrationUtility.IsCellTrapped(FinalGoalCell, pawn.Map, climbTraverseMode, climbDanger))
                {
                    climber.climbParameters.NoWallToClimb = false;
                    //Log.Message(FinalGoalCell + " is trapped for wall climbing check.");
                    if (InfiltrationUtility.IsCellClimbAccessible(FinalGoalCell, pawn.Map, out IntVec3 accessCell))
                    {
                        IntVec3 cellBefore;
                        PawnPath pathFromWall = pawn.Map.pathFinder.FindPathNow(FinalGoalCell, pawn.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));
                        Thing secondThing = pathFromWall.FirstBlockingBuilding(out cellBefore, pawn);
                        if (secondThing != null)
                        {
                            List<IntVec3> reversedPath = pathFromWall.NodesReversed;
                            bool FoundBlocker = false;
                            if (accessCell.IsValid)
                            {
                                climber.climbParameters.EndClimbCell = accessCell;
                            }
                            else
                            {
                                climber.climbParameters.EndClimbCell = cellBefore;
                            }
                            //Log.Message(pawn + " has found climb end point at " + climber.climbParameters.EndClimbCell);


                            for (int i = reversedPath.Count - 1; i >= 0; i--)
                            {
                                if (reversedPath[i].Standable(pawn.Map) && FoundBlocker)
                                {
                                    if (pawn.Map.reachability.CanReach(pawn.Position, reversedPath[i], PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
                                    {
                                        climber.climbParameters.StartClimbCell = reversedPath[i];

                                       // Log.Message(pawn + " has found climb start point at " + climber.climbParameters.StartClimbCell);
                                        break;
                                    }
                                }

                                if (reversedPath[i] == cellBefore)
                                {
                                    FoundBlocker = true;
                                }
                            }
                        }
                        pathFromWall.Dispose();
                    }
                }
                else if (InfiltrationUtility.IsCellTrapped(pawn.Position, pawn.Map, climbTraverseMode, climbDanger))
                {
                    //Log.Message(pawn + " is trapped for wall climbing check.");
                    climber.climbParameters.NoWallToClimb = false;
                    if (InfiltrationUtility.IsCellClimbAccessible(pawn.Position, pawn.Map, out IntVec3 accessCell))
                    {
                        IntVec3 cellBefore;
                        PawnPath pathToWall = pawn.Map.pathFinder.FindPathNow(pawn.Position, FinalGoalCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));
                        Thing firstThing = pathToWall.FirstBlockingBuilding(out cellBefore, pawn);
                        if (firstThing != null)
                        {
                            //Log.Message(pawn + " has found climb start point at " + cellBefore);
                            if (accessCell.IsValid)
                            {
                                climber.climbParameters.StartClimbCell = accessCell;
                            }
                            else
                            {
                                climber.climbParameters.StartClimbCell = cellBefore;
                            }

                            List<IntVec3> reversedPath = pathToWall.NodesReversed;
                            bool FoundBlocker = false;

                            for (int i = reversedPath.Count - 1; i >= 0; i--)
                            {
                                if (reversedPath[i].Standable(pawn.Map) && FoundBlocker)
                                {
                                    if (pawn.Map.reachability.CanReach(FinalGoalCell, reversedPath[i], PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
                                    {
                                        climber.climbParameters.EndClimbCell = reversedPath[i];
                                        break;
                                    }
                                }

                                if (reversedPath[i] == cellBefore)
                                {
                                    //Log.Message(pawn + " has gotten too the start climb point at " + cellBefore);
                                    FoundBlocker = true;
                                }
                            }
                        }
                        pathToWall.Dispose();
                    }
                }
            }

            if (climber.climbParameters.ClimbCellsRegistered)
            {
                Log.Message("Climb cells registered for " + pawn);
                return true;
            }

            return false;
        }
    }
}
