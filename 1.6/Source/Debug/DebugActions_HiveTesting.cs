using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class DebugActions_HiveTesting
    {
        private const string Category = "Alien | Rimworld";
        private enum ClimbBuildTestMode
        {
            MovementOnly,
            ExactBuild,
            EmergentBuild,
            EmergentDoubleWall,
            BreachedControl
        }

        [DebugAction(Category, "Toggle hive room refog", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleHiveRoomRefog()
        {
            XMTHiveDebug.DisableHiveRoomRefog = !XMTHiveDebug.DisableHiveRoomRefog;
            Messages.Message("Hive room refog disabled: " + XMTHiveDebug.DisableHiveRoomRefog, MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Spawn feral rest seeker", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnFeralRestSeeker()
        {
            BeginCellTargeting("Select cryptimorph rest seeker spawn cell.", delegate (IntVec3 spawnCell, Map map)
            {
                Pawn pawn = XenoformingUtility.GenerateFeralXenomorph();
                if (pawn == null)
                {
                    Messages.Message("Could not generate feral cryptimorph.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                IntVec3 finalCell = CellFinder.RandomClosewalkCellNear(spawnCell, map, 2);
                GenSpawn.Spawn(pawn, finalCell, map, WipeMode.VanishOrMoveAside);
                XMTHiveUtility.AddHiveMate(pawn, map);

                if (pawn.needs?.rest != null)
                {
                    pawn.needs.rest.CurLevel = 0.02f;
                }

                if (pawn.needs?.food != null)
                {
                    pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
                }

                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Messages.Message("Spawned tired feral rest seeker at " + finalCell + ".", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void ClearHiveBuildStimuli()
        {
            XMTHiveUtility.ClearHiveBuildStimuli(Find.CurrentMap);
            Messages.Message("Cleared hive build stimuli on current map.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ReportNestBuildDecision()
        {
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Cell.InBounds(Find.CurrentMap)
            };

            Messages.Message("Select a cryptimorph or nearby cell.", MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("No current map found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Pawn pawn = target.Pawn ?? target.Cell.GetFirstPawn(map);
                if (pawn == null || !XMTUtility.IsXenomorph(pawn))
                {
                    pawn = map.mapPawns.AllPawnsSpawned
                        .Where(candidate => candidate.Spawned && XMTUtility.IsXenomorph(candidate) && candidate.Position.DistanceTo(target.Cell) <= 5f)
                        .OrderBy(candidate => candidate.Position.DistanceToSquared(target.Cell))
                        .FirstOrDefault();
                }

                if (pawn == null)
                {
                    Messages.Message("No cryptimorph found near " + target.Cell + ".", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                IntVec3 nestPosition = XMTHiveUtility.GetNestPosition(map);
                if (XMTNestBuildingUtility.TryFindNestBuildRequest(pawn, nestPosition, 5, out NestBuildRequest request, debug: true))
                {
                    Messages.Message(pawn.LabelShort + " nest build decision: " + request.stage + " " + (request.buildDef?.defName ?? "roof") + " at " + request.cell + " score " + request.score + ".", MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    MoteMaker.ThrowText(pawn.Position.ToVector3Shifted(), map, "No nest build job", 6f);
                    Messages.Message(pawn.LabelShort + " found no nest build decision.", MessageTypeDefOf.NeutralEvent, false);
                }
            });
        }

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> BuildTestNodes()
        {
            DebugActionNode root = new DebugActionNode("Build tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    new DebugActionNode("Clear hive build stimuli", DebugActionType.Action, ClearHiveBuildStimuli),
                    new DebugActionNode("Report nest build decision", DebugActionType.Action, ReportNestBuildDecision),
                    ReleaseHiveBuildingCrewNode(),
                    MakeClimbBuildTestsNode()
                };
            };

            yield return root;
        }

        private static DebugActionNode ReleaseHiveBuildingCrewNode()
        {
            DebugActionNode root = new DebugActionNode("Release hive building crew", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                List<DebugActionNode> children = new List<DebugActionNode>();
                foreach (int count in new[] { 1, 3, 6, 10 })
                {
                    int selectedCount = count;
                    children.Add(new DebugActionNode(selectedCount + " builders", DebugActionType.Action, delegate
                    {
                        BeginCellTargeting("Select release cell.", delegate (IntVec3 releaseCell, Map map)
                        {
                            BeginCellTargeting("Select vague hive target.", delegate (IntVec3 targetCell, Map targetMap)
                            {
                                if (map != targetMap)
                                {
                                    Messages.Message("Release and target cells must be on the same map.", MessageTypeDefOf.RejectInput, false);
                                    return;
                                }

                                ReleaseBuildingCrew(map, releaseCell, targetCell, selectedCount);
                            });
                        });
                    }));
                }

                return children;
            };

            return root;
        }

        private static DebugActionNode MakeClimbBuildTestsNode()
        {
            DebugActionNode root = new DebugActionNode("Climb build scenarios", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    MakeClimbBuildTestNode("Movement only: sealed room", ClimbBuildTestMode.MovementOnly, new[] { 1, 3, 6 }),
                    MakeClimbBuildTestNode("Exact build: sealed room", ClimbBuildTestMode.ExactBuild, new[] { 1 }),
                    MakeClimbBuildTestNode("Emergent build: sealed nest", ClimbBuildTestMode.EmergentBuild, new[] { 1, 3, 6 }),
                    MakeClimbBuildTestNode("Emergent build: double wall", ClimbBuildTestMode.EmergentDoubleWall, new[] { 1, 3, 6 }),
                    MakeClimbBuildTestNode("Breached control", ClimbBuildTestMode.BreachedControl, new[] { 1, 3, 6 })
                };
            };

            return root;
        }

        private static DebugActionNode MakeClimbBuildTestNode(string label, ClimbBuildTestMode mode, int[] counts)
        {
            DebugActionNode node = new DebugActionNode(label, DebugActionType.Action, null);
            node.childGetter = delegate
            {
                List<DebugActionNode> children = new List<DebugActionNode>();
                foreach (int count in counts)
                {
                    int selectedCount = count;
                    children.Add(new DebugActionNode(selectedCount + " builders", DebugActionType.Action, delegate
                    {
                        BeginCellTargeting("Select release cell.", delegate (IntVec3 releaseCell, Map map)
                        {
                            BeginCellTargeting("Select chamber target cell.", delegate (IntVec3 targetCell, Map targetMap)
                            {
                                if (map != targetMap)
                                {
                                    Messages.Message("Release and target cells must be on the same map.", MessageTypeDefOf.RejectInput, false);
                                    return;
                                }

                                ReleaseClimbBuildTestCrew(map, releaseCell, targetCell, selectedCount, mode);
                            });
                        });
                    }));
                }

                return children;
            };

            return node;
        }

        private static void ReleaseBuildingCrew(Map map, IntVec3 releaseCell, IntVec3 targetCell, int count)
        {
            XMTHiveUtility.RegisterHiveBuildStimulus(map, targetCell, radius: 24, durationTicks: 45000, strength: 80);
            XMTHiveUtility.ForceNestPosition(targetCell, map);

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = XenoformingUtility.GenerateFeralXenomorph();
                if (pawn == null)
                {
                    continue;
                }

                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(releaseCell, map, 3);
                GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.VanishOrMoveAside);
                spawned++;
            }

            Messages.Message("Released " + spawned + " hive builders with vague target " + targetCell + ".", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ReleaseClimbBuildTestCrew(Map map, IntVec3 releaseCell, IntVec3 targetCell, int count, ClimbBuildTestMode mode)
        {
            bool doubleWall = mode == ClimbBuildTestMode.EmergentDoubleWall;
            bool breached = mode == ClimbBuildTestMode.BreachedControl;
            if (!TryCreateClimbBuildTestSite(map, targetCell, doubleWall, breached))
            {
                Messages.Message("Could not create climb build test site at " + targetCell + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (mode == ClimbBuildTestMode.EmergentBuild || mode == ClimbBuildTestMode.EmergentDoubleWall || mode == ClimbBuildTestMode.BreachedControl)
            {
                XMTHiveUtility.RegisterHiveBuildStimulus(map, targetCell, radius: doubleWall ? 14 : 10, durationTicks: 45000, strength: 120);
                XMTHiveUtility.ForceNestPosition(targetCell, map);
            }

            List<Pawn> pawns = SpawnFeralBuilders(map, releaseCell, count);
            foreach (Pawn pawn in pawns)
            {
                if (mode == ClimbBuildTestMode.MovementOnly)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Goto, targetCell);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                else if (mode == ClimbBuildTestMode.ExactBuild)
                {
                    MoteMaker.ThrowText(targetCell.ToVector3Shifted(), map, "Forced Hivemass target", 6f);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, targetCell);
                    job.plantDefToSow = XenoBuildingDefOf.Hivemass;
                    job.playerForced = true;
                    FeralJobUtility.ReservePlaceForJob(pawn, job, targetCell);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    Messages.Message("Forced exact hive build assigned: Hivemass at " + targetCell + ".", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            Messages.Message("Released " + pawns.Count + " builders for " + mode + " at " + targetCell + ".", MessageTypeDefOf.TaskCompletion, false);
        }

        private static List<Pawn> SpawnFeralBuilders(Map map, IntVec3 releaseCell, int count)
        {
            List<Pawn> pawns = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = XenoformingUtility.GenerateFeralXenomorph();
                if (pawn == null)
                {
                    continue;
                }

                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(releaseCell, map, 3);
                GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.VanishOrMoveAside);
                pawns.Add(pawn);
            }

            return pawns;
        }

        private static bool TryCreateClimbBuildTestSite(Map map, IntVec3 targetCell, bool doubleWall, bool breached)
        {
            if (map == null || !targetCell.InBounds(map))
            {
                return false;
            }

            int outerRadius = doubleWall ? 6 : 5;
            int innerRadius = 3;
            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                for (int z = -outerRadius; z <= outerRadius; z++)
                {
                    IntVec3 cell = targetCell + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map))
                    {
                        return false;
                    }
                }
            }

            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                for (int z = -outerRadius; z <= outerRadius; z++)
                {
                    IntVec3 cell = targetCell + new IntVec3(x, 0, z);
                    ClearCellForDebugBuildSite(cell, map);
                    map.roofGrid.SetRoof(cell, null);

                    bool outerWall = System.Math.Abs(x) == outerRadius || System.Math.Abs(z) == outerRadius;
                    bool innerWall = doubleWall && (System.Math.Abs(x) == innerRadius || System.Math.Abs(z) == innerRadius) && System.Math.Abs(x) <= innerRadius && System.Math.Abs(z) <= innerRadius;
                    bool breachCell = breached && z == 0 && x == -outerRadius;
                    if ((!outerWall && !innerWall) || breachCell)
                    {
                        continue;
                    }

                    Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
                    GenSpawn.Spawn(wall, cell, map, WipeMode.VanishOrMoveAside);
                }
            }

            return true;
        }

        private static void ClearCellForDebugBuildSite(IntVec3 cell, Map map)
        {
            List<Thing> thingList = cell.GetThingList(map);
            for (int i = thingList.Count - 1; i >= 0; i--)
            {
                Thing thing = thingList[i];
                if (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Plant)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static void BeginCellTargeting(string prompt, System.Action<IntVec3, Map> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Cell.InBounds(Find.CurrentMap)
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("No current map found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                onSelected(target.Cell, map);
            });
        }
    }
}
