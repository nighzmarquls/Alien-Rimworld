// Exercises the compiled production service against native WorldPawns collections.
// Minimal uninitialized trackers isolate registry behavior from map generation and the game UI.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xenomorphtype;

internal static class WorldActivitiesState
{
    private static int assertions;
    private static T Empty<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        assertions++;
        Console.WriteLine("PASS " + name);
    }
    private static void CheckNativeInventoryXml([CallerFilePath] string sourcePath = null)
    {
        var document = new XmlDocument();
        document.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath), "../Defs/HediffDefs/Hediffs_Part_Cosmetic.xml")));
        XmlNode entry = document.SelectSingleNode("Defs/HediffDef[defName='XMT_SetupConnor']/comps/li[@Class='Xenomorphtype.XMT_CompProperties_AssignLoadout']/inventory/*");
        typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Empty<PrefsData>());
        var parsed = new ThingDefCountClass();
        parsed.LoadDataFromXmlCustom(entry);
        var references = (System.Collections.IEnumerable)typeof(DirectXmlCrossRefLoader)
            .GetField("wantedRefs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        object wanted = references.Cast<object>().Last(r => ReferenceEquals(Field(r.GetType(), "wanter").GetValue(r), parsed));
        Check((string)Field(wanted.GetType(), "defName").GetValue(wanted) == "Weapon_GrenadeMolotov",
            "native inventory XML loader requests the core Molotov def without concatenating its count");
        Check(parsed.count == 1, "native inventory XML loader reads the requested Molotov count");
    }
    public static void Main()
    {
        Game game = Empty<Game>();
        World world = Empty<World>();
        FactionManager factions = Empty<FactionManager>();
        Field(typeof(FactionManager), "ofPlayer").SetValue(factions, Empty<Faction>());
        Field(typeof(World), "factionManager").SetValue(world, factions);
        WorldPawns worldPawns = new WorldPawns();
        Field(typeof(World), "worldPawns").SetValue(world, worldPawns);
        Field(typeof(Game), "worldInt").SetValue(game, world);
        Field(typeof(Game), "tickManager").SetValue(game, Empty<TickManager>());
        Current.Game = game;
        var state = new GameComponent_XMTWorldState(game);
        Pawn owner = new Pawn { health = Empty<Pawn_HealthTracker>(), thingIDNumber = 101 };
        owner.def = new ThingDef { defName = "XMT_TestHuman", race = new RaceProperties { intelligence = Intelligence.Humanlike } };
        owner.Name = new NameTriple("Test", "Test", "Pawn");
        Field(typeof(Pawn_HealthTracker), "healthState").SetValue(owner.health, PawnHealthState.Mobile);
        Field(typeof(Pawn_HealthTracker), "pawn").SetValue(owner.health, owner);
        var worldAlive = (HashSet<Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(worldPawns);
        worldAlive.Add(owner);
        state.Track(owner, "consumer-A");
        state.Track(owner, "consumer-A");
        state.Track(owner, "consumer-B");
        Check(state.Pawns.Count == 1 && state.Pawns[0].reasons.Count == 2, "retention is idempotent and supports two consumers");
        Check(worldPawns.ForcefullyKeptPawns.Contains(owner), "tracked world pawn is protected by native retention");
        state.Release(owner, "consumer-A");
        Check(state.Protects(owner) && worldPawns.ForcefullyKeptPawns.Contains(owner), "one consumer cannot release another consumer's pawn");
        state.Release(owner, "consumer-B");
        Check(!state.Protects(owner) && !worldPawns.ForcefullyKeptPawns.Contains(owner), "last consumer releases service-owned retention");
        worldPawns.ForcefullyKeptPawns.Add(owner);
        state.Track(owner, "external-coexistence");
        state.Release(owner, "external-coexistence");
        Check(worldPawns.ForcefullyKeptPawns.Contains(owner), "external retention survives release of our claim");
        state.Track(owner, "migrated-owner", adoptRetention: true);
        state.Release(owner, "migrated-owner");
        Check(!worldPawns.ForcefullyKeptPawns.Contains(owner), "migration takes ownership of deprecated retention");

        var childhood = new XMT_CompanionDef { role = "caretaker", defName = "XMT_TestChild" };
        var adulthood = new XMT_CompanionDef { role = "caretaker", defName = "XMT_TestAdult" };
        var explicitBio = new XMT_CompanionDef { role = "caretaker", defName = "XMT_TestBio" };
        state.RegisterCompanion(owner, childhood, 1);
        state.RegisterCompanion(owner, adulthood, 2);
        state.RegisterCompanion(owner, childhood, 1);
        Check(state.Companions.Count == 1 && state.Companions[0].definition == adulthood, "adulthood wins over childhood in either registration order");
        state.RegisterCompanion(owner, explicitBio, 3);
        state.RegisterCompanion(owner, adulthood, 2);
        Check(state.Companions.Count == 1 && state.Companions[0].definition == explicitBio, "explicit bio wins before any companion is generated");
        Check(state.Companions[0].companion == null, "registration resolves priority without eagerly creating pawns");
        state.RegisterCompanion(owner, new XMT_CompanionDef { role = "sibling" }, 1);
        Check(state.Companions.Count == 2, "different companion roles coexist for one owner");
        state.Release(owner, "companion-owner:caretaker");
        Check(state.Protects(owner), "ending one relationship preserves the other relationship's retention");
        Check(!state.BlockWorldReuse(owner), "a free owner can reappear naturally in a generated group");
        state.Track(owner, "companion:fixture");
        Check(state.BlockWorldReuse(owner), "a companion cannot be independently redressed into an unrelated raid");
        state.Release(owner, "companion:fixture");
        state.Track(owner, "captured", Empty<Faction>());
        Check(state.BlockWorldReuse(owner), "captured world pawns are excluded from ordinary pawn reuse");
        state.Release(owner, "captured");
        Check(!state.BlockWorldReuse(owner), "releasing custody restores ordinary owner reuse");

        explicitBio.friendlyResponse = new XMT_ResponseDef { delayDays = new IntRange(1, 120) };
        explicitBio.recoveryResponse = new XMT_ResponseDef { delayDays = new IntRange(1, 3) };
        Check(explicitBio.FriendlyDelay.max == 120 && explicitBio.RecoveryDelay.max == 3, "response delays supply profile defaults");
        explicitBio.friendlyArrivalDelayDays = new IntRange(2, 9);
        Check(explicitBio.FriendlyDelay.min == 2 && explicitBio.FriendlyDelay.max == 9, "companion-specific delays override response defaults");
        Check(explicitBio.retryCap == -1, "default retry cap is unlimited");
        Check(typeof(XMT_ResponseWorker).IsAssignableFrom(typeof(NemesisMissionWorker)), "nemesis workers share the response worker base");
        Check(typeof(XMT_CompanionSource).IsAssignableFrom(typeof(XMT_CompanionSource_Bio))
            && typeof(XMT_CompanionSource).IsAssignableFrom(typeof(XMT_CompanionSource_Generic)), "named and generic companions share a source contract");
        owner.ageTracker = Empty<Pawn_AgeTracker>();
        owner.mindState = Empty<Verse.AI.Pawn_MindState>();
        owner.relations = new Pawn_RelationsTracker(owner);
        Field(typeof(Pawn_HealthTracker), "healthState").SetValue(owner.health, PawnHealthState.Down);
        Check(XMT_PawnStateUtility.StructurallyValid(owner), "incapacitation does not make a pawn structurally invalid");
        Check(!XMT_PawnStateUtility.Available(owner, state), "a valid incapacitated pawn cannot deploy as a rescuer");
        owner.relations.DirectRelations.Add(null);
        Check(!XMT_PawnStateUtility.StructurallyValid(owner), "malformed native relations are detected by the shared validator");
        owner.relations.DirectRelations.Clear();
        Check(XMT_PawnStateUtility.StructuralInvalidity(owner, new PawnKindDef { race = new ThingDef() }) == "kind/race mismatch",
            "cleanup uses the same validator with kind-specific race checks");
        CheckNativeInventoryXml();
        var debugType = typeof(GameComponent_XMTWorldState).Assembly.GetType("Xenomorphtype.DebugActions_XMTWorldActivities", true);
        var roots = ((IEnumerable<LudeonTK.DebugActionNode>)debugType.GetMethod("WorldActivityNodes", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, null)).ToList();
        Check(roots.Select(n => n.label).SequenceEqual(new[] { "Companion", "Reprisal", "Player action" }),
            "native dev menu exposes separate companion, reprisal and player-action submenus");
        var fixtures = roots[0].childGetter().Single(n => n.label == "Pawn fixtures").childGetter();
        Check(fixtures.Any(n => n.label == "Retrieve world pawn...") && fixtures.Any(n => n.label == "Retrieve existing or create from bio..."),
            "companion fixtures expose both native world-pawn retrieval and exact-bio creation");
        Console.WriteLine("PASS " + assertions + " production registry assertions. Map execution and Scribe still require in-game validation.");
    }
}
