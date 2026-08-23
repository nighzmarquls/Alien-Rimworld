using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class TraversalRouteUtility
    {
        private sealed class WalkingArea
        {
            public int Id;
            public readonly HashSet<Region> Regions = new HashSet<Region>();
            public readonly List<InfiltrationPort> Ports = new List<InfiltrationPort>();
            public IntVec3 OpenRoofAnchor = IntVec3.Invalid;
            public bool PortsDiscovered;
        }

        private sealed class AreaTransition
        {
            public WalkingArea Previous;
            public WalkingArea Next;
            public InfiltrationPort EntryPort;
            public InfiltrationPort ExitPort;
        }

        private sealed class SearchContext
        {
            public Pawn Pawn;
            public Map Map;
            public TraverseParms TraverseParms;
            public bool AllowWallClimb;
            public readonly HashSet<InfiltrationNetworkComponent> Components = new HashSet<InfiltrationNetworkComponent>();
            public readonly HashSet<InfiltrationNetworkComponent> DiscoveredComponents = new HashSet<InfiltrationNetworkComponent>();
            public readonly HashSet<Region> RelevantRegions = new HashSet<Region>();
            public readonly Dictionary<InfiltrationNetworkComponent, IReadOnlyList<InfiltrationPort>> PortsByComponent = new Dictionary<InfiltrationNetworkComponent, IReadOnlyList<InfiltrationPort>>();
            public readonly List<WalkingArea> Areas = new List<WalkingArea>();
            public readonly Dictionary<Region, WalkingArea> AreaByRegion = new Dictionary<Region, WalkingArea>();
            public readonly HashSet<WalkingArea> StartAreas = new HashSet<WalkingArea>();
            public readonly HashSet<WalkingArea> GoalAreas = new HashSet<WalkingArea>();
        }

        internal static bool CanReach(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger,
            TraverseMode mode, bool allowWallClimb)
        {
            SearchContext context = TryPrepareSearch(pawn, destination, pathEndMode, maxDanger, mode, allowWallClimb);
            if (context == null || context.StartAreas.Overlaps(context.GoalAreas))
            {
                return false;
            }

            ExploreAreas(context, context.StartAreas, context.GoalAreas, recordParents: false,
                out HashSet<WalkingArea> startVisited, out _, out _, out WalkingArea reachedGoal);
            if (reachedGoal != null || !allowWallClimb)
            {
                return reachedGoal != null;
            }
            bool startHasOpenRoof = startVisited.Any(area => area.OpenRoofAnchor.IsValid);
            if (!startHasOpenRoof)
            {
                return false;
            }

            ExploreAreas(context, context.GoalAreas, startVisited, recordParents: false,
                out HashSet<WalkingArea> goalVisited, out _, out _, out WalkingArea reachedStart);
            if (reachedStart != null)
            {
                return true;
            }

            bool startHasOpenRoofAfterInfiltration = startVisited.Any(area =>
                !context.StartAreas.Contains(area) && area.OpenRoofAnchor.IsValid);
            bool goalHasOpenRoof = goalVisited.Any(area => area.OpenRoofAnchor.IsValid);
            bool goalHasOpenRoofAfterInfiltration = goalVisited.Any(area =>
                !context.GoalAreas.Contains(area) && area.OpenRoofAnchor.IsValid);
            return startHasOpenRoofAfterInfiltration && goalHasOpenRoof ||
                startHasOpenRoof && goalHasOpenRoofAfterInfiltration;
        }

        internal static bool TryBuildRoute(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode, Danger maxDanger,
            bool allowWallClimb, out List<TraversalLeg> legs)
        {
            legs = new List<TraversalLeg>();
            SearchContext context = TryPrepareSearch(pawn, destination, pathEndMode, maxDanger, TraverseMode.ByPawn, allowWallClimb);
            if (context == null || context.StartAreas.Overlaps(context.GoalAreas))
            {
                return false;
            }

            ExploreAreas(context, context.StartAreas, context.GoalAreas, recordParents: true,
                out HashSet<WalkingArea> startVisited, out List<WalkingArea> startVisitOrder,
                out Dictionary<WalkingArea, AreaTransition> startParents, out WalkingArea directGoal);

            List<AreaTransition> transitions;
            WalkingArea startOpen = null;
            WalkingArea goalOpen = null;
            if (directGoal != null)
            {
                transitions = ReconstructForwardTransitions(directGoal, context.StartAreas, startParents);
            }
            else
            {
                if (!allowWallClimb || (startOpen = startVisitOrder.FirstOrDefault(area => area.OpenRoofAnchor.IsValid)) == null)
                {
                    return false;
                }

                ExploreAreas(context, context.GoalAreas, startVisited, recordParents: true,
                    out _, out List<WalkingArea> goalVisitOrder,
                    out Dictionary<WalkingArea, AreaTransition> goalParents, out WalkingArea intersectedStart);
                if (intersectedStart != null)
                {
                    transitions = ReconstructForwardTransitions(intersectedStart, context.StartAreas, startParents);
                    transitions.AddRange(ReconstructReverseTransitions(intersectedStart, context.GoalAreas, goalParents));
                }
                else
                {
                    WalkingArea startOpenAfterInfiltration = startVisitOrder.FirstOrDefault(area =>
                        !context.StartAreas.Contains(area) && area.OpenRoofAnchor.IsValid);
                    WalkingArea goalOpenAfterInfiltration = goalVisitOrder.FirstOrDefault(area =>
                        !context.GoalAreas.Contains(area) && area.OpenRoofAnchor.IsValid);
                    if (startOpenAfterInfiltration != null)
                    {
                        startOpen = startOpenAfterInfiltration;
                        goalOpen = goalVisitOrder.FirstOrDefault(area => area.OpenRoofAnchor.IsValid);
                    }
                    else
                    {
                        goalOpen = goalOpenAfterInfiltration;
                    }
                    if (goalOpen == null)
                    {
                        return false;
                    }
                    transitions = ReconstructForwardTransitions(startOpen, context.StartAreas, startParents);
                    transitions.AddRange(ReconstructReverseTransitions(goalOpen, context.GoalAreas, goalParents));
                }
            }

            foreach (AreaTransition transition in transitions)
            {
                InfiltrationPort entry = transition.EntryPort;
                InfiltrationPort exit = transition.ExitPort;
                legs.Add(TraversalLeg.Infiltration(entry.AccessCell, exit.AccessCell, entry.Component.ProviderKey,
                    entry.Component.CategoryKey, entry.Endpoint.thingIDNumber, exit.Endpoint.thingIDNumber));
            }

            if (startOpen != null && goalOpen != null)
            {
                int insertionIndex = ReconstructForwardTransitions(startOpen, context.StartAreas, startParents).Count;
                if (!ClimbUtility.TryBuildWallTraversalLegs(pawn, startOpen.OpenRoofAnchor, goalOpen.OpenRoofAnchor, out List<TraversalLeg> wallLegs))
                {
                    return false;
                }
                legs.InsertRange(System.Math.Min(insertionIndex, legs.Count), wallLegs);
            }
            return legs.Count > 0;
        }

        private static SearchContext TryPrepareSearch(Pawn pawn, LocalTargetInfo destination, PathEndMode pathEndMode,
            Danger maxDanger, TraverseMode mode, bool allowWallClimb)
        {
            if (pawn?.Map == null || !pawn.Spawned || !destination.IsValid || !destination.Cell.InBounds(pawn.Map) ||
                (destination.HasThing && (!destination.Thing.Spawned || destination.Thing.Map != pawn.Map)))
            {
                return null;
            }

            IReadOnlyList<InfiltrationNetworkComponent> availableComponents = InfiltrationUtility.GetAllComponents(pawn.Map);
            if (availableComponents.Count == 0)
            {
                return null;
            }

            TraverseParms traverseParms = TraverseParms.For(pawn, maxDanger, mode, canBashDoors: false,
                alwaysUseAvoidGrid: false, canBashFences: false);
            Region startRegion = pawn.Position.GetRegion(pawn.Map, RegionType.Set_Passable);
            TargetInfo resolvedTarget = GenPath.ResolvePathMode(pawn, destination.ToTargetInfo(pawn.Map), ref pathEndMode);
            LocalTargetInfo resolvedLocalTarget = resolvedTarget.HasThing
                ? new LocalTargetInfo(resolvedTarget.Thing)
                : new LocalTargetInfo(resolvedTarget.Cell);
            List<Region> targetRegions = new List<Region>();
            if (pathEndMode == PathEndMode.OnCell)
            {
                Region targetRegion = resolvedLocalTarget.Cell.GetRegion(pawn.Map, RegionType.Set_Passable);
                if (targetRegion != null && targetRegion.Allows(traverseParms, isDestination: true))
                {
                    targetRegions.Add(targetRegion);
                }
            }
            else
            {
                TouchPathEndModeUtility.AddAllowedAdjacentRegions(resolvedLocalTarget, traverseParms, pawn.Map, targetRegions);
            }
            targetRegions = targetRegions.Distinct().ToList();
            if (startRegion == null || targetRegions.Count == 0)
            {
                return null;
            }

            if (!pawn.Map.regionDirtyer.AnyDirty)
            {
                bool startCanReachPort = InfiltrationUtility.RegionCanReachInfiltrationPort(pawn.Map, startRegion);
                bool targetCanReachPort = targetRegions.Any(region => InfiltrationUtility.RegionCanReachInfiltrationPort(pawn.Map, region));
                bool startPossible = startCanReachPort || allowWallClimb && ClimbUtility.RegionCanReachOpenRoof(pawn.Map, startRegion);
                bool targetPossible = targetCanReachPort || targetRegions.Any(region =>
                    allowWallClimb && ClimbUtility.RegionCanReachOpenRoof(pawn.Map, region));
                if (!startPossible || !targetPossible || allowWallClimb && !startCanReachPort && !targetCanReachPort)
                {
                    return null;
                }
            }

            SearchContext context = new SearchContext
            {
                Pawn = pawn,
                Map = pawn.Map,
                TraverseParms = traverseParms,
                AllowWallClimb = allowWallClimb
            };
            foreach (InfiltrationNetworkComponent component in availableComponents)
            {
                if (InfiltrationUtility.CanPawnTraverseNetwork(pawn, component.ProviderKey, component.CategoryKey))
                {
                    context.Components.Add(component);
                    IReadOnlyList<InfiltrationPort> ports = InfiltrationUtility.GetPorts(pawn.Map, component);
                    context.PortsByComponent[component] = ports;
                    foreach (InfiltrationPort port in ports)
                    {
                        context.RelevantRegions.Add(port.Region);
                    }
                }
            }
            if (context.Components.Count == 0)
            {
                return null;
            }

            context.RelevantRegions.Add(startRegion);
            context.RelevantRegions.UnionWith(targetRegions);
            context.StartAreas.Add(GetOrCreateWalkingArea(context, startRegion));
            foreach (Region targetRegion in targetRegions)
            {
                context.GoalAreas.Add(GetOrCreateWalkingArea(context, targetRegion));
            }
            return context;
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
                if (context.RelevantRegions.Contains(region))
                {
                    area.Regions.Add(region);
                }
                if (context.AllowWallClimb && !area.OpenRoofAnchor.IsValid)
                {
                    ClimbUtility.TryGetOpenRoofAnchor(context.Map, region, out area.OpenRoofAnchor);
                }
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
            context.Areas.Add(area);
            return area;
        }

        private static void DiscoverAreaPorts(SearchContext context, WalkingArea area)
        {
            if (area.PortsDiscovered)
            {
                return;
            }
            area.PortsDiscovered = true;
            foreach (Region region in area.Regions)
            {
                foreach (InfiltrationPort candidate in InfiltrationUtility.GetPorts(context.Map, region))
                {
                    if (!context.Components.Contains(candidate.Component) || !context.DiscoveredComponents.Add(candidate.Component))
                    {
                        continue;
                    }
                    foreach (InfiltrationPort port in context.PortsByComponent[candidate.Component])
                    {
                        WalkingArea portArea = GetOrCreateWalkingArea(context, port.Region);
                        if (!portArea.Ports.Any(existing => existing.Component == port.Component && existing.Endpoint == port.Endpoint &&
                            existing.AccessCell == port.AccessCell))
                        {
                            portArea.Ports.Add(port);
                        }
                    }
                }
            }
        }

        private static void ExploreAreas(SearchContext context, IEnumerable<WalkingArea> seeds,
            HashSet<WalkingArea> destinations, bool recordParents, out HashSet<WalkingArea> visited,
            out List<WalkingArea> visitOrder, out Dictionary<WalkingArea, AreaTransition> parents,
            out WalkingArea reachedDestination)
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
                foreach (AreaTransition transition in EnumerateTransitions(context, current))
                {
                    if (!visited.Add(transition.Next))
                    {
                        continue;
                    }
                    if (recordParents)
                    {
                        parents[transition.Next] = transition;
                    }
                    open.Enqueue(transition.Next);
                }
            }
        }

        private static IEnumerable<AreaTransition> EnumerateTransitions(SearchContext context, WalkingArea current)
        {
            HashSet<WalkingArea> yielded = new HashSet<WalkingArea>();
            foreach (InfiltrationPort entry in OrderedPorts(current.Ports))
            {
                foreach (InfiltrationPort exit in OrderedPorts(context.PortsByComponent[entry.Component]))
                {
                    WalkingArea next = context.AreaByRegion[exit.Region];
                    if (next != current && yielded.Add(next))
                    {
                        yield return new AreaTransition
                        {
                            Previous = current,
                            Next = next,
                            EntryPort = entry,
                            ExitPort = exit
                        };
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
                    EntryPort = outward.ExitPort,
                    ExitPort = outward.EntryPort
                });
                current = outward.Previous;
            }
            return transitions;
        }
    }
}
