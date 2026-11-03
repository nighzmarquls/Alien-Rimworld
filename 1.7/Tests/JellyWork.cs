// Compile the production eligibility/selection helper against counted engine substitutes.
// Native regions, reservations, movement and save/load still require an in-game smoke test.
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Xenomorphtype;

internal static class JellyWorkTests
{
    private static int assertions;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        assertions++;
        Console.WriteLine("PASS " + name);
    }

    public static void Main()
    {
        var map = new Map();
        var player = new Faction { IsPlayer = true };
        var pawn = new Pawn { Map = map, Faction = player };
        var maker = new CompJellyMaker();
        var def = new ThingDef { category = ThingCategory.Item };
        maker.Inputs.settings.filter.AllowedThingDefs.Add(def);
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "empty map has no deliberate work");
        Check(ClimbUtility.Calls == 0, "negative gate performs no path queries");

        var first = new IntVec3(1);
        var second = new IntVec3(2);
        map.designationManager.designations.Add(new Designation { target = first });
        maker.convertible.Add(first);
        Check(JellyWorkUtility.HasDeliberateWork(pawn, maker), "designated terrain admits boredom override without stored items");
        Check(ClimbUtility.Calls == 0, "positive gate performs no path queries");
        pawn.workSettings.active = false;
        int reads = map.designationManager.Reads + map.listerThings.Reads;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "unassigned Cooking blocks override");
        Check(reads == map.designationManager.Reads + map.listerThings.Reads, "disabled Cooking rejects before enumerating candidates");
        pawn.workSettings.active = true;
        pawn.disabled = true;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "incapable Cooking blocks override");
        pawn.disabled = false;

        map.designationManager.designations.Add(new Designation { target = second });
        maker.convertible.Add(second);
        ClimbUtility.unreachable.Add(first);
        Check(JellyWorkUtility.TryFindDesignatedCell(pawn, maker, false, out var selected) && selected.Equals(second),
            "unreachable first designation cannot starve reachable second designation");
        Check(ClimbUtility.Calls == 2, "one reachability check per eligible designated candidate");
        FeralJobUtility.blockedCells.Add(first);
        FeralJobUtility.blockedCells.Add(second);
        ClimbUtility.Calls = 0;
        Check(!JellyWorkUtility.TryFindDesignatedCell(pawn, maker, false, out _), "unavailable designated cells rejected");
        Check(ClimbUtility.Calls == 0, "reservation/forbidden/area rejection precedes reachability");
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "unavailable designations do not bypass boredom");
        FeralJobUtility.blockedCells.Clear();

        var item = new Thing { Map = map, def = def, Position = second };
        maker.ingredients[second] = item;
        FeralJobUtility.blockedItems.Add(item);
        FeralJobUtility.blockedCells.Add(first);
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "reserved designated ingredient does not bypass boredom through its unreserved cell");
        Check(!JellyWorkUtility.TryFindDesignatedCell(pawn, maker, false, out _), "reserved designated ingredient is not selected");
        FeralJobUtility.blockedItems.Clear();
        FeralJobUtility.blockedCells.Clear();
        maker.ingredients.Clear();
        map.designationManager.designations.Clear();

        map.listerThings.items.Add(item);
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "loose undesignated item does not override boredom");
        map.haulDestinationManager.storage = new SlotGroup { parent = new Zone_Stockpile() };
        Check(JellyWorkUtility.HasDeliberateWork(pawn, maker), "allowed stocked ingredient overrides boredom");
        Check(ClimbUtility.Calls == 0, "stocked-input gate still performs no path queries");
        map.haulDestinationManager.storage.Settings.accept = false;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "stockpile rejects item under its current filter");
        map.haulDestinationManager.storage.Settings.accept = true;
        map.haulDestinationManager.storage.parent = new Thing { Faction = new Faction() };
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "foreign storage does not justify override");
        map.haulDestinationManager.storage.parent = new Thing { Faction = player };
        Check(JellyWorkUtility.HasDeliberateWork(pawn, maker), "same-faction storage building qualifies");
        FeralJobUtility.blockedItems.Add(item);
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "forbidden/reserved stored ingredient rejected");
        FeralJobUtility.blockedItems.Clear();
        maker.accept = false;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "maker filter/conversion rejection respected");
        maker.accept = true;
        item.Spawned = false;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "held or despawned input rejected");
        item.Spawned = true;
        item.Map = new Map();
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "cross-map input rejected");
        item.Map = map;
        maker.Inputs.settings.filter.AllowedThingDefs.Clear();
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "disallowed input definition is not enumerated");

        map.listerThings.items.Clear();
        map.designationManager.designations.Add(new Designation { target = second });
        XMTUtility.playerQueen = false;
        Check(!JellyWorkUtility.HasDeliberateWork(pawn, maker), "designation selection preserves player-queen prerequisite");
        XMTUtility.playerQueen = true;
        CompJellyMaker.light = false;
        ClimbUtility.Calls = 0;
        Check(!JellyWorkUtility.TryFindDesignatedCell(pawn, maker, true, out _), "feral-light requirement preserved");
        Check(ClimbUtility.Calls == 0, "light rejection precedes path check");
        CompJellyMaker.light = true;
        Check(JellyWorkUtility.TryFindDesignatedCell(pawn, maker, false, out _), "eligible designated target selected after conditions recover");

        Log.messages.Clear();
        JellyWorkUtility.HasDeliberateWork(pawn, maker);
        Check(Log.messages.Count == 0, "diagnostics silent with Log JobGiver disabled");
        XMTSettings.LogJobGiver = true;
        JellyWorkUtility.HasDeliberateWork(pawn, maker);
        Check(Log.messages.Count == 1 && Log.messages[0].Contains("[JobGiver][Jelly]"), "diagnostics use existing Log JobGiver setting");
        Console.WriteLine(assertions + " assertions passed.");
    }
}

