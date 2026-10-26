// Runs the production index and terrain matcher against a small engine seam.
// No game assemblies or generated tooling are placed in gameplay Assemblies/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;
using Xenomorphtype;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception(message);
    }

    private static void Main()
    {
        var profiles = XDocument.Load("1.7/Defs/EvoDefs/Resource_Conversions.xml").Root.Elements()
            .Where(element => (string)element.Attribute("ParentName") == "XMT_FertilityBase")
            .Select(element => new ResourceTerrainDef {
                minFertility = (float?)element.Element("minFertility") ?? 0f,
                maxFertility = (float?)element.Element("maxFertility") ?? float.MaxValue,
                useDriesToFertility = true,
                excludedAffordances = new List<TerrainAffordanceDef> { Resin }
            }).ToList();
        foreach (float fertility in new[] { 0.05f, 0.5f, 0.7f, 1f, 1.5f, 3f })
            Check(profiles.Count(profile => profile.Matches(new TerrainDef { fertility = fertility })) == 1,
                "Modded fertile terrain must match exactly one configured depletion profile: " + fertility);
        Check(!profiles.Any(profile => profile.Matches(new TerrainDef { fertility = 0f })), "Barren terrain must not convert");
        Check(!profiles.Any(profile => profile.Matches(new TerrainDef { fertility = 1f,
            affordances = new List<TerrainAffordanceDef> { Resin } })), "Configured resin exclusions must survive");
        Check(profiles.Any(profile => profile.Matches(new TerrainDef { fertility = 0f,
            driesTo = new TerrainDef { fertility = 0.7f } })), "Configured driesTo fertility must be used");
        var resource = new QueenIngestibleResourceDef { terrainProfiles = profiles };
        var maker = new CompJellyMaker { Resources = new List<ResourceConversionEntry> {
            new ResourceConversionEntry { resourceDef = resource } } };
        var pawn = new Pawn { maker = maker, Spawned = true };
        var fertile = new TerrainDef { fertility = 0.7f };
        var barren = new TerrainDef();
        foreach (Rot4 side in new[] { Rot4.West, Rot4.East, Rot4.South, Rot4.North })
        {
            var map = new Map(37, 21, fertile);
            var index = new NemesisHarvestIndex(map, new[] { pawn }, Entry(map, side));
            Check(!index.Exhausted, "An unscanned map is never exhausted");
            int previous = 0;
            while (index.CellsExamined < map.cellIndices.NumGridCells)
            {
                index.Tick(13);
                Check(index.CellsExamined - previous <= 13, "A tick must obey the shared scan budget");
                previous = index.CellsExamined;
            }
            Check(index.CandidateCount == map.cellIndices.NumGridCells,
                "Rectangular-map scan must cover each cell exactly once from every edge");
        }

        {
            var map = new Map(20, 20, barren);
            var cell = new IntVec3(10, 0, 10);
            map.plants.Add(cell, new Plant { Position = cell, Map = map, def = new ThingDef { convertible = true } });
            var index = new NemesisHarvestIndex(map, new[] { pawn }, new IntVec3(0, 0, 10));
            index.Tick(1);
            Check(index.CandidateCount == 1 && index.CellsExamined == 1,
                "Existing native plant-list candidates must use the shared incremental budget");
            while (!index.ScanComplete) index.Tick(13);
            Check(index.CandidateCount == 1 && index.CellsExamined == 401,
                "Terrain scan must preserve a plant indexed on barren ground");
        }

        {
            var map = new Map(320, 160, barren);
            int remote = map.cellIndices.CellToIndex(new IntVec3(315, 0, 155));
            map.terrainGrid.cells[remote] = fertile;
            var index = new NemesisHarvestIndex(map, new[] { pawn }, new IntVec3(0, 0, 20));
            while (index.CellsExamined < map.cellIndices.NumGridCells) index.Tick(256);
            Check(index.CandidateCount == 1, "A remote candidate must remain indexed");
            bool found = false;
            int examined = index.CellsExamined;
            for (int offset = 0; offset < 400; offset++)
                found |= index.Candidates(pawn, offset).Any(cell => map.cellIndices.CellToIndex(cell) == remote);
            Check(found, "Empty or blocked edge sectors must not starve a remote sector");
            Check(index.CellsExamined == examined, "Pawn candidate queries must never restart terrain scans");
            map.terrainGrid.cells[remote] = barren;
            index.MarkDirty(map.cellIndices.IndexToCell(remote));
            Check(!index.Exhausted, "Pending terrain reconciliation blocks completion");
            index.Tick(1);
            Check(index.Exhausted, "Depleting the final target must establish exhaustion");
            map.terrainGrid.cells[0] = fertile;
            index.MarkDirty(new IntVec3(0, 0, 0));
            Check(!index.Exhausted, "A change behind the scan cursor must invalidate exhaustion immediately");
            index.Tick(1);
            Check(index.CandidateCount == 1, "Changed fertile terrain must reenter the index");
            for (int i = 0; i < 100; i++) index.MarkDirty(map.cellIndices.IndexToCell(i));
            examined = index.CellsExamined;
            index.Tick(7);
            Check(index.CellsExamined - examined == 7, "Dirty notifications share the scan budget");
        }

        {
            var map = new Map(32, 32, barren);
            var mission = new LordJob_NemesisHarvest();
            var lord = new Lord { LordJob = mission, ownedPawns = new List<Pawn> { pawn } };
            map.lordManager.lords.Add(lord);
            var state = new MapComponent_NemesisHarvest(map);
            state.Begin(mission, lord.ownedPawns, new IntVec3(0, 0, 10));
            for (int i = 0; i < 4; i++) state.MapComponentTick();
            Check(state.Index.Exhausted, "Empty map must become exhausted after bounded indexing");
            var plant = new Plant { Position = new IntVec3(5, 0, 5), def = new ThingDef { convertible = true } };
            map.plants.Add(plant.Position, plant);
            map.thingListChangedCallbacks.onThingAdded(plant);
            Check(!state.Index.Exhausted, "Native plant-added callback must invalidate completion");
            state.MapComponentTick();
            Check(state.Index.CandidateCount == 1, "A plant alone on barren terrain is a target");
            map.plants.Remove(plant.Position);
            map.thingListChangedCallbacks.onThingRemoved(plant);
            state.MapComponentTick();
            Check(state.Complete() && !state.Complete() && XenoformingUtility.completions == 1,
                "Completion event must fire once");
            mission.Withdrawing = true;
            state.MapComponentTick();
            Check(state.Index == null && map.events.TerrainChanged == null
                && map.thingListChangedCallbacks.onThingAdded == null
                && map.thingListChangedCallbacks.onThingRemoved == null,
                "Withdrawal must discard the cache and unsubscribe native callbacks");
            mission.Withdrawing = false;
            state.Begin(mission, lord.ownedPawns, new IntVec3(0, 0, 10));
            Check(!state.Index.Exhausted && state.Index.CellsExamined == 0,
                "A new mission must build fresh derived state");
            var nextMission = new LordJob_NemesisHarvest();
            state.Begin(nextMission, lord.ownedPawns, new IntVec3(0, 0, 10));
            state.End(mission);
            Check(state.Index != null, "Cleanup of an older withdrawing lord must not discard a newer mission's index");
            state.End();
        }
        {
            var map = new Map(64, 48, fertile);
            pawn.Position = new IntVec3(15, 0, 15);
            var index = new NemesisHarvestIndex(map, new[] { pawn }, new IntVec3(0, 0, 15));
            while (!index.ScanComplete) index.Tick(256);
            int examined = index.CellsExamined;
            IntVec3[] candidates = index.Candidates(pawn, 5).ToArray();
            Check(candidates.Take(4).All(cell => cell.DistanceToSquared(pawn.Position) == 1),
                "Adjacent cardinal targets must precede diagonal and distant targets");
            Check(candidates.Take(24).Any(cell => cell.x == 16 && cell.z == 15),
                "Adjacent work across a sector boundary must not wait for sector rotation");
            Check(candidates.Length <= 152 && candidates.Distinct().Count() == candidates.Length,
                "Nearest-first selection must remain bounded and avoid duplicate candidates");
            Check(candidates.Select(cell => cell.DistanceToSquared(pawn.Position)).SequenceEqual(
                candidates.Select(cell => cell.DistanceToSquared(pawn.Position)).OrderBy(distance => distance)),
                "Candidates must prefer the smallest squared distance");
            Check(index.CellsExamined == examined, "Local preference must not trigger terrain scans");
            pawn.Position = new IntVec3(0, 0, 0);
            Check(index.Candidates(pawn, 5).All(cell => cell.InBounds(map)),
                "Neighborhood preference must stay inside map edges");
        }
        Console.WriteLine("PASS: " + checks + " harvesting index and terrain-profile checks");
    }

    private static readonly TerrainAffordanceDef Resin = new TerrainAffordanceDef();
    private static IntVec3 Entry(Map map, Rot4 side) => side == Rot4.West ? new IntVec3(0, 0, 10)
        : side == Rot4.East ? new IntVec3(map.Size.x - 1, 0, 10)
        : side == Rot4.South ? new IntVec3(18, 0, 0) : new IntVec3(18, 0, map.Size.z - 1);
}

