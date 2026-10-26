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
        public List<InfiltrationNetworkComponent> Components = new List<InfiltrationNetworkComponent>();
    }

    internal sealed class InfiltrationTopologyCache
    {
        public readonly Dictionary<InfiltrationNetworkDef, GeometricCategoryCache> Categories = new Dictionary<InfiltrationNetworkDef, GeometricCategoryCache>();
        public bool ComponentsDirty = true;
        public bool PortsDirty = true;
        public int PendingGeneration;
        public bool BuildingPorts;
        public bool RegionBindingsDirty = true;
        public readonly HashSet<InfiltrationNetworkComponent> AccessDirty = new HashSet<InfiltrationNetworkComponent>();
        public int ComponentsRevision;
        public int PortsRevision;
        public int RegionRevision;
        public List<InfiltrationNetworkComponent> Components = new List<InfiltrationNetworkComponent>();
        public readonly Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>> PortsByComponent = new Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>>();
        public readonly Dictionary<Region, List<InfiltrationPort>> PortsByRegion = new Dictionary<Region, List<InfiltrationPort>>();
        public readonly Dictionary<int, Building> EndpointsById = new Dictionary<int, Building>();
        public readonly Dictionary<Region, bool> ReachablePortByRegion = new Dictionary<Region, bool>();

        public void Clear()
        {
            PendingGeneration++;
            RegionBindingsDirty = true;
            AccessDirty.Clear();
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

        // Notifications request verification. Published topology/revisions survive until
        // its traversal members, connectivity or usable access cells actually differ.
        public void InvalidateComponents()
        {
            ComponentsDirty = true;
            PendingGeneration++;
        }

        public void InvalidatePorts()
        {
            PortsDirty = true;
            PendingGeneration++;
        }

        public void InvalidateRegions()
        {
            RegionBindingsDirty = true;
            InvalidatePorts();
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

        internal static int PublishedTopologyVersion(Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            return cache == null ? 0 : unchecked(cache.ComponentsRevision * 397 ^ cache.PortsRevision);
        }

        internal static void VerifyPendingChanges(Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache != null && (cache.ComponentsDirty || cache.PortsDirty))
                EnsurePorts(map);
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
                NotifyAccessCellsChanged(building, building.Map);
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
                NotifyAccessCellsChanged(building, previousMap);
                if (building.AllComps?.Any(comp => comp is CompResource) == true)
                {
                    GetCache(previousMap)?.InvalidateComponents();
                }
            }
        }

        private static void NotifyAccessCellsChanged(Building changed, Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null)
                return;
            bool affected = false;
            foreach (InfiltrationNetworkComponent component in cache.Components)
                if (component.Endpoints.Any(endpoint => endpoint != null &&
                    changed.OccupiedRect().Any(cell => endpoint.OccupiedRect().ExpandedBy(1).Contains(cell))))
                {
                    cache.AccessDirty.Add(component);
                    affected = true;
                }
            if (affected)
                cache.InvalidatePorts();
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
            GeometricCategoryCache pending = new GeometricCategoryCache();
            BuildGeometricCategory(map, networkDef, pending);
            categoryCache.Dirty = false;
            if (categoryCache.Components.Count == pending.Components.Count &&
                categoryCache.Components.All(existing => pending.Components.Any(candidate => SameNetwork(existing, candidate))))
                return;
            categoryCache.Components = pending.Components;
            categoryCache.Revision++;
        }

        private static void BuildGeometricCategory(Map map, InfiltrationNetworkDef networkDef, GeometricCategoryCache categoryCache)
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

            List<Building> members = memberDefs.SelectMany(def => map.listerThings.ThingsOfDef(def)).OfType<Building>()
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

        internal static bool SameNetwork(InfiltrationNetworkComponent left, InfiltrationNetworkComponent right)
        {
            return left.ProviderKey == right.ProviderKey && left.CategoryKey == right.CategoryKey &&
                left.GeometricDef == right.GeometricDef && left.Members.Count == right.Members.Count &&
                left.Endpoints.Count == right.Endpoints.Count &&
                new HashSet<Building>(left.Members).SetEquals(right.Members) &&
                new HashSet<Building>(left.Endpoints).SetEquals(right.Endpoints);
        }

        internal static int ReuseEquivalentComponents(IReadOnlyList<InfiltrationNetworkComponent> previous, List<InfiltrationNetworkComponent> candidates)
        {
            // Index by a stable endpoint, rather than comparing every network pair.
            var indexed = previous.Where(component => component.Endpoints.Count > 0)
                .GroupBy(component => (component.ProviderKey, component.CategoryKey, component.Endpoints.Min(endpoint => endpoint.thingIDNumber)))
                .ToDictionary(group => group.Key, group => group.ToList());
            HashSet<InfiltrationNetworkComponent> retained = new HashSet<InfiltrationNetworkComponent>();
            for (int i = 0; i < candidates.Count; i++)
            {
                InfiltrationNetworkComponent candidate = candidates[i];
                if (candidate.Endpoints.Count == 0 || !indexed.TryGetValue(
                    (candidate.ProviderKey, candidate.CategoryKey, candidate.Endpoints.Min(endpoint => endpoint.thingIDNumber)), out var matches))
                    continue;
                InfiltrationNetworkComponent existing = matches.FirstOrDefault(component =>
                    !retained.Contains(component) && SameNetwork(component, candidate));
                if (existing == null)
                    continue;
                existing.ProviderIdentity = candidate.ProviderIdentity;
                candidates[i] = existing;
                retained.Add(existing);
            }
            return retained.Count;
        }

        private static void EnsureComponents(Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null || !cache.ComponentsDirty)
                return;

            EnsureDefRegistry();
            int generation = cache.PendingGeneration;
            List<InfiltrationNetworkComponent> candidates = new List<InfiltrationNetworkComponent>();
            foreach (InfiltrationNetworkDef networkDef in geometricDefs)
                candidates.AddRange(GetGeometricCategory(map, networkDef).Components.Where(component => component.Endpoints.Count > 0));
            AddPipeNetComponents(map, candidates);

            // Equivalent PipeNet replacements must not replace component identities held
            // by an in-progress route search. Preserve unrelated networks as well.
            int retained = ReuseEquivalentComponents(cache.Components, candidates);
            if (generation != cache.PendingGeneration)
                return;

            cache.ComponentsDirty = false;
            if (retained == cache.Components.Count && retained == candidates.Count)
                return;

            // Publish a new list; callers may still be enumerating the old snapshot.
            cache.Components = candidates;
            cache.EndpointsById.Clear();
            foreach (Building endpoint in candidates.SelectMany(component => component.Endpoints))
                cache.EndpointsById[endpoint.thingIDNumber] = endpoint;
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
                    if (cell.InBounds(map) && !cell.GetThingList(map).OfType<Building>().Any(members.Contains))
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

        internal static bool SamePorts(IReadOnlyList<InfiltrationPort> left, IReadOnlyList<InfiltrationPort> right, bool includeRegions)
        {
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i].Endpoint != right[i].Endpoint || left[i].AccessCell != right[i].AccessCell ||
                    includeRegions && left[i].Region != right[i].Region)
                    return false;
            return true;
        }

        private static bool EnsurePorts(Map map)
        {
            InfiltrationTopologyCache cache = GetCache(map);
            if (cache == null || cache.BuildingPorts)
                return false;

            // GetRegion can synchronously emit RegionsRoomsChanged. Settle that before
            // taking a generation or enumerating any published port collections.
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            EnsureComponents(map);
            if (cache.ComponentsDirty || map.regionDirtyer.AnyDirty)
                return false;
            if (!cache.PortsDirty)
                return true;

            int generation = cache.PendingGeneration;
            bool topologyChanged = cache.PortsByComponent.Count != cache.Components.Count;
            bool bindingsChanged = false;
            Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>> pending = new Dictionary<InfiltrationNetworkComponent, List<InfiltrationPort>>();
            cache.BuildingPorts = true;
            try
            {
                foreach (InfiltrationNetworkComponent component in cache.Components)
                {
                    List<InfiltrationPort> ports = !cache.RegionBindingsDirty && !cache.AccessDirty.Contains(component) &&
                        cache.PortsByComponent.TryGetValue(component, out List<InfiltrationPort> unchanged)
                        ? unchanged : BuildPorts(component, map).ToList();
                    if (cache.PortsByComponent.TryGetValue(component, out List<InfiltrationPort> previous))
                    {
                        topologyChanged |= !SamePorts(previous, ports, includeRegions: false);
                        if (SamePorts(previous, ports, includeRegions: true))
                            ports = previous;
                        else
                            bindingsChanged = true;
                    }
                    else
                        topologyChanged = true;
                    pending.Add(component, ports);
                }
                if (generation != cache.PendingGeneration || map.regionDirtyer.AnyDirty)
                    return false;

                if (topologyChanged || bindingsChanged)
                {
                    cache.PortsByComponent.Clear();
                    cache.PortsByRegion.Clear();
                    cache.ReachablePortByRegion.Clear();
                    foreach (var pair in pending)
                    {
                        cache.PortsByComponent.Add(pair.Key, pair.Value);
                        foreach (InfiltrationPort port in pair.Value)
                        {
                            if (!cache.PortsByRegion.TryGetValue(port.Region, out List<InfiltrationPort> regionPorts))
                                cache.PortsByRegion.Add(port.Region, regionPorts = new List<InfiltrationPort>());
                            regionPorts.Add(port);
                        }
                    }
                    if (topologyChanged)
                        cache.PortsRevision++;
                    if (bindingsChanged || topologyChanged)
                        cache.RegionRevision++;
                }
                // Region links/door state can change without replacing Region objects.
                cache.ReachablePortByRegion.Clear();
                cache.RegionBindingsDirty = false;
                cache.AccessDirty.Clear();
                cache.PortsDirty = false;
                return true;
            }
            finally
            {
                cache.BuildingPorts = false;
            }
        }
        internal static IReadOnlyList<InfiltrationPort> GetPorts(Map map, InfiltrationNetworkComponent component)
        {
            return EnsurePorts(map) && GetCache(map)?.PortsByComponent.TryGetValue(component, out List<InfiltrationPort> ports) == true
                ? ports
                : (IReadOnlyList<InfiltrationPort>)Array.Empty<InfiltrationPort>();
        }

        internal static IReadOnlyList<InfiltrationPort> GetPorts(Map map, Region region)
        {
            return EnsurePorts(map) && region != null && GetCache(map)?.PortsByRegion.TryGetValue(region, out List<InfiltrationPort> ports) == true
                ? ports
                : (IReadOnlyList<InfiltrationPort>)Array.Empty<InfiltrationPort>();
        }

        internal static bool RegionCanReachInfiltrationPort(Map map, Region region)
        {
            if (map == null || region == null)
            {
                return false;
            }
            if (!EnsurePorts(map))
                return false;
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

            if (!EnsurePorts(map))
                return false;
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
            if (!EnsurePorts(map))
                return "topology verification pending";
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
