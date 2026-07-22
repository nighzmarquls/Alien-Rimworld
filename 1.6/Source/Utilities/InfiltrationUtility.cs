using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class InfiltrationNetworkDef : Def
    {
        public List<ThingDef> endpointDefs = new List<ThingDef>();
        public List<ThingDef> connectorDefs = new List<ThingDef>();
        public string settingsKey;
        public float maxBodySize = -1f;

        public string CategoryKey => settingsKey.NullOrEmpty() ? defName : settingsKey;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (endpointDefs == null || endpointDefs.Count == 0)
            {
                yield return defName + " has no infiltration endpointDefs.";
            }
        }
    }

    public enum TraversalLegType : byte
    {
        WallClimb,
        Infiltration
    }

    public class TraversalLeg : IExposable
    {
        public TraversalLegType type;
        public IntVec3 start = IntVec3.Invalid;
        public IntVec3 end = IntVec3.Invalid;
        public string providerKey;
        public string categoryKey;
        public int entryThingId = -1;
        public int exitThingId = -1;

        public bool IsInfiltration => type == TraversalLegType.Infiltration;

        public TraversalLeg()
        {
        }

        public static TraversalLeg WallClimb(IntVec3 start, IntVec3 end)
        {
            return new TraversalLeg
            {
                type = TraversalLegType.WallClimb,
                start = start,
                end = end
            };
        }

        internal static TraversalLeg Infiltration(InfiltrationPort entry, InfiltrationPort exit)
        {
            return new TraversalLeg
            {
                type = TraversalLegType.Infiltration,
                start = entry.AccessCell,
                end = exit.AccessCell,
                providerKey = entry.Component.ProviderKey,
                categoryKey = entry.Component.CategoryKey,
                entryThingId = entry.Endpoint.thingIDNumber,
                exitThingId = exit.Endpoint.thingIDNumber
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type", TraversalLegType.WallClimb);
            Scribe_Values.Look(ref start, "start", IntVec3.Invalid);
            Scribe_Values.Look(ref end, "end", IntVec3.Invalid);
            Scribe_Values.Look(ref providerKey, "providerKey");
            Scribe_Values.Look(ref categoryKey, "categoryKey");
            Scribe_Values.Look(ref entryThingId, "entryThingId", -1);
            Scribe_Values.Look(ref exitThingId, "exitThingId", -1);
        }

        public override string ToString()
        {
            return type + " " + start + " -> " + end +
                (IsInfiltration ? " [" + providerKey + "/" + categoryKey + ", " + entryThingId + " -> " + exitThingId + "]" : string.Empty);
        }
    }

    internal sealed class InfiltrationNetworkComponent
    {
        public string ProviderKey;
        public string CategoryKey;
        public object ProviderIdentity;
        public InfiltrationNetworkDef GeometricDef;
        public readonly List<Building> Members = new List<Building>();
        public readonly List<Building> Endpoints = new List<Building>();
    }

    internal sealed class InfiltrationPort
    {
        public InfiltrationNetworkComponent Component;
        public Building Endpoint;
        public IntVec3 AccessCell;
        public Region Region;
    }

    public static class InfiltrationUtility
    {
        private const string GeometricProviderKey = "geometric";
        private const string PipeNetProviderKey = "pipeNet";

        private sealed class GeometricCategoryCache
        {
            public bool Dirty = true;
            public int Revision;
            public readonly List<InfiltrationNetworkComponent> Components = new List<InfiltrationNetworkComponent>();
        }

        private sealed class MapTopologyCache
        {
            public readonly Dictionary<InfiltrationNetworkDef, GeometricCategoryCache> Categories = new Dictionary<InfiltrationNetworkDef, GeometricCategoryCache>();
        }

        private sealed class WalkingArea
        {
            public int Id;
            public readonly HashSet<Region> Regions = new HashSet<Region>();
            public readonly List<InfiltrationPort> Ports = new List<InfiltrationPort>();
            public IntVec3 OpenRoofAnchor = IntVec3.Invalid;
            public bool PortsDiscovered;
        }

        private enum TransitionType : byte
        {
            Network,
            OpenRoofClimb
        }

        private sealed class AreaTransition
        {
            public WalkingArea Previous;
            public WalkingArea Next;
            public TransitionType Type;
            public InfiltrationPort EntryPort;
            public InfiltrationPort ExitPort;
        }

        private sealed class SearchContext
        {
            public Pawn Pawn;
            public Map Map;
            public TraverseParms TraverseParms;
            public readonly List<WalkingArea> Areas = new List<WalkingArea>();
            public readonly Dictionary<Region, WalkingArea> AreaByRegion = new Dictionary<Region, WalkingArea>();
            public readonly Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>> PortsByComponent = new Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>>();
            public readonly Dictionary<Building, List<InfiltrationNetworkComponent>> ComponentsByEndpoint = new Dictionary<Building, List<InfiltrationNetworkComponent>>();
            public readonly HashSet<WalkingArea> StartAreas = new HashSet<WalkingArea>();
            public readonly HashSet<WalkingArea> GoalAreas = new HashSet<WalkingArea>();
        }

        private static ConditionalWeakTable<Map, MapTopologyCache> topologyCaches = new ConditionalWeakTable<Map, MapTopologyCache>();
        private static Dictionary<ThingDef, List<InfiltrationNetworkDef>> geometricDefsByThing;
        private static List<InfiltrationNetworkDef> geometricDefs;

        public static bool CacheGeometricNetworks = true;

        public static void ClearAllCaches()
        {
            topologyCaches = new ConditionalWeakTable<Map, MapTopologyCache>();
        }

        public static void ClearCache(Map map)
        {
            if (map != null)
            {
                topologyCaches.Remove(map);
            }
        }

        public static bool IsCellTrapped(IntVec3 cell, Map map, TraverseMode traverseMode = TraverseMode.PassDoors, Danger maxDanger = Danger.None)
        {
            if (!cell.IsValid || map == null || !cell.InBounds(map))
            {
                return false;
            }
            if (!RCellFinder.TryFindEdgeCellFromPositionAvoidingColony(cell, map, candidate => true, out IntVec3 escapeCell))
            {
                return true;
            }
            return !map.reachability.CanReach(cell, escapeCell, PathEndMode.ClosestTouch, traverseMode, maxDanger);
        }

        public static void NotifyBuildingSpawned(Building building)
        {
            if (building?.Map != null)
            {
                MarkDirty(building.Map, building.def);
            }
        }

        public static void NotifyBuildingDespawned(Building building, Map previousMap)
        {
            if (building != null && previousMap != null)
            {
                MarkDirty(previousMap, building.def);
            }
        }

        private static void MarkDirty(Map map, ThingDef thingDef)
        {
            EnsureDefRegistry();
            if (map == null || thingDef == null || !geometricDefsByThing.TryGetValue(thingDef, out List<InfiltrationNetworkDef> affectedDefs) ||
                !topologyCaches.TryGetValue(map, out MapTopologyCache mapCache))
            {
                return;
            }

            foreach (InfiltrationNetworkDef networkDef in affectedDefs)
            {
                if (mapCache.Categories.TryGetValue(networkDef, out GeometricCategoryCache categoryCache))
                {
                    categoryCache.Dirty = true;
                }
            }
        }

        private static void EnsureDefRegistry()
        {
            if (geometricDefs != null)
            {
                return;
            }

            geometricDefs = DefDatabase<InfiltrationNetworkDef>.AllDefsListForReading
                .Where(def => def.endpointDefs != null && def.endpointDefs.Count > 0)
                .OrderBy(def => def.defName)
                .ToList();
            geometricDefsByThing = new Dictionary<ThingDef, List<InfiltrationNetworkDef>>();
            foreach (InfiltrationNetworkDef networkDef in geometricDefs)
            {
                IEnumerable<ThingDef> things = networkDef.endpointDefs.Concat(networkDef.connectorDefs ?? Enumerable.Empty<ThingDef>()).Where(def => def != null).Distinct();
                foreach (ThingDef thingDef in things)
                {
                    if (!geometricDefsByThing.TryGetValue(thingDef, out List<InfiltrationNetworkDef> categories))
                    {
                        categories = new List<InfiltrationNetworkDef>();
                        geometricDefsByThing.Add(thingDef, categories);
                    }
                    categories.Add(networkDef);
                }
            }
        }

        private static GeometricCategoryCache GetGeometricCategory(Map map, InfiltrationNetworkDef networkDef)
        {
            if (!CacheGeometricNetworks)
            {
                GeometricCategoryCache uncached = new GeometricCategoryCache();
                RebuildGeometricCategory(map, networkDef, uncached);
                return uncached;
            }

            if (!topologyCaches.TryGetValue(map, out MapTopologyCache mapCache))
            {
                mapCache = new MapTopologyCache();
                topologyCaches.Add(map, mapCache);
            }
            if (!mapCache.Categories.TryGetValue(networkDef, out GeometricCategoryCache categoryCache))
            {
                categoryCache = new GeometricCategoryCache();
                mapCache.Categories.Add(networkDef, categoryCache);
            }
            if (categoryCache.Dirty)
            {
                RebuildGeometricCategory(map, networkDef, categoryCache);
            }
            return categoryCache;
        }

        private static void RebuildGeometricCategory(Map map, InfiltrationNetworkDef networkDef, GeometricCategoryCache categoryCache)
        {
            categoryCache.Components.Clear();
            categoryCache.Dirty = false;
            categoryCache.Revision++;

            HashSet<ThingDef> endpointDefs = new HashSet<ThingDef>(networkDef.endpointDefs.Where(def => def != null));
            HashSet<ThingDef> memberDefs = new HashSet<ThingDef>(endpointDefs);
            if (networkDef.connectorDefs != null)
            {
                memberDefs.UnionWith(networkDef.connectorDefs.Where(def => def != null));
            }

            List<Building> members = map.listerThings.AllThings
                .OfType<Building>()
                .Where(building => building.Spawned && memberDefs.Contains(building.def))
                .OrderBy(building => building.thingIDNumber)
                .ToList();
            if (members.Count == 0)
            {
                return;
            }

            Dictionary<IntVec3, List<int>> membersByCell = new Dictionary<IntVec3, List<int>>();
            for (int i = 0; i < members.Count; i++)
            {
                foreach (IntVec3 cell in members[i].OccupiedRect())
                {
                    if (!membersByCell.TryGetValue(cell, out List<int> indexes))
                    {
                        indexes = new List<int>();
                        membersByCell.Add(cell, indexes);
                    }
                    indexes.Add(i);
                }
            }

            int[] parents = Enumerable.Range(0, members.Count).ToArray();
            int Find(int index)
            {
                while (parents[index] != index)
                {
                    parents[index] = parents[parents[index]];
                    index = parents[index];
                }
                return index;
            }
            void Union(int left, int right)
            {
                int leftRoot = Find(left);
                int rightRoot = Find(right);
                if (leftRoot != rightRoot)
                {
                    parents[rightRoot] = leftRoot;
                }
            }

            for (int i = 0; i < members.Count; i++)
            {
                foreach (IntVec3 occupiedCell in members[i].OccupiedRect())
                {
                    ConnectAt(occupiedCell, i);
                    for (int direction = 0; direction < 4; direction++)
                    {
                        ConnectAt(occupiedCell + GenAdj.CardinalDirections[direction], i);
                    }
                }
            }

            void ConnectAt(IntVec3 cell, int sourceIndex)
            {
                if (membersByCell.TryGetValue(cell, out List<int> indexes))
                {
                    foreach (int index in indexes)
                    {
                        Union(sourceIndex, index);
                    }
                }
            }

            Dictionary<int, InfiltrationNetworkComponent> componentsByRoot = new Dictionary<int, InfiltrationNetworkComponent>();
            for (int i = 0; i < members.Count; i++)
            {
                int root = Find(i);
                if (!componentsByRoot.TryGetValue(root, out InfiltrationNetworkComponent component))
                {
                    component = new InfiltrationNetworkComponent
                    {
                        ProviderKey = GeometricProviderKey,
                        CategoryKey = networkDef.CategoryKey,
                        ProviderIdentity = root,
                        GeometricDef = networkDef
                    };
                    componentsByRoot.Add(root, component);
                    categoryCache.Components.Add(component);
                }
                component.Members.Add(members[i]);
                if (endpointDefs.Contains(members[i].def))
                {
                    component.Endpoints.Add(members[i]);
                }
            }
        }

        private static List<InfiltrationNetworkComponent> GetAllComponents(Map map)
        {
            EnsureDefRegistry();
            List<InfiltrationNetworkComponent> components = new List<InfiltrationNetworkComponent>();
            foreach (InfiltrationNetworkDef networkDef in geometricDefs)
            {
                components.AddRange(GetGeometricCategory(map, networkDef).Components.Where(component => component.Endpoints.Count > 0));
            }
            AddPipeNetComponents(map, components);
            return components;
        }

        private static void AddPipeNetComponents(Map map, List<InfiltrationNetworkComponent> components)
        {
            Dictionary<object, InfiltrationNetworkComponent> byPipeNet = new Dictionary<object, InfiltrationNetworkComponent>();
            foreach (Building building in map.listerThings.AllThings.OfType<Building>().Where(building => building.Spawned && !(building is Building_Pipe)))
            {
                if (building.AllComps == null)
                {
                    continue;
                }
                foreach (CompResource comp in building.AllComps.OfType<CompResource>())
                {
                    if (comp?.PipeNet == null || comp.Props?.pipeNet == null)
                    {
                        continue;
                    }
                    object identity = comp.PipeNet;
                    if (!byPipeNet.TryGetValue(identity, out InfiltrationNetworkComponent component))
                    {
                        component = new InfiltrationNetworkComponent
                        {
                            ProviderKey = PipeNetProviderKey,
                            CategoryKey = comp.Props.pipeNet.defName,
                            ProviderIdentity = identity
                        };
                        byPipeNet.Add(identity, component);
                        components.Add(component);
                    }
                    if (!component.Members.Contains(building))
                    {
                        component.Members.Add(building);
                        component.Endpoints.Add(building);
                    }
                }
            }
        }

        private static IEnumerable<InfiltrationPort> GetPorts(InfiltrationNetworkComponent component, Map map)
        {
            HashSet<Building> members = new HashSet<Building>(component.Members);
            foreach (Building endpoint in component.Endpoints.Where(endpoint => endpoint != null && endpoint.Spawned && endpoint.Map == map).OrderBy(endpoint => endpoint.thingIDNumber))
            {
                HashSet<IntVec3> candidates = new HashSet<IntVec3>();
                if (endpoint.def.passability != Traversability.Impassable)
                {
                    foreach (IntVec3 cell in endpoint.OccupiedRect())
                    {
                        candidates.Add(cell);
                    }
                }
                foreach (IntVec3 cell in endpoint.OccupiedRect().AdjacentCellsCardinal)
                {
                    if (!cell.GetThingList(map).OfType<Building>().Any(members.Contains))
                    {
                        candidates.Add(cell);
                    }
                }

                foreach (IntVec3 cell in candidates.Where(cell => cell.InBounds(map) && cell.Standable(map)).OrderBy(cell => cell.x).ThenBy(cell => cell.z))
                {
                    Region region = cell.GetRegion(map, RegionType.Set_Passable);
                    if (region != null)
                    {
                        yield return new InfiltrationPort
                        {
                            Component = component,
                            Endpoint = endpoint,
                            AccessCell = cell,
                            Region = region
                        };
                    }
                }
            }
        }

        private static SearchContext BuildSearchContext(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger, TraverseMode mode)
        {
            if (pawn?.Map == null || !pawn.Spawned || !destination.IsValid || !destination.Cell.InBounds(pawn.Map))
            {
                return null;
            }

            SearchContext context = new SearchContext
            {
                Pawn = pawn,
                Map = pawn.Map,
                TraverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors: false, alwaysUseAvoidGrid: false, canBashFences: false)
            };

            foreach (InfiltrationNetworkComponent component in GetAllComponents(pawn.Map))
            {
                if (!CanPawnTraverseNetwork(pawn, component.ProviderKey, component.CategoryKey))
                {
                    continue;
                }
                foreach (Building endpoint in component.Endpoints)
                {
                    if (!context.ComponentsByEndpoint.TryGetValue(endpoint, out List<InfiltrationNetworkComponent> endpointComponents))
                    {
                        endpointComponents = new List<InfiltrationNetworkComponent>();
                        context.ComponentsByEndpoint.Add(endpoint, endpointComponents);
                    }
                    endpointComponents.Add(component);
                }
            }
            if (context.ComponentsByEndpoint.Count == 0)
            {
                return null;
            }

            Region startRegion = pawn.Position.GetRegion(pawn.Map, RegionType.Set_Passable);
            if (startRegion == null)
            {
                return null;
            }
            context.StartAreas.Add(GetOrCreateWalkingArea(context, startRegion));

            foreach (Region targetRegion in GetTargetRegions(pawn, destination, pathEndMode, context.TraverseParms))
            {
                context.GoalAreas.Add(GetOrCreateWalkingArea(context, targetRegion));
            }
            if (context.GoalAreas.Count == 0)
            {
                return null;
            }

            return context;
        }

        private static void DiscoverAreaPorts(SearchContext context, WalkingArea area)
        {
            if (area.PortsDiscovered)
            {
                return;
            }
            area.PortsDiscovered = true;

            HashSet<Room> rooms = new HashSet<Room>();
            HashSet<Building> candidateEndpoints = new HashSet<Building>();
            foreach (Region region in area.Regions)
            {
                if (region.Room != null && rooms.Add(region.Room))
                {
                    // RimWorld reuses Room's backing list, so consume it immediately.
                    foreach (Building building in region.Room.ContainedAndAdjacentThings.ToList().OfType<Building>())
                    {
                        candidateEndpoints.Add(building);
                    }
                }
            }

            foreach (Building endpoint in candidateEndpoints.OrderBy(building => building.thingIDNumber))
            {
                if (!context.ComponentsByEndpoint.TryGetValue(endpoint, out List<InfiltrationNetworkComponent> components))
                {
                    continue;
                }
                foreach (InfiltrationNetworkComponent component in components)
                {
                    EnsureComponentPorts(context, component);
                }
            }
        }

        private static void EnsureComponentPorts(SearchContext context, InfiltrationNetworkComponent component)
        {
            if (context.PortsByComponent.ContainsKey(component))
            {
                return;
            }
            List<InfiltrationPort> ports = GetPorts(component, context.Map).ToList();
            context.PortsByComponent.Add(component, ports);
            foreach (InfiltrationPort port in ports)
            {
                WalkingArea portArea = GetOrCreateWalkingArea(context, port.Region);
                if (!portArea.Ports.Any(existing => existing.Component == component && existing.Endpoint == port.Endpoint && existing.AccessCell == port.AccessCell))
                {
                    portArea.Ports.Add(port);
                }
            }
        }

        private static IEnumerable<Region> GetTargetRegions(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, TraverseParms traverseParms)
        {
            TargetInfo resolvedTarget = GenPath.ResolvePathMode(pawn, destination.ToTargetInfo(pawn.Map), ref pathEndMode);
            LocalTargetInfo resolvedLocalTarget = resolvedTarget.HasThing ? new LocalTargetInfo(resolvedTarget.Thing) : new LocalTargetInfo(resolvedTarget.Cell);
            List<Region> regions = new List<Region>();
            if (pathEndMode == PathEndMode.OnCell)
            {
                Region region = resolvedLocalTarget.Cell.GetRegion(pawn.Map, RegionType.Set_Passable);
                if (region != null && region.Allows(traverseParms, isDestination: true))
                {
                    regions.Add(region);
                }
            }
            else
            {
                TouchPathEndModeUtility.AddAllowedAdjacentRegions(resolvedLocalTarget, traverseParms, pawn.Map, regions);
            }
            return regions.Distinct();
        }

        private static WalkingArea GetOrCreateWalkingArea(SearchContext context, Region seed)
        {
            if (context.AreaByRegion.TryGetValue(seed, out WalkingArea existing))
            {
                return existing;
            }

            WalkingArea area = new WalkingArea { Id = context.Areas.Count };
            RegionEntryPredicate entryCondition = (Region from, Region region) => region.Allows(context.TraverseParms, isDestination: false);
            RegionProcessor processor = delegate (Region region)
            {
                area.Regions.Add(region);
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(seed, entryCondition, processor, 99999);
            if (area.Regions.Count == 0)
            {
                area.Regions.Add(seed);
            }
            foreach (Region region in area.Regions)
            {
                context.AreaByRegion[region] = area;
            }
            area.OpenRoofAnchor = FindOpenRoofAnchor(area, context.Map);
            context.Areas.Add(area);
            return area;
        }

        private static IntVec3 FindOpenRoofAnchor(WalkingArea area, Map map)
        {
            foreach (Region region in area.Regions)
            {
                foreach (IntVec3 cell in region.Cells)
                {
                    if (cell.InBounds(map) && cell.Standable(map) && !cell.Roofed(map))
                    {
                        return cell;
                    }
                }
            }
            return IntVec3.Invalid;
        }

        public static bool CanReachByInfiltration(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger, TraverseMode mode = TraverseMode.ByPawn)
        {
            SearchContext context = BuildSearchContext(pawn, destination, pathEndMode, maxDanger, mode);
            if (context == null)
            {
                return false;
            }
            if (context.StartAreas.Overlaps(context.GoalAreas))
            {
                return false;
            }

            HashSet<WalkingArea> startVisited = ExploreReachableAreas(context, context.StartAreas, context.GoalAreas, out bool reachedGoal);
            if (reachedGoal)
            {
                return true;
            }
            if (!startVisited.Any(area => area.OpenRoofAnchor.IsValid))
            {
                return false;
            }

            HashSet<WalkingArea> goalVisited = ExploreReachableAreas(context, context.GoalAreas, startVisited, out bool reachedStart);
            return reachedStart || goalVisited.Any(area => area.OpenRoofAnchor.IsValid);
        }

        public static bool TryBuildTraversalRoute(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger, out List<TraversalLeg> legs)
        {
            legs = new List<TraversalLeg>();
            SearchContext context = BuildSearchContext(pawn, destination, pathEndMode, maxDanger, TraverseMode.ByPawn);
            if (context == null)
            {
                return false;
            }
            if (context.StartAreas.Overlaps(context.GoalAreas))
            {
                return false;
            }

            ExploreAreasWithParents(context, context.StartAreas, context.GoalAreas,
                out HashSet<WalkingArea> startVisited, out List<WalkingArea> startVisitOrder,
                out Dictionary<WalkingArea, AreaTransition> startParents, out WalkingArea directGoal);

            List<AreaTransition> transitions;
            if (directGoal != null)
            {
                transitions = ReconstructForwardTransitions(directGoal, context.StartAreas, startParents);
            }
            else
            {
                WalkingArea startOpen = startVisitOrder.FirstOrDefault(area => area.OpenRoofAnchor.IsValid);
                if (startOpen == null)
                {
                    return false;
                }

                ExploreAreasWithParents(context, context.GoalAreas, startVisited,
                    out HashSet<WalkingArea> goalVisited, out List<WalkingArea> goalVisitOrder,
                    out Dictionary<WalkingArea, AreaTransition> goalParents, out WalkingArea intersectedStart);

                if (intersectedStart != null)
                {
                    transitions = ReconstructForwardTransitions(intersectedStart, context.StartAreas, startParents);
                    transitions.AddRange(ReconstructReverseTransitions(intersectedStart, context.GoalAreas, goalParents));
                }
                else
                {
                    WalkingArea goalOpen = goalVisitOrder.FirstOrDefault(area => area.OpenRoofAnchor.IsValid);
                    if (goalOpen == null)
                    {
                        return false;
                    }
                    transitions = ReconstructForwardTransitions(startOpen, context.StartAreas, startParents);
                    transitions.Add(new AreaTransition
                    {
                        Previous = startOpen,
                        Next = goalOpen,
                        Type = TransitionType.OpenRoofClimb
                    });
                    transitions.AddRange(ReconstructReverseTransitions(goalOpen, context.GoalAreas, goalParents));
                }
            }

            foreach (AreaTransition transition in transitions)
            {
                if (transition.Type == TransitionType.Network)
                {
                    legs.Add(TraversalLeg.Infiltration(transition.EntryPort, transition.ExitPort));
                }
                else
                {
                    if (!ClimbUtility.TryBuildWallTraversalLegs(pawn, transition.Previous.OpenRoofAnchor, transition.Next.OpenRoofAnchor, out List<TraversalLeg> wallLegs))
                    {
                        return false;
                    }
                    legs.AddRange(wallLegs);
                }
            }
            return legs.Count > 0;
        }

        private static HashSet<WalkingArea> ExploreReachableAreas(SearchContext context, IEnumerable<WalkingArea> seeds,
            HashSet<WalkingArea> destinations, out bool reachedDestination)
        {
            Queue<WalkingArea> open = new Queue<WalkingArea>(seeds.OrderBy(area => area.Id));
            HashSet<WalkingArea> visited = new HashSet<WalkingArea>(seeds);
            reachedDestination = false;
            while (open.Count > 0)
            {
                WalkingArea current = open.Dequeue();
                if (destinations.Contains(current))
                {
                    reachedDestination = true;
                    return visited;
                }
                DiscoverAreaPorts(context, current);
                foreach (WalkingArea next in ConnectedAreas(context, current))
                {
                    if (visited.Add(next))
                    {
                        open.Enqueue(next);
                    }
                }
            }
            return visited;
        }

        private static void ExploreAreasWithParents(SearchContext context, IEnumerable<WalkingArea> seeds,
            HashSet<WalkingArea> destinations, out HashSet<WalkingArea> visited, out List<WalkingArea> visitOrder,
            out Dictionary<WalkingArea, AreaTransition> parents, out WalkingArea reachedDestination)
        {
            Queue<WalkingArea> open = new Queue<WalkingArea>(seeds.OrderBy(area => area.Id));
            visited = new HashSet<WalkingArea>(seeds);
            visitOrder = new List<WalkingArea>();
            parents = new Dictionary<WalkingArea, AreaTransition>();
            reachedDestination = null;
            while (open.Count > 0)
            {
                WalkingArea current = open.Dequeue();
                visitOrder.Add(current);
                if (destinations.Contains(current))
                {
                    reachedDestination = current;
                    return;
                }
                DiscoverAreaPorts(context, current);
                foreach (InfiltrationPort entry in OrderedPorts(current.Ports))
                {
                    foreach (InfiltrationPort exit in OrderedPorts(context.PortsByComponent[entry.Component]))
                    {
                        WalkingArea next = context.AreaByRegion[exit.Region];
                        if (next == current || !visited.Add(next))
                        {
                            continue;
                        }
                        parents[next] = new AreaTransition
                        {
                            Previous = current,
                            Next = next,
                            Type = TransitionType.Network,
                            EntryPort = entry,
                            ExitPort = exit
                        };
                        open.Enqueue(next);
                    }
                }
            }
        }

        private static IEnumerable<WalkingArea> ConnectedAreas(SearchContext context, WalkingArea current)
        {
            HashSet<WalkingArea> yielded = new HashSet<WalkingArea>();
            foreach (InfiltrationPort entry in OrderedPorts(current.Ports))
            {
                foreach (InfiltrationPort exit in OrderedPorts(context.PortsByComponent[entry.Component]))
                {
                    WalkingArea next = context.AreaByRegion[exit.Region];
                    if (next != current && yielded.Add(next))
                    {
                        yield return next;
                    }
                }
            }
        }

        private static IOrderedEnumerable<InfiltrationPort> OrderedPorts(IEnumerable<InfiltrationPort> ports)
        {
            return ports.OrderBy(port => port.Endpoint.thingIDNumber).ThenBy(port => port.AccessCell.x).ThenBy(port => port.AccessCell.z);
        }

        private static List<AreaTransition> ReconstructForwardTransitions(WalkingArea destination, HashSet<WalkingArea> seeds,
            Dictionary<WalkingArea, AreaTransition> parents)
        {
            List<AreaTransition> transitions = new List<AreaTransition>();
            WalkingArea current = destination;
            while (!seeds.Contains(current))
            {
                if (!parents.TryGetValue(current, out AreaTransition transition))
                {
                    return new List<AreaTransition>();
                }
                transitions.Add(transition);
                current = transition.Previous;
            }
            transitions.Reverse();
            return transitions;
        }

        private static List<AreaTransition> ReconstructReverseTransitions(WalkingArea start, HashSet<WalkingArea> goalSeeds,
            Dictionary<WalkingArea, AreaTransition> goalParents)
        {
            List<AreaTransition> transitions = new List<AreaTransition>();
            WalkingArea current = start;
            while (!goalSeeds.Contains(current))
            {
                if (!goalParents.TryGetValue(current, out AreaTransition outward))
                {
                    return new List<AreaTransition>();
                }
                transitions.Add(new AreaTransition
                {
                    Previous = current,
                    Next = outward.Previous,
                    Type = outward.Type,
                    EntryPort = outward.ExitPort,
                    ExitPort = outward.EntryPort
                });
                current = outward.Previous;
            }
            return transitions;
        }

        public static bool CanPawnTraverseNetwork(Pawn pawn, string providerKey, string categoryKey)
        {
            // Deliberately dormant until per-category maximum body-size settings are enabled.
            return pawn != null;
        }

        public static bool ValidateTraversalLeg(Map map, TraversalLeg leg)
        {
            if (map == null || leg == null || !leg.IsInfiltration || !leg.start.InBounds(map) || !leg.end.InBounds(map) ||
                !leg.start.Standable(map) || !leg.end.Standable(map))
            {
                return false;
            }

            Building entry = FindBuildingById(map, leg.entryThingId);
            Building exit = FindBuildingById(map, leg.exitThingId);
            if (entry == null || exit == null || !entry.Spawned || !exit.Spawned)
            {
                return false;
            }

            foreach (InfiltrationNetworkComponent component in GetAllComponents(map).Where(component => component.ProviderKey == leg.providerKey && component.CategoryKey == leg.categoryKey))
            {
                if (!component.Endpoints.Contains(entry) || !component.Endpoints.Contains(exit))
                {
                    continue;
                }
                bool validEntry = GetPorts(component, map).Any(port => port.Endpoint == entry && port.AccessCell == leg.start);
                bool validExit = GetPorts(component, map).Any(port => port.Endpoint == exit && port.AccessCell == leg.end);
                return validEntry && validExit;
            }
            return false;
        }

        private static Building FindBuildingById(Map map, int thingId)
        {
            return map.listerThings.AllThings.OfType<Building>().FirstOrDefault(building => building.thingIDNumber == thingId);
        }

        public static string CacheReport(Map map)
        {
            EnsureDefRegistry();
            if (map == null)
            {
                return "no map";
            }
            List<string> lines = new List<string>();
            foreach (InfiltrationNetworkDef networkDef in geometricDefs)
            {
                GeometricCategoryCache category = GetGeometricCategory(map, networkDef);
                lines.Add(networkDef.defName + ": revision=" + category.Revision + ", dirty=" + category.Dirty +
                    ", components=" + category.Components.Count + ", members=" + category.Components.Sum(component => component.Members.Count) +
                    ", endpoints=" + category.Components.Sum(component => component.Endpoints.Count));
            }
            return lines.Count == 0 ? "no geometric infiltration definitions" : string.Join("\n", lines);
        }
    }
}
