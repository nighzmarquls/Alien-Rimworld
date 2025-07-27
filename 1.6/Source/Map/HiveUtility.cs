using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.Scripting.GarbageCollector;

namespace Xenomorphtype
{
    public class HiveUtility
    {
        public const float HiveHungerCostPerTick = 0.000085f;
        protected class NestSite
        {
            public int Suitability;
            public IntVec3 Position;
            public Map map;

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

            protected List<Ovamorph> ovamorphs;
            public  List<Ovamorph> Ovamorphs
            {
                get
                {
                    if (ovamorphs == null)
                    {
                        ovamorphs = new List<Ovamorph>();
                    }
                    return ovamorphs;
                }
            }

            protected List<GeneOvamorph> geneOvamorphs;
            public List<GeneOvamorph> GeneOvamorphs
            {
                get
                {
                    if (geneOvamorphs == null)
                    {
                        geneOvamorphs = new List<GeneOvamorph>();
                    }
                    return geneOvamorphs;
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

            protected List<Pawn> ovamorphingPawns;

            public List<Pawn> OvamorphingPawns
            {
                get
                {
                    if (ovamorphingPawns == null)
                    {
                        ovamorphingPawns = new List<Pawn>();
                    }
                    return ovamorphingPawns;
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

            public int EggCount => Ovamorphs.Count + OvamorphingPawns.Count;

            public bool HaveHosts => AvailableHosts.Count > 0;

            public bool HaveEggs => Ovamorphs.Count > 0;

            public bool NeedEggs => XMTUtility.NoQueenPresent()? EggCount <= AvailableHosts.Count : false;

            public bool NeedHosts => EggCount > AvailableHosts.Count;

            public int TotalHiveMates => HiveMates.Count;

            public int TotalCocooned => Cocooned.Count;
        }

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

        public static int TotalHivePopulation(Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return 0;
            }

            return localNest.TotalHiveMates;
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

            if(localNest.HaveEggs && localNest.HaveHosts)
            {
                return true;
            }

            if(ShouldCoolNest(map))
            {
                return true;
            }

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
            }
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
                                if (food.CurCategory == HungerCategory.UrgentlyHungry || (food.CurCategory == HungerCategory.Starving))
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
            Room lairRoom = localNest.Position.GetRoomOrAdjacent(map);

            if(lairRoom == null)
            {
                return true;
            }

            if(lairRoom.OutdoorsForWork)
            {
                return true;
            }

            return false;
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

            result.AddRange(localNest.Ovamorphs);
            result.AddRange(localNest.GeneOvamorphs);

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
                float currentOffense = 0f;
                Building_Turret turret = building as Building_Turret;
                if (turret != null)
                {
                    currentOffense += 50;
                }

                CompGlower glower = building.GetComp<CompGlower>();
                if (glower != null)
                {
                    currentOffense += glower.GlowRadius * (glower.GlowColor.r * 10);
                }

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
                    lairSpot = plant.Position;
                    break;
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
                    lairSpot = Geyser.Position;
                    break;
                }
            }

            Hive.Add(InitializeNest(lairSpot, map));
            if (lairSpot.InBounds(map))
            {
                GenSpawn.Spawn(InternalDefOf.XMT_HiddenNestSpot, lairSpot, map, WipeMode.VanishOrMoveAside);
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
            map.terrainGrid.SetTerrain(cell, InternalDefOf.HiveFloor);
        }

