using HarmonyLib;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class DebugActions_ClimbNavigation
    {
        private const int MatrixColumnSpacing = 20;
        private const int MatrixRowSpacing = 14;
        private static readonly List<Thing> spawnedThings = new List<Thing>();
        private static readonly List<RoofChange> roofChanges = new List<RoofChange>();
        private static readonly List<Tuple<Pawn, Job>> pendingJobs = new List<Tuple<Pawn, Job>>();
        private static Map testMap;

        private enum ClimbComplexity
        {
            SealedRoofless,
            LongDoorRoute,
            PartiallyRoofed,
            ShortDoorRoute,
            FullyRoofedNegative,
            DoubleWall
        }

        private enum ClimbJobType
        {
            GoInto,
            GetOut,
            HaulOut
        }

        private struct RoofChange
        {
            public IntVec3 cell;
            public RoofDef previousRoof;
            public RoofDef assignedRoof;

            public RoofChange(IntVec3 cell, RoofDef previousRoof, RoofDef assignedRoof)
            {
                this.cell = cell;
                this.previousRoof = previousRoof;
                this.assignedRoof = assignedRoof;
            }
        }

        internal static DebugActionNode MakeRootNode()
        {
            DebugActionNode root = new DebugActionNode("Climb navigation tests", DebugActionType.Action, null)
            {
                childGetter = delegate
                {
                    return new List<DebugActionNode>
                    {
                        BuildSingleScenarioNode(),
                        BuildComplexityRowNode(),
                        new DebugActionNode("Build full 4 x 3 matrix", DebugActionType.Action, BeginBuildFullMatrix),
                        new DebugActionNode("Report climb decision", DebugActionType.Action, BeginReportClimbDecision),
                        new DebugActionNode("Clear last climb test", DebugActionType.Action, ClearLastTest)
                    };
                }
            };

            return root;
        }

        private static DebugActionNode BuildSingleScenarioNode()
        {
            DebugActionNode root = new DebugActionNode("Build one scenario", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                return Enum.GetValues(typeof(ClimbComplexity)).Cast<ClimbComplexity>()
                    .Select(complexity => BuildJobChoiceNode(ComplexityLabel(complexity), complexity))
                    .ToList();
            };
            return root;
        }

        private static DebugActionNode BuildJobChoiceNode(string label, ClimbComplexity complexity)
        {
            DebugActionNode node = new DebugActionNode(label, DebugActionType.Action, null);
            node.childGetter = delegate
            {
                return Enum.GetValues(typeof(ClimbJobType)).Cast<ClimbJobType>()
                    .Select(jobType => new DebugActionNode(JobLabel(jobType), DebugActionType.Action,
                        delegate { BeginBuildSingle(complexity, jobType); }))
                    .ToList();
            };
            return node;
        }

        private static DebugActionNode BuildComplexityRowNode()
        {
            DebugActionNode root = new DebugActionNode("Build one complexity row", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                return Enum.GetValues(typeof(ClimbComplexity)).Cast<ClimbComplexity>()
                    .Select(complexity => new DebugActionNode(ComplexityLabel(complexity), DebugActionType.Action,
                        delegate { BeginBuildRow(complexity); }))
                    .ToList();
            };
            return root;
        }

        private static void BeginBuildSingle(ClimbComplexity complexity, ClimbJobType jobType)
        {
            BeginCellTargeting("Select the room center for the climb navigation test.", delegate (IntVec3 center, Map map)
            {
                ClearLastTestInternal();
                if (!CanBuildScenario(map, center, complexity))
                {
                    RejectBuild(center);
                    return;
                }

                BeginTest(map);
                BuildScenario(map, center, complexity, jobType);
                FinalizeTest(map, 1);
            });
        }

        private static void BeginBuildRow(ClimbComplexity complexity)
        {
            BeginCellTargeting("Select the center of the first job test; the row extends north.", delegate (IntVec3 anchor, Map map)
            {
                ClearLastTestInternal();
                List<IntVec3> centers = Enum.GetValues(typeof(ClimbJobType)).Cast<ClimbJobType>()
                    .Select(jobType => anchor + new IntVec3(0, 0, (int)jobType * MatrixRowSpacing))
                    .ToList();
                if (centers.Any(center => !CanBuildScenario(map, center, complexity)))
                {
                    RejectBuild(anchor);
                    return;
                }

                BeginTest(map);
                foreach (ClimbJobType jobType in Enum.GetValues(typeof(ClimbJobType)))
                {
                    BuildScenario(map, centers[(int)jobType], complexity, jobType);
                }
                FinalizeTest(map, centers.Count);
            });
        }

        private static void BeginBuildFullMatrix()
        {
            BeginCellTargeting("Select the southwest room center; the matrix extends east and north.", delegate (IntVec3 anchor, Map map)
            {
                ClearLastTestInternal();
                ClimbComplexity[] complexities =
                {
                    ClimbComplexity.SealedRoofless,
                    ClimbComplexity.LongDoorRoute,
                    ClimbComplexity.PartiallyRoofed,
                    ClimbComplexity.ShortDoorRoute
                };

                List<Tuple<IntVec3, ClimbComplexity, ClimbJobType>> scenarios = new List<Tuple<IntVec3, ClimbComplexity, ClimbJobType>>();
                for (int column = 0; column < complexities.Length; column++)
                {
                    foreach (ClimbJobType jobType in Enum.GetValues(typeof(ClimbJobType)))
                    {
                        IntVec3 center = anchor + new IntVec3(column * MatrixColumnSpacing, 0, (int)jobType * MatrixRowSpacing);
                        scenarios.Add(Tuple.Create(center, complexities[column], jobType));
                    }
                }

                if (scenarios.Any(scenario => !CanBuildScenario(map, scenario.Item1, scenario.Item2)))
                {
                    RejectBuild(anchor);
                    return;
                }

                BeginTest(map);
                foreach (Tuple<IntVec3, ClimbComplexity, ClimbJobType> scenario in scenarios)
                {
                    BuildScenario(map, scenario.Item1, scenario.Item2, scenario.Item3);
                }
                FinalizeTest(map, scenarios.Count);
            });
        }

        private static void BeginReportClimbDecision()
        {
            TargetingParameters pawnTargeting = new TargetingParameters
            {
                canTargetPawns = true,
                validator = target => target.Thing is Pawn targetPawn && targetPawn.GetClimberComp() != null
            };
            Messages.Message("Select a spawned climbing cryptimorph.", MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(pawnTargeting, delegate (LocalTargetInfo pawnTarget)
            {
                Pawn pawn = pawnTarget.Pawn;
                TargetingParameters destinationTargeting = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetBuildings = true,
                    canTargetItems = true,
                    canTargetPawns = false,
                    validator = target => target.IsValid && target.Cell.InBounds(pawn.Map)
                };
                Messages.Message("Select the proposed job destination.", MessageTypeDefOf.NeutralEvent, false);
                Find.Targeter.BeginTargeting(destinationTargeting, delegate (LocalTargetInfo destination)
                {
                    PathEndMode peMode = destination.HasThing ? PathEndMode.ClosestTouch : PathEndMode.OnCell;
                    string report = pawn.LabelShort + " -> " + destination + ": " + ClimbUtility.GetClimbDecisionReport(pawn, destination, peMode);
                    Log.Message("[Alien | Rimworld] " + report);
                    Messages.Message(report, MessageTypeDefOf.TaskCompletion, false);
                });
            });
        }

        private static bool CanBuildScenario(Map map, IntVec3 center, ClimbComplexity complexity)
        {
            int minX = complexity == ClimbComplexity.DoubleWall ? -10 : -9;
            int maxX = complexity == ClimbComplexity.DoubleWall ? 7 : 5;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = -6; z <= 6; z++)
                {
                    IntVec3 cell = center + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || cell.GetThingList(map).Any(thing =>
                        thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item || thing is Pawn))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void BeginTest(Map map)
        {
            testMap = map;
            spawnedThings.Clear();
            roofChanges.Clear();
            pendingJobs.Clear();
        }

        private static void BuildScenario(Map map, IntVec3 center, ClimbComplexity complexity, ClimbJobType jobType)
        {
            ClearScenarioRoofs(map, center, complexity);
            BuildRoomShell(map, center, complexity);

            IntVec3 inside = center + (complexity == ClimbComplexity.PartiallyRoofed ? new IntVec3(0, 0, 1) : IntVec3.Zero);
            IntVec3 outside = center + new IntVec3(complexity == ClimbComplexity.DoubleWall ? -9 : -8, 0, 0);
            IntVec3 pawnCell = jobType == ClimbJobType.GetOut ? inside : outside;
            Pawn pawn = PawnGenerator.GeneratePawn(XenoPawnKindDefOf.XMT_StarbeastKind, Faction.OfPlayer);
            if (pawn == null)
            {
                Log.Error("Could not generate a player-controlled cryptimorph for climb navigation test " + complexity + "/" + jobType + ".");
                return;
            }

            GenSpawn.Spawn(pawn, pawnCell, map, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(pawn);
            foreach (Building_Door door in spawnedThings.OfType<Building_Door>().Where(door => door.Map == map && door.Position.DistanceToSquared(center) < 60f))
            {
                XMTDoorUtility.ForceHoldOpenAndOpen(door, pawn);
                AccessTools.Method(typeof(Building_Door), "DoorOpen").Invoke(door, new object[] { 0 });
            }

            MoteMaker.ThrowText(center.ToVector3Shifted(), map, ComplexityLabel(complexity) + " / " + JobLabel(jobType), 8f);
            if (jobType == ClimbJobType.HaulOut)
            {
                Thing item = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                item.stackCount = Math.Min(10, item.def.stackLimit);
                GenSpawn.Spawn(item, inside, map, WipeMode.VanishOrMoveAside);
                spawnedThings.Add(item);
                Job haulJob = JobMaker.MakeJob(JobDefOf.HaulToCell, item, outside);
                haulJob.count = item.stackCount;
                haulJob.haulMode = HaulMode.ToCellNonStorage;
                haulJob.playerForced = true;
                pendingJobs.Add(Tuple.Create(pawn, haulJob));
            }
            else
            {
                IntVec3 destination = jobType == ClimbJobType.GoInto ? inside : outside;
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, destination);
                gotoJob.playerForced = true;
                pendingJobs.Add(Tuple.Create(pawn, gotoJob));
            }
        }

        private static void BuildRoomShell(Map map, IntVec3 center, ClimbComplexity complexity)
        {
            bool shortDoor = complexity == ClimbComplexity.ShortDoorRoute;
            bool longDoor = complexity == ClimbComplexity.LongDoorRoute;
            for (int x = -3; x <= 3; x++)
            {
                for (int z = -3; z <= 3; z++)
                {
                    if (Math.Abs(x) != 3 && Math.Abs(z) != 3)
                    {
                        continue;
                    }

                    bool doorway = x == -3 && z == (shortDoor ? 0 : longDoor ? 2 : int.MinValue);
                    SpawnStructure(map, center + new IntVec3(x, 0, z), doorway);
                }
            }

            if (longDoor)
            {
                for (int x = -6; x <= -3; x++)
                {
                    SpawnStructure(map, center + new IntVec3(x, 0, 1), false);
                    SpawnStructure(map, center + new IntVec3(x, 0, 3), false);
                }
                SpawnStructure(map, center + new IntVec3(-6, 0, 2), true);
            }

            if (complexity == ClimbComplexity.DoubleWall)
            {
                for (int x = -6; x <= 6; x++)
                {
                    for (int z = -6; z <= 6; z++)
                    {
                        if (Math.Abs(x) == 6 || Math.Abs(z) == 6)
                        {
                            SpawnStructure(map, center + new IntVec3(x, 0, z), false);
                        }
                    }
                }
            }

            if (complexity == ClimbComplexity.PartiallyRoofed || complexity == ClimbComplexity.FullyRoofedNegative)
            {
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        if (complexity == ClimbComplexity.FullyRoofedNegative || z >= 0)
                        {
                            SetRoofTracked(map, center + new IntVec3(x, 0, z), RoofDefOf.RoofConstructed);
                        }
                    }
                }
            }
        }

        private static void SpawnStructure(Map map, IntVec3 cell, bool door)
        {
            Thing thing = door
                ? ThingMaker.MakeThing(ThingDefOf.Door, DefDatabase<ThingDef>.GetNamed("BlocksSlate"))
                : ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
            GenSpawn.Spawn(thing, cell, map, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(thing);
        }

        private static void ClearScenarioRoofs(Map map, IntVec3 center, ClimbComplexity complexity)
        {
            int minX = complexity == ClimbComplexity.DoubleWall ? -10 : -9;
            int maxX = complexity == ClimbComplexity.DoubleWall ? 7 : 5;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = -6; z <= 6; z++)
                {
                    SetRoofTracked(map, center + new IntVec3(x, 0, z), null);
                }
            }
        }

        private static void SetRoofTracked(Map map, IntVec3 cell, RoofDef roof)
        {
            RoofDef previous = map.roofGrid.RoofAt(cell);
            if (previous == roof)
            {
                return;
            }

            roofChanges.Add(new RoofChange(cell, previous, roof));
            map.roofGrid.SetRoof(cell, roof);
        }

        private static void FinalizeTest(Map map, int scenarioCount)
        {
            map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            foreach (Tuple<Pawn, Job> pendingJob in pendingJobs)
            {
                if (pendingJob.Item1.Spawned && !pendingJob.Item1.Dead)
                {
                    pendingJob.Item1.jobs.StartJob(pendingJob.Item2, JobCondition.InterruptForced);
                }
            }
            pendingJobs.Clear();
            Messages.Message("Built " + scenarioCount + " climb navigation scenario(s). Use Report climb decision for District details.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearLastTest()
        {
            bool hadTest = spawnedThings.Count > 0 || roofChanges.Count > 0;
            ClearLastTestInternal();
            Messages.Message(hadTest ? "Cleared the last climb navigation test." : "No climb navigation test is registered for cleanup.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearLastTestInternal()
        {
            Map map = testMap;
            foreach (Thing thing in spawnedThings.ToList())
            {
                if (thing is Pawn pawn)
                {
                    PawnClimber flyer = pawn.GetClimberComp()?.pawnClimber;
                    if (flyer != null && !flyer.Destroyed)
                    {
                        flyer.Destroy(DestroyMode.Vanish);
                    }
                }
                if (thing != null && !thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }

            if (map != null)
            {
                for (int i = roofChanges.Count - 1; i >= 0; i--)
                {
                    RoofChange change = roofChanges[i];
                    if (change.cell.InBounds(map) && map.roofGrid.RoofAt(change.cell) == change.assignedRoof)
                    {
                        map.roofGrid.SetRoof(change.cell, change.previousRoof);
                    }
                }
                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            }

            spawnedThings.Clear();
            roofChanges.Clear();
            pendingJobs.Clear();
            testMap = null;
        }

        private static string ComplexityLabel(ClimbComplexity complexity)
        {
            switch (complexity)
            {
                case ClimbComplexity.SealedRoofless: return "Sealed roofless (required)";
                case ClimbComplexity.LongDoorRoute: return "Two-door detour (preferred)";
                case ClimbComplexity.PartiallyRoofed: return "Partially roofed (required)";
                case ClimbComplexity.ShortDoorRoute: return "One-door direct (walking control)";
                case ClimbComplexity.FullyRoofedNegative: return "Fully roofed sealed (negative)";
                case ClimbComplexity.DoubleWall: return "Two sealed walls (route stress)";
                default: return complexity.ToString();
            }
        }

        private static string JobLabel(ClimbJobType jobType)
        {
            switch (jobType)
            {
                case ClimbJobType.GoInto: return "Go into room";
                case ClimbJobType.GetOut: return "Get out of room";
                case ClimbJobType.HaulOut: return "Pick up item and haul out";
                default: return jobType.ToString();
            }
        }

        private static void RejectBuild(IntVec3 anchor)
        {
            Messages.Message("The climb test footprint at " + anchor + " is out of bounds or contains buildings, items, or pawns.", MessageTypeDefOf.RejectInput, false);
        }

        private static void BeginCellTargeting(string prompt, Action<IntVec3, Map> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                validator = target => Find.CurrentMap != null && target.Cell.InBounds(Find.CurrentMap)
            };
            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    onSelected(target.Cell, map);
                }
            });
        }
    }
}
