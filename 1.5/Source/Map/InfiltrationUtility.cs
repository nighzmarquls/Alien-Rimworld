using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Noise;
using static UnityEngine.Scripting.GarbageCollector;

namespace Xenomorphtype
{
    public class InfiltrationUtility
    {

        public static String DubsFixtureName = "BasedFixture";

        protected static float MaxSqueezeSize = 1.0f;
        private static bool IsGoodEscape(IntVec3 test)
        {
            return true;
        }
        public static bool IsCellTrapped(IntVec3 Cell, Map map,TraverseMode traverseMode = TraverseMode.PassDoors, Danger maxDanger = Danger.None)
        {
            if (!Cell.IsValid)
            {
                return false;
            }

            IntVec3 StartCell = Cell;
            IntVec3 EscapeCell;

            RCellFinder.TryFindEdgeCellFromPositionAvoidingColony(StartCell, map, IsGoodEscape, out EscapeCell);

            return !map.reachability.CanReach(StartCell, EscapeCell, PathEndMode.ClosestTouch, traverseMode, maxDanger);
        }
        protected enum EndPointType
        {
            Vent = 0,
            VEPipes = 1,
            DubPlumbing,
            Rimatomics,
            Rimafeller,
            Max
        }
        protected struct InfiltrationEndPoint
        {
            public Building building;
            public List<Room> connectedRooms;
            public EndPointType type;
        }
        protected struct InfiltrationNetwork
        {
            public List<InfiltrationEndPoint> endpoints;
            public List<Room> connectedRooms;
            public List<Building> connectedBuildings;
            public EndPointType type;
        }
        protected class InfiltrationCache
        {
            public List<InfiltrationNetwork> networks = new List<InfiltrationNetwork>();
            public int lastColonyBuildingCount = 0;
        }

        protected static Dictionary<Map, InfiltrationCache> cache = new Dictionary<Map, InfiltrationCache>();