        private static bool IsCellValidCocoon(IntVec3 cell, Map map)
        {
            if(cell == IntVec3.Invalid)
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

            Room space = cell.GetRoomOrAdjacent(map);

            IEnumerable<Ovamorph> ovamorphs = space.ContainedThings<Ovamorph>();
            IEnumerable<CocoonBase> cocoonBases = space.ContainedThings<CocoonBase>();


            if(ovamorphs != null && ovamorphs.Any())
            {
                
                foreach (Ovamorph ovamorph in ovamorphs)
                {
                    if(cell == ovamorph.Position)
                    {
                        return false;
                    }
                    if(cell.AdjacentToCardinal(ovamorph.Position))
                    {
                        return false;
                    }
                }
            }

            if(cocoonBases != null && cocoonBases.Any())
            {

                foreach(CocoonBase cocoonBase in cocoonBases)
                {
                    if(cell == cocoonBase.Position)
                    {
                        return false;
                    }

                    if(cell.AdjacentToCardinal(cocoonBase.Position))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        public static Building TryPlaceHiveMassSupport(IntVec3 startingPosition, Pawn owner )
        {
            if (owner == null)
            {
                return null;
            }
            ThingDef newThingDef = InternalDefOf.Hivemass;

            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(startingPosition, 1.5f, false);

            Building support = null;
            IntVec3 BestCell = cells.First(); 
            foreach (IntVec3 cell in cells)
            {
                IEnumerable<Building> things = cell.GetThingList(owner.MapHeld).OfType<Building>();

                if(things.Any())
                {
                    foreach(Building thing in things)
                    {
                        if(thing.def.holdsRoof)
                        {
                            support = thing;
                            break;
                        }
                    }
                }
                else
                {
                    BestCell = cell;
                }
            }

            if(support == null)
            {
                support = GenSpawn.Spawn(newThingDef, BestCell, owner.MapHeld, WipeMode.FullRefund) as Building_Bed; ;
            }

            if (owner.Map.roofGrid.RoofAt(startingPosition) != RoofDefOf.RoofRockThick)
            {
                owner.Map.roofGrid.SetRoof(startingPosition, RoofDefOf.RoofRockThin);
            }

            return support;
        }

        protected static List<Job> GenerateNestBuildJobs(Map map, NestSite localNest)
        {
            List<Job> jobs = new List<Job>();
            float Radius = Rand.Range(4,8);

            IntVec3 NWcorner = localNest.Position + IntVec3.FromVector3( (Vector3.forward + Vector3.left )* Radius);

            IntVec3 SEcorner = localNest.Position + IntVec3.FromVector3((Vector3.back + Vector3.right) * Radius);

            IntVec3[] Circle;

            Room nestRoom = localNest.Position.GetRoomOrAdjacent(map);

            if (nestRoom.CellCount > 1000 || nestRoom == null)
            {
                //Log.Message("attempting to Generate Nest Circle");
                if (NWcorner.InBounds(map) && SEcorner.InBounds(map))
                {
                    //Log.Message("corners are in bounds");
                    Circle = GenRadial.RadialCellsAround(localNest.Position, Radius - 1, Radius).ToArray();
                    ThingDef wallDef = InternalDefOf.Hivemass;
                    ThingDef doorDef = InternalDefOf.HiveWebbing;
                    int divisionCount = Circle.Length / Rand.Range(3,8);
                    int countTilDoor = 0;
                    foreach (IntVec3 cell in Circle)
                    {
                        
                        if (countTilDoor > 0)
                        {
                            if (cell.GetEdifice(map) == null)
                            {
                                countTilDoor--;
                                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, cell);
                                job.plantDefToSow = wallDef;
                                jobs.Add(job);
                            }
                        }
                        else
                        {
                            countTilDoor = divisionCount;
                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HiveBuilding, cell);
                            job.plantDefToSow = doorDef;
                            jobs.Add(job);
                        }
                    }
                    Circle = GenRadial.RadialCellsAround(localNest.Position,Radius,true).ToArray();
                }
                else
                {
                    //Log.Message("circle not in bounds");
                }
            }
            else
            {
                foreach(IntVec3 cell in nestRoom.Cells)
                {
                    if(map.roofGrid.Roofed(cell))
                    {
                        continue;
                    }
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThin);
                }
            }
            

            return jobs;
        }

        public static Job GetNestBuildJob(Pawn builder)
        {
            Job job = null;

            if(!XMTUtility.IsXenomorph(builder))
            {
                return job;
            }
            Map map = builder.Map;

            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            if(localNest.BuildJobs.Count() == 0)
            {
                localNest.BuildJobs = GenerateNestBuildJobs(map, localNest);
            }
            if (localNest.BuildJobs.Count() > 0)
            {
                job = localNest.BuildJobs.First();
                localNest.BuildJobs.Remove(job);
            }
            return job;
        }
        public static Building TryPlaceCocoonBase(IntVec3 startingPosition, Pawn target, float radius = 6f)
        {
            if(target == null)
            {
                return null;
            }
            ThingDef newThingDef = target.IsAnimal? InternalDefOf.XMT_CocoonBaseAnimal : InternalDefOf.XMT_CocoonBase;

            IntVec3 AvailableCell = startingPosition;
            Map map = target.MapHeld;

            if(!IsCellValidCocoon(AvailableCell, map))
            {
                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(startingPosition, radius, false);
                foreach(IntVec3 cell in cells)
                {
                    if(IsCellValidCocoon(cell, map))
                    {
                        AvailableCell = cell;
                        break;
                    }
                }

            }
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

            int score = Mathf.CeilToInt((0 - map.glowGrid.GroundGlowAt(site,true,true))*100);
            score = site.Roofed(map) ? score : 0;
            Room space = site.GetRoomOrAdjacent(map);

            if (space == null)
            {
                return -100;
            }

            IEnumerable<Ovamorph> ovamorphs = space.ContainedThings<Ovamorph>();
            score += ovamorphs.Count()*10;
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

        internal static Ovamorph GetOvamorph(Map map , bool requireReady = true)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null || localNest.Ovamorphs.Count == 0)
            {
                return null;
            }

