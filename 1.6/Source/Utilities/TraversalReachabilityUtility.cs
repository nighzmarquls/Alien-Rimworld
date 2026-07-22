using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class TraversalReachabilityUtility
    {
        public static bool IsTraversalPawn(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.Map != null &&
                XMTUtility.IsXenomorph(pawn) && pawn.GetClimberComp() != null;
        }

        public static Thing ClosestThingGlobalReachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet,
            PathEndMode peMode, TraverseParms traverseParms, float maxDistance, Predicate<Thing> validator,
            Func<Thing, float> priorityGetter, bool canLookInHaulableSources)
        {
            Thing result = GenClosest.ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParms,
                maxDistance, validator, priorityGetter, canLookInHaulableSources);
            if (result != null || !CanUseTraversalFallback(center, map, traverseParms))
            {
                return result;
            }

            return ClosestThingTraversalFallback(center, map, searchSet, peMode, traverseParms, maxDistance,
                validator, priorityGetter, canLookInHaulableSources);
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq,
            PathEndMode peMode, TraverseParms traverseParms, float maxDistance, Predicate<Thing> validator,
            IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax,
            bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions,
            bool lookInHaulSources)
        {
            Thing result = GenClosest.ClosestThingReachable(root, map, thingReq, peMode, traverseParms, maxDistance,
                validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch,
                traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            if (result != null || !CanUseTraversalFallback(root, map, traverseParms))
            {
                return result;
            }

            IEnumerable<Thing> searchSet = customGlobalSearchSet;
            if (searchSet == null)
            {
                searchSet = thingReq.IsUndefined
                    ? Enumerable.Empty<Thing>()
                    : map.listerThings.ThingsMatching(thingReq);
            }

            return ClosestThingTraversalFallback(root, map, searchSet, peMode, traverseParms, maxDistance,
                validator, null, lookInHaulSources);
        }

        public static bool CanReachHaulDestination(Reachability reachability, IntVec3 start,
            LocalTargetInfo destination, PathEndMode peMode, TraverseParms traverseParms)
        {
            if (reachability.CanReach(start, destination, peMode, traverseParms))
            {
                return true;
            }

            Pawn pawn = traverseParms.pawn;
            if (!IsTraversalPawn(pawn) || pawn.Map.reachability != reachability ||
                !destination.IsValid || !destination.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            return ClimbUtility.CanReachByWalkingOrClimb(pawn, destination, peMode, traverseParms.maxDanger,
                traverseParms.canBashDoors, traverseParms.canBashFences, traverseParms.mode);
        }

        private static bool CanUseTraversalFallback(IntVec3 root, Map map, TraverseParms traverseParms)
        {
            Pawn pawn = traverseParms.pawn;
            if (!IsTraversalPawn(pawn) || pawn.Map != map || !root.IsValid || !root.InBounds(map))
            {
                return false;
            }

            return root == pawn.Position || ClimbUtility.CanReachByWalkingOrClimb(pawn, root, PathEndMode.OnCell,
                traverseParms.maxDanger, traverseParms.canBashDoors, traverseParms.canBashFences, traverseParms.mode);
        }

        private static Thing ClosestThingTraversalFallback(IntVec3 center, Map map, IEnumerable<Thing> searchSet,
            PathEndMode peMode, TraverseParms traverseParms, float maxDistance, Predicate<Thing> validator,
            Func<Thing, float> priorityGetter, bool lookInHaulSources)
        {
            Pawn pawn = traverseParms.pawn;
            Dictionary<Region, bool> reachableByRegion = new Dictionary<Region, bool>();
            Predicate<Thing> traversalValidator = delegate (Thing thing)
            {
                if (thing == null || (validator != null && !validator(thing)))
                {
                    return false;
                }

                Thing spawned = thing.SpawnedParentOrMe;
                Region region = null;
                if (spawned != null && spawned.Spawned && spawned.Map == map && spawned.Position.Standable(map))
                {
                    region = spawned.Position.GetRegion(map, RegionType.Set_Passable);
                    if (region != null && reachableByRegion.TryGetValue(region, out bool cached))
                    {
                        return cached;
                    }
                }

                bool reachable = ClimbUtility.CanReachByWalkingOrClimb(pawn, thing, peMode, traverseParms.maxDanger,
                    traverseParms.canBashDoors, traverseParms.canBashFences, traverseParms.mode);
                if (region != null)
                {
                    reachableByRegion[region] = reachable;
                }
                return reachable;
            };

            return GenClosest.ClosestThing_Global(center, searchSet, maxDistance, traversalValidator,
                priorityGetter, lookInHaulSources);
        }
    }
}
