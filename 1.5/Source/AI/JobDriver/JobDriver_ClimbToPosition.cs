using RimWorld;
using System.Collections.Generic;
using Verse.AI;
using Verse;


namespace Xenomorphtype
{
    public class JobDriver_ClimbToPosition : JobDriver
    {
        protected virtual IntVec3 FinalGoalCell => TargetA.Cell;
        protected IntVec3 StartClimbCell = IntVec3.Invalid;
        protected IntVec3 EndClimbCell = IntVec3.Invalid;
        protected bool ClimbCellsRegistered = false;
        protected bool NoWallToClimb = true;
        protected bool Tunneling = false;
        protected bool InTransit = false;
        PawnClimber pawnClimber;
        protected bool PopulateClimbCells()
        {
            if (InTransit)
            {
                return false;
            }

            bool ClimbOver = true;
            Tunneling = false;
            IntVec3 Start = pawn.Position;
            //Log.Message("Trying infiltrate from Position: " + Start);
            if(InfiltrationUtility.GetInfiltrationEntry(pawn.Map, Start, FinalGoalCell, out Building entry))
            {
                StartClimbCell = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(Start, entry);
                //Log.Message("Found Infiltration Target Entry: " + entry + " at " + StartClimbCell);
                if (InfiltrationUtility.GetInfiltrationExit(entry, FinalGoalCell, out Building exit))
                {
                   
                    EndClimbCell = InfiltrationUtility.GetGoalOnOrAdjacentToFrom(FinalGoalCell, exit);
                    //Log.Message("Found Infiltration Target Exit: " + exit + " at " + EndClimbCell);
                    NoWallToClimb = false;
                    ClimbOver = false;
                    Tunneling = true;
                }
            }

            if (ClimbOver)
            {
                TraverseMode climbTraverseMode = pawn.Faction == Faction.OfPlayer? TraverseMode.PassDoors : TraverseMode.NoPassClosedDoors ;
                Danger climbDanger = pawn.Faction == Faction.OfPlayer ? Danger.Deadly : Danger.None;

                if (InfiltrationUtility.IsCellTrapped(FinalGoalCell, pawn.Map,climbTraverseMode,climbDanger))
                {
                    NoWallToClimb = false;
                    Log.Message(FinalGoalCell + " is trapped for wall climbing check.");
                    if (InfiltrationUtility.IsCellClimbAccessible(FinalGoalCell, pawn.Map, out IntVec3 accessCell))
                    {
                        IntVec3 cellBefore;
                        PawnPath pathFromWall = pawn.Map.pathFinder.FindPath(FinalGoalCell, pawn.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));
                        Thing secondThing = pathFromWall.FirstBlockingBuilding(out cellBefore, pawn);
                        if (secondThing != null)
                        {
                            List<IntVec3> reversedPath = pathFromWall.NodesReversed;
                            bool FoundBlocker = false;
                            if (accessCell.IsValid)
                            {
                                EndClimbCell = accessCell;
                            }
                            else
                            {
                                EndClimbCell = cellBefore;
                            }
                            Log.Message(pawn + " has found climb end point at " + EndClimbCell);
                            

                            for (int i = reversedPath.Count - 1; i >= 0; i--)
                            {
                                if (reversedPath[i].Standable(pawn.Map) && FoundBlocker)
                                {
                                    if (pawn.Map.reachability.CanReach(pawn.Position, reversedPath[i], PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
                                    {
                                        StartClimbCell = reversedPath[i];

                                        Log.Message(pawn + " has found climb start point at " + StartClimbCell);
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
                else if (InfiltrationUtility.IsCellTrapped(pawn.Position, pawn.Map,climbTraverseMode, climbDanger))
                {
                    Log.Message(pawn + " is trapped for wall climbing check.");
                    NoWallToClimb = false;
                    if (InfiltrationUtility.IsCellClimbAccessible(pawn.Position, pawn.Map, out IntVec3 accessCell))
                    {
                        IntVec3 cellBefore;
                        PawnPath pathToWall = pawn.Map.pathFinder.FindPath(pawn.Position, FinalGoalCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings));
                        Thing firstThing = pathToWall.FirstBlockingBuilding(out cellBefore, pawn);
                        if (firstThing != null)
                        {
                            Log.Message(pawn + " has found climb start point at " + cellBefore);
                            if (accessCell.IsValid)
                            {
                                StartClimbCell = accessCell;
                            }
                            else
                            {
                                StartClimbCell = cellBefore;
                            }

                            List<IntVec3> reversedPath = pathToWall.NodesReversed;
                            bool FoundBlocker = false;

                            for (int i = reversedPath.Count - 1; i >= 0; i--)
                            {
                                if (reversedPath[i].Standable(pawn.Map) && FoundBlocker)
                                {
                                    if (pawn.Map.reachability.CanReach(FinalGoalCell, reversedPath[i], PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
                                    {
                                        EndClimbCell = reversedPath[i];
                                        break;
                                    }
                                }

                                if (reversedPath[i] == cellBefore)
                                {
                                    Log.Message(pawn + " has gotten too the start climb point at " + cellBefore);
                                    FoundBlocker = true;
                                }
                            }
                        }
                        pathToWall.Dispose();
                    }
                }
            }

            if (StartClimbCell.IsValid && EndClimbCell.IsValid)
            {
                //Log.Message("Valid Climb Positions ");
                ClimbCellsRegistered = true;
                return true;
            }

            return false;
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            PopulateClimbCells();

            return ClimbCellsRegistered || NoWallToClimb;
        }

        protected Toil ClimbOverWall()
        {
            Toil toil = ToilMaker.MakeToil("ClimbingOverWall");
            toil.initAction = delegate
            {
                PopulateClimbCells(); 
                IntVec3 position = pawn.Position;
                IntVec3 cell = EndClimbCell;
                Map map = pawn.Map;
                bool flag = Find.Selector.IsSelected(pawn);
                
                pawnClimber = Tunneling ? PawnClimber.MakeClimber(InternalDefOf.PawnClimbUnder, pawn, cell,EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true,StartClimbCell.ToVector3(), null,EndClimbCell)
                : PawnClimber.MakeClimber(InternalDefOf.PawnClimber, pawn, cell, EffecterDefOf.ConstructDirt, SoundDefOf.Pawn_Melee_Punch_Miss, true, StartClimbCell.ToVector3(), null, EndClimbCell);
                
                if (pawnClimber != null)
                {
                    
                    pawnClimber.underground = Tunneling;
                    GenSpawn.Spawn(pawnClimber, cell, map);
                    InTransit = true;
                    if (flag)
                    {
                        Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                    }

                    if (!Tunneling)
                    {
                        RoofCollapserImmediate.DropRoofInCells(StartClimbCell, map);
                    }
                }
            };
            toil.tickAction = delegate
            {
                if(pawn.Position == EndClimbCell && pawn.Spawned)
                {
                  
                    Log.Message("Arrived and Done");
                    InTransit = false;
                    ReadyForNextToil();
                }
            };
            toil.AddFinishAction(delegate
            {
                CompMatureMorph morph = pawn.GetComp<CompMatureMorph>();
                if (morph != null)
                {
                    morph.ClearAllTickLimits();
                }

            });
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            if(NoWallToClimb)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
            }
            else
            {
                yield return Toils_Goto.GotoCell(StartClimbCell, PathEndMode.ClosestTouch);
                yield return ClimbOverWall();
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);

            }
        }
    }
}
