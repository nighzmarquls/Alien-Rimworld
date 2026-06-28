using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;

namespace Xenomorphtype
{
    public class XMTHiveUtility
    {
        public const float HiveHungerCostPerTick = 0.000085f;
        protected class NestSite
        {
            public int Suitability;
            public IntVec3 Position;
            public Map map;

            public Room Room
            {
                get
                {
                    return Position.GetRoom(map);
                }
            }

            protected List<Job> buildJobs;

            public List<Job> BuildJobs
            {
                get
                {
                    if (buildJobs == null)
                    {
                        buildJobs = new List<Job>();
                    }
                    return buildJobs;
                }

                set
                {
                    buildJobs = value;
                }
            }

            protected List<Ovomorph> ovomorphs;
            public  List<Ovomorph> Ovomorphs
            {
                get
                {
                    if (ovomorphs == null)
                    {
                        ovomorphs = new List<Ovomorph>();
                    }
                    return ovomorphs;
                }
            }

            protected List<GeneOvomorph> geneOvomorphs;
            public List<GeneOvomorph> GeneOvomorphs
            {
                get
                {
                    if (geneOvomorphs == null)
                    {
                        geneOvomorphs = new List<GeneOvomorph>();
                    }
                    return geneOvomorphs;
                }
            }

            protected List<Pawn> cocooned;

            public List<Pawn> Cocooned
            {
                get
                {
                    if (cocooned == null)
                    {
                        cocooned = new List<Pawn>();
                    }
                    return cocooned;
                }
            }
            protected List<Pawn> availableHosts;

            public List<Pawn> AvailableHosts
            {
                get
                {
                    if (availableHosts == null)
                    {
                        availableHosts = new List<Pawn>();
                    }
                    return availableHosts;
                }
            }

            protected List<Pawn> implantedHosts;

            public List<Pawn> ImplantedHosts
            {
                get
                {
                    if (implantedHosts == null)
                    {
                        implantedHosts = new List<Pawn>();
                    }
                    return implantedHosts;
                }
            }

            protected List<Pawn> ovomorphingPawns;

            public List<Pawn> OvomorphingPawns
            {
                get
                {
                    if (ovomorphingPawns == null)
                    {
                        ovomorphingPawns = new List<Pawn>();
                    }
                    return ovomorphingPawns;
                }
            }

            protected List<MeatballLarder> meatballThings;

            public List<MeatballLarder> MeatballThings
            {
                get
                {
                    if(meatballThings == null)
                    {
                        meatballThings = new List<MeatballLarder> ();
                    }
                    return meatballThings;
                }
            }

            protected List<Pawn> hiveMatePawns;

            public List<Pawn> HiveMates
            {
                get
                {
                    if (hiveMatePawns == null)
                    {
                        hiveMatePawns = new List<Pawn>();
                    }
                    return hiveMatePawns;
                }
            }

            private List<HiveRoomRecord> hiveRooms;
            public List<HiveRoomRecord> HiveRooms
            {
                get
                {
                    if (hiveRooms == null)
                    {
                        hiveRooms = new List<HiveRoomRecord>();
                    }

                    return hiveRooms;
                }
            }

            public int EggCount => Ovomorphs.Count + OvomorphingPawns.Count;

            public bool HaveHosts => AvailableHosts.Count > 0;

            public bool HaveEggs => Ovomorphs.Count > 0;

            public bool NeedEggs => XMTUtility.NoQueenPresent()? EggCount <= AvailableHosts.Count : false;

            public bool NeedHosts => EggCount > AvailableHosts.Count;

            public int TotalHiveMates => HiveMates.Count;

            public int TotalCocooned => Cocooned.Count;
        }

        protected class HiveRoomRecord
        {
            public Room room;
            public IntVec3 anchorCell;
            public IntVec3 restExpansionAnchor = IntVec3.Invalid;
            public IntVec3 cocoonExpansionAnchor = IntVec3.Invalid;
            public int score;
            public int lastSeenTick;
        }

        private class HiveBuildStimulus
        {
            public IntVec3 center;
            public Map map;
            public int radius;
            public int expireTick;
            public int strength;
        }

        private class HiveOverextensionSignal
        {
            public Map map;
            public int expireTick;
            public int strength;
        }

        private const int MaxHiveBuildCandidateCells = 80;
        private const int MaxHiveWanderCandidateCells = 48;
        private const int MaxHiveRestCandidates = 24;

        protected static List<NestSite> hive;
        protected static List<NestSite> Hive
        {
            get
            {
                if (hive == null)
                {
                    hive = new List<NestSite>();
                }
                return hive;
            }
        }

        private static List<HiveBuildStimulus> buildStimuli;
        private static List<HiveBuildStimulus> BuildStimuli
        {
            get
            {
                if (buildStimuli == null)
                {
                    buildStimuli = new List<HiveBuildStimulus>();
                }
                return buildStimuli;
            }
        }

        private static List<HiveOverextensionSignal> overextensionSignals;
        private static List<HiveOverextensionSignal> OverextensionSignals
        {
            get
            {
                if (overextensionSignals == null)
                {
                    overextensionSignals = new List<HiveOverextensionSignal>();
                }
                return overextensionSignals;
            }
        }

        internal static void RegisterHiveBuildStimulus(Map map, IntVec3 center, int radius = 20, int durationTicks = 30000, int strength = 50)
        {
            if (map == null || !center.InBounds(map))
            {
                return;
            }

            BuildStimuli.Add(new HiveBuildStimulus
            {
                map = map,
                center = center,
                radius = radius,
                expireTick = Find.TickManager.TicksGame + durationTicks,
                strength = strength
            });
        }

        internal static void ClearHiveBuildStimuli(Map map = null)
        {
            if (map == null)
            {
                BuildStimuli.Clear();
                OverextensionSignals.Clear();
                return;
            }

            BuildStimuli.RemoveAll(stimulus => stimulus.map == map);
            OverextensionSignals.RemoveAll(signal => signal.map == map);
        }

        internal static void RegisterHiveOverextensionSignal(Pawn pawn, int severity = 1)
        {
            if (pawn?.MapHeld == null)
            {
                return;
            }

            int strength = Mathf.Clamp(15 + severity * 10, 20, 90);
            OverextensionSignals.Add(new HiveOverextensionSignal
            {
                map = pawn.MapHeld,
                expireTick = Find.TickManager.TicksGame + 30000,
                strength = strength
            });
        }

        internal static void NotifyLocalThreatStimulus(Thing victim, Thing aggressor, IntVec3 eventPosition, Map map, float radius)
        {
            if (map == null || !eventPosition.InBounds(map))
            {
                return;
            }

            RegisterHiveBuildStimulus(map, eventPosition, Mathf.CeilToInt(radius * 2f), 15000, 60);

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(eventPosition, map, radius, true))
            {
                if (thing is HibernationCocoon cocoon && cocoon.ContainedThing is Pawn contained && XMTUtility.IsXenomorph(contained))
                {
                    cocoon.Open();
                }
            }
        }

        public static int TotalHivePopulation(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return 0;
            }

