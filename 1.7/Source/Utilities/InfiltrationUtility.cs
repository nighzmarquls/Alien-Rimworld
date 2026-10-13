using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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

    internal sealed class GeometricCategoryCache
    {
        public bool Dirty = true;
        public int Revision;
        public readonly List<InfiltrationNetworkComponent> Components = new List<InfiltrationNetworkComponent>();
    }

    internal sealed class InfiltrationTopologyCache
    {
        public readonly Dictionary<InfiltrationNetworkDef, GeometricCategoryCache> Categories = new Dictionary<InfiltrationNetworkDef, GeometricCategoryCache>();
        public bool ComponentsDirty = true;
        public bool PortsDirty = true;
        public int ComponentsRevision;
        public int PortsRevision;
        public int RegionRevision;
        public readonly List<InfiltrationNetworkComponent> Components = new List<InfiltrationNetworkComponent>();
        public readonly Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>> PortsByComponent = new Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>>();
        public readonly Dictionary<Region, List<InfiltrationPort>> PortsByRegion = new Dictionary<Region, List<InfiltrationPort>>();
        public readonly Dictionary<int, Building> EndpointsById = new Dictionary<int, Building>();
        public readonly Dictionary<Region, bool> ReachablePortByRegion = new Dictionary<Region, bool>();

        public void Clear()
        {
            Categories.Clear();
            Components.Clear();
            PortsByComponent.Clear();
            PortsByRegion.Clear();
            EndpointsById.Clear();
            ReachablePortByRegion.Clear();
            ComponentsDirty = true;
            PortsDirty = true;
            ComponentsRevision++;
            PortsRevision++;
            RegionRevision++;
        }

        public void InvalidateComponents()
        {
            ComponentsDirty = true;
            InvalidatePorts();
        }

        public void InvalidatePorts()
        {
            PortsDirty = true;
            ReachablePortByRegion.Clear();
            RegionRevision++;
        }

        public void InvalidateRegions()
        {
            PortsDirty = true;
            PortsByComponent.Clear();
            PortsByRegion.Clear();
            ReachablePortByRegion.Clear();
            RegionRevision++;
        }
    }

    public static class InfiltrationUtility
    {
        private const string GeometricProviderKey = "geometric";
        private const string PipeNetProviderKey = "pipeNet";

        private static Dictionary<ThingDef, List<InfiltrationNetworkDef>> geometricDefsByThing;
        private static List<InfiltrationNetworkDef> geometricDefs;

        public static bool CacheGeometricNetworks = true;

        public static void ClearAllCaches()
        {
            if (Current.Game?.Maps == null)
            {
                return;
            }
            foreach (Map map in Current.Game.Maps)
            {
                ClearCache(map);
            }
        }

        public static void ClearCache(Map map)
        {
            GetCache(map)?.Clear();
        }

        private static InfiltrationTopologyCache GetCache(Map map)
        {
            return TraversalTopologyMapComponent.For(map)?.InfiltrationCache;
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
                if (building.AllComps?.Any(comp => comp is CompResource) == true)
                {
                    GetCache(building.Map)?.InvalidateComponents();
                }
            }
        }

        public static void NotifyRegionsRoomsChanged(Map map)
        {
            GetCache(map)?.InvalidateRegions();
        }

        public static void NotifyPipeTopologyChanged(Map map)
        {
            GetCache(map)?.InvalidateComponents();
        }

        public static void NotifyBuildingDespawned(Building building, Map previousMap)
        {
            if (building != null && previousMap != null)
            {
                MarkDirty(previousMap, building.def);
                if (building.AllComps?.Any(comp => comp is CompResource) == true)
                {
                    GetCache(previousMap)?.InvalidateComponents();
                }
            }
        }

        private static void MarkDirty(Map map, ThingDef thingDef)
        {
            EnsureDefRegistry();
            if (map == null || thingDef == null || !geometricDefsByThing.TryGetValue(thingDef, out List<InfiltrationNetworkDef> affectedDefs) ||
                GetCache(map) is not InfiltrationTopologyCache mapCache)
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
            mapCache.InvalidateComponents();
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

            InfiltrationTopologyCache mapCache = GetCache(map);
            if (mapCache == null)
            {
                GeometricCategoryCache uncached = new GeometricCategoryCache();
                RebuildGeometricCategory(map, networkDef, uncached);
                return uncached;
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

        private static void EnsureComponents(Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null || (!cache.ComponentsDirty && CacheGeometricNetworks))
            {
                return;
            }

            EnsureDefRegistry();
            cache.Components.Clear();
            cache.EndpointsById.Clear();
            foreach (InfiltrationNetworkDef networkDef in geometricDefs)
            {
                cache.Components.AddRange(GetGeometricCategory(map, networkDef).Components.Where(component => component.Endpoints.Count > 0));
            }
            AddPipeNetComponents(map, cache.Components);
            foreach (Building endpoint in cache.Components.SelectMany(component => component.Endpoints).Where(endpoint => endpoint != null))
            {
                cache.EndpointsById[endpoint.thingIDNumber] = endpoint;
            }
            cache.ComponentsDirty = false;
            cache.ComponentsRevision++;
            cache.InvalidatePorts();
        }

        internal static IReadOnlyList<InfiltrationNetworkComponent> GetAllComponents(Map map)
        {
            EnsureComponents(map);
            return GetCache(map)?.Components ?? (IReadOnlyList<InfiltrationNetworkComponent>)Array.Empty<InfiltrationNetworkComponent>();
        }

        private static void AddPipeNetComponents(Map map, List<InfiltrationNetworkComponent> components)
        {
            PipeNetManager manager = map.GetComponent<PipeNetManager>();
            if (manager?.pipeNets == null)
            {
                return;
            }

            foreach (PipeNet pipeNet in manager.pipeNets)
            {
                if (pipeNet == null || pipeNet.connectors == null || pipeNet.def == null)
                {
                    continue;
                }

                InfiltrationNetworkComponent component = new InfiltrationNetworkComponent
                {
                    ProviderKey = PipeNetProviderKey,
                    CategoryKey = pipeNet.def.defName,
                    ProviderIdentity = pipeNet
                };
                foreach (CompResource comp in pipeNet.connectors)
                {
                    if (!(comp?.parent is Building building) || !building.Spawned || building.Map != map || building is Building_Pipe)
                    {
                        continue;
                    }
                    if (!component.Members.Contains(building))
                    {
                        component.Members.Add(building);
                        component.Endpoints.Add(building);
                    }
                }
                if (component.Endpoints.Count > 0)
                {
                    components.Add(component);
                }
            }
        }

        private static IEnumerable<InfiltrationPort> BuildPorts(InfiltrationNetworkComponent component, Map map)
        {
            HashSet<Building> members = new HashSet<Building>(component.Members);
            foreach (Building endpoint in component.Endpoints.Where(endpoint => endpoint != null && !endpoint.Destroyed && endpoint.Spawned && endpoint.Map == map).OrderBy(endpoint => endpoint.thingIDNumber))
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

        private static void EnsurePorts(Map map)
        {
            EnsureComponents(map);
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null || !cache.PortsDirty)
            {
                return;
            }

            cache.PortsByComponent.Clear();
            cache.PortsByRegion.Clear();
            cache.ReachablePortByRegion.Clear();
            foreach (InfiltrationNetworkComponent component in cache.Components)
            {
                List<InfiltrationPort> ports = BuildPorts(component, map).ToList();
                cache.PortsByComponent[component] = ports;
                foreach (InfiltrationPort port in ports)
                {
                    if (!cache.PortsByRegion.TryGetValue(port.Region, out List<InfiltrationPort> regionPorts))
                    {
                        regionPorts = new List<InfiltrationPort>();
                        cache.PortsByRegion.Add(port.Region, regionPorts);
                    }
                    regionPorts.Add(port);
                }
            }
            cache.PortsDirty = false;
            cache.PortsRevision++;
        }

        internal static IReadOnlyList<InfiltrationPort> GetPorts(Map map, InfiltrationNetworkComponent component)
        {
            EnsurePorts(map);
            return GetCache(map)?.PortsByComponent.TryGetValue(component, out List<InfiltrationPort> ports) == true
                ? ports
                : (IReadOnlyList<InfiltrationPort>)Array.Empty<InfiltrationPort>();
        }

        internal static IReadOnlyList<InfiltrationPort> GetPorts(Map map, Region region)
        {
            EnsurePorts(map);
            return region != null && GetCache(map)?.PortsByRegion.TryGetValue(region, out List<InfiltrationPort> ports) == true
                ? ports
                : (IReadOnlyList<InfiltrationPort>)Array.Empty<InfiltrationPort>();
        }

        internal static bool RegionCanReachInfiltrationPort(Map map, Region region)
        {
            if (map == null || region == null)
            {
                return false;
            }
            EnsurePorts(map);
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null || cache.PortsByRegion.Count == 0)
            {
                return false;
            }
            if (cache.ReachablePortByRegion.TryGetValue(region, out bool cached))
            {
                return cached;
            }

            HashSet<Region> visited = new HashSet<Region>();
            bool reachedPort = false;
            TraverseParms permissiveParms = TraverseParms.For(TraverseMode.PassDoors);
            RegionEntryPredicate entryCondition = (Region from, Region next) => next.Allows(permissiveParms, isDestination: false);
            RegionProcessor processor = delegate (Region current)
            {
                visited.Add(current);
                reachedPort = cache.PortsByRegion.ContainsKey(current);
                return reachedPort;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, processor, 99999);
            foreach (Region visitedRegion in visited)
            {
                cache.ReachablePortByRegion[visitedRegion] = reachedPort;
            }
            return reachedPort;
        }

        internal static void WarmTopologyFor(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Spawned || pawn.Map.regionDirtyer.AnyDirty)
            {
                return;
            }
            Region region = pawn.Position.GetRegion(pawn.Map, RegionType.Set_Passable);
            if (region != null)
            {
                RegionCanReachInfiltrationPort(pawn.Map, region);
            }
        }

        public static bool CanReachByInfiltration(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger,
            TraverseMode mode = TraverseMode.ByPawn)
        {
            return TraversalRouteUtility.CanReach(pawn, destination, pathEndMode, maxDanger, mode, allowWallClimb: false);
        }

        public static bool TryBuildInfiltrationRoute(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode,
            Danger maxDanger, out List<TraversalLeg> legs)
        {
            return TraversalRouteUtility.TryBuildRoute(pawn, destination, pathEndMode, maxDanger, allowWallClimb: false, out legs);
        }

        public static bool CanPawnTraverseNetwork(Pawn pawn, string providerKey, string categoryKey)
        {
            // Deliberately dormant until per-category maximum body-size settings are enabled.
            return pawn != null;
        }

        public static bool ValidateTraversalLeg(Pawn pawn, TraversalLeg leg)
        {
            Map map = pawn?.Map;
            if (map == null || leg == null || !leg.IsInfiltration || !leg.start.InBounds(map) || !leg.end.InBounds(map) ||
                !leg.start.Standable(map) || !leg.end.Standable(map) ||
                !CanPawnTraverseNetwork(pawn, leg.providerKey, leg.categoryKey))
            {
                return false;
            }

            EnsurePorts(map);
            InfiltrationTopologyCache cache = GetCache(map);
            Building entry = cache?.EndpointsById.TryGetValue(leg.entryThingId, out Building cachedEntry) == true ? cachedEntry : null;
            Building exit = cache?.EndpointsById.TryGetValue(leg.exitThingId, out Building cachedExit) == true ? cachedExit : null;
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
                IReadOnlyList<InfiltrationPort> ports = GetPorts(map, component);
                bool validEntry = ports.Any(port => port.Endpoint == entry && port.AccessCell == leg.start);
                bool validExit = ports.Any(port => port.Endpoint == exit && port.AccessCell == leg.end);
                return validEntry && validExit;
            }
            return false;
        }

        public static string CacheReport(Map map)
        {
            EnsureDefRegistry();
            if (map == null)
            {
                return "no map";
            }
            List<string> lines = new List<string>();
            EnsurePorts(map);
            InfiltrationTopologyCache cache = GetCache(map);
            lines.Add("aggregate: componentsRevision=" + (cache?.ComponentsRevision ?? 0) +
                ", portsRevision=" + (cache?.PortsRevision ?? 0) + ", regionRevision=" + (cache?.RegionRevision ?? 0) +
                ", components=" + (cache?.Components.Count ?? 0) + ", ports=" + (cache?.PortsByComponent.Values.Sum(ports => ports.Count) ?? 0) +
                ", cachedRegionResults=" + (cache?.ReachablePortByRegion.Count ?? 0));
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