        public static IntVec3 GetGoalOnOrAdjacentToFrom(IntVec3 start, Building building)
        {
            IntVec3 goal = building.Position;
            Map map = building.Map;
            PathEndMode peMode = PathEndMode.OnCell;
            TraverseMode traverseMode = TraverseMode.NoPassClosedDoors;
            Danger danger = Danger.Deadly;
            bool reachable = map.reachability.CanReach(start, building, peMode, traverseMode, danger);

            if (!reachable)
            {
                CellRect cellRect = building.OccupiedRect();


                IEnumerable<IntVec3> Edges = cellRect.EdgeCells;
                foreach (IntVec3 cell in Edges)
                {
                    reachable = map.reachability.CanReach(start, cell, peMode, traverseMode, danger);
                    if (reachable)
                    {
                        return cell;
                    }
                }

                IEnumerable<IntVec3> Adjacent = cellRect.AdjacentCells;

                foreach (IntVec3 cell in Adjacent)
                {
                    reachable = map.reachability.CanReach(start, cell, peMode, traverseMode, danger);
                    if (reachable)
                    {
                        return cell;
                    }
                }

            }
            return goal;
        }
        protected static bool CheckReachable(Map map, IntVec3 start, Building building, PathEndMode peMode, TraverseMode traverseMode)
        {
            bool reachable = map.reachability.CanReach(start, building, peMode, traverseMode, Danger.Deadly);
            PawnPath path = PawnPath.NotFound;
            if (!reachable)
            {
                path = map.pathFinder.FindPath(start, building, TraverseParms.For(traverseMode), peMode);
                reachable = path != PawnPath.NotFound;
                path.Dispose();

            }
            return reachable;
        }
        public static bool GetInfiltrationExit(Building Entry, IntVec3 goalCell, out Building Exit)
        {
            Exit = null;

            Map map = Entry.Map;

            if (!cache.ContainsKey(map))
            {
                Log.Error("How did you do this?! You have fucked up the order! Get Valid Entry Then Get Exit!");
                return false;
            }

            InfiltrationCache infiltrationCache = cache[map];

            foreach (InfiltrationNetwork network in infiltrationCache.networks)
            {
                if(network.connectedBuildings.Contains(Entry))
                {
                    foreach (InfiltrationEndPoint endpoint in network.endpoints)
                    {
                        
                        bool Reachable = CheckReachable(map, goalCell, endpoint.building, PathEndMode.ClosestTouch, TraverseMode.NoPassClosedDoors);
                        if (Reachable)
                        {
                            Exit = endpoint.building;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool GetInfiltrationEntry(Map map, IntVec3 startCell, IntVec3 goalCell, out Building Entry)
        {
            Entry = null;
            int ColonyBuildingCount = map.listerBuildings.allBuildingsColonist.Count;

            Room goalRoom = goalCell.GetRoom(map);
            Room startRoom = startCell.GetRoom(map);

            if (goalRoom == null)
            {
                return false;
            }

            if(goalRoom.OpenRoofCount > 0 && (startRoom != null && startRoom.OpenRoofCount > 0))
            {
                return false;
            }

            if (!cache.ContainsKey(map))
            {
                cache.Add(map, new InfiltrationCache());
            }

            InfiltrationCache infiltrationCache = cache[map];

            if (infiltrationCache.lastColonyBuildingCount == ColonyBuildingCount)
            {
                foreach (InfiltrationNetwork network in infiltrationCache.networks)
                {
                    bool pointsDirty = false;
                    InfiltrationNetwork workingNetwork = network;
                    List<InfiltrationEndPoint> dirtyPoints = new List<InfiltrationEndPoint>();
                    if (network.connectedRooms.Contains(goalRoom))
                    {
                        foreach (InfiltrationEndPoint endpoint in network.endpoints)
                        {
                            if (!endpoint.building.Spawned)
                            {
                                pointsDirty = true;

                                dirtyPoints.Add(endpoint);
                            }
                            bool Reachable = CheckReachable(map, startCell, endpoint.building, PathEndMode.ClosestTouch, TraverseMode.NoPassClosedDoors);
                            if (Reachable)
                            {
                                Entry = endpoint.building;
                                break;
                            }
                        }
                    }
                    else if (network.connectedRooms.Contains(startRoom))
                    {
                        foreach (InfiltrationEndPoint endpoint in network.endpoints)
                        {
                            if (!endpoint.building.Spawned)
                            {
                                pointsDirty = true;

                                dirtyPoints.Add(endpoint);
                            }
                            bool Reachable = CheckReachable(map, startCell, endpoint.building, PathEndMode.ClosestTouch, TraverseMode.NoPassClosedDoors);
                            if (Reachable)
                            {
                                Entry = endpoint.building;
                                break;
                            }
                        }
                    }

                    if (pointsDirty)
                    {
                        foreach (InfiltrationEndPoint endpoint in dirtyPoints)
                        {
                            RemoveEndpointFromNetwork(endpoint, ref workingNetwork);
                        }
                    }
                    if (Entry != null)
                    {
                        break;
                    }

                }
                if (Entry != null)
                {
                    return true;
                }
            }
            else
            {
                infiltrationCache.networks.Clear();
            }

            infiltrationCache.lastColonyBuildingCount = ColonyBuildingCount;
            if (GenerateInfiltrationNetworks(map, goalRoom, ref infiltrationCache))
            {
                foreach (InfiltrationNetwork network in infiltrationCache.networks)
                {
                    if (network.connectedRooms.Contains(goalRoom))
                    {
                        foreach (InfiltrationEndPoint endpoint in network.endpoints)
                        {

                            bool Reachable = CheckReachable(map, startCell, endpoint.building, PathEndMode.ClosestTouch, TraverseMode.NoPassClosedDoors);
                            if (Reachable)
                            {
                                Entry = endpoint.building;
                                return true;
                            }
                        }
                    }
                    else if(network.connectedRooms.Contains(startRoom))
                    {
                        foreach (InfiltrationEndPoint endpoint in network.endpoints)
                        {
                            bool Reachable = CheckReachable(map, startCell, endpoint.building, PathEndMode.ClosestTouch, TraverseMode.NoPassClosedDoors);
                            if (Reachable)
                            {
                                Entry = endpoint.building;
                                break;
                            }
                        }
                    }
                }
            }
            Log.Message("No Valid Entry");
            return false;
        }

        protected static bool IsAtomicEntry(ThingDef thing)
        {
            if (thing == null)
            {
                return false;
            }

            if (thing == ExternalDefOf.CoolingTower)
            {
                return true;
            }

            if (thing == ExternalDefOf.CoolingWater)
            {
                return true;
            }

            if (thing == ExternalDefOf.ReactorCoreA)
            {
                return true;
            }

            if (thing == ExternalDefOf.ReactorCoreB)
            {
                return true;
            }

            if (thing == ExternalDefOf.ReactorCoreC)
            {
                return true;
            }

            if (thing == ExternalDefOf.Turbine)
            {
                return true;
            }

            if (thing == ExternalDefOf.BigTurbine)
            {
                return true;
            }

            if (thing == ExternalDefOf.CoolingRadiator)
            {
                return true;
            }

            return false;
        }
        protected static bool IsPlumbingEntry(ThingDef thing)
        {
            if (thing == null)
            {
                return false;
            }

            if (thing == ExternalDefOf.BasinStuff)
            {
                return true;
            }

            if (thing == ExternalDefOf.KitchenSink)
            {
                return true;
            }

            if (thing == ExternalDefOf.ToiletStuff)
            {
                return true;
            }

            if (thing == ExternalDefOf.BathtubStuff)
            {
                return true;
            }

            if (thing == ExternalDefOf.WaterTowerS)
            {
                return true;
            }

            if (thing == ExternalDefOf.WaterTowerL)
            {
                return true;
            }

            if (thing == ExternalDefOf.SewageOutlet)
            {
                return true;
            }

            if (thing == ExternalDefOf.SewageTreatment)
            {
                return true;
            }

            if (thing == ExternalDefOf.SewageSepticTank)
            {
                return true;
            }

            if (thing == ExternalDefOf.PitLatrine)
            {
                return true;
            }

            if (thing == ExternalDefOf.DBHSwimmingPool)
            {
                return true;
            }

            if (thing == ExternalDefOf.HotTub)
            {
                return true;
            }

            if (thing == ExternalDefOf.WaterTrough)
            {
                return true;
            }

            return false;
        }
        protected static bool GenerateInfiltrationNetworks(Map map, Room room, ref InfiltrationCache cache)
        {
            IEnumerable<Building> buildings = room.ContainedAndAdjacentThings.OfType<Building>();
           
            bool foundNetwork = false;
            foreach (Building building in buildings)
            {
                if (building == null)
                {
                    continue;
                }

                if (building is Building_Pipe || building.def == ExternalDefOf.sewagePipeStuff)
                {
                    continue;
                }

                if (building is Building_Vent or Building_Cooler)
                {
                    InfiltrationNetwork newNetwork = new InfiltrationNetwork();
                    newNetwork.connectedRooms = new List<Room>();
                    newNetwork.connectedBuildings = new List<Building>();
                    newNetwork.endpoints = new List<InfiltrationEndPoint>();
                    newNetwork.connectedRooms.Add(room);
                    newNetwork.type = EndPointType.Vent;
                    bool skip = false;
                    foreach (InfiltrationNetwork network in cache.networks)
                    {
                        if (network.connectedBuildings.Contains(building))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        break;
                    }
                    InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = building, type = EndPointType.Vent };

                    AddAllEndpointsToNetwork(map, infiltrationEndPoint, ref newNetwork);
                    cache.networks.Add(newNetwork);
                    foundNetwork = true;
                    continue;
                }

                if (building.HasComp<CompResource>())
                {
                    InfiltrationNetwork newNetwork = new InfiltrationNetwork();
                    newNetwork.connectedRooms = new List<Room>();
                    newNetwork.connectedBuildings = new List<Building>();
                    newNetwork.endpoints = new List<InfiltrationEndPoint>();
                    newNetwork.type = EndPointType.VEPipes;
                    bool skip = false;
                    foreach (InfiltrationNetwork network in cache.networks)
                    {
                        if(network.connectedBuildings.Contains(building))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if(skip)
                    {
                        break;
                    }
                    newNetwork.connectedRooms.Add(room);
                    InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = building, type = EndPointType.VEPipes };

                    AddAllEndpointsToNetwork(map,infiltrationEndPoint,ref newNetwork);
                    cache.networks.Add(newNetwork);

                    foundNetwork = true;
                    continue;
                }

                
        
                if (IsPlumbingEntry(building.def))
                {
                    InfiltrationNetwork newNetwork = new InfiltrationNetwork();
                    newNetwork.connectedRooms = new List<Room>();
                    newNetwork.connectedBuildings = new List<Building>();
                    newNetwork.endpoints = new List<InfiltrationEndPoint>();
                    newNetwork.type = EndPointType.DubPlumbing;
                    bool skip = false;
                    foreach (InfiltrationNetwork network in cache.networks)
                    {
                        if (network.connectedBuildings.Contains(building))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        break;
                    }
                    newNetwork.connectedRooms.Add(room);
                    InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = building, type = EndPointType.DubPlumbing };

                    AddAllEndpointsToNetwork(map, infiltrationEndPoint, ref newNetwork);
                    cache.networks.Add(newNetwork);

                    foundNetwork = true;
                    continue;
                }

                if (IsAtomicEntry(building.def))
                {
                    Log.Message("Found Atomic Entry");
                    InfiltrationNetwork newNetwork = new InfiltrationNetwork();
                    newNetwork.connectedRooms = new List<Room>();
                    newNetwork.connectedBuildings = new List<Building>();
                    newNetwork.endpoints = new List<InfiltrationEndPoint>();
                    newNetwork.type = EndPointType.Rimatomics;
                    bool skip = false;
                    foreach (InfiltrationNetwork network in cache.networks)
                    {
                        if (network.connectedBuildings.Contains(building))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        break;
                    }
                    newNetwork.connectedRooms.Add(room);
                    InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = building, type = EndPointType.Rimatomics };

                    AddAllEndpointsToNetwork(map, infiltrationEndPoint, ref newNetwork);
                    cache.networks.Add(newNetwork);

                    foundNetwork = true;
                    continue;
                }

            }
            return foundNetwork;
        }

        protected static void AddAllEndpointsToNetwork(Map map, InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            switch (endPoint.type)
            {
                case EndPointType.Vent:
                    AddEndpointToNetwork(map, endPoint, ref network);
                    return;
                case EndPointType.VEPipes:
                    AddAllVEPipePointsToNetwork(map, endPoint, ref network);
                    return;
                case EndPointType.DubPlumbing:
                    AddAllBadHygeinePointsToNetwork(map, endPoint, ref network);
                    return;
                case EndPointType.Rimatomics:
                    AddAllAtomicPointsToNetwork(map, endPoint, ref network);
                    return;

            }
        }

        private static IEnumerable<Building> GetAllConnectedEndPoints(Map map, Building FirstEndPoint, ThingDef[] connectorDefs, EndPointType type)
        {
            List<Building> ConnectedEndPoints = new List<Building>();

            CellRect cellRect = FirstEndPoint.OccupiedRect();
            List<IntVec3> ToCheck = cellRect.AdjacentCellsCardinal.ToList();
            
            for(int i = 0; i < ToCheck.Count(); i++){

                IEnumerable<Building> Buildings = ToCheck[i].GetThingList(map).OfType<Building>();

                foreach (Building candidate in Buildings)
                {
                    bool connector = false;
                    switch (type)
                    {
                        case EndPointType.DubPlumbing:

                            if (IsPlumbingEntry(candidate.def))
                            {
                                if (ConnectedEndPoints.Contains(candidate))
                                {
                                    break;
                                }

                                ConnectedEndPoints.Add(candidate);
                                connector = true;
                            }
                            break;
                        case EndPointType.Rimatomics:
                            if (IsAtomicEntry(candidate.def))
                            {
                                if (ConnectedEndPoints.Contains(candidate))
                                {
                                    break;
                                }
                                ConnectedEndPoints.Add(candidate);
                                connector = true;
                            }
                            break;
                    }

                    for(int d =0; d < connectorDefs.Length; d++)
                    {
                        if (candidate.def == connectorDefs[d])
                        {
                            connector = true;
                            break;
                        }
                    }

                    if (connector)
                    {
                        CellRect candidateRect = candidate.OccupiedRect();
                        IEnumerable<IntVec3> Additions = candidateRect.AdjacentCellsCardinal;

                        foreach (IntVec3 Addition in Additions)
                        {

                            if (ToCheck.Contains(Addition))
                            {

                                continue;
                            }
 
                            ToCheck.Add(Addition);
                        }
                    }

                }
            }

            return ConnectedEndPoints;
        }

        private static void AddAllAtomicPointsToNetwork(Map map, InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            if (endPoint.type != EndPointType.Rimatomics)
            {
                Log.Error("Attempted to find Rimatomics Network on a non-Rimatomics Building. What did you do?!");
            }
            AddEndpointToNetwork(map, endPoint, ref network);
            IEnumerable<Building> endpoints = GetAllConnectedEndPoints(map, endPoint.building, new ThingDef[] { ExternalDefOf.waterPipe, ExternalDefOf.waterValve, ExternalDefOf.coolingPipe, ExternalDefOf.coolantValve }, EndPointType.Rimatomics);
            foreach (Building endpoint in endpoints)
            {
                Log.Message(endpoint + " being checked");
                InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = endpoint as Building, type = EndPointType.Rimatomics };
                AddEndpointToNetwork(map, infiltrationEndPoint, ref network);
            }
        }

        private static void AddAllBadHygeinePointsToNetwork(Map map, InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            if (endPoint.type != EndPointType.DubPlumbing)
            {
                Log.Error("Attempted to find Dubs Bad Hygeine Plumbing Network on a non-Dubs Bad Hygeine Plumbing Building. What did you do?!");
            }
            AddEndpointToNetwork(map, endPoint, ref network);
            IEnumerable<Building> endpoints = GetAllConnectedEndPoints(map, endPoint.building, new ThingDef[] { ExternalDefOf.sewagePipeStuff}, EndPointType.DubPlumbing);
            foreach (Building endpoint in endpoints)
            {
                Log.Message(endpoint + " being checked");
                InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = endpoint as Building, type = EndPointType.DubPlumbing };
                AddEndpointToNetwork(map, infiltrationEndPoint, ref network);
            }
        }
        private static void AddAllVEPipePointsToNetwork(Map map, InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            CompResource compResource = endPoint.building.GetComp<CompResource>();
            if (compResource == null)
            {
                Log.Error("Attempted to find VEPipesNetwork on a non-VEPipe Building. What did you do?!");
            }

            AddEndpointToNetwork(map, endPoint, ref network);
            foreach (CompResource connector in compResource.PipeNet.connectors)
            {
                
                if (connector.parent is Building_Pipe)
                {
                    continue;
                }
            
                InfiltrationEndPoint infiltrationEndPoint = new InfiltrationEndPoint { building = connector.parent as Building, type = EndPointType.VEPipes };
                AddEndpointToNetwork(map, infiltrationEndPoint, ref network);
            }
        }

