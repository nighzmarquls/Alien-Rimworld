using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    // Derived, mission-local data. Native map events reconcile changes behind the scan cursor.
    internal sealed class NemesisHarvestIndex
    {
        internal const int SectorSize = 16;
        // A fixed 5x5 neighborhood, excluding the worker's occupied cell.
        private static readonly IntVec3[] NearbyOffsets = Enumerable.Range(-2, 5)
            .SelectMany(x => Enumerable.Range(-2, 5).Select(z => new IntVec3(x, 0, z)))
            .Where(cell => cell.x != 0 || cell.z != 0)
            .OrderBy(cell => cell.x * cell.x + cell.z * cell.z).ToArray();
        private readonly Map map;
        private readonly List<QueenIngestibleResourceDef> resources;
        private readonly Dictionary<TerrainDef, bool> terrainEligibility = new Dictionary<TerrainDef, bool>();
        private readonly Dictionary<ThingDef, bool> plantEligibility = new Dictionary<ThingDef, bool>();
        private readonly HashSet<int> dirty = new HashSet<int>();
        private readonly HashSet<int> plantCells = new HashSet<int>();
        private readonly Queue<Thing> plantsToInspect;
        private readonly Dictionary<int, HashSet<int>> sectors = new Dictionary<int, HashSet<int>>();
        private readonly int sectorsWide;
        private readonly int[] sectorOrder;
        private readonly List<Pawn> consumers;
        private readonly Rot4 entrySide;
        private int cursor;
        internal int CandidateCount { get; private set; }
        internal int CellsExamined { get; private set; }
        internal bool ScanComplete => cursor >= map.cellIndices.NumGridCells && plantsToInspect.Count == 0 && dirty.Count == 0;
        internal bool Exhausted => ScanComplete && CandidateCount == 0;

        internal NemesisHarvestIndex(Map map, IEnumerable<Pawn> members, IntVec3 entry)
        {
            this.map = map;
            plantsToInspect = new Queue<Thing>(map.listerThings.ThingsInGroup(ThingRequestGroup.Plant));
            consumers = members.Where(pawn => pawn.GetComp<CompJellyMaker>() != null).ToList();
            resources = consumers.SelectMany(pawn => pawn.GetComp<CompJellyMaker>()?.Resources
                .Where(entryResource => entryResource?.resourceDef != null && entryResource.Weight > 0f
                    && ResourceHelperUtility.Available(entryResource.resourceDef, pawn)).Select(entryResource => entryResource.resourceDef)
                ?? Enumerable.Empty<QueenIngestibleResourceDef>()).Distinct().ToList();
            sectorsWide = (map.Size.x + SectorSize - 1) / SectorSize;
            entrySide = CellRect.WholeMap(map).GetClosestEdge(entry);
            int sectorsHigh = (map.Size.z + SectorSize - 1) / SectorSize;
            sectorOrder = Enumerable.Range(0, sectorsWide * sectorsHigh)
                .OrderBy(sector => SectorCenter(sector).DistanceToSquared(entry)).ToArray();
        }

        internal void MarkDirty(IntVec3 cell)
        {
            if (cell.InBounds(map)) dirty.Add(map.cellIndices.CellToIndex(cell));
        }

        internal void Tick(int budget)
        {
            // One shared budget, including notifications, even under heavy terraforming.
            while (budget > 0 && dirty.Count > 0)
            {
                int index = dirty.First();
                dirty.Remove(index);
                Refresh(index);
                budget--;
            }
            // Native plant index supplies existing vegetation; eligibility work shares the
            // terrain budget. Spawn/despawn notifications cover changes to this snapshot.
            while (budget > 0 && plantsToInspect.Count > 0)
            {
                Thing plant = plantsToInspect.Dequeue();
                if (plant.Spawned && plant.Map == map && plant.Position.InBounds(map))
                    Refresh(map.cellIndices.CellToIndex(plant.Position));
                budget--;
            }
            while (budget-- > 0 && cursor < map.cellIndices.NumGridCells)
            {
                // Scan strips inward from the arrival side; no full-map sort or allocation.
                int width = entrySide.IsHorizontal ? map.Size.z : map.Size.x;
                int depth = cursor / width;
                int cross = cursor++ % width;
                IntVec3 cell = entrySide == Rot4.West ? new IntVec3(depth, 0, cross)
                    : entrySide == Rot4.East ? new IntVec3(map.Size.x - 1 - depth, 0, cross)
                    : entrySide == Rot4.South ? new IntVec3(cross, 0, depth)
                    : new IntVec3(cross, 0, map.Size.z - 1 - depth);
                Refresh(map.cellIndices.CellToIndex(cell), inspectPlant: false);
            }
        }

        private void Refresh(int index, bool inspectPlant = true)
        {
            CellsExamined++;
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            TerrainDef terrain = map.terrainGrid.TerrainAt(index);
            if (!terrainEligibility.TryGetValue(terrain, out bool eligible))
            {
                var evaluation = ResourceHelperUtility.EvaluateTerrain(resources, terrain);
                eligible = !evaluation.conflict && evaluation.values.Any(value => value.Value > 0f);
                terrainEligibility.Add(terrain, eligible);
            }
            if (inspectPlant)
            {
                Plant plant = cell.GetPlant(map);
                if (plant != null)
                {
                    if (!plantEligibility.TryGetValue(plant.def, out bool plantAllowed))
                    {
                        plantAllowed = consumers.Any(pawn => ResourceHelperUtility.CanConvertDefinition(plant.def,
                            pawn.GetComp<CompJellyMaker>().Resources, pawn));
                        plantEligibility.Add(plant.def, plantAllowed);
                    }
                    if (plantAllowed) plantCells.Add(index);
                    else plantCells.Remove(index);
                }
                else plantCells.Remove(index);
            }
            eligible |= plantCells.Contains(index);
            int sector = SectorFor(cell);
            if (!sectors.TryGetValue(sector, out HashSet<int> cells))
            {
                if (!eligible) return;
                sectors.Add(sector, cells = new HashSet<int>());
            }
            if (eligible) { if (cells.Add(index)) CandidateCount++; }
            else if (cells.Remove(index)) CandidateCount--;
            if (cells.Count == 0) sectors.Remove(sector);
        }

        private int SectorFor(IntVec3 cell) => cell.x / SectorSize + cell.z / SectorSize * sectorsWide;
        private IntVec3 SectorCenter(int sector) => new IntVec3((sector % sectorsWide) * SectorSize + SectorSize / 2,
            0, (sector / sectorsWide) * SectorSize + SectorSize / 2);

        // Bounded candidates and path tests per decision; exhausted/blocked sectors cannot trigger rescans.
        internal IEnumerable<IntVec3> Candidates(Pawn pawn, int offset)
        {
            foreach (IntVec3 delta in NearbyOffsets)
            {
                IntVec3 cell = new IntVec3(pawn.Position.x + delta.x, 0, pawn.Position.z + delta.z);
                if (cell.InBounds(map) && sectors.TryGetValue(SectorFor(cell), out HashSet<int> cells)
                    && cells.Contains(map.cellIndices.CellToIndex(cell))) yield return cell;
            }
            foreach (IntVec3 cell in SectorCandidates(offset)
                .Where(cell => System.Math.Abs(cell.x - pawn.Position.x) > 2 || System.Math.Abs(cell.z - pawn.Position.z) > 2)
                .OrderBy(cell => cell.DistanceToSquared(pawn.Position))) yield return cell;
        }

        private IEnumerable<IntVec3> SectorCandidates(int offset)
        {
            // Rotate through every sector, including blocked ones. A blocked entrance sector
            // cannot starve the rest of the map. Cells rotate on subsequent complete passes.
            for (int step = 0; step < 4 && step < sectorOrder.Length; step++)
            {
                int sector = sectorOrder[(offset * 4 + step) % sectorOrder.Length];
                if (!sectors.TryGetValue(sector, out HashSet<int> cells)) continue;
                int cellOffset = (offset / sectorOrder.Length * 32) % cells.Count;
                foreach (int index in cells.Skip(cellOffset).Concat(cells.Take(cellOffset)).Take(32))
                    yield return map.cellIndices.IndexToCell(index);
            }
        }
    }

    public sealed class MapComponent_NemesisHarvest : MapComponent
    {
        private LordJob_NemesisHarvest owner;
        private NemesisHarvestIndex index;
        private bool completed;
        public bool Completed => completed;
        internal NemesisHarvestIndex Index => index;
        public MapComponent_NemesisHarvest(Map map) : base(map) { }

        internal void Begin(LordJob_NemesisHarvest mission, IEnumerable<Pawn> members, IntVec3 entry)
        {
            if (owner == mission && index != null) return;
            End();
            owner = mission;
            index = new NemesisHarvestIndex(map, members, entry);
            map.events.TerrainChanged += TerrainChanged;
            map.thingListChangedCallbacks.onThingAdded += ThingChanged;
            map.thingListChangedCallbacks.onThingRemoved += ThingChanged;
        }

        private void TerrainChanged(IntVec3 cell) => index?.MarkDirty(cell);
        private void ThingChanged(Thing thing) { if (thing is Plant) index?.MarkDirty(thing.Position); }

        internal void End(LordJob_NemesisHarvest mission = null)
        {
            if (mission != null && owner != mission) return;
            if (index != null)
            {
                map.events.TerrainChanged -= TerrainChanged;
                map.thingListChangedCallbacks.onThingAdded -= ThingChanged;
                map.thingListChangedCallbacks.onThingRemoved -= ThingChanged;
            }
            index = null;
            owner = null;
        }

        public override void MapComponentTick()
        {
            if (index == null) return;
            if (owner.Withdrawing || !map.lordManager.lords.Any(lord => lord.LordJob == owner
                && lord.ownedPawns.Any(pawn => pawn.Spawned && !pawn.Dead && !pawn.Downed))) { End(); return; }
            index.Tick(owner.Mission.workerSettings.harvestCellsPerTick);
        }

        internal bool Complete()
        {
            if (completed || index?.Exhausted != true || !XenoformingUtility.CompleteHarvestXenoforming(map)) return false;
            completed = true;
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref completed, "harvestXenoformingCompleted");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) End();
        }
    }
}
