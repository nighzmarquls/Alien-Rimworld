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
    public static class DebugActions_InfiltrationNavigation
    {
        private static readonly List<Thing> spawnedThings = new List<Thing>();
        private static readonly List<Zone> spawnedZones = new List<Zone>();
        private static readonly List<RoofChange> roofChanges = new List<RoofChange>();
        private static Map testMap;

        private enum Geometry
        {
            SingleVent,
            SingleCooler,
            ContiguousMixed,
            ChainedVents,
            DoorAirgap,
            EnclosedViaOpenSky,
            HivePipeNetwork
        }

        private enum TestJob
        {
            GoInto,
            GetOut,
            HaulOut,
            MedicalRest,
            HaulSelected,
            HaulAutomatic
        }

        private sealed class TestPatientGoToBedGiver : JobGiver_PatientGoToBed
        {
            public Job TryGiveMedicalRestJob(Pawn pawn)
            {
                return TryGiveJob(pawn);
            }
        }

        private struct RoofChange
        {
            public IntVec3 Cell;
            public RoofDef Previous;
            public RoofDef Assigned;
        }

        internal static DebugActionNode MakeRootNode()
        {
            return new DebugActionNode("Infiltration navigation tests", DebugActionType.Action, null)
            {
                childGetter = delegate
                {
                    List<DebugActionNode> nodes = Enum.GetValues(typeof(Geometry)).Cast<Geometry>()
                        .Select(BuildGeometryNode)
                        .ToList();
                    nodes.Add(new DebugActionNode("Report selected route", DebugActionType.Action, BeginReportRoute));
                    nodes.Add(new DebugActionNode("Report topology cache", DebugActionType.Action, ReportCache));
                    nodes.Add(new DebugActionNode("Clear topology cache", DebugActionType.Action, ClearCache));
                    nodes.Add(new DebugActionNode("Toggle geometric cache", DebugActionType.Action, ToggleCache));
                    nodes.Add(new DebugActionNode("Clear last infiltration test", DebugActionType.Action, ClearLastTest));
                    return nodes;
                }
            };
        }

        private static DebugActionNode BuildGeometryNode(Geometry geometry)
        {
            return new DebugActionNode(GeometryLabel(geometry), DebugActionType.Action, null)
            {
                childGetter = delegate
                {
                    return Enum.GetValues(typeof(TestJob)).Cast<TestJob>()
                        .Select(job => new DebugActionNode(JobLabel(job), DebugActionType.Action,
                            delegate { BeginBuild(geometry, job); }))
                        .ToList();
                }
            };
        }

        private static void BeginBuild(Geometry geometry, TestJob testJob)
        {
            BeginCellTargeting("Select the center of the infiltration test structure.", delegate (IntVec3 center, Map map)
            {
                ClearLastTestInternal();
                int halfWidth = geometry == Geometry.EnclosedViaOpenSky || geometry == Geometry.HivePipeNetwork ? 8 :
                    geometry == Geometry.ChainedVents || geometry == Geometry.DoorAirgap ? 6 : 4;
                if (!FootprintClear(map, center, halfWidth))
                {
                    Messages.Message("The infiltration test footprint is obstructed or out of bounds.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                testMap = map;
                if (geometry == Geometry.HivePipeNetwork)
                {
                    BuildHivePipeNetworkRooms(map, center);
                }
                else
                {
                    BuildShell(map, center, halfWidth);
                    if (geometry == Geometry.EnclosedViaOpenSky)
                    {
                        BuildDivider(map, center, -4, new[] { 0 }, new[] { ThingDefOf.Vent });
                        BuildDivider(map, center, 0, Array.Empty<int>(), Array.Empty<ThingDef>());
                        BuildDivider(map, center, 4, new[] { 0 }, new[] { ThingDefOf.Vent });
                        for (int x = -3; x <= 3; x++)
                        {
                            for (int z = -2; z <= 2; z++)
                            {
                                SetRoof(map, center + new IntVec3(x, 0, z), null);
                            }
                        }
                    }
                    else if (geometry == Geometry.ChainedVents || geometry == Geometry.DoorAirgap)
                    {
                        BuildDivider(map, center, -2, new[] { 0 }, new[] { ThingDefOf.Vent });
                        BuildDivider(map, center, 2, new[] { 0 }, new[] { ThingDefOf.Vent });
                        if (geometry == Geometry.DoorAirgap)
                        {
                            BuildDoorDivider(map, center, 0);
                        }
                    }
                    else
                    {
                        switch (geometry)
                        {
                            case Geometry.SingleVent:
                                BuildDivider(map, center, 0, new[] { 0 }, new[] { ThingDefOf.Vent });
                                break;
                            case Geometry.SingleCooler:
                                BuildDivider(map, center, 0, new[] { 0 }, new[] { ThingDefOf.Cooler });
                                break;
                            case Geometry.ContiguousMixed:
                                BuildDivider(map, center, 0, new[] { -1, 0, 1 }, new[] { ThingDefOf.Vent, ThingDefOf.Cooler, ThingDefOf.Vent });
                                break;
                        }
                    }
                }

                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                InfiltrationUtility.ClearCache(map);
                StartTestJob(map, center, halfWidth, geometry, testJob);
                Messages.Message("Built " + GeometryLabel(geometry) + " / " + JobLabel(testJob) + ".", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void BuildShell(Map map, IntVec3 center, int halfWidth)
        {
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                SpawnWall(map, center + new IntVec3(x, 0, -3));
                SpawnWall(map, center + new IntVec3(x, 0, 3));
            }
            for (int z = -2; z <= 2; z++)
            {
                SpawnWall(map, center + new IntVec3(-halfWidth, 0, z));
                SpawnWall(map, center + new IntVec3(halfWidth, 0, z));
            }
            for (int x = -halfWidth + 1; x < halfWidth; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    SetRoof(map, center + new IntVec3(x, 0, z), RoofDefOf.RoofConstructed);
                }
            }
        }

        private static void BuildHivePipeNetworkRooms(Map map, IntVec3 center)
        {
            BuildEnclosedRoom(map, center, -8, -2);
            BuildEnclosedRoom(map, center, 2, 8);

            ThingDef pipeDef = DefDatabase<ThingDef>.GetNamed("XMT_JellyPipe");
            for (int x = -5; x <= 5; x++)
            {
                SpawnBuilding(map, center + new IntVec3(x, 0, 0), pipeDef, Rot4.North);
            }

            SpawnBuilding(map, center + new IntVec3(-6, 0, 0), DefDatabase<ThingDef>.GetNamed("XMT_JellyPool"), Rot4.East);
            IntVec3 drainCell = center + new IntVec3(6, 0, 0);
            SpawnWall(map, drainCell);
            SpawnBuilding(map, drainCell, DefDatabase<ThingDef>.GetNamed("XMT_JellyDrain"), Rot4.West);
        }

        private static void BuildEnclosedRoom(Map map, IntVec3 center, int minX, int maxX)
        {
            for (int x = minX; x <= maxX; x++)
            {
                SpawnWall(map, center + new IntVec3(x, 0, -3));
                SpawnWall(map, center + new IntVec3(x, 0, 3));
            }
            for (int z = -2; z <= 2; z++)
            {
                SpawnWall(map, center + new IntVec3(minX, 0, z));
                SpawnWall(map, center + new IntVec3(maxX, 0, z));
            }
            for (int x = minX + 1; x < maxX; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    SetRoof(map, center + new IntVec3(x, 0, z), RoofDefOf.RoofConstructed);
                }
            }
        }

        private static void BuildDivider(Map map, IntVec3 center, int x, int[] portZ, ThingDef[] portDefs)
        {
            Dictionary<int, ThingDef> ports = new Dictionary<int, ThingDef>();
            for (int i = 0; i < portZ.Length; i++)
            {
                ports[portZ[i]] = portDefs[i];
            }
            for (int z = -2; z <= 2; z++)
            {
                IntVec3 cell = center + new IntVec3(x, 0, z);
                if (ports.TryGetValue(z, out ThingDef portDef))
                {
                    Thing port = ThingMaker.MakeThing(portDef);
                    GenSpawn.Spawn(port, cell, map, WipeMode.VanishOrMoveAside);
                    spawnedThings.Add(port);
                }
                else
                {
                    SpawnWall(map, cell);
                }
            }
        }

        private static void BuildDoorDivider(Map map, IntVec3 center, int x)
        {
            for (int z = -2; z <= 2; z++)
            {
                IntVec3 cell = center + new IntVec3(x, 0, z);
                if (z == 0)
                {
                    Thing door = ThingMaker.MakeThing(ThingDefOf.Door, DefDatabase<ThingDef>.GetNamed("BlocksSlate"));
                    GenSpawn.Spawn(door, cell, map, WipeMode.VanishOrMoveAside);
                    spawnedThings.Add(door);
                }
                else
                {
                    SpawnWall(map, cell);
                }
            }
        }

        private static void StartTestJob(Map map, IntVec3 center, int halfWidth, Geometry geometry, TestJob testJob)
        {
            IntVec3 left = geometry == Geometry.HivePipeNetwork
                ? center + new IntVec3(-5, 0, 1)
                : center + new IntVec3(-halfWidth + 2, 0, 0);
            IntVec3 right = geometry == Geometry.HivePipeNetwork
                ? center + new IntVec3(5, 0, 1)
                : center + new IntVec3(halfWidth - 2, 0, 0);
            IntVec3 pawnCell = testJob == TestJob.GetOut ? right : left;
            Pawn pawn = PawnGenerator.GeneratePawn(XenoPawnKindDefOf.XMT_StarbeastKind, Faction.OfPlayer);
            GenSpawn.Spawn(pawn, pawnCell, map, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(pawn);
            foreach (Building_Door door in spawnedThings.OfType<Building_Door>())
            {
                XMTDoorUtility.ForceHoldOpenAndOpen(door, pawn);
                AccessTools.Method(typeof(Building_Door), "DoorOpen").Invoke(door, new object[] { 0 });
            }

            if (geometry == Geometry.HivePipeNetwork)
            {
                ReportHivePipeRoutePreflight(pawn, testJob == TestJob.GetOut ? left : right);
            }

            Job job;
            if (testJob == TestJob.HaulOut)
            {
                Thing item = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                item.stackCount = Math.Min(10, item.def.stackLimit);
                GenSpawn.Spawn(item, right, map, WipeMode.VanishOrMoveAside);
                spawnedThings.Add(item);
                job = JobMaker.MakeJob(JobDefOf.HaulToCell, item, left);
                job.count = item.stackCount;
                job.haulMode = HaulMode.ToCellNonStorage;
            }
            else if (testJob == TestJob.MedicalRest)
            {
                job = MakeMedicalRestJob(map, pawn, right);
            }
            else if (testJob == TestJob.HaulSelected)
            {
                job = MakeVanillaHaulJob(map, pawn, left, right, automatic: false);
            }
            else if (testJob == TestJob.HaulAutomatic)
            {
                job = MakeVanillaHaulJob(map, pawn, right, left, automatic: true);
            }
            else
            {
                IntVec3 destination = testJob == TestJob.GoInto ? right : left;
                job = JobMaker.MakeJob(JobDefOf.Goto, destination);
            }

            if (job == null)
            {
                Messages.Message(JobLabel(testJob) + " selector returned no job.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            job.playerForced = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        private static Job MakeMedicalRestJob(Map map, Pawn pawn, IntVec3 destinationRoomCell)
        {
            IntVec3 bedCell = destinationRoomCell + new IntVec3(0, 0, -1);
            Building_Bed bed = ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog) as Building_Bed;
            bed.SetFaction(pawn.Faction);
            GenSpawn.Spawn(bed, bedCell, map, Rot4.North, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(bed);
            bed.Medical = true;

            for (int i = 0; i < 3 && !HealthAIUtility.ShouldSeekMedicalRest(pawn); i++)
            {
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f));
            }

            Job job = new TestPatientGoToBedGiver().TryGiveMedicalRestJob(pawn);
            bool valid = job != null && job.targetA.Thing == bed;
            string report = "Medical-rest selector " + (valid ? "passed" : "FAILED") +
                ": seeksRest=" + HealthAIUtility.ShouldSeekMedicalRest(pawn) +
                ", job=" + job + ", selectedBed=" + job?.targetA.Thing +
                ", bedFaction=" + bed.Faction + ", pawnFaction=" + pawn.Faction;
            Log.Message("[Alien | Rimworld] " + report);
            Messages.Message(report, valid ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            return valid ? job : null;
        }

        private static Job MakeVanillaHaulJob(Map map, Pawn pawn, IntVec3 itemRoomCell, IntVec3 storageRoomCell, bool automatic)
        {
            IntVec3 itemCell = itemRoomCell + new IntVec3(0, 0, 1);
            IntVec3 storageCell = storageRoomCell + new IntVec3(0, 0, 1);
            Thing item = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            item.stackCount = Math.Min(10, item.def.stackLimit);
            GenSpawn.Spawn(item, itemCell, map, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(item);

            Zone_Stockpile stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile);
            stockpile.AddCell(storageCell);
            StorageSettings settings = stockpile.GetStoreSettings();
            settings.Priority = StoragePriority.Critical;
            settings.filter.SetDisallowAll();
            settings.filter.SetAllow(ThingDefOf.WoodLog, true);
            spawnedZones.Add(stockpile);

            Job job;
            if (automatic)
            {
                pawn.workSettings?.EnableAndInitializeIfNotAlreadyInitialized();
                if (pawn.workSettings != null)
                {
                    foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                    {
                        pawn.workSettings.SetPriority(workType, workType == WorkTypeDefOf.Hauling ? 1 : 0);
                    }
                }
                job = new JobGiver_Work().TryIssueJobPackage(pawn, default).Job;
            }
            else
            {
                job = new WorkGiver_HaulGeneral().JobOnThing(pawn, item, forced: true);
            }

            bool valid = job != null && job.targetA.Thing == item && job.targetB.Cell == storageCell;
            string report = (automatic ? "Automatic haul selector " : "Selected haul selector ") +
                (valid ? "passed" : "FAILED") + ": job=" + job +
                ", item=" + item.Position + ", storage=" + storageCell +
                ", selectedStorage=" + (job == null ? "none" : job.targetB.Cell.ToString());
            Log.Message("[Alien | Rimworld] " + report);
            Messages.Message(report, valid ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            return valid ? job : null;
        }

        private static void ReportHivePipeRoutePreflight(Pawn pawn, IntVec3 destination)
        {
            bool heuristic = InfiltrationUtility.CanReachByInfiltration(pawn, destination, PathEndMode.OnCell, pawn.NormalMaxDanger());
            bool exact = InfiltrationUtility.TryBuildTraversalRoute(pawn, destination, PathEndMode.OnCell, pawn.NormalMaxDanger(), out List<TraversalLeg> legs);
            bool valid = heuristic && exact && legs.Count == 1 && legs[0].IsInfiltration &&
                legs[0].providerKey == "pipeNet" && legs[0].categoryKey == "XMT_JellyNet";
            string report = "Hive pipe route preflight " + (valid ? "passed" : "FAILED") +
                ": heuristic=" + heuristic + ", exact=" + exact +
                ", legs=" + (exact ? string.Join("; ", legs) : "none");
            Log.Message("[Alien | Rimworld] " + report);
            Messages.Message(report, valid ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
        }

        private static void BeginReportRoute()
        {
            TargetingParameters pawnTargeting = new TargetingParameters
            {
                canTargetPawns = true,
                validator = target => target.Thing is Pawn targetPawn && targetPawn.GetClimberComp() != null
            };
            Messages.Message("Select a climbing cryptimorph.", MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(pawnTargeting, delegate (LocalTargetInfo pawnTarget)
            {
                Pawn pawn = pawnTarget.Pawn;
                BeginCellTargeting("Select the infiltration destination.", delegate (IntVec3 destination, Map map)
                {
                    bool heuristic = InfiltrationUtility.CanReachByInfiltration(pawn, destination, PathEndMode.OnCell, pawn.NormalMaxDanger());
                    bool exact = InfiltrationUtility.TryBuildTraversalRoute(pawn, destination, PathEndMode.OnCell, pawn.NormalMaxDanger(), out List<TraversalLeg> legs);
                    string report = pawn.LabelShort + " -> " + destination + ": heuristic=" + heuristic + ", exact=" + exact +
                        ", legs=" + (exact ? string.Join("; ", legs) : "none");
                    Log.Message("[Alien | Rimworld] " + report);
                    Messages.Message(report, heuristic == exact ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
                });
            });
        }

        private static bool FootprintClear(Map map, IntVec3 center, int halfWidth)
        {
            for (int x = -halfWidth - 1; x <= halfWidth + 1; x++)
            {
                for (int z = -4; z <= 4; z++)
                {
                    IntVec3 cell = center + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || cell.GetZone(map) != null ||
                        cell.GetThingList(map).Any(thing => thing is Building || thing is Pawn || thing.def.category == ThingCategory.Item))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void SpawnWall(Map map, IntVec3 cell)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
            GenSpawn.Spawn(wall, cell, map, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(wall);
        }

        private static void SpawnBuilding(Map map, IntVec3 cell, ThingDef def, Rot4 rotation)
        {
            Thing building = ThingMaker.MakeThing(def);
            GenSpawn.Spawn(building, cell, map, rotation, WipeMode.VanishOrMoveAside);
            spawnedThings.Add(building);
        }

        private static void SetRoof(Map map, IntVec3 cell, RoofDef roof)
        {
            RoofDef previous = map.roofGrid.RoofAt(cell);
            if (previous == roof)
            {
                return;
            }
            roofChanges.Add(new RoofChange { Cell = cell, Previous = previous, Assigned = roof });
            map.roofGrid.SetRoof(cell, roof);
        }

        private static void ReportCache()
        {
            string report = InfiltrationUtility.CacheReport(Find.CurrentMap);
            Log.Message("[Alien | Rimworld] Infiltration topology cache\n" + report);
            Messages.Message("Infiltration cache report written to the log.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearCache()
        {
            InfiltrationUtility.ClearCache(Find.CurrentMap);
            Messages.Message("Cleared the current map's infiltration cache.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ToggleCache()
        {
            InfiltrationUtility.CacheGeometricNetworks = !InfiltrationUtility.CacheGeometricNetworks;
            InfiltrationUtility.ClearAllCaches();
            Messages.Message("Geometric infiltration caching: " + InfiltrationUtility.CacheGeometricNetworks, MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearLastTest()
        {
            ClearLastTestInternal();
            Messages.Message("Cleared the last infiltration test.", MessageTypeDefOf.TaskCompletion, false);
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
                foreach (Zone zone in spawnedZones.ToList())
                {
                    zone?.Delete();
                }
                for (int i = roofChanges.Count - 1; i >= 0; i--)
                {
                    RoofChange change = roofChanges[i];
                    if (change.Cell.InBounds(map) && map.roofGrid.RoofAt(change.Cell) == change.Assigned)
                    {
                        map.roofGrid.SetRoof(change.Cell, change.Previous);
                    }
                }
                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                InfiltrationUtility.ClearCache(map);
            }
            spawnedThings.Clear();
            spawnedZones.Clear();
            roofChanges.Clear();
            testMap = null;
        }

        private static string GeometryLabel(Geometry geometry)
        {
            switch (geometry)
            {
                case Geometry.SingleVent: return "Single vent";
                case Geometry.SingleCooler: return "Single cooler";
                case Geometry.ContiguousMixed: return "Contiguous vent/cooler network";
                case Geometry.ChainedVents: return "Independent vents with room airgap";
                case Geometry.DoorAirgap: return "Independent vents with forced-open door airgap";
                case Geometry.EnclosedViaOpenSky: return "Enclosed rooms via infiltration and open-sky climb";
                case Geometry.HivePipeNetwork: return "Fully enclosed rooms via hive pipe network";
                default: return geometry.ToString();
            }
        }

        private static string JobLabel(TestJob job)
        {
            switch (job)
            {
                case TestJob.GoInto: return "Go into room";
                case TestJob.GetOut: return "Get out of room";
                case TestJob.HaulOut: return "Haul through network";
                case TestJob.MedicalRest: return "Select medical rest across traversal";
                case TestJob.HaulSelected: return "Select haul into remote stockpile";
                case TestJob.HaulAutomatic: return "Auto-select remote item and haul back";
                default: return job.ToString();
            }
        }

        private static void BeginCellTargeting(string prompt, Action<IntVec3, Map> action)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetLocations = true,
                validator = target => Find.CurrentMap != null && target.Cell.InBounds(Find.CurrentMap)
            }, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    action(target.Cell, map);
                }
            });
        }
    }
}
