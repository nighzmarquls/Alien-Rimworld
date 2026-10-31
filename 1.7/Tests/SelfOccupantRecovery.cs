// Fault-inject engine substitutes around the production containment/recovery classes.
// Real map registration, Scribe round-trips, and UI/job lifecycle still require in-game testing.
using System;
using System.Collections.Generic;
using Verse;
using Xenomorphtype;

internal static class SelfOccupantRecovery
{
    private static int assertions;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        assertions++;
        Console.WriteLine("PASS " + name);
    }

    private static (Fixture building, Pawn pawn, Map map) Setup(bool selected = false)
    {
        Find.Maps.Clear();
        Find.Selector.selected = null;
        Find.TickManager.TicksGame = 0;
        Current.Game = new Game();
        var map = new Map();
        Find.Maps.Add(map);
        var building = new Fixture { Spawned = true, Map = map, Position = new IntVec3(10) };
        var pawn = new Pawn { Spawned = true, Map = map, Position = new IntVec3(10) };
        if (selected) Find.Selector.selected = pawn;
        return (building, pawn, map);
    }

    public static void Main()
    {
        var s = Setup();
        Check(s.building.TryAssignOccupant(s.pawn), "unselected pawn enters same-tick container");
        Check(!s.pawn.Spawned && s.pawn.holdingOwner == s.building.GetDirectlyHeldThings(), "original pawn is owned, not orphaned");
        Check(Find.Selector.selected == null, "automatic hibernation does not select pawn or container");
        Check(s.building.TryAssignOccupant(s.pawn) && s.building.GetDirectlyHeldThings().Count == 1, "repeat assignment is idempotent");

        s = Setup(true);
        Check(s.building.TryAssignOccupant(s.pawn) && Find.Selector.selected == s.building, "selected pawn also enters successfully");

        s = Setup();
        s.building.RejectAcceptance = true;
        Check(s.building.TryAssignOccupant(s.pawn), "direct holder fallback handles rejected normal acceptance");

        s = Setup();
        s.building.GetDirectlyHeldThings().Reject = true;
        Check(!s.building.TryAssignOccupant(s.pawn) && s.pawn.Spawned && !s.pawn.Destroyed, "both insertion failures restore original pawn");
        s.building.Tick();
        Check(s.building.Destroyed && s.pawn.Spawned, "failed empty container cleanup cannot delete recovered pawn");

        s = Setup();
        s.building.ThrowBeforeAcceptance = true;
        Check(!s.building.TryAssignOccupant(s.pawn) && s.pawn.Spawned, "insertion exception restores original pawn");

        s = Setup();
        s.building.ThrowAfterInsertion = true;
        Check(s.building.TryAssignOccupant(s.pawn) && s.pawn.holdingOwner == s.building.GetDirectlyHeldThings(), "post-insertion callback exception preserves successful ownership");

        s = Setup();
        s.pawn.ThrowAfterDespawn = true;
        Check(!s.building.TryAssignOccupant(s.pawn) && s.pawn.Spawned, "exception after despawn also recovers pawn");

        s = Setup();
        s.map.AllowPlacement = false;
        s.building.GetDirectlyHeldThings().Reject = true;
        Check(!s.building.TryAssignOccupant(s.pawn), "failed placement reports failed containment");
        var recovery = Current.Game.GetComponent<GameComponent_SelfOccupantRecovery>();
        Check(recovery.GetDirectlyHeldThings().Contains(s.pawn) && !s.pawn.Destroyed, "unplaceable pawn is owned by saved recovery holder");
        int attempts = s.map.PlacementAttempts;
        s.building.Tick();
        Check(s.building.Destroyed && recovery.GetDirectlyHeldThings().Contains(s.pawn), "emergency custody outlives failed container");
        s.map.AllowPlacement = true;
        Find.TickManager.TicksGame = 599;
        recovery.GameComponentTick();
        Check(s.map.PlacementAttempts == attempts, "negative placement searches have bounded retry delay");
        Find.TickManager.TicksGame = 600;
        recovery.GameComponentTick();
        Check(s.pawn.Spawned && recovery.GetDirectlyHeldThings().Count == 0, "retry releases identical pawn when space becomes available");

        s = Setup();
        s.map.ThrowDuringPlacement = true;
        s.building.ThrowBeforeAcceptance = true;
        Check(!s.building.TryAssignOccupant(s.pawn), "placement exception does not escape containment");
        recovery = Current.Game.GetComponent<GameComponent_SelfOccupantRecovery>();
        Check(recovery.GetDirectlyHeldThings().Contains(s.pawn), "placement exception after owner removal reclaims pawn");
        s.map.ThrowDuringPlacement = false;
        Find.TickManager.TicksGame = 600;
        recovery.GameComponentTick();
        Check(s.pawn.Spawned, "reclaimed pawn can be placed on later retry");

        s = Setup();
        s.map.AllowPlacement = false;
        s.building.ThrowBeforeAcceptance = true;
        s.building.TryAssignOccupant(s.pawn);
        recovery = Current.Game.GetComponent<GameComponent_SelfOccupantRecovery>();
        Find.TickManager.TicksGame = 599;
        var secondPawn = new Pawn { Spawned = true, Map = s.map, Position = new IntVec3(12) };
        s.building.TryAssignOccupant(secondPawn);
        s.map.AllowPlacement = true;
        Find.TickManager.TicksGame = 600;
        recovery.GameComponentTick();
        Check(s.pawn.Spawned && secondPawn.Spawned, "new failures cannot postpone existing occupants' recovery");

        s = Setup();
        s.map.AllowPlacement = false;
        s.building.ThrowBeforeAcceptance = true;
        s.building.TryAssignOccupant(s.pawn);
        recovery = Current.Game.GetComponent<GameComponent_SelfOccupantRecovery>();
        Find.Maps.Clear();
        Find.TickManager.TicksGame = 600;
        recovery.GameComponentTick();
        Check(recovery.GetDirectlyHeldThings().Contains(s.pawn), "missing original map preserves pawn without moving it to another map");

        s = Setup();
        s.pawn.Spawned = false;
        s.pawn.Map = null;
        s.building.ThrowBeforeAcceptance = true;
        s.building.TryAssignOccupant(s.pawn);
        Check(s.pawn.Spawned && s.pawn.Map == s.map, "newly generated unspawned occupant recovers beside its container");

        s = Setup();
        s.building.Destroyed = true;
        Check(!s.building.TryAssignOccupant(s.pawn) && s.pawn.Spawned, "destroyed destination cannot despawn pawn");
        Console.WriteLine(assertions + " assertions passed.");
    }

    private sealed class Fixture : SelfOccupyingBuilding
    {
        public bool RejectAcceptance, ThrowBeforeAcceptance, ThrowAfterInsertion;
        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (ThrowBeforeAcceptance) throw new Exception("injected insertion failure");
            return !RejectAcceptance && base.TryAcceptThing(thing, allowSpecialEffects);
        }
        protected override void Notify_OccupantRegistered(Pawn pawn)
        {
            if (ThrowAfterInsertion) throw new Exception("injected registration failure");
        }
        public void Tick() => TickInterval(1);
    }
}