        protected static void RemoveEndpointFromNetwork(InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            network.endpoints.Remove(endPoint);
            network.connectedBuildings.Remove(endPoint.building);
            foreach (Room room in endPoint.connectedRooms)
            {
                bool clearRoom = true;
                foreach(InfiltrationEndPoint otherEndpoint in network.endpoints)
                {
                    if(otherEndpoint.connectedRooms.Contains(room))
                    {
                        clearRoom = false;
                        break;
                    }
                }
                if(clearRoom)
                {
                    network.connectedRooms.Remove(room);
                }
            }
        }

        protected static void AddEndpointToNetwork(Map map, InfiltrationEndPoint endPoint, ref InfiltrationNetwork network)
        {
            if(network.connectedBuildings.Contains(endPoint.building) || endPoint.building == null)
            {
                return;
            }


            network.connectedBuildings.Add(endPoint.building);

            List<Room> rooms = GetAllRoomsAdjacent(map, endPoint.building.Position);
            endPoint.connectedRooms = rooms;


            foreach (Room connected in rooms)
            {
                if(connected == null)
                {
                    continue;
                }

                if (network.connectedRooms.Contains(connected))
                {
                    continue;
                }
                network.connectedRooms.Add(connected);
            }

            if (!network.endpoints.Contains(endPoint))
            {
                Log.Message("added " +  endPoint.building);
                network.endpoints.Add(endPoint);
            }
        }

        protected static List<Room> GetAllRoomsAdjacent(Map map, IntVec3 cell)
        {
            List<Room> rooms = new List<Room>();
            Room room = RegionAndRoomQuery.RoomAt(cell, map);

            if (room != null)
            {
                rooms.Add(room);
            }

            for (int i = 0; i < 8; i++)
            {
                room = RegionAndRoomQuery.RoomAt(cell + GenAdj.AdjacentCells[i], map);
                if (room != null && !rooms.Contains(room))
                {
                    rooms.Add(room);
                }
            }

            return rooms;
        }

        internal static bool IsCellClimbAccessible(IntVec3 finalGoalCell, Map map, out IntVec3 openCell)
        {
            openCell = IntVec3.Invalid;
            Room room = finalGoalCell.GetRoom(map);
            if (room != null)
            {
                if(room.OpenRoofCount < room.CellCount)
                {
                    foreach (IntVec3 cell in room.Cells)
                    {
                        if (cell.GetRoof(map) != RoofDefOf.RoofRockThick && cell.Standable(map))
                        {
                            openCell = cell;
                            return true;
                        }
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