namespace Verse
{
    public class Def { public string defName; public virtual IEnumerable<string> ConfigErrors() { yield break; } }
    public class TerrainAffordanceDef { }
    public class TerrainDef { public float fertility; public TerrainDef driesTo; public List<TerrainAffordanceDef> affordances; }
    public class ThingDef { public bool convertible; }
    public class Thing { public IntVec3 Position; public ThingDef def; public bool Spawned = true; public Map Map; }
    public class Pawn : Thing { public bool Dead, Downed; public CompJellyMaker maker;
        public T GetComp<T>() where T : class => maker as T; }
    public struct IntVec3
    {
        public int x, y, z;
        public IntVec3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public int DistanceToSquared(IntVec3 other) => (x-other.x)*(x-other.x)+(z-other.z)*(z-other.z);
    }
    public struct Rot4
    {
        private int value;
        public static readonly Rot4 North = new Rot4 { value = 0 }, East = new Rot4 { value = 1 },
            South = new Rot4 { value = 2 }, West = new Rot4 { value = 3 };
        public bool IsHorizontal => value == 1 || value == 3;
        public static bool operator ==(Rot4 a, Rot4 b) => a.value == b.value;
        public static bool operator !=(Rot4 a, Rot4 b) => a.value != b.value;
        public override bool Equals(object other) => other is Rot4 value && this == value;
        public override int GetHashCode() => value;
    }
    public static class Extensions
    {
        public static bool InBounds(this IntVec3 cell, Map map) => cell.x >= 0 && cell.z >= 0 && cell.x < map.Size.x && cell.z < map.Size.z;
        public static Plant GetPlant(this IntVec3 cell, Map map) => map.plants.TryGetValue(cell, out Plant plant) ? plant : null;
    }
    public struct CellRect
    {
        private Map map;
        public static CellRect WholeMap(Map map) => new CellRect { map = map };
        public Rot4 GetClosestEdge(IntVec3 cell) => cell.x == 0 ? Rot4.West : cell.x == map.Size.x - 1 ? Rot4.East
            : cell.z == 0 ? Rot4.South : Rot4.North;
    }
    public class CellIndices
    {
        public int width, NumGridCells;
        public int CellToIndex(IntVec3 cell) => cell.x + cell.z * width;
        public IntVec3 IndexToCell(int index) => new IntVec3(index % width, 0, index / width);
    }
    public class TerrainGrid { public TerrainDef[] cells; public TerrainDef TerrainAt(int index) => cells[index]; }
    public class MapEvents { public Action<IntVec3> TerrainChanged; }
    public class ThingListChangedCallbacks { public Action<Thing> onThingAdded, onThingRemoved; }
    public class Lord { public LordJob_NemesisHarvest LordJob; public List<Pawn> ownedPawns; }
    public class LordManager { public List<Lord> lords = new List<Lord>(); }
    public enum ThingRequestGroup { Plant }
    public class ListerThings { public Map map; public List<Thing> ThingsInGroup(ThingRequestGroup group) => map.plants.Values.Cast<Thing>().ToList(); }
    public class Map
    {
        public IntVec3 Size; public CellIndices cellIndices; public TerrainGrid terrainGrid;
        public MapEvents events = new MapEvents(); public ThingListChangedCallbacks thingListChangedCallbacks = new ThingListChangedCallbacks();
        public ListerThings listerThings;
        public Dictionary<IntVec3, Plant> plants = new Dictionary<IntVec3, Plant>(); public LordManager lordManager = new LordManager();
        public Map(int width, int height, TerrainDef terrain) {
            Size = new IntVec3(width, 0, height); cellIndices = new CellIndices { width = width, NumGridCells = width * height };
            listerThings = new ListerThings { map = this };
            terrainGrid = new TerrainGrid { cells = Enumerable.Repeat(terrain, width * height).ToArray() };
        }
    }
    public class MapComponent { protected Map map; public MapComponent(Map map) { this.map = map; }
        public virtual void MapComponentTick() { } public virtual void ExposeData() { } }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values { public static void Look(ref bool value, string label) { } }
}
namespace RimWorld { public class Plant : Thing { } }
namespace Xenomorphtype
{
    public class QueenIngestibleResourceDef { public List<ResourceTerrainDef> terrainProfiles; public float allocationWeight = 1f; }
    public class CompJellyMaker { public List<ResourceConversionEntry> Resources; }
    public static class ResourceHelperUtility
    {
        public class TerrainEvaluation { public bool conflict; public Dictionary<QueenIngestibleResourceDef, float> values = new Dictionary<QueenIngestibleResourceDef, float>(); }
        public static bool Available(QueenIngestibleResourceDef resource, Pawn pawn) => true;
        public static bool CanConvertDefinition(ThingDef def, List<ResourceConversionEntry> resources, Pawn pawn) => def.convertible;
        public static TerrainEvaluation EvaluateTerrain(IEnumerable<QueenIngestibleResourceDef> resources, TerrainDef terrain) {
            var result = new TerrainEvaluation();
            foreach (var resource in resources) if (resource.terrainProfiles.Any(profile => profile.Matches(terrain))) result.values.Add(resource, 1);
            return result;
        }
    }
    public class Settings { public int harvestCellsPerTick = 256; }
    public class Mission { public Settings workerSettings = new Settings(); }
    public class LordJob_NemesisHarvest { public bool Withdrawing; public Mission Mission = new Mission(); }
    public static class XenoformingUtility { public static int completions; public static bool CompleteHarvestXenoforming(Map map) { completions++; return true; } }
}