namespace Xenomorphtype
{
    public static class XMTUtility { public static bool IsXenomorph(Pawn pawn) => true; }
    public static class HiveColonistBarUtility { public static void NotifyContentsChanged() { } }
}
namespace UnityEngine { public class Texture2D { } }
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public interface IThingHolder { IThingHolder ParentHolder { get; } ThingOwner GetDirectlyHeldThings(); void GetChildHolders(List<IThingHolder> outChildren); }
    public interface ISuspendableThingHolder { bool IsContentsSuspended { get; } }
    public class UnsavedAttribute : Attribute { public UnsavedAttribute(bool value) { } }
    public struct IntVec3
    {
        public int value;
        public IntVec3(int value) { this.value = value; }
        public static IntVec3 Invalid => new IntVec3(-1);
        public bool InBounds(Map map) => value >= 0;
    }
    public class Map
    {
        public IntVec3 Center => new IntVec3(50);
        public bool AllowPlacement = true, ThrowDuringPlacement;
        public int PlacementAttempts;
    }
    public class Faction { public bool IsPlayer; }
    public class ThingDef { public string defName = "Fixture"; }
    public class Thing
    {
        public bool Spawned, Destroyed;
        public Map Map;
        public Map MapHeld => Map;
        public IntVec3 Position;
        public IntVec3 PositionHeld => Position;
        public ThingOwner holdingOwner;
        public Faction Faction;
        public ThingDef def = new ThingDef();
    }
    public class Pawn : Thing
    {
        public bool Dead, ThrowAfterDespawn;
        public int thingIDNumber = 1;
        public bool DeSpawnOrDeselect()
        {
            bool selected = Find.Selector.IsSelected(this);
            Spawned = false;
            Map = null;
            if (selected) Find.Selector.selected = null;
            if (ThrowAfterDespawn) throw new Exception("injected despawn failure");
            return selected;
        }
        public void SetFaction(Faction faction) => Faction = faction;
    }
    public class ThingOwner
    {
        protected readonly List<Thing> things = new List<Thing>();
        public bool Reject, dontTickContents;
        public int Count => things.Count;
        public Thing First => Count == 0 ? null : things[0];
        public bool Contains(Thing thing) => things.Contains(thing);
        public bool TryAdd(Thing thing, bool canMergeWithExistingStacks = true)
        {
            if (Reject || thing.Spawned || thing.holdingOwner != null) return false;
            things.Add(thing); thing.holdingOwner = this; return true;
        }
        public bool TryAddOrTransfer(Thing thing, bool canMergeWithExistingStacks = true)
        {
            if (Reject) return false;
            thing.holdingOwner?.Remove(thing);
            return TryAdd(thing, canMergeWithExistingStacks);
        }
        public void Remove(Thing thing) { things.Remove(thing); thing.holdingOwner = null; }
        public bool TryDrop(Thing thing, IntVec3 cell, Map map, ThingPlaceMode mode, out Thing result, bool playDropSound = true)
        {
            map.PlacementAttempts++;
            result = null;
            if (!map.AllowPlacement) return false;
            Remove(thing);
            if (map.ThrowDuringPlacement) throw new Exception("injected SpawnSetup failure");
            thing.Map = map; thing.Position = cell; thing.Spawned = true; result = thing; return true;
        }
    }
    public class ThingOwner<T> : ThingOwner where T : Thing { public ThingOwner(IThingHolder owner) { } }
    public static class ThingOwnerUtility { public static void AppendThingHoldersFromThings(List<IThingHolder> list, ThingOwner contents) { } }
    public enum ThingPlaceMode { Near }
    public class Game
    {
        private readonly Dictionary<Type, GameComponent> components = new Dictionary<Type, GameComponent>();
        public T GetComponent<T>() where T : GameComponent
        {
            if (!components.ContainsKey(typeof(T))) components[typeof(T)] = (T)Activator.CreateInstance(typeof(T), this);
            return (T)components[typeof(T)];
        }
    }
    public static class Current { public static Game Game = new Game(); }
    public class GameComponent { public virtual void ExposeData() { } public virtual void GameComponentTick() { } }
    public class Selector
    {
        public object selected;
        public bool IsSelected(object thing) => selected == thing;
        public void Select(object thing, bool playSound = false, bool forceDesignatorDeselect = false) => selected = thing;
    }
    public class TickManager { public int TicksGame; }
    public static class Find
    {
        public static Selector Selector = new Selector();
        public static TickManager TickManager = new TickManager();
        public static List<Map> Maps = new List<Map>();
    }
    public static class CellFinder { public static IntVec3 RandomCell(Map map) => map.Center; }
    public static class Prefs { public static bool DevMode; }
    public static class Log
    {
        public static void Warning(string text) { }
        public static void Error(string text) { }
        public static void ErrorOnce(string text, int id) { }
    }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public enum LookMode { Deep }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) { } }
    public static class Scribe_Values { public static void Look<T>(ref T value, string key, T defaultValue) { } }
    public static class Scribe_Deep { public static void Look<T>(ref T value, string key, params object[] args) { } }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string key, LookMode mode) { } }
    public class Gizmo { }
    public class Command_Action : Gizmo { public Action action; public string defaultLabel, defaultDesc; public object hotKey, icon; }
    public static class KeyBindingDefOf { public static object Misc8; }
    public static class ContentFinder<T> { public static T Get(string path) => default; }
    public static class Translation { public static string Translate(this string value) => value; }
}
namespace RimWorld
{
    public class Building_CryptosleepCasket : Thing, IThingHolder
    {
        private readonly ThingOwner contents = new ThingOwner();
        public Thing ContainedThing => contents.First;
        public IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) { }
        public virtual void ExposeData() { }
        public virtual void SpawnSetup(Map map, bool respawningAfterLoad) { Map = map; Spawned = true; }
        public virtual void Open() { }
        public virtual void EjectContents() { }
        public virtual bool Accepts(Thing thing) => true;
        public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true) => Accepts(thing) && contents.TryAddOrTransfer(thing);
        protected virtual void TickInterval(int delta) { }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
        public void Destroy() { Destroyed = true; Spawned = false; }
    }
}