namespace RimWorld
{
    public class WorkTypeDef { }
    public class Zone_Stockpile { }
    public class SlotGroup { public object parent; public StorageSettings Settings = new StorageSettings(); }
    public class StorageSettings { public ThingFilter filter = new ThingFilter(); public bool accept = true; public bool AllowedToAccept(Thing t) => accept; }
    public class ThingFilter { public List<ThingDef> AllowedThingDefs = new List<ThingDef>(); }
}
namespace Verse
{
    public enum ThingCategory { Item }
    public enum Danger { Deadly }
    public class ThingDef { public ThingCategory category; }
    public class Faction { public bool IsPlayer; }
    public struct IntVec3
    {
        public int value;
        public IntVec3(int value) { this.value = value; }
        public static IntVec3 Invalid => new IntVec3(-1);
    }
    public class Thing { public bool Spawned = true; public Map Map; public Faction Faction; public IntVec3 Position; public ThingDef def; }
    public class Pawn : Thing { public WorkSettings workSettings = new WorkSettings(); public bool disabled; public bool WorkTypeIsDisabled(WorkTypeDef def) => disabled; }
    public class WorkSettings { public bool active = true; public bool WorkIsActive(WorkTypeDef def) => active; }
    public class Map
    {
        public HaulDestinationManager haulDestinationManager = new HaulDestinationManager();
        public DesignationManager designationManager = new DesignationManager();
        public ListerThings listerThings = new ListerThings();
    }
    public class HaulDestinationManager { public SlotGroup storage; public SlotGroup SlotGroupAt(IntVec3 cell) => storage; }
    public class Designation { public LocalTargetInfo target; }
    public class DesignationManager
    {
        public int Reads; public List<Designation> designations = new List<Designation>();
        public IEnumerable<Designation> SpawnedDesignationsOfDef(object def) { Reads++; return designations; }
    }
    public class ListerThings
    {
        public int Reads; public List<Thing> items = new List<Thing>();
        public IEnumerable<Thing> ThingsOfDef(ThingDef def) { Reads++; return items.FindAll(t => t.def == def); }
    }
    public static class Log { public static List<string> messages = new List<string>(); public static void Message(string text) => messages.Add(text); }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch }
    public struct LocalTargetInfo { public IntVec3 Cell; public static implicit operator LocalTargetInfo(IntVec3 cell) => new LocalTargetInfo { Cell = cell }; }
}
namespace Xenomorphtype
{
    public static class XMTSettings { public static bool LogJobGiver; }
    public static class XenoWorkDefOf { public static WorkTypeDef Cooking = new WorkTypeDef(); public static object XMT_MakeJelly = new object(); }
    public static class InternalDefOf { public static ThingDef Starbeast_Jelly = new ThingDef(); }
    public static class XMTUtility { public static bool playerQueen = true; public static bool QueenIsPlayer() => playerQueen; }
    public class HiveInputSettings { public StorageSettings settings = new StorageSettings(); public StorageSettings GetStoreSettings() => settings; }
    public class CompJellyMaker
    {
        public bool accept = true; public static bool light = true;
        public HiveInputSettings Inputs = new HiveInputSettings();
        public HashSet<IntVec3> convertible = new HashSet<IntVec3>();
        public Dictionary<IntVec3, Thing> ingredients = new Dictionary<IntVec3, Thing>();
        public ThingDef GetJellyProduct() => InternalDefOf.Starbeast_Jelly;
        public bool CanConvertCell(IntVec3 cell) => convertible.Contains(cell);
        public bool CanMakeIntoJelly(Thing thing) => accept;
        public Thing FindIngredient(IntVec3 cell, Map map) => ingredients.TryGetValue(cell, out var item) ? item : null;
        public static bool IsProperLightLevelForFeralJelly(IntVec3 cell, Map map) => light;
    }
    public static class FeralJobUtility
    {
        public static HashSet<IntVec3> blockedCells = new HashSet<IntVec3>();
        public static HashSet<Thing> blockedItems = new HashSet<Thing>();
        public static bool IsPlaceAvailableForJobBy(Pawn pawn, IntVec3 cell) => !blockedCells.Contains(cell);
        public static bool IsThingAvailableForJobBy(Pawn pawn, Thing item) => !blockedItems.Contains(item);
    }
    public static class ClimbUtility
    {
        public static int Calls; public static HashSet<IntVec3> unreachable = new HashSet<IntVec3>();
        public static bool CanReachByWalkingOrClimb(Pawn pawn, LocalTargetInfo target, PathEndMode mode, Danger danger)
        { Calls++; return !unreachable.Contains(target.Cell); }
    }
}