            foreach (Ovamorph ova in localNest.Ovamorphs)
            {
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

        internal static Pawn GetHost(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null || localNest.AvailableHosts.Count == 0)
            {
                return null;
            }
            if (localNest.AvailableHosts.Any())
            {
                foreach (Pawn hostCandidate in localNest.AvailableHosts)
                {
                    if(map.reservationManager.IsReserved(hostCandidate))
                    {
                        continue;
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
            return localNest.Ovamorphs.Count() > 0;
        }
        internal static bool NeedEggs(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

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
        internal static Pawn GetOvamorphCandidate(Map map)
        {
            NestSite localNest = GetLocalNest(map);
            foreach (Pawn candidate in localNest.Cocooned)
            {
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
                    if(candidate.IsForbidden(pawn))
                    {
                        continue;
                    }

                    if (map.reservationManager.IsReserved(candidate))
                    {
                        continue;
                    }

                    if (candidate.CanBePruned())
                    {
                        int Score = candidate.HitPoints;
                        if (Score > BestScore)
                        {
                            Score = BestScore;
                            larder = candidate;

                        }
                    }
                }
            }

            return larder;
        }

        internal static Pawn GetHungriestHivemate(Map map)
        {
            Pawn hungryCandidate = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                float lowestFoodNeed = float.MaxValue;
                foreach (Pawn candidate in CurrentNest.HiveMates)
                {
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

                    if (Score > BestScore)
                    {
                        Score = BestScore;
                        hungryCandidate = candidate;
                        lowestFoodNeed = food.CurLevelPercentage;
                    }
                }
            }

            return hungryCandidate;
        }

        internal static Pawn GetBestLarderCandidate(Map map)
        {
            Pawn bestLarderCandidate = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                foreach (Pawn candidate in CurrentNest.Cocooned)
                {
                    if(XMTUtility.IsInorganic(candidate))
                    {
                        continue;
                    }

                    if(XMTUtility.NotPrey(candidate))
                    {
                        continue;
                    }

                    if (map.reservationManager.IsReserved(candidate))
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
        internal static Pawn GetHungriestCocooned(Map map)
        {
            Pawn hungryCandidate = null;
            int BestScore = int.MinValue;
            NestSite CurrentNest = GetLocalNest(map);
            if (CurrentNest != null)
            {
                float lowestFoodNeed = float.MaxValue;
                foreach (Pawn candidate in CurrentNest.Cocooned)
                {
                    if(candidate.needs == null)
                    {
                        continue;
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
                        Score = BestScore;
                        hungryCandidate = candidate;
                        lowestFoodNeed = food.CurLevelPercentage;
                    }
                }
            }

            return hungryCandidate;
        }

        internal static void AddOvamorph(Ovamorph ovamorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if(localNest.Ovamorphs.Contains(ovamorph))
            {
                return;
            }
            
            if (ovamorph.Unhatched)
            {
                localNest.Ovamorphs.Add(ovamorph);
            }

        }

        internal static void RemoveOvamorph(Ovamorph ovamorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }

            localNest.Ovamorphs.Remove(ovamorph);
            
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

        internal static void AddOvamorphing(Pawn pawn, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }
            if (localNest.OvamorphingPawns.Contains(pawn))
            {
                return;
            }
            localNest.OvamorphingPawns.Add(pawn);
        }

        internal static void RemoveOvamorphing(Pawn host, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }
            localNest.OvamorphingPawns.Remove(host);
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

        internal static IntVec3 GetClearNestCell(Map map)
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
                if(cell.GetEdifice(map) == null)
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

        internal static void AddGeneOvamorph(GeneOvamorph geneOvamorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                FindGoodNestSite(map);
                localNest = GetLocalNest(map);
            }

            if (localNest.GeneOvamorphs.Contains(geneOvamorph))
            {
                return;
            }
            localNest.GeneOvamorphs.Add(geneOvamorph);
            
        }

        internal static void RemoveGeneOvamorph(GeneOvamorph geneOvamorph, Map map)
        {
            NestSite localNest = GetLocalNest(map);

            if (localNest == null)
            {
                return;
            }

            localNest.GeneOvamorphs.Remove(geneOvamorph);
        }

        public static bool PlayerXenosOnMap(Map localMap)
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

            if(localNest.TotalHiveMates <= 0)
            {
                return false;
            }

            foreach(Pawn hiveMate in localNest.HiveMates)
            {
                if(hiveMate.Faction != null)
                {
                    if(hiveMate.Faction.IsPlayer)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
