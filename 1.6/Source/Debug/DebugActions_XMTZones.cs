using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class DebugActions_XMTZones
    {
        private const string Category = "Alien | Rimworld";
        private const int HalfWidth = 5;
        private const int HalfHeight = 4;

        private static ZoneTestFixture fixture;

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> ZoneTestNodes()
        {
            DebugActionNode root = new DebugActionNode("Cryptimorph zone tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    new DebugActionNode("Create zoning fixture", DebugActionType.Action, BeginCreateFixture),
                    new DebugActionNode("Report zoning fixture", DebugActionType.Action, ReportFixture),
                    new DebugActionNode("Start storage move job", DebugActionType.Action, StartStorageMoveJob),
                    new DebugActionNode("Invalidate active storage destination", DebugActionType.Action, InvalidateStorageDestination),
                    new DebugActionNode("Breach host room", DebugActionType.Action, BreachHostRoom),
                    new DebugActionNode("Add loose storage blockers", DebugActionType.Action, AddLooseStorageBlockers),
                    new DebugActionNode("Clear last zoning test", DebugActionType.Action, ClearFixture)
                };
            };

            yield return root;
        }

        private static void BeginCreateFixture()
        {
            Messages.Message("Select the center of an empty 11 x 9 zoning test site.", MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => Find.CurrentMap != null && target.Cell.InBounds(Find.CurrentMap)
            };

            Find.Targeter.BeginTargeting(parameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null || !TryCreateFixture(map, target.Cell))
                {
                    Messages.Message("Could not create the zoning fixture at that location.", MessageTypeDefOf.RejectInput, false);
                }
            });
        }

        private static bool TryCreateFixture(Map map, IntVec3 center)
        {
            ClearFixture();
            if (XMTUtility.QueenPresent() && !XMTUtility.QueenIsPlayer())
            {
                Messages.Message("Remove the active non-player queen before creating this player-zone fixture.", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            CellRect rect = CellRect.CenteredOn(center, HalfWidth, HalfHeight);
            if (!rect.InBounds(map) || !FixtureAreaIsClear(rect, map))
            {
                return false;
            }

            ZoneTestFixture newFixture = new ZoneTestFixture(map, center);
            fixture = newFixture;

            foreach (IntVec3 cell in rect)
            {
                bool perimeter = cell.x == rect.minX ||
                                 cell.x == rect.maxX ||
                                 cell.z == rect.minZ ||
                                 cell.z == rect.maxZ;
                if (perimeter)
                {
                    Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
                    GenSpawn.Spawn(wall, cell, map, WipeMode.VanishOrMoveAside);
                    newFixture.spawnedThings.Add(wall);
                }
                else
                {
                    newFixture.originalRoofs[cell] = map.roofGrid.RoofAt(cell);
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                }
            }

            Zone_HostPlacement hostZone = new Zone_HostPlacement(map.zoneManager);
            Zone_OvomorphStorage storageZone = new Zone_OvomorphStorage(map.zoneManager);
            newFixture.zones.Add(hostZone);
            newFixture.zones.Add(storageZone);

            for (int x = rect.minX + 1; x <= rect.maxX - 1; x++)
            {
                for (int z = rect.minZ + 1; z <= rect.maxZ - 1; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (x <= center.x - 2)
                    {
                        hostZone.AddCell(cell);
                    }
                    else if (x >= center.x + 2)
                    {
                        storageZone.AddCell(cell);
                    }
                }
            }

            Pawn queen = XenoformingUtility.GenerateFeralQueen();
            if (queen == null)
            {
                ClearFixture();
                return false;
            }

            queen.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(queen, center, map, WipeMode.VanishOrMoveAside);
            newFixture.spawnedThings.Add(queen);
            newFixture.testQueen = queen;
            XMTUtility.DeclareQueen(queen);

            Pawn worker = PawnGenerator.GeneratePawn(XenoPawnKindDefOf.XMT_StarbeastKind, Faction.OfPlayer);
            if (worker == null)
            {
                ClearFixture();
                return false;
            }

            GenSpawn.Spawn(worker, center + IntVec3.South, map, WipeMode.VanishOrMoveAside);
            newFixture.spawnedThings.Add(worker);
            newFixture.worker = worker;
            if (worker.workSettings == null ||
                !worker.workSettings.EverWork ||
                worker.WorkTypeIsDisabled(XenoWorkDefOf.Hauling))
            {
                ClearFixture();
                return false;
            }

            worker.workSettings.SetPriority(XenoWorkDefOf.Hauling, 3);
            if (!worker.workSettings.WorkIsActive(XenoWorkDefOf.Hauling))
            {
                ClearFixture();
                return false;
            }

            SpawnEligibleOvomorphs(newFixture, center + IntVec3.North);
            Messages.Message(
                "Created host and ovomorph-storage zone fixtures with a player queen and hauling worker.",
                MessageTypeDefOf.TaskCompletion,
                false);
            return true;
        }

        private static bool FixtureAreaIsClear(CellRect rect, Map map)
        {
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map))
                {
                    return false;
                }

                if (cell.GetThingList(map).Any(thing =>
                    thing.def.category == ThingCategory.Building ||
                    thing.def.category == ThingCategory.Item ||
                    thing.def.category == ThingCategory.Plant ||
                    thing is Pawn))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SpawnEligibleOvomorphs(ZoneTestFixture testFixture, IntVec3 nearCell)
        {
            List<ThingDef> eligibleDefs = XenoBuildingDefOf.XMT_OvomorphStorageEligible?.things;
            if (eligibleDefs == null)
            {
                return;
            }

            int offset = 0;
            foreach (ThingDef def in eligibleDefs.Where(def => def != null))
            {
                IntVec3 spawnCell = nearCell + new IntVec3(offset, 0, 0);
                Thing thing = ThingMaker.MakeThing(def);
                GenSpawn.Spawn(thing, spawnCell, testFixture.map, WipeMode.VanishOrMoveAside);
                testFixture.spawnedThings.Add(thing);
                offset++;
            }
        }

        private static void ReportFixture()
        {
            if (!FixtureIsActive())
            {
                Messages.Message("No active zoning fixture.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            bool hostCellFound = XMTZoneUtility.TryGetAbductionCocoonCell(fixture.worker, out IntVec3 hostCell);
            bool storageJobFound = XMTZoneUtility.TryMakeOvomorphStorageJob(fixture.worker, out Job storageJob);
            if (storageJobFound)
            {
                FeralJobUtility.ClearFeralJobReservationsClaimedBy(fixture.map, fixture.worker);
            }

            string report = "[XMT][ZoneTest] hostCell=" + (hostCellFound ? hostCell.ToString() : "none") +
                            ", storageJob=" + (storageJobFound ? storageJob.ToString() : "none") +
                            ", hostZones=" + fixture.map.zoneManager.AllZones.OfType<Zone_HostPlacement>().Count() +
                            ", storageZones=" + fixture.map.zoneManager.AllZones.OfType<Zone_OvomorphStorage>().Count() +
                            ", capacity={" + XMTZoneUtility.HostZoneCapacityDebugReport(fixture.worker) + "}.";
            Log.Message(report);
            Messages.Message(report, MessageTypeDefOf.TaskCompletion, false);
        }

        private static void StartStorageMoveJob()
        {
            if (!FixtureIsActive())
            {
                Messages.Message("No active zoning fixture.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!XMTZoneUtility.TryMakeOvomorphStorageJob(fixture.worker, out Job job))
            {
                Messages.Message("The fixture worker found no storage move job.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            fixture.worker.jobs.StartJob(job, JobCondition.InterruptForced);
            Messages.Message("Started the existing XMT_MoveOvomorph job for storage.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void BreachHostRoom()
        {
            if (!FixtureIsActive())
            {
                Messages.Message("No active zoning fixture.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            IntVec3 breachCell = fixture.center + new IntVec3(-HalfWidth, 0, 0);
            Thing wall = breachCell.GetEdifice(fixture.map);
            if (wall == null || !fixture.spawnedThings.Contains(wall))
            {
                Messages.Message("The fixture breach wall is already absent.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            wall.Destroy(DestroyMode.Vanish);
            fixture.map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            Messages.Message("Breached the host-zone room; report the fixture to verify fallback.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void InvalidateStorageDestination()
        {
            if (!FixtureIsActive() ||
                fixture.worker.CurJob?.def != XenoWorkDefOf.XMT_MoveOvomorph ||
                !fixture.worker.CurJob.GetTarget(TargetIndex.C).IsValid)
            {
                Messages.Message("The fixture worker has no active storage move destination.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            IntVec3 destination = fixture.worker.CurJob.GetTarget(TargetIndex.B).Cell;
            if (fixture.map.zoneManager.ZoneAt(destination) is not Zone_OvomorphStorage storageZone)
            {
                Messages.Message("The active destination is no longer in a storage zone.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            storageZone.RemoveCell(destination);
            Messages.Message(
                "Removed the active storage destination cell; the shared move job should retarget or preserve its minified ovomorph.",
                MessageTypeDefOf.TaskCompletion,
                false);
        }

        private static void AddLooseStorageBlockers()
        {
            if (!FixtureIsActive())
            {
                Messages.Message("No active zoning fixture.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Zone_OvomorphStorage storageZone = fixture.zones.OfType<Zone_OvomorphStorage>().FirstOrDefault();
            if (storageZone == null)
            {
                return;
            }

            foreach (IntVec3 cell in storageZone.Cells.Take(3))
            {
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = 5;
                GenSpawn.Spawn(steel, cell, fixture.map, WipeMode.VanishOrMoveAside);
                fixture.spawnedThings.Add(steel);
            }

            Messages.Message("Added loose item stacks to storage cells.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static bool FixtureIsActive()
        {
            return fixture?.map != null &&
                   !fixture.map.Disposed &&
                   fixture.worker?.Spawned == true &&
                   fixture.worker.Map == fixture.map;
        }

        private static void ClearFixture()
        {
            ZoneTestFixture oldFixture = fixture;
            fixture = null;
            if (oldFixture?.map == null || oldFixture.map.Disposed)
            {
                return;
            }

            foreach (Pawn pawn in oldFixture.spawnedThings.OfType<Pawn>())
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                FeralJobUtility.ForceClearFeralJobReservationsClaimedBy(oldFixture.map, pawn);

                if (pawn.carryTracker?.CarriedThing is MinifiedThing carried &&
                    oldFixture.spawnedThings.Contains(carried.InnerThing))
                {
                    pawn.carryTracker.TryDropCarriedThing(
                        pawn.Position,
                        ThingPlaceMode.Near,
                        out Thing dropped,
                        null);
                    if (dropped != null && !oldFixture.spawnedThings.Contains(dropped))
                    {
                        oldFixture.spawnedThings.Add(dropped);
                    }
                }
            }

            foreach (Zone zone in oldFixture.zones.ToList())
            {
                if (oldFixture.map.zoneManager.AllZones.Contains(zone))
                {
                    zone.Delete();
                }
            }

            foreach (Thing thing in oldFixture.spawnedThings.ToList())
            {
                if (thing == null || thing.Destroyed)
                {
                    continue;
                }

                if (thing == oldFixture.testQueen)
                {
                    XMTUtility.QueenDied(oldFixture.testQueen);
                }

                thing.Destroy(DestroyMode.Vanish);
            }

            foreach (KeyValuePair<IntVec3, RoofDef> roof in oldFixture.originalRoofs)
            {
                oldFixture.map.roofGrid.SetRoof(roof.Key, roof.Value);
            }

            oldFixture.map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            Messages.Message("Cleared the last zoning test fixture.", MessageTypeDefOf.TaskCompletion, false);
        }

        private sealed class ZoneTestFixture
        {
            internal readonly Map map;
            internal readonly IntVec3 center;
            internal readonly List<Thing> spawnedThings = new List<Thing>();
            internal readonly List<Zone> zones = new List<Zone>();
            internal readonly Dictionary<IntVec3, RoofDef> originalRoofs = new Dictionary<IntVec3, RoofDef>();
            internal Pawn testQueen;
            internal Pawn worker;

            internal ZoneTestFixture(Map map, IntVec3 center)
            {
                this.map = map;
                this.center = center;
            }
        }
    }
}
