using Verse;
using System.Collections.Generic;
using System.Linq;

namespace Xenomorphtype
{
    public class TraversalTopologyMapComponent : MapComponent
    {
        private const int WarmIntervalTicks = 120;
        private bool subscribed;
        private int warmPawnCursor;

        internal ClimbTopologyCache ClimbCache { get; } = new ClimbTopologyCache();
        internal InfiltrationTopologyCache InfiltrationCache { get; } = new InfiltrationTopologyCache();

        public TraversalTopologyMapComponent(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Subscribe();
        }

        public override void MapRemoved()
        {
            Unsubscribe();
            ClearCaches();
            base.MapRemoved();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % WarmIntervalTicks != map.uniqueID % WarmIntervalTicks)
            {
                return;
            }

            List<Pawn> capablePawns = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn?.GetClimberComp() != null)
                .ToList();
            if (capablePawns.Count == 0)
            {
                return;
            }

            Pawn sample = capablePawns[warmPawnCursor++ % capablePawns.Count];
            ClimbUtility.WarmTopologyFor(sample);
            InfiltrationUtility.WarmTopologyFor(sample);
        }

        internal static TraversalTopologyMapComponent For(Map map)
        {
            return map?.GetComponent<TraversalTopologyMapComponent>();
        }

        internal void ClearCaches()
        {
            ClimbCache.Clear();
            InfiltrationCache.Clear();
        }

        internal static void ClearAllMapCaches()
        {
            if (Current.Game?.Maps == null)
            {
                return;
            }
            foreach (Map loadedMap in Current.Game.Maps)
            {
                For(loadedMap)?.ClearCaches();
            }
        }

        private void Subscribe()
        {
            if (subscribed || map?.events == null)
            {
                return;
            }
            map.events.RegionsRoomsChanged += HandleRegionsRoomsChanged;
            map.events.RoofChanged += HandleRoofChanged;
            map.events.BuildingSpawned += HandleBuildingSpawned;
            map.events.BuildingDespawned += HandleBuildingDespawned;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || map?.events == null)
            {
                return;
            }
            map.events.RegionsRoomsChanged -= HandleRegionsRoomsChanged;
            map.events.RoofChanged -= HandleRoofChanged;
            map.events.BuildingSpawned -= HandleBuildingSpawned;
            map.events.BuildingDespawned -= HandleBuildingDespawned;
            subscribed = false;
        }

        private void HandleRegionsRoomsChanged()
        {
            ClimbUtility.NotifyRegionsRoomsChanged(map);
            InfiltrationUtility.NotifyRegionsRoomsChanged(map);
        }

        private void HandleRoofChanged(IntVec3 cell)
        {
            ClimbUtility.NotifyRoofChanged(map, cell);
        }

        private void HandleBuildingSpawned(Building building)
        {
            InfiltrationUtility.NotifyBuildingSpawned(building);
        }

        private void HandleBuildingDespawned(Building building)
        {
            InfiltrationUtility.NotifyBuildingDespawned(building, map);
        }
    }
}