            return localNest.TotalHiveMates;
        }
        internal static bool IsValidOvomorphingTarget(Pawn candidate)
        {
            if (candidate == null || candidate.Destroyed || candidate.Dead || candidate.MapHeld == null || !candidate.Downed)
            {
                return false;
            }

            return !(XMTUtility.IsMorphing(candidate) || XMTUtility.HasEmbryo(candidate) || XMTUtility.IsInorganic(candidate));
        }

        private static void PruneInvalidAvailableHosts(NestSite localNest)
        {
            if (localNest == null)
            {
                return;
            }

            localNest.AvailableHosts.RemoveAll(x => !IsValidOvomorphingTarget(x) || !localNest.Cocooned.Contains(x));
        }

        public static void CleanNestLists(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }

            List<Pawn> removeList = new List<Pawn>();
        }

        public static void ClearAllNestSites()
        {
            Hive.Clear();
        }
        protected static NestSite GetLocalNest(Map map)
        {
            return Hive.Where(x => x.map == map).FirstOrDefault();
        }
        public static bool ShouldTendNest(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if(localNest == null)
            {
                return false;
            }

            PruneInvalidAvailableHosts(localNest);

            if(localNest.HaveEggs && localNest.HaveHosts)
            {
                return true;
            }

            if(ShouldCoolNest(map))
            {
                return true;
            }

            /*
            IEnumerable<FillableChrysalis> Chrysali = map.listerBuildings.AllBuildingsColonistOfClass<FillableChrysalis>();

            if(Chrysali.Any())
            {
                foreach(FillableChrysalis chrysalis in Chrysali)
                {
                    if (!chrysalis.Filled)
                    {
                        return true;
                    }
                }
            }*/

            if (localNest.Cocooned.Count() > 0)
            {
                foreach (Pawn cocooned in localNest.Cocooned)
                {
                    if (cocooned != null)
                    {
                        if (cocooned.Map == map)
                        {
                            Need_Food food = cocooned.needs.food;

                            if (food != null)
                            {
                                if (food.CurCategory != HungerCategory.Fed)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            if(localNest.HiveMates.Count() > 1)
            {
                foreach(Pawn morph in localNest.HiveMates)
                {
                    if(morph != null)
                    {
                        if(morph.Map == map)
                        {
                            Need_Food food = morph.needs.food;

                            if (food != null)
                            {
                                if (food.CurCategory != HungerCategory.Fed)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool ShouldCoolNest(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return false;
            }

            Room lairRoom = localNest.Position.GetRoomOrAdjacent(map);

            if(lairRoom == null)
            {
                return false;
            }

            if (lairRoom.UsesOutdoorTemperature)
            {
                return false;
            }

            if(lairRoom.Temperature > 50)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldBuildNest(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return false;
            }

            return XMTNestBuildingUtility.ShouldBuildNest(map, localNest.Position);
        }

        public static bool HasLocalNest(Map map)
        {
            return GetLocalNest(map) != null;
        }

        public static void AddLarder(Thing larder, Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            MeatballLarder meatballLarder = larder as MeatballLarder;

            if(meatballLarder == null)
            {
                return;
            }

            if (localNest.MeatballThings.Contains(larder))
            {
                return;
            }
            
            localNest.MeatballThings.Add(meatballLarder);

        }

        public static void RemoveLarder(Thing larder, Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return;
            }

            MeatballLarder meatballLarder = larder as MeatballLarder;

            if (meatballLarder == null)
            {
                return;
            }

            localNest.MeatballThings.Remove(meatballLarder);
 
        }
        public static void AddHiveMate(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if (localNest.HiveMates.Count() > 0 && localNest.HiveMates.Contains(pawn))
            {
                return;
            }
            if (XMTUtility.IsXenomorphFriendly(pawn) || XMTUtility.IsXenomorph(pawn))
            {
                localNest.HiveMates.Add(pawn);
            }
        }

        public static void RemoveHiveMate(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return;
            }
            localNest.HiveMates.Remove(pawn);
        }
        public static void AddCocooned(Pawn pawn, Map map)
        {
            
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if (localNest.Cocooned.Contains(pawn))
            {
                return;
            }

            if (XMTUtility.IsHost(pawn))
            {
                localNest.AvailableHosts.Add(pawn);
            }

            localNest.Cocooned.Add(pawn);
        }

        public static void RemoveCocooned(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return;
            }
            localNest.Cocooned.Remove(pawn);
            localNest.AvailableHosts.Remove(pawn);
        }
        public static bool IsCloseToNest(IntVec3 site, Map map, float distance = 4f)
        {
            
            if (site == IntVec3.Invalid)
            {
                return false;
            }
            NestSite localNest = GetLocalNest(map);

            if(localNest == null)
            {
                return false;
            }

            return (float)(site - localNest.Position).LengthHorizontalSquared < distance;

        }
        public static bool IsInsideNest(IntVec3 site, Map map)
        {
            if (site == IntVec3.Invalid )
            {
                return false;
            }
            NestSite localNest = GetLocalNest(map);

            if(localNest == null)
            {
                return false;
            }

            Room nestRoom = localNest.Position.GetRoomOrAdjacent(map);

            if(nestRoom == null)
            {
                return false;
            }

            return nestRoom.Cells.Contains(site);

        }

        public static List<Thing> GetAllGeneCarriers(Map map)
        {
            List<Thing> result = new List<Thing>();

            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return result;
            }

            result.AddRange(localNest.Ovomorphs);
            result.AddRange(localNest.GeneOvomorphs);

            foreach(Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if(XMTUtility.IsXenomorph(pawn))
                {
                    result.Add(pawn);
                }

            }

            return result;
        }

        public static Thing GetMostOffensiveThingInNest(IntVec3 site, Map map)
        {
            if (site == IntVec3.Invalid || map == null)
            {
                return null;
            }

            Room lairRoom = site.GetRoomOrAdjacent(map);

            if(lairRoom.PsychologicallyOutdoors)
            {
                return null;
            }

            IEnumerable<Building> buildings = lairRoom.ContainedThings<Building>();

            float mostOffense = 0f;
            Thing mostOffensive = null;
            foreach (Building building in buildings)
            {
                if (building.def == ThingDefOf.VoidMonolith)
                {
                    continue;
                }

                if(building.IsBrokenDown())
                {
                    continue;
                }

                float currentOffense = 0f;
                Building_Turret turret = building as Building_Turret;
                if (turret != null)
                {
                    currentOffense += 50;
                }

                currentOffense += HiveLightOffenseScore(building);

                if (currentOffense > mostOffense)
                {
                    mostOffense = currentOffense;
                    mostOffensive = building;
                }
            }
            return mostOffensive;
        }
        protected static void FindGoodNestSite(Map map)
        {
            List<Room> rooms = map.regionGrid.AllRooms.Where(x=> !x.IsDoorway).ToList();

            rooms.Shuffle();

            int bestScore = 0;
            IntVec3 lairSpot = IntVec3.Invalid;
                
            foreach (Room room in rooms)
            {
                int currentScore = 0;
                if(room.Fogged)
                {
                    currentScore += 50;
                }

                if (IsRoomValidForNest(room))
                {
                    IntVec3 currentSpot = room.Cells.RandomElement();

                    if (currentSpot.CloseToEdge(map, 10))
                    {
                        continue;
                    }

                    currentScore += EvaluateNestSite(currentSpot, map);
                    if (currentScore > bestScore)
                    {
                        bestScore = currentScore;
                        lairSpot = currentSpot;
                    }
                }
            }

            if(lairSpot != IntVec3.Invalid)
            {
                IEnumerable<Plant> CaveFungus = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().Where(p => 
                p.def == ExternalDefOf.Bryolux || p.def == ExternalDefOf.Glowstool || p.def == ExternalDefOf.Agarilux);
                foreach(Plant plant in CaveFungus)
                { 
                    if(plant.Position.CloseToEdge(map,10))
                    {
                        continue;
                    }

                    Room plantRoom = plant.Position.GetRoom(map);
                    if (IsRoomValidForNest(plantRoom))
                    {
                        lairSpot = plant.Position;
                        break;
                    }
                }
            }

            if(lairSpot == IntVec3.Invalid)
            {
                IEnumerable<Building> Geysers = map.listerBuildings.AllBuildingsNonColonistOfDef(ThingDefOf.SteamGeyser);
                foreach(Building Geyser in Geysers)
                {
                    if (Geyser.Position.CloseToEdge(map, 10))
                    {
                        continue;
                    }

                    Room geyserRoom = Geyser.Position.GetRoom(map);
                    if (IsRoomValidForNest(geyserRoom))
                    {
                        lairSpot = Geyser.Position;
                        break;
                    }
                }
            }

            if(lairSpot == IntVec3.Invalid)
            {
                for(int i = 0; i < 10; i++)
                {
                    IntVec3 cell = CellFinder.RandomCell(map);

                    if (cell.CloseToEdge(map, 10))
                    {
                        continue;
                    }
                    Room geyserRoom = cell.GetRoom(map);
                    if (IsRoomValidForNest(geyserRoom))
                    {
                        lairSpot = cell;
                        break;
                    }
                }
            }

            if(lairSpot == IntVec3.Invalid)
            {
                lairSpot = map.Center;
            }

            Hive.Add(InitializeNest(lairSpot, map));
            if (lairSpot.InBounds(map))
            {
                GenSpawn.Spawn(XenoBuildingDefOf.XMT_HiddenNestSpot, lairSpot, map, WipeMode.VanishOrMoveAside);
            }

        }

        private static bool IsRoomValidForNest(Room room)
        {
            if(room == null)
            {
                return false;
            }

            if(room.Map == null)
            {
                return false;
            }

            if(room.Cells == null || room.Cells.Count() == 0)
            {
                return false;
            }

            IntVec3 StartCell = room.Cells.RandomElement();
     
            bool canReach = !InfiltrationUtility.IsCellTrapped(StartCell, room.Map);
            return  (room.CellCount > 9) && canReach;
        }

        public static void TryPlaceResinFloor(IntVec3 cell, Map map)
        {
            if (map.terrainGrid.BaseTerrainAt(cell) != InternalDefOf.HeavyHiveFloor)
            {
                map.terrainGrid.SetTerrain(cell, InternalDefOf.HiveFloor);
            }
        }

        public static bool IsCellValidCocoon(IntVec3 cell, Map map, bool fast = false)
        {
            if(cell == IntVec3.Invalid || map == null || !cell.InBounds(map))
            {
                return false;
            }

            if(!cell.Standable(map))
            {
                return false;
            }

            Building Edifice = cell.GetEdifice(map);

            if (Edifice != null)
            {
                return false;
            }

            if(fast)
            {
                return HasAdjacentOpenEggPlacementCell(cell, map, ignorePawns: true);
            }

            Building support = null;

            IEnumerable<IntVec3> Adjacents = GenRadial.RadialCellsAround(cell, 1f, false);

            foreach (IntVec3 adjacent in Adjacents)
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                IEnumerable<Building> things = adjacent.GetThingList(map).OfType<Building>();

                if (things.Any())
                {
                    foreach (Building thing in things)
                    {
                        if(thing is CocoonBase || thing is Ovomorph || thing is Building_Door)
                        {
                            return false;
                        }

                        if (thing.def.holdsRoof)
                        {
                            support = thing;
                            break;
                        }
                    }
                }
            }

            if (support == null)
            {
                return false;
            }

            return HasAdjacentOpenEggPlacementCell(cell, map);
        }

        internal static bool HasAdjacentOpenEggPlacementCell(IntVec3 cocoonCell, Map map, Pawn forPawn = null, IntVec3 blockedCell = default, bool ignorePawns = false)
        {
            if (!cocoonCell.IsValid || map == null || !cocoonCell.InBounds(map))
            {
                return false;
            }

            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cocoonCell, Rot4.North, new IntVec2(1, 1)))
            {
                if (IsOpenEggPlacementCell(adjacent, map, forPawn, blockedCell, ignorePawns))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsOpenEggPlacementCell(IntVec3 cell, Map map, Pawn forPawn = null, IntVec3 blockedCell = default, bool ignorePawns = false)
        {
            if (!cell.IsValid || cell == blockedCell || map == null || !cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map) != null)
            {
                return false;
            }

            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null || terrain == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            if (!ignorePawns && cell.GetFirstPawn(map) != null)
            {
                return false;
            }

            if (forPawn != null && !FeralJobUtility.IsPlaceAvailableForJobBy(forPawn, cell))
            {
                return false;
            }

            return true;
        }

        internal static bool CanPlaceNonOvomorphHiveUtilityAt(IntVec3 cell, Map map, Pawn forPawn = null)
        {
            if (!IsOpenEggPlacementCell(cell, map, forPawn))
            {
                return false;
            }

            if (IsCellValidCocoon(cell, map, fast: true))
            {
                return false;
            }

            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                if (adjacent.GetEdifice(map) is CocoonBase && !HasAdjacentOpenEggPlacementCell(adjacent, map, forPawn, cell, ignorePawns: true))
                {
                    return false;
                }

                if (IsCellValidCocoon(adjacent, map, fast: true) && !HasAdjacentOpenEggPlacementCell(adjacent, map, forPawn, cell, ignorePawns: true))
                {
                    return false;
                }
            }

            return true;
        }
        
        private static bool IsHiveTerrain(IntVec3 cell, Map map)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            return terrain == InternalDefOf.HiveFloor || terrain == InternalDefOf.HeavyHiveFloor;
        }

        private static bool IsHiveStructure(Building building)
        {
            if (building == null)
            {
                return false;
            }

            return building.def == XenoBuildingDefOf.Hivemass || building.def == XenoBuildingDefOf.HiveWebbing || building.def == XenoBuildingDefOf.HiveReinforcement;
        }

        private static bool IsSolidStructure(Building building)
        {
            if (building == null)
            {
                return false;
            }

            return building.def.passability == Traversability.Impassable;
        }

        public static bool IsLightSuitableAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }
            return map.glowGrid.GroundGlowAt(cell, true, true) < 0.5f;
        }
        public static float HiveLightSuitabilityAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - map.glowGrid.GroundGlowAt(cell, true, true));
        }

        internal static int HiveLightOffenseScore(Thing thing)
        {
            if (thing == null || thing.MapHeld == null)
            {
                return 0;
            }

            CompGlower glower = thing.TryGetComp<CompGlower>();
            if (glower == null || glower.GlowRadius <= 0f)
            {
                return 0;
            }

            float redBias = glower.GlowColor.r > 0.25f ? glower.GlowColor.r * 10f : 0f;
            return Mathf.CeilToInt(glower.GlowRadius * Mathf.Max(1f, redBias));
        }

        internal static int CryptimorphRoomShapeStage(Room room)
        {
            if (room == null || room.PsychologicallyOutdoors || room.CellCount <= 1)
            {
                return -1;
            }

            int borderCells = room.BorderCellsCardinal.Count();
            int sideCells = borderCells / 4;
            int squareCellCount = sideCells * sideCells;
            float difference = squareCellCount - room.CellCount;
            float ratioOfDifference = difference / room.CellCount;

            if (ratioOfDifference > 0.25f)
            {
                return 2;
            }

            if (ratioOfDifference > 0.10f)
            {
                return -1;
            }

            if (ratioOfDifference < 0.05f)
            {
                return 0;
            }

            return 1;
        }

        internal static int HiveUsedRoomScore(Room room)
        {
            if (room == null)
            {
                return 0;
            }

            int score = 0;
            score += room.ContainedThings<Ovomorph>().Count() * 20;
            score += room.ContainedThings<CocoonBase>().Count() * 25;
            score += room.ContainedThings<MeatballLarder>().Count() * 20;
            score += room.ContainedThings<Building>().Count(building => IsHiveStructure(building) || building.def == XenoBuildingDefOf.HiveSleepingSpot || building.def == XenoBuildingDefOf.AtmospherePylon) * 10;

            int sampled = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                if (sampled >= 40)
                {
                    break;
                }

                sampled++;
                if (IsHiveTerrain(cell, room.Map))
                {
                    score += 2;
                }
            }

            return score;
        }

        internal static void NotifyHiveRoomCompleted(Room room)
        {
            if (!IsHiveRoomTrackable(room))
            {
                return;
            }

            NestSite localNest = GetLocalNest(room.Map);
            if (localNest == null)
            {
                FindGoodNestSite(room.Map);
                localNest = GetLocalNest(room.Map);
            }

            AddOrUpdateTrackedHiveRoom(localNest, room);
        }

        private static bool TryDiscoverCurrentHiveRoom(Pawn pawn, NestSite localNest)
        {
            if (pawn == null || localNest == null || pawn.Map != localNest.map)
            {
                return false;
            }

            Room currentRoom = pawn.Position.GetRoomOrAdjacent(pawn.Map);
            if (!IsHiveRoomTrackable(currentRoom))
            {
                return false;
            }

            AddOrUpdateTrackedHiveRoom(localNest, currentRoom);
            return true;
        }

        private static void AddOrUpdateTrackedHiveRoom(NestSite localNest, Room room)
        {
            IntVec3 anchorCell = CalculateRoomAnchor(room);
            if (!anchorCell.IsValid)
            {
                return;
            }

            HiveRoomRecord record = localNest.HiveRooms.FirstOrDefault(existing => existing.room == room);
            if (record == null)
            {
                record = new HiveRoomRecord();
                localNest.HiveRooms.Add(record);
            }

            record.room = room;
            record.anchorCell = anchorCell;
            if (record.restExpansionAnchor.IsValid && !IsRestExpansionAnchorValidForRoom(localNest.map, record.room, record.restExpansionAnchor))
            {
                record.restExpansionAnchor = IntVec3.Invalid;
            }

            if (record.restExpansionAnchor.IsValid && record.restExpansionAnchor.GetRoomOrAdjacent(localNest.map) == room)
            {
                record.restExpansionAnchor = IntVec3.Invalid;
            }

            if (record.cocoonExpansionAnchor.IsValid && !IsExpansionAnchorValidForRoom(localNest.map, record.room, record.cocoonExpansionAnchor))
            {
                record.cocoonExpansionAnchor = IntVec3.Invalid;
            }

            if (record.cocoonExpansionAnchor.IsValid && record.cocoonExpansionAnchor.GetRoomOrAdjacent(localNest.map) == room)
            {
                record.cocoonExpansionAnchor = IntVec3.Invalid;
            }

            record.score = HiveUsedRoomScore(room);
            record.lastSeenTick = Find.TickManager.TicksGame;
        }

        private static void ValidateTrackedHiveRooms(NestSite localNest)
        {
            if (localNest == null)
            {
                return;
            }

            localNest.HiveRooms.RemoveAll(record => !IsTrackedHiveRoomValid(localNest, record));
        }

        private static bool IsTrackedHiveRoomValid(NestSite localNest, HiveRoomRecord record)
        {
            if (localNest == null || record == null || !IsHiveRoomTrackable(record.room))
            {
                return false;
            }

            if (record.room.Map != localNest.map || !record.anchorCell.IsValid || !record.anchorCell.InBounds(localNest.map))
            {
                return false;
            }

            Room anchorRoom = record.anchorCell.GetRoomOrAdjacent(localNest.map);
            if (anchorRoom != record.room)
            {
                return false;
            }

            if (record.restExpansionAnchor.IsValid && !IsRestExpansionAnchorValidForRoom(localNest.map, record.room, record.restExpansionAnchor))
            {
                record.restExpansionAnchor = IntVec3.Invalid;
            }

            if (record.cocoonExpansionAnchor.IsValid && !IsExpansionAnchorValidForRoom(localNest.map, record.room, record.cocoonExpansionAnchor))
            {
                record.cocoonExpansionAnchor = IntVec3.Invalid;
            }

            record.score = HiveUsedRoomScore(record.room);
            return true;
        }

        private static bool IsHiveRoomTrackable(Room room)
        {
            if (room == null || room.Map == null || room.IsDoorway || room.TouchesMapEdge || room.CellCount <= 0 || room.OpenRoofCount > 0)
            {
                return false;
            }

            int countedCells = 0;

            int goalCells = room.Cells.Count() -(room.Cells.Count() / 3);
            foreach (IntVec3 cell in room.Cells)
            {
                if (IsHiveTerrain(cell, room.Map))
                {
                    countedCells++;
                }

                foreach (Thing thing in cell.GetThingList(room.Map))
                {
                    if (thing is Building building &&
                        (IsHiveStructure(building) || building.def == XenoBuildingDefOf.HiveSleepingSpot || building.def == XenoBuildingDefOf.HiveSleepingCocoon))
                    {
                        return true;
                    }
                }

                if (countedCells >= goalCells)
                {
                    return true;
                }
            }

            return false;
        }

        private static IntVec3 CalculateRoomAnchor(Room room)
        {
            if (room == null || room.CellCount <= 0)
            {
                return IntVec3.Invalid;
            }

            float totalX = 0f;
            float totalZ = 0f;
            int count = 0;
            foreach (IntVec3 cell in room.Cells)
            {
                totalX += cell.x;
                totalZ += cell.z;
                count++;
            }

            if (count <= 0)
            {
                return IntVec3.Invalid;
            }

            float averageX = totalX / count;
            float averageZ = totalZ / count;
            IntVec3 bestCell = IntVec3.Invalid;
            float bestDistance = float.MaxValue;

            foreach (IntVec3 cell in room.Cells)
            {
                float x = cell.x - averageX;
                float z = cell.z - averageZ;
                float distance = x * x + z * z;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = cell;
                }
            }

            return bestCell.IsValid ? bestCell : room.Cells.RandomElement();
        }

        private static int HiveBuildStimulusScore(Map map, IntVec3 cell)
        {
            int ticks = Find.TickManager.TicksGame;
            BuildStimuli.RemoveAll(stimulus => stimulus.map == null || stimulus.expireTick <= ticks);

            int score = 0;
            foreach (HiveBuildStimulus stimulus in BuildStimuli)
            {
                if (stimulus.map != map)
                {
                    continue;
                }

                float distance = cell.DistanceTo(stimulus.center);
                if (distance > stimulus.radius)
                {
                    continue;
                }

                score += Mathf.CeilToInt(stimulus.strength * (1f - (distance / stimulus.radius)));
            }

            return score;
        }

        private static bool HasActiveHiveBuildStimulus(Map map)
        {
            int ticks = Find.TickManager.TicksGame;
            BuildStimuli.RemoveAll(stimulus => stimulus.map == null || stimulus.expireTick <= ticks);
            return BuildStimuli.Any(stimulus => stimulus.map == map);
        }

        private static List<IntVec3> SampleHiveCandidateCells(Pawn pawn, NestSite localNest, float pawnRadius, float nestRadius, int maxCells)
        {
            List<IntVec3> cells = new List<IntVec3>();
            if (localNest != null && localNest.Position.IsValid)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(localNest.Position, nestRadius, true))
                {
                    if (cell.InBounds(pawn.Map) && !cells.Contains(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, pawnRadius, true))
            {
                if (cell.InBounds(pawn.Map) && !cells.Contains(cell))
                {
                    cells.Add(cell);
                }
            }

            if (cells.Count > maxCells)
            {
                cells.RemoveRange(maxCells, cells.Count - maxCells);
            }

            return cells;
        }

        private static int HiveUsedCellScore(IntVec3 cell, Map map, NestSite localNest)
        {
            if (map == null || !cell.InBounds(map))
            {
                return 0;
            }

            int score = 0;
            if (localNest != null && localNest.Position.IsValid)
            {
                float distance = cell.DistanceTo(localNest.Position);
                if (distance < 28f)
                {
                    score += Mathf.CeilToInt(28f - distance);
                }
            }

            if (IsHiveTerrain(cell, map))
            {
                score += 18;
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                if (IsHiveTerrain(adjacent, map))
                {
                    score += 12;
                }

                Building edifice = adjacent.GetEdifice(map);
                if (IsSolidStructure(edifice))
                {
                    score += 18;
                }
                else if (edifice?.def == XenoBuildingDefOf.HiveWebbing)
                {
                    score += 3;
                }
                else if (edifice != null && edifice.def.holdsRoof)
                {
                    score += 6;
                }
            }

            return score;
        }

        private static bool HasOpenHiveFrontier(Map map, NestSite localNest)
        {
            return false;
        }

        private static bool IsAdjacentToRoofHolder(IntVec3 cell, Map map)
        {
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                Building edifice = adjacent.GetEdifice(map);
                if (edifice != null && edifice.def.holdsRoof)
                {
                    return true;
                }
            }

            return false;
        }
        private static bool IsLocallyHiveClaimed(IntVec3 cell, Map map, IntVec3 nestPosition)
        {
            if ((cell - nestPosition).LengthHorizontalSquared <= 144f)
            {
                return true;
            }

            if (IsHiveTerrain(cell, map))
            {
                return true;
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                if (IsHiveTerrain(adjacent, map) || IsHiveStructure(adjacent.GetEdifice(map)))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ActiveAwakeAdultNonQueenCount(Map map)
        {
            if (map == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Pawn pawn in GetHiveMembersOnMap(map))
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed || !pawn.DevelopmentalStage.Adult())
                {
                    continue;
                }

                if (XMTUtility.IsQueen(pawn) || pawn.CurJobDef == XenoWorkDefOf.XMT_Hibernate)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static int NearbyAmbushSpotCount(Map map, IntVec3 cell, float radius = 18f)
        {
            return GenRadial.RadialDistinctThingsAround(cell, map, radius, true).Count(thing => thing.def == XenoBuildingDefOf.XMT_AmbushSpot);
        }

        internal static Job GetSurplusHibernationJob(Pawn pawn)
        {

            if (pawn == null || pawn.Map == null || !pawn.DevelopmentalStage.Adult() || XMTUtility.IsQueen(pawn))
            {
                return null;
            }

            NestSite localNest = GetLocalNest(pawn.Map);
            if (HasActiveHiveBuildStimulus(pawn.Map) || HasOpenHiveFrontier(pawn.Map, localNest) || ShouldBuildNest(pawn.Map) || ShouldCoolNest(pawn.Map))
            {
                return null;
            }

            if (ActiveAwakeAdultNonQueenCount(pawn.Map) <= 10 || NearbyAmbushSpotCount(pawn.Map, pawn.Position, 20f) < 3)
            {
                return null;
            }

            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            List<IntVec3> cells = GenRadial.RadialCellsAround(pawn.Position, 10f, true).ToList();
            cells.Shuffle();
            if (cells.Count > MaxHiveBuildCandidateCells)
            {
                cells.RemoveRange(MaxHiveBuildCandidateCells, cells.Count - MaxHiveBuildCandidateCells);
            }

            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) || cell.GetEdifice(pawn.Map) != null || !FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                {
                    continue;
                }

                int score = Mathf.CeilToInt(HiveLightSuitabilityAt(cell, pawn.Map) * 50f);
                score += cell.Roofed(pawn.Map) ? 25 : 0;
                score += HiveUsedCellScore(cell, pawn.Map, localNest);
                score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell.IsValid ? JobMaker.MakeJob(XenoWorkDefOf.XMT_Hibernate, bestCell) : null;
        }

        internal static IntVec3 GetBestEggCellNearHost(Pawn host, Pawn forPawn = null, float radius = 2.5f)
        {
            if (!XMTUtility.IsHost(host))
            {
                return IntVec3.Invalid;
            }

            Map map = host.MapHeld;
            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            List<IntVec3> cells = GenAdj.CellsAdjacent8Way(host.PositionHeld, Rot4.North, new IntVec2(1, 1)).ToList();
            cells.Shuffle();
            NestSite localNest = GetLocalNest(map);

            foreach (IntVec3 cell in cells)
            {
                if (!IsOpenEggPlacementCell(cell, map, forPawn))
                {
                    continue;
                }

                int score = Mathf.CeilToInt(HiveLightSuitabilityAt(cell, map) * 50f);
                score += IsHiveTerrain(cell, map) ? 25 : 0;
                score += IsAdjacentToRoofHolder(cell, map) ? 20 : 0;
                score += HiveUsedCellScore(cell, map, localNest);
                score += cell.Roofed(map) ? 10 : 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        internal static IntVec3 GetBestAtmospherePylonCell(Pawn builder)
        {
            if (builder == null || builder.Map == null)
            {
                return IntVec3.Invalid;
            }

            NestSite localNest = GetLocalNest(builder.Map);
            Room lairRoom = localNest?.Position.GetRoomOrAdjacent(builder.Map);
            if (lairRoom == null)
            {
                return IntVec3.Invalid;
            }

            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            List<IntVec3> cells = lairRoom.Cells.ToList();
            cells.Shuffle();

            foreach (IntVec3 cell in cells)
            {
                if (!CanPlaceNonOvomorphHiveUtilityAt(cell, builder.Map, builder))
                {
                    continue;
                }

                int score = Mathf.CeilToInt(HiveLightSuitabilityAt(cell, builder.Map) * 60f);
                score += IsHiveTerrain(cell, builder.Map) ? 30 : 0;
                score += cell.Roofed(builder.Map) ? 20 : 0;
                score += AdjacentHiveTerrainCount(builder.Map, cell) * 8;
                score -= Mathf.CeilToInt(cell.DistanceTo(localNest.Position));
                score -= Mathf.CeilToInt(cell.DistanceTo(builder.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        private static int AdjacentHiveTerrainCount(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return 0;
            }

            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (adjacent.InBounds(map) && IsHiveTerrain(adjacent, map))
                {
                    count++;
                }
            }

            return count;
        }

        internal static Job GetHiveRestJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return null;
            }

            NestSite localNest = PrepareHiveRoomRecords(pawn);

            Job activeRestExpansionJob = TryGetActiveHiveRestExpansionJob(pawn, localNest);
            if (activeRestExpansionJob != null)
            {
                return activeRestExpansionJob;
            }

            if (localNest.HiveRooms.Count == 0)
            {
                Job nestBuildJob = GetHiveRestNestBuildJob(pawn, null);
                if (nestBuildJob != null)
                {
                    pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(localNest.Position), nestBuildJob);
                    ReportHiveRestFailure(pawn, "No tracked hive rooms available for rest; delegating to nest build job " + nestBuildJob.def.defName + " at " + nestBuildJob.targetA + ".");
                    return nestBuildJob;
                }

                ReportHiveRestFailure(pawn, "No tracked hive rooms available for rest.");
                return null;
            }

            List<HiveRoomRecord> restRooms = HiveRoomsForRest(pawn, localNest);
            if (restRooms.Count == 0)
            {
                ReportHiveRestFailure(pawn, "No reachable tracked hive room available for rest.");
                return null;
            }

            string restUnavailableReason = null;
            List<HiveRoomRecord> fullRestRooms = new List<HiveRoomRecord>();
            foreach (HiveRoomRecord room in restRooms)
            {
                Building bestSpot = BestHiveRestBuilding(pawn, room, localNest, out string roomUnavailableReason);
                if (bestSpot != null)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Wait_Asleep, bestSpot.Position);
                    FeralJobUtility.ReserveThingForJob(pawn, job, bestSpot);
                    return job;
                }

                if (roomUnavailableReason != null)
                {
                    restUnavailableReason = roomUnavailableReason;
                }
            }

            foreach (HiveRoomRecord room in restRooms)
            {
                if (RoomHasPendingHiveRestBuild(room.room))
                {
                    continue;
                }

                IntVec3 buildCell = BestHiveRestBuildCell(pawn, room, localNest);
                if (buildCell.IsValid)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, buildCell);
                    job.plantDefToSow = XenoBuildingDefOf.HiveSleepingSpot;
                    FeralJobUtility.ReservePlaceForJob(pawn, job, buildCell);
                    return job;
                }

                if (RoomHasHiveRestFurnitureOrPendingBuild(room.room))
                {
                    fullRestRooms.Add(room);
                }
            }

            if (fullRestRooms.Count == 0)
            {
                fullRestRooms.AddRange(restRooms);
            }

            Job fallbackNestBuildJob = GetHiveRestNestBuildJob(pawn, fullRestRooms);
            if (fallbackNestBuildJob != null)
            {
                pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(localNest.Position), fallbackNestBuildJob);
                ReportHiveRestFailure(pawn, "Tracked hive rest room is full; expanding from room edge with " + fallbackNestBuildJob.def.defName + " at " + fallbackNestBuildJob.targetA + ".");
                return fallbackNestBuildJob;
            }

            if (restUnavailableReason != null)
            {
                Job roomEntryJob = GetHiveRestRoomEntryJob(pawn, restRooms);
                if (roomEntryJob != null)
                {
                    ReportHiveRestFailure(pawn, "Tracked hive rooms are not ready for rest job; moving to room anchor at " + roomEntryJob.targetA + ". " + restUnavailableReason);
                    return roomEntryJob;
                }

                ReportHiveRestFailure(pawn, "Tracked hive rooms have rest furniture, but none is currently available. " + restUnavailableReason);
                return null;
            }

            Job fallbackEntryJob = GetHiveRestRoomEntryJob(pawn, restRooms);
            if (fallbackEntryJob != null)
            {
                ReportHiveRestFailure(pawn, "No rest furniture or rest build cell in tracked hive rooms; moving to room anchor at " + fallbackEntryJob.targetA + ".");
                return fallbackEntryJob;
            }

            ReportHiveRestFailure(pawn, "No rest furniture or rest build cell in tracked hive rooms.");
            return null;
        }

        private static NestSite PrepareHiveRoomRecords(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return null;
            }

            NestSite localNest = GetLocalNest(pawn.Map);
            if (localNest == null)
            {
                localNest = InitializeNest(pawn.Position, pawn.Map);
                Hive.Add(localNest);
            }

            ValidateTrackedHiveRooms(localNest);
            TryDiscoverCurrentHiveRoom(pawn, localNest);
            TryDiscoverNearbyHiveRooms(pawn, localNest);
            TryDiscoverHiveRoomsNearTrackedRooms(localNest);
            ValidateTrackedHiveRooms(localNest);
            return localNest;
        }

        private static bool TryDiscoverNearbyHiveRooms(Pawn pawn, NestSite localNest)
        {
            if (pawn == null || localNest == null || pawn.Map != localNest.map)
            {
                return false;
            }

            bool discovered = false;
            discovered |= TryDiscoverHiveRoomAt(pawn.Position, localNest);
            return discovered;
        }

        private static bool TryDiscoverHiveRoomsNearTrackedRooms(NestSite localNest)
        {
            if (localNest?.map == null || localNest.HiveRooms.Count == 0)
            {
                return false;
            }

            bool discovered = false;
            List<IntVec3> anchors = localNest.HiveRooms
                .Where(record => record != null)
                .SelectMany(record => new[] { record.anchorCell, record.restExpansionAnchor, record.cocoonExpansionAnchor })
                .Where(cell => cell.IsValid && cell.InBounds(localNest.map))
                .Distinct()
                .ToList();

            foreach (IntVec3 anchor in anchors)
            {
                discovered |= TryDiscoverHiveRoomAt(anchor, localNest);
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(anchor, 6f, true))
                {
                    if (cell.InBounds(localNest.map))
                    {
                        discovered |= TryDiscoverHiveRoomAt(cell, localNest);
                    }
                }
            }

            return discovered;
        }

        private static bool TryDiscoverHiveRoomAt(IntVec3 cell, NestSite localNest)
        {
            if (localNest == null || localNest.map == null || !cell.IsValid || !cell.InBounds(localNest.map))
            {
                return false;
            }

            Room room = cell.GetRoomOrAdjacent(localNest.map);
            if (!IsHiveRoomTrackable(room))
            {
                return false;
            }

            AddOrUpdateTrackedHiveRoom(localNest, room);
            return true;
        }

        private static List<HiveRoomRecord> HiveRoomsForRest(Pawn pawn, NestSite localNest)
        {
            List<HiveRoomRecord> rooms = HiveRoomsForUse(pawn, localNest);
            rooms.SortByDescending(record => HiveRestRoomScore(pawn, record));
            return rooms;
        }

        private static List<HiveRoomRecord> HiveRoomsForUse(Pawn pawn, NestSite localNest)
        {
            List<HiveRoomRecord> rooms = new List<HiveRoomRecord>();
            if (pawn == null || localNest == null)
            {
                return rooms;
            }

            foreach (HiveRoomRecord record in localNest.HiveRooms)
            {
                if (record?.room == null || !ClimbUtility.CanReachByWalkingOrClimb(pawn, record.anchorCell, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                rooms.Add(record);
            }

            return rooms;
        }

        internal static bool TryGetHiveCocoonCell(Pawn pawn, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn == null || pawn.Map == null)
            {
                return false;
            }

            NestSite localNest = PrepareHiveRoomRecords(pawn);
            List<HiveRoomRecord> rooms = HiveRoomsForUse(pawn, localNest);
            rooms.SortByDescending(record => HiveCocoonRoomScore(pawn, record));

            foreach (HiveRoomRecord room in rooms)
            {
                cell = BestHiveCocoonCell(pawn, room, localNest);
                if (cell.IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Job GetHiveCocoonExpansionJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return null;
            }

            NestSite localNest = PrepareHiveRoomRecords(pawn);
            if (localNest == null)
            {
                return null;
            }

            foreach (HiveRoomRecord room in localNest.HiveRooms)
            {
                Job activeExpansionJob = TryGetAnchoredHiveCocoonExpansionJob(pawn, room);
                if (activeExpansionJob != null)
                {
                    return activeExpansionJob;
                }
            }

            List<HiveRoomRecord> rooms = HiveRoomsForUse(pawn, localNest);
            rooms.SortByDescending(record => HiveCocoonRoomScore(pawn, record));

            foreach (HiveRoomRecord room in rooms)
            {
                IntVec3 expansionSeed = BestHiveCocoonExpansionSeed(pawn, room);
                if (!expansionSeed.IsValid)
                {
                    continue;
                }

                room.cocoonExpansionAnchor = expansionSeed;
                Job job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, expansionSeed);
                if (job != null)
                {
                    return job;
                }

                Job expansionJob = XMTNestBuildingUtility.TryGetHiveRoomExpansionBuildJob(pawn, room.room, expansionSeed);
                if (expansionJob != null)
                {
                    room.cocoonExpansionAnchor = expansionJob.targetA.Cell;
                    return expansionJob;
                }

                job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, room.cocoonExpansionAnchor);
                if (job != null)
                {
                    return job;
                }
            }

            if (rooms.Count > 0)
            {
                return null;
            }

            return XMTNestBuildingUtility.TryGetNestBuildJob(pawn, localNest.Position);
        }

        private static int HiveCocoonRoomScore(Pawn pawn, HiveRoomRecord record)
        {
            if (pawn == null || record == null || !record.anchorCell.IsValid)
            {
                return int.MinValue;
            }

            int score = record.score;
            score += (record.room.Temperature > 0) ? (record.room.Temperature < 30) ? 50 : -10 : -50;
            score += Mathf.CeilToInt(HiveLightSuitabilityAt(record.anchorCell, pawn.Map) * 45f);
            score += record.anchorCell.Roofed(pawn.Map) ? 25 : 0;
            score -= Mathf.CeilToInt(record.anchorCell.DistanceTo(pawn.Position));
            return score;
        }

        private static IntVec3 BestHiveCocoonCell(Pawn pawn, HiveRoomRecord record, NestSite localNest)
        {
            if (pawn == null || record?.room == null || pawn.Map == null)
            {
                return IntVec3.Invalid;
            }

            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            foreach (IntVec3 candidate in record.room.Cells)
            {
                if (!IsHiveCocoonCellAvailableFor(pawn, candidate))
                {
                    continue;
                }

                int score = HiveUsedCellScore(candidate, pawn.Map, localNest);
                score += Mathf.CeilToInt(HiveLightSuitabilityAt(candidate, pawn.Map) * 55f);
                score += candidate.Roofed(pawn.Map) ? 35 : 0;
                score -= Mathf.CeilToInt(candidate.DistanceTo(pawn.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = candidate;
                }
            }

            return bestCell;
        }

        private static bool IsHiveCocoonCellAvailableFor(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || !IsCellValidCocoon(cell, pawn.Map))
            {
                return false;
            }

            if (cell.GetFirstPawn(pawn.Map) != null)
            {
                return false;
            }

            if (!FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
            {
                return false;
            }

            if (!HasAdjacentOpenEggPlacementCell(cell, pawn.Map, pawn))
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly);
        }

        private static Job TryGetAnchoredHiveCocoonExpansionJob(Pawn pawn, HiveRoomRecord room)
        {
            if (pawn == null || pawn.Map == null || room == null || !IsExpansionAnchorValidForRoom(pawn.Map, room.room, room.cocoonExpansionAnchor))
            {
                if (room != null)
                {
                    room.cocoonExpansionAnchor = IntVec3.Invalid;
                }

                return null;
            }

            if (room.cocoonExpansionAnchor.GetRoomOrAdjacent(pawn.Map) != room.room && IsHiveRoomTrackable(room.cocoonExpansionAnchor.GetRoomOrAdjacent(pawn.Map)))
            {
                room.cocoonExpansionAnchor = IntVec3.Invalid;
                return null;
            }

            Job job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, room.cocoonExpansionAnchor);
            if (job != null)
            {
                return job;
            }

            Job expansionJob = XMTNestBuildingUtility.TryGetHiveRoomExpansionBuildJob(pawn, room.room, room.cocoonExpansionAnchor);
            if (expansionJob != null)
            {
                room.cocoonExpansionAnchor = expansionJob.targetA.Cell;
                return expansionJob;
            }

            return null;
        }

        private static int HiveRestRoomScore(Pawn pawn, HiveRoomRecord record)
        {
            if (pawn == null || record == null || !record.anchorCell.IsValid)
            {
                return int.MinValue;
            }

            int score = record.score;
            score += Mathf.CeilToInt(HiveLightSuitabilityAt(record.anchorCell, pawn.Map) * 35f);
            score += record.anchorCell.Roofed(pawn.Map) ? 20 : 0;
            score -= Mathf.CeilToInt(record.anchorCell.DistanceTo(pawn.Position));
            return score;
        }

        private static Building BestHiveRestBuilding(Pawn pawn, HiveRoomRecord record, NestSite localNest, out string unavailableReason)
        {
            Building bestSpot = null;
            int bestScore = int.MinValue;
            int checkedSpots = 0;
            int pendingSpots = 0;
            unavailableReason = "No final hive rest object was found in the room.";

            foreach (Building spot in HiveRestRoomBuildings(record.room))
            {
                if (checkedSpots >= MaxHiveRestCandidates)
                {
                    break;
                }

                if (IsPendingHiveRestBuilding(spot))
                {
                    pendingSpots++;
                    unavailableReason = "Found " + pendingSpots + " pending HiveSleepingSpotBuildable object(s), but no final HiveSleepingSpot/HiveSleepingCocoon.";
                    continue;
                }

                if (!IsHiveRestBuilding(spot))
                {
                    continue;
                }

                checkedSpots++;
                string rejectionReason = HiveRestBuildingUnavailableReason(pawn, spot);
                if (rejectionReason != null)
                {
                    unavailableReason = spot.def.defName + " at " + spot.Position + " rejected: " + rejectionReason;
                    continue;
                }

                int score = Mathf.CeilToInt(HiveLightSuitabilityAt(spot.Position, pawn.Map) * 50f);
                score += HiveUsedCellScore(spot.Position, pawn.Map, localNest);
                score -= Mathf.CeilToInt(spot.Position.DistanceTo(pawn.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = spot;
                }
            }

            return bestSpot;
        }

        private static IntVec3 BestHiveRestBuildCell(Pawn pawn, HiveRoomRecord record, NestSite localNest)
        {
            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;

            foreach (IntVec3 cell in record.room.Cells)
            {
                if (!CanBuildHiveRestSpotAt(pawn, cell))
                {
                    continue;
                }

                int score = Mathf.CeilToInt(HiveLightSuitabilityAt(cell, pawn.Map) * 50f);
                score += HiveUsedCellScore(cell, pawn.Map, localNest);
                score += cell.Roofed(pawn.Map) ? 25 : 0;
                score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        private static bool CanBuildHiveRestSpotAt(Pawn pawn, IntVec3 cell)
        {
            Map map = pawn.Map;
            if (!cell.InBounds(map) || !cell.Standable(map) || !IsHiveTerrain(cell, map))
            {
                return false;
            }

            if (cell.GetEdifice(map) != null || cell.GetFirstBuilding(map) != null)
            {
                return false;
            }

            if (!FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
            {
                return false;
            }

            if (HasAdjacentHiveRestBuilding(map, cell))
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly);
        }

        private static bool HasAdjacentHiveRestBuilding(Map map, IntVec3 cell)
        {
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacent8Way(cell, Rot4.North, new IntVec2(1, 1)))
            {
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                foreach (Thing thing in adjacent.GetThingList(map))
                {
                    if (thing is Building building && (IsHiveRestBuilding(building) || IsPendingHiveRestBuilding(building)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsHiveRestBuilding(Building building)
        {
            return building != null &&
                   (building.def == XenoBuildingDefOf.HiveSleepingSpot || building.def == XenoBuildingDefOf.HiveSleepingCocoon);
        }

        private static bool IsPendingHiveRestBuilding(Building building)
        {
            return building != null && building.def == XenoBuildingDefOf.HiveSleepingSpotBuildable;
        }

        private static bool RoomHasHiveRestFurnitureOrPendingBuild(Room room)
        {
            if (room == null)
            {
                return false;
            }

            foreach (Building building in HiveRestRoomBuildings(room))
            {
                if (IsHiveRestBuilding(building) || IsPendingHiveRestBuilding(building))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RoomHasPendingHiveRestBuild(Room room)
        {
            if (room == null)
            {
                return false;
            }

            foreach (Building building in HiveRestRoomBuildings(room))
            {
                if (IsPendingHiveRestBuilding(building))
                {
                    return true;
                }
            }

            return false;
        }

        private static Job GetHiveRestNestBuildJob(Pawn pawn, List<HiveRoomRecord> restRooms)
        {
            if (pawn == null || pawn.Map == null || !pawn.ageTracker.Adult)
            {
                return null;
            }

            if (restRooms != null)
            {
                foreach (HiveRoomRecord room in restRooms)
                {
                    IntVec3 expansionSeed = BestHiveRestExpansionSeed(pawn, room);
                    if (expansionSeed.IsValid)
                    {
                        room.restExpansionAnchor = expansionSeed;
                        Job job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, expansionSeed);
                        if (job != null)
                        {
                            return job;
                        }

                        Job expansionJob = XMTNestBuildingUtility.TryGetHiveRoomExpansionBuildJob(pawn, room.room, expansionSeed);
                        if (expansionJob != null)
                        {
                            room.restExpansionAnchor = expansionJob.targetA.Cell;
                            return expansionJob;
                        }

                        job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, room.restExpansionAnchor);
                        if (job != null)
                        {
                            return job;
                        }
                    }
                }
            }

            if (restRooms != null && restRooms.Count > 0)
            {
                return null;
            }

            NestSite localNest = GetLocalNest(pawn.Map);
            return localNest != null ? XMTNestBuildingUtility.TryGetNestBuildJob(pawn, localNest.Position) : null;
        }

        private static Job GetHiveRestRoomEntryJob(Pawn pawn, List<HiveRoomRecord> restRooms)
        {
            if (pawn == null || pawn.Map == null || restRooms == null)
            {
                return null;
            }

            HiveRoomRecord bestRoom = null;
            int bestScore = int.MinValue;
            foreach (HiveRoomRecord room in restRooms)
            {
                if (room == null || !room.anchorCell.IsValid || !room.anchorCell.InBounds(pawn.Map))
                {
                    continue;
                }

                if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, room.anchorCell, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                int score = room.anchorCell.Roofed(pawn.Map) ? 30 : 0;
                score += Mathf.CeilToInt(HiveLightSuitabilityAt(room.anchorCell, pawn.Map) * 40f);
                score -= Mathf.CeilToInt(room.anchorCell.DistanceTo(pawn.Position));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = room;
                }
            }

            return bestRoom != null ? JobMaker.MakeJob(JobDefOf.Goto, bestRoom.anchorCell) : null;
        }

        private static Job TryGetActiveHiveRestExpansionJob(Pawn pawn, NestSite localNest)
        {
            if (pawn == null || pawn.Map == null || localNest == null || localNest.map != pawn.Map || !pawn.ageTracker.Adult)
            {
                return null;
            }

            foreach (HiveRoomRecord room in localNest.HiveRooms)
            {
                Job activeExpansionJob = TryGetAnchoredHiveRestExpansionJob(pawn, room);
                if (activeExpansionJob != null)
                {
                    ReportHiveRestFailure(pawn, "Continuing active hive rest room expansion from seed " + room.restExpansionAnchor + " with " + activeExpansionJob.def.defName + " at " + activeExpansionJob.targetA + ".");
                    return activeExpansionJob;
                }
            }

            return null;
        }

        private static Job TryGetAnchoredHiveRestExpansionJob(Pawn pawn, HiveRoomRecord room)
        {
            if (pawn == null || pawn.Map == null || room == null || !IsRestExpansionAnchorValidForRoom(pawn.Map, room.room, room.restExpansionAnchor))
            {
                if (room != null)
                {
                    room.restExpansionAnchor = IntVec3.Invalid;
                }

                return null;
            }

            if (room.restExpansionAnchor.GetRoomOrAdjacent(pawn.Map) != room.room && IsHiveRoomTrackable(room.restExpansionAnchor.GetRoomOrAdjacent(pawn.Map)))
            {
                room.restExpansionAnchor = IntVec3.Invalid;
                return null;
            }

            Job job = XMTNestBuildingUtility.TryGetNestBuildJobFromExpansionSeed(pawn, room.restExpansionAnchor);
            if (job != null)
            {
                return job;
            }

            Job expansionJob = XMTNestBuildingUtility.TryGetHiveRoomExpansionBuildJob(pawn, room.room, room.restExpansionAnchor);
            if (expansionJob != null)
            {
                room.restExpansionAnchor = expansionJob.targetA.Cell;
                return expansionJob;
            }

            return null;
        }

        private static bool IsRestExpansionAnchorValidForRoom(Map map, Room room, IntVec3 anchor)
        {
            return IsExpansionAnchorValidForRoom(map, room, anchor);
        }

        private static bool IsExpansionAnchorValidForRoom(Map map, Room room, IntVec3 anchor)
        {
            if (map == null || room == null || !anchor.IsValid || !anchor.InBounds(map) || anchor.GetTerrain(map) == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            if (anchor.GetRoomOrAdjacent(map) == room)
            {
                return true;
            }

            foreach (IntVec3 roomCell in room.Cells)
            {
                if (roomCell.DistanceToSquared(anchor) <= 144f)
                {
                    return true;
                }
            }

            return false;
        }

        private static IntVec3 BestHiveRestExpansionSeed(Pawn pawn, HiveRoomRecord record)
        {
            if (pawn == null || record?.room == null || pawn.Map == null)
            {
                return IntVec3.Invalid;
            }

            Map map = pawn.Map;
            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            foreach (IntVec3 roomCell in record.room.Cells)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    for (int distance = 4; distance <= 8; distance++)
                    {
                        IntVec3 cell = roomCell + (direction * distance);
                        if (!IsHiveRestExpansionSeedCell(map, cell) || cell.GetRoomOrAdjacent(map) == record.room)
                        {
                            continue;
                        }

                        int targetDistance = Mathf.Abs(distance - 6);
                        int score = Rand.RangeInclusive(0, 12);
                        score += (8 - targetDistance) * 20;
                        score += Mathf.CeilToInt(HiveLightSuitabilityAt(cell, map) * 30f);
                        score += AdjacentHiveFloorCountForRestExpansion(map, cell) * 10;
                        score += AdjacentHiveStructureCountForRestExpansion(map, cell) * 10;
                        score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position) * 2f);
                        score -= Mathf.CeilToInt(cell.DistanceTo(record.anchorCell));

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestCell = cell;
                        }
                    }
                }
            }

            return bestCell;
        }

        private static IntVec3 BestHiveCocoonExpansionSeed(Pawn pawn, HiveRoomRecord record)
        {
            if (pawn == null || record?.room == null || pawn.Map == null)
            {
                return IntVec3.Invalid;
            }

            Map map = pawn.Map;
            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            foreach (IntVec3 roomCell in record.room.Cells)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    for (int distance = 8; distance <= 14; distance++)
                    {
                        IntVec3 cell = roomCell + (direction * distance);
                        if (!IsHiveRestExpansionSeedCell(map, cell) || cell.GetRoomOrAdjacent(map) == record.room)
                        {
                            continue;
                        }

                        int viableCells = ViableCocoonExpansionClusterCells(map, cell);
                        if (viableCells < 14)
                        {
                            continue;
                        }

                        int targetDistance = Mathf.Abs(distance - 11);
                        int adjacentHive = AdjacentHiveFloorCountForRestExpansion(map, cell);
                        int adjacentStructure = AdjacentHiveStructureCountForRestExpansion(map, cell);
                        int score = Rand.RangeInclusive(0, 12);
                        score += viableCells * 6;
                        score += (14 - targetDistance) * 14;
                        score += Mathf.CeilToInt(HiveLightSuitabilityAt(cell, map) * 45f);
                        score += cell.Roofed(map) ? 20 : 0;
                        score -= adjacentHive * 18;
                        score -= adjacentStructure * 10;
                        score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position));
                        score -= Mathf.CeilToInt(cell.DistanceTo(record.anchorCell) * 0.5f);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestCell = cell;
                        }
                    }
                }
            }

            return bestCell.IsValid ? bestCell : BestHiveRestExpansionSeed(pawn, record);
        }

        private static int ViableCocoonExpansionClusterCells(Map map, IntVec3 center)
        {
            int count = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 3.9f, true))
            {
                if (IsHiveRestExpansionSeedCell(map, cell))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsHiveRestExpansionSeedCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map) || !cell.Standable(map) || IsHiveTerrain(cell, map))
            {
                return false;
            }

            if (cell.GetTerrain(map) == ExternalDefOf.EmptySpace)
            {
                return false;
            }

            Building edifice = cell.GetEdifice(map);
            if (edifice != null && edifice.def.passability == Traversability.Impassable)
            {
                return false;
            }

            return true;
        }

        private static int AdjacentHiveFloorCountForRestExpansion(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (adjacent.InBounds(map) && IsHiveTerrain(adjacent, map))
                {
                    count++;
                }
            }

            return count;
        }

        private static int AdjacentHiveStructureCountForRestExpansion(Map map, IntVec3 cell)
        {
            int count = 0;
            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 adjacent = cell + direction;
                if (adjacent.InBounds(map) && IsHiveStructure(adjacent.GetEdifice(map)))
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerable<Building> HiveRestRoomBuildings(Room room)
        {
            if (room == null)
            {
                yield break;
            }

            HashSet<Building> yielded = new HashSet<Building>();
            foreach (Building building in room.ContainedThings<Building>())
            {
                if (building != null && yielded.Add(building))
                {
                    yield return building;
                }
            }

            foreach (IntVec3 cell in room.Cells)
            {
                foreach (Thing thing in cell.GetThingList(room.Map))
                {
                    if (thing is Building building && yielded.Add(building))
                    {
                        yield return building;
                    }
                }
            }
        }

        private static bool IsHiveRestBuildingAvailableFor(Pawn pawn, Building spot)
        {
            return HiveRestBuildingUnavailableReason(pawn, spot) == null;
        }

        private static string HiveRestBuildingUnavailableReason(Pawn pawn, Building spot)
        {
            if (pawn == null)
            {
                return "no pawn.";
            }

            if (spot == null)
            {
                return "no spot.";
            }

            if (pawn.MapHeld == null)
            {
                return "pawn has no held map.";
            }

            if (!spot.Spawned)
            {
                return "spot is not spawned.";
            }

            if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, spot, PathEndMode.OnCell, Danger.Deadly))
            {
                return "spot is not climb-reachable.";
            }

            if (ForbidUtility.CaresAboutForbidden(pawn, false) && (spot.IsForbidden(pawn) || !spot.Position.InAllowedArea(pawn)))
            {
                return "spot is forbidden or outside allowed area.";
            }

            if (pawn.MapHeld.physicalInteractionReservationManager.IsReserved(spot) &&
                !pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, spot))
            {
                return "spot has a physical interaction reservation by another pawn.";
            }

            if (pawn.Faction != null && !pawn.CanReserve(spot))
            {
                return "spot cannot be reserved by this pawn.";
            }

            return null;
        }

        private static void ReportHiveRestFailure(Pawn pawn, string reason)
        {
            if (XMTSettings.LogJobGiver)
            {
                Log.Message(pawn + " failed HiveRestJob with Reason: " + reason);
            }
        }

        internal static Job GetHiveWanderJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return null;
            }

            NestSite localNest = GetLocalNest(pawn.Map);
            if (localNest == null)
            {
                return null;
            }

            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MinValue;
            List<IntVec3> cells = SampleHiveCandidateCells(pawn, localNest, 10f, 10f, MaxHiveWanderCandidateCells);

            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) || !FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
                {
                    continue;
                }

                Room room = cell.GetRoomOrAdjacent(pawn.Map);
                int score = HiveUsedCellScore(cell, pawn.Map, localNest);
                score += IsLocallyHiveClaimed(cell, pawn.Map, localNest.Position) ? 30 : 0;
                score += room != null && (room.OutdoorsForWork || room.PsychologicallyOutdoors) ? 10 : 0;
                score += HiveBuildStimulusScore(pawn.Map, cell);
                score += Mathf.CeilToInt(HiveLightSuitabilityAt(cell, pawn.Map) * 15f);
                score -= Mathf.CeilToInt(cell.DistanceTo(pawn.Position));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            if (!bestCell.IsValid || bestCell == pawn.Position)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, bestCell);
            FeralJobUtility.ReservePlaceForJob(pawn, job, bestCell);
            return job;
        }

        protected static List<Job> GenerateNestBuildJobs(Map map, NestSite localNest)
        {
            return new List<Job>();
        }

        public static Job GetNestBuildJob(Pawn builder)
        {
            if (builder == null || builder.Map == null)
            {
                return null;
            }

            NestSite localNest = GetLocalNest(builder.Map);
            if (localNest == null)
            {
                return null;
            }

            return XMTNestBuildingUtility.TryGetNestBuildJob(builder, localNest.Position);
        }

        public static IntVec3 GetValidCocoonCell(Map map, Pawn forPawn = null)
        {
            if (forPawn != null && TryGetHiveCocoonCell(forPawn, out IntVec3 hiveRoomCell))
            {
                return hiveRoomCell;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message(forPawn + " found no valid hive room cell for cocoon.");
            }

            return IntVec3.Invalid;
        }
        public static Building TryPlaceCocoonBase(IntVec3 startingPosition, Pawn target, float radius = 1.5f)
        {
            if(target == null)
            {
                return null;
            }
            ThingDef newThingDef = target.IsAnimal? XenoBuildingDefOf.XMT_CocoonBaseAnimal : XenoBuildingDefOf.XMT_CocoonBase;

            IntVec3 AvailableCell = startingPosition;
            Map map = target.MapHeld;

            Building occupying = AvailableCell.GetEdifice(map);
            if (occupying != null)
            {
                occupying.Kill();
            }
            TryPlaceResinFloor(AvailableCell, map);
            CocoonBase building = GenSpawn.Spawn(newThingDef, AvailableCell, map, WipeMode.FullRefund) as CocoonBase;

            building.SetFaction(target.Faction);
            building.Rotation = Rot4.South;
            return building;
        }
   
        public static void RegisterNestMap( Map map)
        {
            FindGoodNestSite(map);
        }

        public static void DeregisterNestMap(Map map)
        {
            Hive.RemoveWhere(x=>x.map == map);
        }

        public static int EvaluateNestSite(Pawn pawn)
        {
            return EvaluateNestSite(pawn.Position,pawn.Map);
        }
        public static int EvaluateNestSite(IntVec3 site, Map map)
        {
            if(!site.IsValid || !site.InBounds(map))
            {
                return -100;
            }

            int score = Mathf.CeilToInt(HiveLightSuitabilityAt(site, map) * 100f);
            score = site.Roofed(map) ? score : 0;
            Room space = site.GetRoomOrAdjacent(map);

            if (space == null)
            {
                return -100;
            }

            IEnumerable<Ovomorph> Ovomorphs = space.ContainedThings<Ovomorph>();
            score += Ovomorphs.Count()*10;
            IEnumerable<CocoonBase> cocoons = space.ContainedThings<CocoonBase>();
            score += cocoons.Count()*20;

            score -= (space.OpenRoofCount * 5);

            score += space.PsychologicallyOutdoors ? 0 : 25;

            return score;
        }
        private static NestSite InitializeNest(IntVec3 site, Map map)
        {
   
            NestSite tempSite = new NestSite();
            tempSite.Position = site;
            tempSite.Suitability = EvaluateNestSite(site, map);
           
            tempSite.map = map;

            Room space = site.GetRoomOrAdjacent(map);

            return tempSite;
        }
        public static void ChestburstBirth(Pawn child, Pawn mother)
        {
            //TODO: If chestburst happens off the map of the mother's nest and player birther have baby return.
            //TODO: If chestburst happens off the map of the mother's nest and not player birther begin world hive event shenanigans.

            if (mother == null)
            {

                /*Pawn eldestMother = null;
                int eldestAge = 0;
               
                
                if (lair.owner != null && lair.owner.ageTracker != null)
                {
                    int age = lair.owner.ageTracker.AgeBiologicalYears;
                    if (eldestAge < age)
                    {
                        eldestAge = age;
                        eldestMother = lair.owner;
                    }
                }
               

                if (eldestMother != null)
                {
                   
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, eldestMother);
                    child.SetFaction(eldestMother.Faction);
                   
                } */
            }
        }

        internal static void ForceNestPosition(IntVec3 position, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if(localNest == null)
            {
                Hive.Add(InitializeNest(position, map));
            }
            else
            {
                localNest.Position = position;
            }
        }
        internal static IntVec3 GetNestPosition(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if(localNest == null)
            {
                HiveMapComponent hiveComponent = map.GetComponent<HiveMapComponent>();

                FindGoodNestSite(map);
                
                localNest = GetLocalNest(map);
            }

            return localNest.Position;
        }

        internal static bool NestOnMap(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return false;
            }

            return true;
        }
        internal static bool NoNestOnMap(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return true;
            }

            return false;
        }

        internal static Ovomorph GetOvomorph(Map map , bool requireReady = true, Pawn forPawn = null)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null || localNest.Ovomorphs.Count == 0)
            {
                return null;
            }

            foreach (Ovomorph ova in localNest.Ovomorphs)
            {
                if(!ova.Spawned)
                {
                    continue;
                }

                if(forPawn != null)
                {
                   if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn,ova))
                    {
                        continue;
                    }

                }

                if (requireReady)
                {
                    if (ova.Ready)
                    {
                        IEnumerable<Pawn> PossibleHosts = GenRadial.RadialDistinctThingsAround(ova.Position, map, 1.5f, true).OfType<Pawn>()
                            .Where(x => XMTUtility.IsHost(x));

                        if (PossibleHosts.Any())
                        {
                            continue;
                        }

                        if (map.reservationManager.IsReserved(ova))
                        {
                            continue;
                        }

                        return ova;
                    }
                }
                else
                {
                    if(ova.Unhatched)
                    {
                        return ova;
                    }
                }
            }

            return null;
        }

        internal static Pawn GetHost(Map map, Pawn forPawn = null)
        {
            NestSite localNest = GetLocalNest(map);
            PruneInvalidAvailableHosts(localNest);
            if (localNest == null || localNest.AvailableHosts.Count == 0)
            {
                return null;
            }
            if (localNest.AvailableHosts.Any())
            {
                foreach (Pawn hostCandidate in localNest.AvailableHosts)
                {
                    if (!XMTUtility.IsHost(hostCandidate))
                    {
                        continue;
                    }

                    if(forPawn != null)
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn,hostCandidate))
                        {
                            continue;
                        }
                    }
             

                    return hostCandidate;
                }
            }

            return null;
        }

        internal static bool HaveEggs(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return false;
            }
            return localNest.Ovomorphs.Count() > 0;
        }
        internal static bool NeedEggs(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            if (localNest == null)
            {
                return false;
            }

            PruneInvalidAvailableHosts(localNest);
            return localNest.NeedEggs;
        }

        internal static bool NeedAbductions(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            if (localNest == null)
            {
                return false;
            }

            PruneInvalidAvailableHosts(localNest);
            return localNest.NeedEggs || localNest.NeedHosts || localNest.TotalCocooned == 0;
        }

        internal static bool NeedAbductions(Pawn pawn)
        {
            NestSite localNest = PrepareHiveRoomRecords(pawn);
            if (localNest == null)
            {
                return false;
            }

            PruneInvalidAvailableHosts(localNest);
            return localNest.NeedEggs || localNest.NeedHosts || localNest.TotalCocooned == 0;
        }

        internal static bool IsMorphingCandidate(Pawn candidate)
        {
            if (XMTUtility.IsXenomorphFriendly(candidate))
            {
                return false;
            }

            if (XMTUtility.HasEmbryo(candidate))
            {
                return false;
            }

            if (XMTUtility.IsMorphing(candidate))
            {
                return false;
            }

            if (XMTUtility.IsInorganic(candidate))
            {
                return false;
            }

            if (XMTUtility.IsXenomorph(candidate))
            {
                return false;
            }
            return true;
        }
        internal static Pawn GetOvomorphingCandidate(Map map, Pawn forPawn = null)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                return null;
            }

            foreach (Pawn candidate in localNest.Cocooned)
            {
                if (forPawn != null)
                {
                    if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn,candidate))
                    {
                        continue;
                    }
                }

                if (IsMorphingCandidate(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        public static Pawn GetAbductionCandidate(Map map)
        {

            return null;
        }

        internal static MeatballLarder GetMostPrunableLarder(Map map, Pawn pawn)
        {
            MeatballLarder larder = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                foreach (MeatballLarder candidate in CurrentNest.MeatballThings)
                {
                    if(!FeralJobUtility.IsThingAvailableForJobBy(pawn, candidate))
                    {
                        continue;
                    }

                    if (candidate.CanBePruned())
                    {
                        int Score = candidate.HitPoints;
                        if (Score > BestScore)
                        {
                            BestScore = Score;
                            larder = candidate;

                        }
                    }
                }
            }

            return larder;
        }

        internal static Pawn GetHungriestHivemate(Map map, Pawn forPawn = null)
        {
            Pawn hungryCandidate = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                float lowestFoodNeed = float.MaxValue;

                foreach (Pawn candidate in CurrentNest.HiveMates)
                {
                    if (forPawn != null)
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn,candidate))
                        {
                            continue;
                        }
                    }

                    if (candidate.needs == null)
                    {
                        continue;
                    }

                    Need_Food food = candidate.needs.food;
                    int Score = 0;

                    if (food == null)
                    {
                        continue;
                    }

                    if (food.CurCategory == HungerCategory.Fed)
                    {
                        continue;
                    }

                    if (food.CurLevelPercentage < lowestFoodNeed)
                    {
                        Score += Mathf.CeilToInt((1 - food.CurLevelPercentage) * 100);
                    }

                    if (!candidate.DevelopmentalStage.Adult())
                    {
                        Score += 40;
                        if (food.CurLevelPercentage < 0.9f)
                        {
                            Score += 60;
                        }
                    }

                    if (Score > BestScore)
                    {
                        BestScore = Score;
                        hungryCandidate = candidate;
                        lowestFoodNeed = food.CurLevelPercentage;
                    }
                }
            }

            return hungryCandidate;
        }

        internal static Pawn GetBestLarderCandidate(Map map, Pawn forPawn = null)
        {
            if (map == null)
            {
                return null;
            }

            Pawn bestLarderCandidate = null;
            int BestScore = int.MinValue;
           
            if (forPawn.Faction != null)
            {
                if(map.designationManager.AnySpawnedDesignationOfDef(XenoWorkDefOf.XMT_Larder))
                {
                    IEnumerable<Designation> larderTargets = map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_Larder);

                    foreach(Designation designation in larderTargets)
                    {
                        if(designation.target == null)
                        {
                            continue;
                        }

                        Pawn candidate = designation.target.Pawn;

                        if (!FeralJobUtility.IsThingAvailableForJobBy(forPawn, candidate))
                        {
                            continue;
                        }

                        if(!IsMorphingCandidate(candidate))
                        {
                            continue;
                        }

                        int Score = 0;
                        if (XMTUtility.IsHost(candidate))
                        {
                            Score -= 20;
                        }

                        Score += Mathf.CeilToInt(candidate.BodySize * 10);

                        if (Score > BestScore)
                        {
                            bestLarderCandidate = candidate;
                            BestScore = Score;
                        }
                    }

                }

                return bestLarderCandidate;
            }

            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
               
                foreach (Pawn candidate in CurrentNest.Cocooned)
                {
                    if (forPawn != null)
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn, candidate))
                        {
                            continue;
                        }
                    }

                    if (!IsMorphingCandidate(candidate))
                    {
                        continue;
                    }

                    int Score = 0;
                    if (XMTUtility.IsHost(candidate))
                    {
                        Score -= 20;
                    }

                    Score += Mathf.CeilToInt(candidate.BodySize * 10);

                    if (Score > BestScore)
                    {
                        bestLarderCandidate = candidate;
                        BestScore = Score;
                    }
                }
            }

            return bestLarderCandidate;
        }
        internal static Pawn GetHungriestCocooned(Map map, Pawn forPawn = null)
        {
            Pawn hungryCandidate = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                float lowestFoodNeed = float.MaxValue;

                if (XMTSettings.LogJobGiver)
                {
                    Log.Message(forPawn + " is searching " + CurrentNest.Cocooned.Count + " cocooned candidates for feeding.");
                }
                foreach (Pawn candidate in CurrentNest.Cocooned)
                {
                    if(candidate.needs == null)
                    {
                        continue;
                    }

                    if (forPawn != null)
                    {
                        if(!FeralJobUtility.IsThingAvailableForJobBy(forPawn,candidate))
                        {
                            continue;
                        }
                    }

                    Need_Food food = candidate.needs.food;
                    int Score = 0;

                    if (food == null)
                    {
                        continue;
                    }

                    if(food.CurCategory == HungerCategory.Fed)
                    {
                        continue;
                    }

                    if (food.CurLevelPercentage < lowestFoodNeed)
                    {
                        Score += Mathf.CeilToInt((1-food.CurLevelPercentage)*100);
                    }
                   

                    if (candidate.RaceProps != null)
                    {
                        if (candidate.RaceProps.Humanlike)
                        {
                            Score += 10;
                        }
                    }

                    if (XMTUtility.HasEmbryo(candidate))
                    {
                        Score += 50;
                    }

                    if (XMTUtility.IsMorphing(candidate))
                    {
                        Score += 25;
                    }

                    if (candidate.Faction != null && candidate.Faction.IsPlayer)
                    {
                        Score += 25;
                    }

                    if (Score > BestScore)
                    {
                        BestScore = Score;
                        hungryCandidate = candidate;
                        lowestFoodNeed = food.CurLevelPercentage;
                    }
                }
            }

            return hungryCandidate;
        }

        internal static void AddOvomorph(Ovomorph Ovomorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if(localNest.Ovomorphs.Contains(Ovomorph))
            {
                return;
            }
            
            if (Ovomorph.Unhatched)
            {
                localNest.Ovomorphs.Add(Ovomorph);
            }

        }

        internal static void RemoveOvomorph(Ovomorph Ovomorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }

            localNest.Ovomorphs.Remove(Ovomorph);
            
        }

        internal static void AddImplantedHosts(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if (!localNest.ImplantedHosts.Contains(pawn))
            {
                localNest.ImplantedHosts.Add(pawn);
            }
            localNest.AvailableHosts.Remove(pawn);
            
        }

        internal static void RemoveImplantedHosts(Pawn host, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }
            localNest.ImplantedHosts.Remove(host);
          

        }

        internal static void AddOvomorphing(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if (localNest.OvomorphingPawns.Contains(pawn))
            {
                return;
            }
            localNest.OvomorphingPawns.Add(pawn);
        }

        internal static void RemoveOvomorphing(Pawn host, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }
            localNest.OvomorphingPawns.Remove(host);
        }

        internal static void RemoveHost(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }
            localNest.AvailableHosts.Remove(pawn);

            
        }

        internal static IntVec3 GetClearNestCell(Map map, Pawn forPawn = null)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

           Room nestRoom = localNest.Position.GetRoomOrAdjacent(map);

            foreach (IntVec3 cell in nestRoom.Cells)
            {
                
                if (forPawn != null && ForbidUtility.CaresAboutForbidden(forPawn, false))
                {
                    if (!FeralJobUtility.IsPlaceAvailableForJobBy(forPawn, cell))
                    {
                        continue;
                    }
                }

                if (cell.GetEdifice(map) == null)
                {
                    return cell;
                }
            }

            return IntVec3.Invalid;
        }

        public static TargetInfo GetNestSpot(Map map)
        {
            IntVec3 nestSite = GetNestPosition(map);
            return nestSite.GetFirstBuilding(map);
        }

        internal static void AddGeneOvomorph(GeneOvomorph geneOvomorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            if (localNest.GeneOvomorphs.Contains(geneOvomorph))
            {
                return;
            }
            localNest.GeneOvomorphs.Add(geneOvomorph);
            
        }

        internal static void RemoveGeneOvomorph(GeneOvomorph geneOvomorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }

            localNest.GeneOvomorphs.Remove(geneOvomorph);
        }

        public static bool XenosOnMap(Map localMap)
        {
            if (localMap == null)
            {
                return false;
            }

            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return false;
            }

            if (localNest.TotalHiveMates <= 0)
            {
                return false;
            }
            return true;
        }

        public static List<Pawn> GetHiveMembersOnMap(Map localMap)
        {
            List<Pawn> returnList = new List<Pawn>();

            if (localMap == null)
            {
                return returnList;
            }

            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return returnList;
            }
            localNest.HiveMates.CopyToList(returnList);

            return returnList;
        }
        public static bool PlayerXenosOnMap(Map localMap)
        {
            if (XMTUtility.QueenIsPlayer())
            {
                return true;
            }

            if (Faction.OfPlayer.def.defName == InternalDefOf.XMT_PlayerHive.defName)
            {
                return true;
            }

            if (localMap == null)
            {
                return false;
            }

            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return false;
            }

            if(localNest.TotalHiveMates <= 0)
            {
                return false;
            }

            foreach(Pawn hiveMate in localNest.HiveMates)
            {
                if(hiveMate.Faction != null)
                {
                    if(hiveMate.GuestStatus == GuestStatus.Slave || hiveMate.GuestStatus == GuestStatus.Prisoner)
                    {
                        continue;
                    }

                    if(!hiveMate.GetMorphComp().Integrated)
                    {
                        continue;
                    }

                    if(hiveMate.Faction.IsPlayer)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void PlayerJoinXenomorphs(Map localMap)
        {
            if (localMap == null)
            {
                return;
            }
            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return;
            }

            bool rebellion = false;

            List<Pawn> rebellingPawns = new List<Pawn>();
            foreach (Pawn colonist in localMap.mapPawns.FreeColonists)
            {
                if (colonist.Dead)
                {
                    continue;
                }

                if (XMTUtility.IsXenomorph(colonist))
                {
                    continue;
                }

                CompPawnInfo info = colonist.Info();
                if (info != null)
                {
                    if (!info.IsObsessed())
                    {
                        if (colonist.WorkTagIsDisabled(WorkTags.Violent))
                        {
                            colonist.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "", forced: true, forceWake: true, false);
                            continue;
                        }
                        
                        rebellion = true;
                        rebellingPawns.Add(colonist);
                        colonist.SetFaction(Faction.OfAncientsHostile);
                    }
                }
            }

            foreach(Pawn animal in localMap.mapPawns.ColonyAnimals)
            {
                if (animal.Dead)
                {
                    continue;
                }

                if (XMTUtility.IsXenomorph(animal))
                {
                    continue;
                }

                if (animal.Info() is CompPawnInfo info)
                {
                    if (info.IsObsessed())
                    {
                        continue;
                    }
                }

                if (rebellion)
                {
                    animal.SetFaction(Faction.OfAncientsHostile);
                }
                else
                {
                    animal.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee, "", forced: true, forceWake: true, false);
                }
            }

            if (localNest.TotalHiveMates > 0)
            {
                foreach (Pawn hiveMate in localNest.HiveMates)
                {
                    if (XenoformingUtility.IsQueenAidDefender(hiveMate))
                    {
                        continue;
                    }
                    hiveMate.SetFaction(Faction.OfPlayer);
                }
            }

            if (!XMTUtility.QueenIsPlayer())
            {
                string hivetext = "XMT_HiveRisesLetterDescription".Translate();
                Find.LetterStack.ReceiveLetter("XMT_HiveRisesLetterTitle".Translate(), hivetext, LetterDefOf.NeutralEvent);
            }

            if (rebellion)
            {
                RCellFinder.TryFindRandomExitSpot(rebellingPawns[0], out var spot, TraverseMode.PassDoors);

                if (!PrisonBreakUtility.TryFindGroupUpLoc(rebellingPawns, spot, out var groupUpLoc))
                {
                    groupUpLoc = localMap.Center;
                }

                string rebeltext = "XMT_HumanRebellionLetterDescription".Translate();
                Find.LetterStack.ReceiveLetter("XMT_HumanRebellionLetterTitle".Translate(), rebeltext, LetterDefOf.NegativeEvent, new LookTargets(rebellingPawns));

                LordMaker.MakeNewLord(rebellingPawns[0].Faction, new LordJob_HumanRebellion(groupUpLoc, spot, rebellingPawns[0].thingIDNumber), localMap, rebellingPawns);

                for (int m = 0; m < rebellingPawns.Count; m++)
                {
                    if (!rebellingPawns[m].Awake())
                    {
                        RestUtility.WakeUp(rebellingPawns[m]);
                    }

                    if (rebellingPawns[m].drafter != null)
                    {
                        rebellingPawns[m].drafter.Drafted = false;
                    }

                    if (rebellingPawns[m].CurJob != null)
                    {
                        rebellingPawns[m].jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }

                    if (rebellingPawns[m].equipment.Primary == null)
                    {
                        Log.Message(rebellingPawns[m] + " has no weapon");

                        Thing weapon = XMTUtility.SearchRegionForUnreservedWeapon(rebellingPawns[m]);
                        Job job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
                        rebellingPawns[m].Reserve(weapon, job);

                        rebellingPawns[m].jobs.StartJob(job);

                    }


                    rebellingPawns[m].Map.attackTargetsCache.UpdateTarget(rebellingPawns[m]);

                    if (rebellingPawns[m].carryTracker.CarriedThing != null)
                    {
                        rebellingPawns[m].carryTracker.TryDropCarriedThing(rebellingPawns[m].Position, ThingPlaceMode.Near, out var _);
                    }
                }
            }

            Log.Message(" assigning faction def");
            Faction.OfPlayer.def = InternalDefOf.XMT_PlayerHive;
        }

        internal static int Population(Map localMap)
        {

            if (localMap == null)
            {
                return 0;
            }
            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return 0;
            }

            return localNest.TotalHiveMates;
        }

        internal static bool HasCocooned(Map localMap)
        {

            if (localMap == null)
            {
                return false;
            }
            NestSite localNest = GetLocalNest(localMap);

            if (localNest == null)
            {
                return false;
            }

            return localNest.Cocooned.Count > 0;
        }
    }
}
