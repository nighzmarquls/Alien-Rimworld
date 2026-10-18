// Exercises the built implementation, with synthetic defs and no running save.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Xenomorphtype;

public static class HiveStructureChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int checks;
    private static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Check(string label, bool condition)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label); checks++;
    }
    private static ThingDef Item(string name, float mass)
    {
        ThingDef def = Raw<ThingDef>(); def.defName = name; def.category = ThingCategory.Item; def.thingClass = typeof(Thing);
        def.virtualDefs = new List<ThingDef>();
        def.statBases = new List<StatModifier> { new StatModifier { stat = StatDefOf.Mass, value = mass }, new StatModifier { stat = StatDefOf.Nutrition, value = 0f } };
        DefDatabase<ThingDef>.Add(def); return def;
    }
    private static int Convert(CompJellyMaker maker, QueenIngestibleResourceDef resource, float mass, bool commit)
    {
        var allocations = new List<ResourceHelperUtility.Allocation> { new ResourceHelperUtility.Allocation { resource = resource, mass = mass } };
        var products = (List<ThingDefCountClass>)typeof(CompJellyMaker).GetMethod("FinalCounts", Flags).Invoke(maker, new object[] { allocations, 1f, commit });
        return products.Sum(p => p.count);
    }
    private static CompJellyMaker Maker(ThingDef parent, QueenIngestibleResourceDef resource) => new CompJellyMaker {
        parent = new ThingWithComps { def = parent }, props = new CompJellyMakerProperties { conversionRate = 0.5f,
            resources = new List<ResourceConversionEntry> { new ResourceConversionEntry { resourceDef = resource } } }
    };
    private static void ArchitectThreadDispatch()
    {
        typeof(UnityData).GetField("mainThreadId", Flags).SetValue(null, System.Threading.Thread.CurrentThread.ManagedThreadId);
        var category = new DesignationCategoryDef { defName = "XMT_Hive" };
        category.AllResolvedDesignators.Add(Raw<Designator_HiveHost>());
        var callbacks = (List<Action>)typeof(LongEventHandler).GetField("toExecuteWhenFinished", Flags).GetValue(null);
        FieldInfo currentEvent = typeof(LongEventHandler).GetField("currentEvent", Flags);
        object previousEvent = currentEvent.GetValue(null);
        object loading = Activator.CreateInstance(currentEvent.FieldType, true);
        currentEvent.FieldType.GetField("doAsynchronously", Flags).SetValue(loading, true);
        currentEvent.FieldType.GetField("alreadyDisplayed", Flags).SetValue(loading, true);
        currentEvent.SetValue(null, loading);
        int previousCallbacks = callbacks.Count;
        Exception failure = null;
        var worker = new System.Threading.Thread(() => {
            try { HiveArchitectCommandsPatch.Postfix(category); }
            catch (Exception error) { failure = error; }
        });
        worker.Start(); worker.Join();
        if (failure != null) throw new Exception("Background architect setup failed", failure);
        Check("architect initialization defers background work", failure == null && callbacks.Count == previousCallbacks + 1);
        Check("background initialization does not mutate UI commands", category.AllResolvedDesignators.Count == 1);
        Action finish = callbacks[previousCallbacks]; callbacks.RemoveAt(previousCallbacks);
        currentEvent.SetValue(null, previousEvent);
        finish();
        Check("architect initialization completes on main thread", category.AllResolvedDesignators.Count == 0);
    }
    private static int nextBarPawn = 100000;
    private static Pawn BarPawn()
    {
        Pawn pawn = Raw<Pawn>();
        pawn.thingIDNumber = ++nextBarPawn;
        pawn.def = Raw<ThingDef>(); pawn.def.defName = "BarFixturePawn"; pawn.def.category = ThingCategory.Pawn;
        pawn.def.virtualDefs = new List<ThingDef>();
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(pawn, (sbyte)-1);
        return pawn;
    }
    private static void HoldForBar(Pawn pawn, ThingOwner<Thing> owner)
    {
        if (pawn.holdingOwner is ThingOwner<Thing> previous)
        {
            int index = previous.InnerListForReading.FindIndex(t => ReferenceEquals(t, pawn));
            if (index >= 0) previous.InnerListForReading.RemoveAt(index);
        }
        owner.InnerListForReading.Add(pawn);
        typeof(Thing).GetField("holdingOwner", Flags).SetValue(pawn, owner);
    }
    private static ThingOwner<Thing> CasketContents(Building_Casket casket)
    {
        var contents = new ThingOwner<Thing>(casket);
        AccessTools.Field(typeof(Building_Casket), "innerContainer").SetValue(casket, contents);
        return contents;
    }
    private static UIRoot fixtureBarUI;
    public static bool FixtureBarUI(ref UIRoot __result) { __result = fixtureBarUI; return false; }
    private static void ColonistBarContainment()
    {
        Pawn ordinary = BarPawn(), sleeping = BarPawn(), ambushing = BarPawn(), preserved = BarPawn(), queen = BarPawn();
        Check("ordinary colonist remains eligible for the bar", !HiveColonistBarUtility.ShouldHide(ordinary));
        var cocoon = new HibernationCocoon();
        HoldForBar(sleeping, CasketContents(cocoon));
        HoldForBar(ambushing, CasketContents(new AmbushTrap()));
        var preservation = new CompPawnPreservation { parent = new AcidPit() };
        HoldForBar(preserved, (ThingOwner<Thing>)preservation.GetDirectlyHeldThings());
        Check("hibernating colonist is hidden", HiveColonistBarUtility.ShouldHide(sleeping));
        Check("ambush-trap occupant is hidden", HiveColonistBarUtility.ShouldHide(ambushing));
        Check("preserved structure contributor is hidden", HiveColonistBarUtility.ShouldHide(preserved));
        var throne = (Building_Casket)RuntimeHelpers.GetUninitializedObject(typeof(CompQueen).Assembly.GetType("Xenomorphtype.EggSack"));
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(throne, (sbyte)-1);
        HoldForBar(queen, CasketContents(throne));
        Check("queen in her eggsack remains eligible", !HiveColonistBarUtility.ShouldHide(queen));
        var cryosleep = CasketContents(new Building_CryptosleepCasket());
        HoldForBar(ordinary, cryosleep);
        Check("ordinary releasable cryptosleep keeps vanilla eligibility", !HiveColonistBarUtility.ShouldHide(ordinary));
        // Preserve the same holder graph while minified and carried: classification follows ownership.
        var minified = new MinifiedThing();
        var inventoryPawn = BarPawn();
        var inventory = new Pawn_InventoryTracker(inventoryPawn);
        var shell = new ThingOwner<Thing>(minified);
        AccessTools.Field(typeof(MinifiedThing), "innerContainer").SetValue(minified, shell);
        shell.InnerListForReading.Add(cocoon);
        typeof(Thing).GetField("holdingOwner", Flags).SetValue(cocoon, shell);
        ((ThingOwner<Thing>)inventory.GetDirectlyHeldThings()).InnerListForReading.Add(minified);
        typeof(Thing).GetField("holdingOwner", Flags).SetValue(minified, inventory.GetDirectlyHeldThings());
        Check("inventory-held minified hibernation remains hidden", HiveColonistBarUtility.ShouldHide(sleeping));
        HoldForBar(sleeping, cryosleep);
        Check("transfer out of hibernation restores eligibility", !HiveColonistBarUtility.ShouldHide(sleeping));
        var candidates = new List<Pawn> { ordinary, sleeping, ambushing, preserved, queen };
        HiveColonistBarUtility.Filter(candidates);
        Check("bar candidates exclude hidden pawns before layout counting", candidates.Count == 3 && candidates.Contains(queen) && !candidates.Contains(preserved));
        MethodInfo recache = AccessTools.Method(typeof(ColonistBar), "CheckRecacheEntries");
        var instructions = PatchProcessor.GetOriginalInstructions(recache);
        var filtered = XMTColonistBarPatches.Transpiler(instructions).ToList();
        MethodInfo filteredSort = AccessTools.Method(typeof(HiveColonistBarUtility), nameof(HiveColonistBarUtility.FilterAndSort));
        Check("both map and caravan bar lists receive the containment filter", filtered.Count(i => i.Calls(filteredSort)) == 2);
        var harmony = new Harmony("XMT.Tests.ColonistBar");
        try
        {
            harmony.Patch(recache, transpiler: new HarmonyMethod(typeof(XMTColonistBarPatches), nameof(XMTColonistBarPatches.Transpiler)));
            Check("colonist-bar transpiler installs against the real native method", Harmony.GetPatchInfo(recache).Transpilers.Any(p => p.owner == harmony.Id));
            var ui = Raw<UIRoot_Play>(); ui.mapUI = Raw<MapInterface>(); ui.mapUI.colonistBar = Raw<ColonistBar>();
            fixtureBarUI = ui;
            harmony.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.UIRoot)), prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureBarUI)));
            HiveColonistBarUtility.NotifyContentsChanged();
            Check("containment changes invalidate the colonist-bar cache", (bool)AccessTools.Field(typeof(ColonistBar), "entriesDirty").GetValue(ui.mapUI.colonistBar));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
    private static List<WorkGiver> nestGivers;
    private static Pawn acidSearchPawn;
    public static bool FixtureForbiddenCare(Pawn pawn, ref bool __result)
    {
        if (pawn != acidSearchPawn) return true;
        __result = false; return false;
    }
    private static readonly Dictionary<WorkTypeDef, int> nestCalls = new Dictionary<WorkTypeDef, int>();
    public static bool FixtureEverWork(ref bool __result) { __result = true; return false; }
    public static bool FixtureWorkGivers(ref List<WorkGiver> __result) { __result = nestGivers; return false; }
    public static bool FixtureNestWork(WorkTypeDef workType, ref Job job, ref bool __result)
    {
        nestCalls.TryGetValue(workType, out int count); nestCalls[workType] = count + 1;
        job = null; __result = false; return false;
    }
    private static void AcidSearchEarlyOuts(Map map)
    {
        map.listerThings = Raw<ListerThings>();
        var byDef = new Dictionary<ThingDef, List<Thing>>();
        typeof(ListerThings).GetField("listsByDef", Flags).SetValue(map.listerThings, byDef);
        var pawn = Raw<Pawn>(); pawn.health = Raw<Pawn_HealthTracker>();
        acidSearchPawn = pawn;
        // Expansion state is absent in this standalone process; reservation managers remain real.
        var availability = new Harmony("XMT.Tests.AcidAvailability");
        availability.Patch(AccessTools.Method(typeof(ForbidUtility), "CaresAboutForbidden"),
            prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureForbiddenCare)));
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(pawn, (sbyte)0);
        var giver = new WorkGiver_AcidDisposal();
        Check("disposal uses the standard hauling base", giver is WorkGiver_Haul);
        Check("human hauling skips without a ready pit", giver.ShouldSkip(pawn));
        Check("no pit exposes no haul candidates", !giver.PotentialWorkThingsGlobal(pawn).Any());
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            if (AcidDisposalUtility.FindJob(pawn) != null) throw new Exception("Unexpected acid-disposal job");
        watch.Stop();
        // The synthetic map deliberately has no region/reachability grids: reaching a search would fail.
        Check("1000 failed nest queries never enter item/path search", AcidDisposalUtility.WaitingToRetry(pawn));
        Console.WriteLine("No-pit 1000-query elapsed ms: " + watch.Elapsed.TotalMilliseconds.ToString("F3"));
        ThingDef pitDef = Item("SearchAcidPit", 1f);
        pitDef.comps = new List<CompProperties> { new CompProperties_AcidPit() };
        var parent = new ThingWithComps { def = pitDef };
        var pit = new CompAcidPit { parent = parent, props = pitDef.comps[0] };
        typeof(ThingWithComps).GetField("comps", Flags).SetValue(parent, new List<ThingComp> { pit });
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(parent, (sbyte)0);
        byDef[pitDef] = new List<Thing> { parent };
        Check("available unreserved pit passes the early gate", AcidDisposalUtility.AnyReadyPit(pawn));
        map.physicalInteractionReservationManager.Reserve(Raw<Pawn>(), new Job(), parent);
        Check("reserved pit fails before searching items", giver.ShouldSkip(pawn) && !giver.PotentialWorkThingsGlobal(pawn).Any());
        map.physicalInteractionReservationManager.ReleaseAllForTarget(parent);
        typeof(CompAcidPit).GetField("processing", Flags).SetValue(pit, false);
        Check("disabled pit fails before searching items", giver.ShouldSkip(pawn));
        typeof(CompAcidPit).GetField("processing", Flags).SetValue(pit, true);
        ((ThingOwner<Thing>)pit.GetDirectlyHeldThings()).InnerListForReading.Add(new Thing());
        Check("busy pit fails before searching items", giver.ShouldSkip(pawn));
        ((ThingOwner<Thing>)pit.GetDirectlyHeldThings()).InnerListForReading.Clear();
        Check("pit is ready again before testing morph routing", AcidDisposalUtility.AnyReadyPit(pawn));
        var morph = new CompMatureMorph { parent = pawn };
        typeof(ThingWithComps).GetField("comps", Flags).SetValue(pawn, new List<ThingComp> { morph });
        Check("mature morphs never enter the human disposal scanner", giver.ShouldSkip(pawn) && !giver.PotentialWorkThingsGlobal(pawn).Any());
        pawn.workSettings = Raw<Pawn_WorkSettings>();
        var hauling = new WorkTypeDef { defName = "TestHauling" }; var growing = new WorkTypeDef { defName = "TestGrowing" };
        nestGivers = new List<WorkGiver>();
        for (int i = 0; i < 25; i++) nestGivers.Add(new WorkGiver_HaulGeneral { def = new WorkGiverDef { workType = hauling } });
        nestGivers.Add(new WorkGiver_HaulGeneral { def = new WorkGiverDef { workType = growing } });
        var harmony = new Harmony("XMT.Tests.NestWorkDispatch");
        try
        {
            harmony.Patch(typeof(Pawn_WorkSettings).GetProperty("EverWork").GetMethod, prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureEverWork)));
            harmony.Patch(typeof(Pawn_WorkSettings).GetProperty("WorkGiversInOrderNormal").GetMethod, prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureWorkGivers)));
            harmony.Patch(typeof(CompMatureMorph).GetMethod("GetNestJobByWorkType"), prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureNestWork)));
            Check("nest pass completes with no matching jobs", morph.GetTendNestJob() == null);
            Check("25 hauling workgivers cause one nest hauling attempt", nestCalls[hauling] == 1);
            Check("failed hauling still permits other nest work types", nestCalls[growing] == 1);
        }
        finally { harmony.UnpatchAll(harmony.Id); availability.UnpatchAll(availability.Id); }
    }
    private sealed class ProcessingCue : SubEffecter
    {
        public int ticks, cleanups;
        public ProcessingCue(Effecter effect) : base(new SubEffecterDef(), effect) { }
        public override void SubEffectTick(TargetInfo a, TargetInfo b) { ticks++; }
        public override void SubCleanup() { cleanups++; }
    }
    private static void AcidProcessingChecks(CompAcidPit pit)
    {
        var fuel = new CompRefuelable { parent = pit.parent, props = new CompProperties_Refuelable { fuelCapacity = 25f } };
        typeof(ThingWithComps).GetField("comps", Flags).SetValue(pit.parent, new List<ThingComp> { pit, fuel });
        var debtField = typeof(CompAcidPit).GetField("remainingCost", Flags);
        var acidField = typeof(CompAcidPit).GetField("acid", Flags);
        debtField.SetValue(pit, 25d); acidField.SetValue(pit, 0d);
        Check("processing ETA uses actual recharge cadence", pit.ProcessingTicksRemaining == 7500);
        Check("empty fuel reports additional jelly needed", pit.ProcessingNeedsFuel);
        AccessTools.FieldRefAccess<CompRefuelable, float>("fuel")(fuel) = 25f;
        Check("one fueling covers one fueling of digestion", !pit.ProcessingNeedsFuel);
        acidField.SetValue(pit, 12.5d);
        Check("stored acid shortens processing ETA", pit.ProcessingTicksRemaining == 3780);
        acidField.SetValue(pit, 0d); debtField.SetValue(pit, 50d);
        Check("oversized input estimates repeated fuelings", pit.ProcessingTicksRemaining == 15000 && pit.ProcessingNeedsFuel);
        Check("busy direct order rejects before inspecting pawn or map", AcidDisposalUtility.FindJobForPit(null, pit, out string reason) == null && !string.IsNullOrEmpty(reason));
        var effect = Raw<Effecter>();
        var cue = new ProcessingCue(effect);
        typeof(Effecter).GetField("children", Flags).SetValue(effect, new List<SubEffecter> { cue });
        typeof(CompAcidPit).GetField("processingEffect", Flags).SetValue(pit, effect);
        MethodInfo tick = typeof(CompAcidPit).GetMethod("TickProcessingEffect", Flags);
        tick.Invoke(pit, null);
        Check("fueled processing maintains the native effecter", cue.ticks == 1 && cue.cleanups == 0);
        AccessTools.FieldRefAccess<CompRefuelable, float>("fuel")(fuel) = 0f;
        tick.Invoke(pit, null);
        Check("fuel starvation cleans up processing effects", cue.cleanups == 1
            && typeof(CompAcidPit).GetField("processingEffect", Flags).GetValue(pit) == null);
        typeof(CompAcidPit).GetField("processingEffect", Flags).SetValue(pit, effect);
        pit.PostDeSpawn(null);
        Check("despawning cleans up processing effects", cue.cleanups == 2);
    }
    private static readonly Dictionary<Pawn, List<float>> sprayHits = new();
    private static readonly HashSet<IntVec3> sprayFilth = new();
    public static bool SprayImmunity(ref bool __result) { __result = false; return false; }
    public static bool SprayDamage(Thing __instance, DamageInfo dinfo, ref DamageWorker.DamageResult __result)
    {
        if (__instance is Pawn pawn && sprayHits.TryGetValue(pawn, out var hits)) hits.Add(dinfo.Amount);
        __result = new DamageWorker.DamageResult(); return false;
    }
    public static bool SprayFilth(IntVec3 c, ref bool __result) { sprayFilth.Add(c); __result = true; return false; }
    private static void AcidSprayChecks(Map map)
    {
        map.info = new MapInfo();
        typeof(MapInfo).GetField("sizeInt", Flags).SetValue(map.info, new IntVec3(12, 1, 12));
        map.cellIndices = new CellIndices(12, 12);
        map.edificeGrid = new EdificeGrid(map);
        map.thingGrid = new ThingGrid(map);
        var origin = new IntVec3(5, 0, 5);
        var east = new IntVec3(8, 0, 5);
        var cells = AcidPitSpray.AffectedCells(origin, east, map, 3f, 60f);
        Check("spray reaches three tiles forward", cells.Contains(east));
        Check("spray widens toward the front", cells.Contains(new IntVec3(7, 0, 6)) && cells.Contains(new IntVec3(7, 0, 4)));
        Check("spray excludes origin, rear, side, and fourth tile", !cells.Contains(origin) && !cells.Contains(new IntVec3(4, 0, 5))
            && !cells.Contains(new IntVec3(5, 0, 6)) && !cells.Contains(new IntVec3(9, 0, 5)));
        foreach (var direction in GenAdj.AdjacentCells)
            Check("cone can aim in direction " + direction, AcidPitSpray.AffectedCells(origin, origin + direction, map, 3f, 60f).Contains(origin + direction));
        Check("same-cell aim has no cone", AcidPitSpray.AffectedCells(origin, origin, map, 3f, 60f).Count == 0);
        Check("map-edge spray stays in bounds", AcidPitSpray.AffectedCells(new IntVec3(1, 0, 1), IntVec3.Zero, map, 3f, 60f).All(c => c.InBounds(map)));
        var wallDef = Item("SprayWall", 1f); wallDef.fillPercent = 1f;
        var wall = new Building { def = wallDef };
        var edifices = (Building[])typeof(EdificeGrid).GetField("innerArray", Flags).GetValue(map.edificeGrid);
        var wallCell = new IntVec3(6, 0, 5);
        edifices[map.cellIndices.CellToIndex(wallCell)] = wall;
        Check("wall and cells behind it are excluded", !AcidPitSpray.AffectedCells(origin, east, map, 3f, 60f).Contains(east)
            && !AcidPitSpray.AffectedCells(origin, east, map, 3f, 60f).Contains(wallCell));
        var pitProps = new CompProperties_AcidPit();
        var building = new ThingWithComps { def = Item("SprayPit", 1f) };
        typeof(Thing).GetField("positionInt", Flags).SetValue(building, origin);
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(building, (sbyte)0);
        var pit = new CompAcidPit { parent = building, props = pitProps };
        typeof(CompAcidPit).GetField("pricedCost", Flags).SetValue(pit, AcidPitEconomy.CostPerKg(pitProps));
        typeof(CompAcidPit).GetField("acid", Flags).SetValue(pit, pit.Capacity);
        Check("blocked aim is invalid and spends no acid", !pit.CanSplashAt(east) && !pit.Splash(east) && pit.Acid == pit.Capacity);
        edifices[map.cellIndices.CellToIndex(wallCell)] = null;
        Check("clearing wall restores cell targeting", pit.CanSplashAt(east));
        var harmony = new Harmony("XMT.Tests.AcidSpray");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(CompAcidPit).Assembly.GetType("Xenomorphtype.AcidUtility"), "IsAcidImmune"), prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(SprayImmunity)));
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)), prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(SprayDamage)));
            var filth = typeof(FilthMaker).GetMethods(Flags).Single(m => m.Name == "TryMakeFilth" && m.GetParameters().Length == 6 && m.GetParameters()[3].ParameterType == typeof(int));
            harmony.Patch(filth, prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(SprayFilth)));
            foreach (int id in new[] { 9101, 9102 })
            {
                var pawn = Raw<Pawn>(); pawn.def = Item("SprayPawn" + id, 1f); pawn.thingIDNumber = id; pawn.health = Raw<Pawn_HealthTracker>();
                typeof(Pawn_HealthTracker).GetField("healthState", Flags).SetValue(pawn.health, id == 9101 ? PawnHealthState.Mobile : PawnHealthState.Down);
                typeof(Thing).GetField("positionInt", Flags).SetValue(pawn, new IntVec3(7, 0, 5));
                typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(pawn, (sbyte)0);
                pawn.Position.GetThingList(map).Add(pawn); sprayHits[pawn] = new List<float>();
            }
            DamageDefOf.AcidBurn = new DamageDef { defName = "SprayAcidBurn" };
            Check("cell-targeted spray fires", pit.Splash(east));
            Check("each pawn in one cell receives a full 20 hit: " + string.Join(";", sprayHits.Values.Select(h => string.Join(",", h))), sprayHits.Values.All(h => h.Count == 1 && h[0] == 20f));
            Check("empty cells receive acid blood, occupied cells receive damage", sprayFilth.Contains(east) && !sprayFilth.Contains(new IntVec3(7, 0, 5)));
            Check("one spray spends half the reserve", Math.Abs(pit.Acid - pit.Capacity / 2d) < 1e-8);
            Check("cooldown prevents repeated discharge", !pit.Splash(east));
            Current.Game.tickManager.DebugSetTicksGame(1000);
            Check("second spray exhausts the reserve", pit.Splash(east) && pit.Acid == 0d);
            Current.Game.tickManager.DebugSetTicksGame(2000);
            Check("empty pit refuses a third spray", !pit.Splash(east));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
    private static void Schema(string root)
    {
        string[] files = { "1.7/Defs/EvoDefs/Hive_StructureUnlocks.xml", "1.7/Defs/ThingDefs_Buildings/Buildings_Special.xml", "1.7/Defs/Misc/HiveWorkPresentation.xml" };
        foreach (string file in files)
            foreach (XElement node in XDocument.Load(System.IO.Path.Combine(root, file)).Root.Elements())
            {
                Type type = FindType(node.Name.LocalName);
                Check("def type " + node.Element("defName")?.Value, type != null);
                Validate(node, type);
            }
        var all = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "1.7/Defs"), "*.xml", System.IO.SearchOption.AllDirectories)
            .Concat(System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "../../Data/Core/Defs")), "*.xml", System.IO.SearchOption.AllDirectories))
            .SelectMany(f => XDocument.Load(f).Root.Elements()).ToList();
        var parents = new HashSet<string>(all.Where(e => e.Attribute("Name") != null).Select(e => e.Attribute("Name").Value));
        foreach (XElement def in XDocument.Load(System.IO.Path.Combine(root, files[1])).Root.Elements().Where(e => e.Attribute("ParentName") != null))
            Check("inheritance " + def.Element("defName")?.Value, parents.Contains(def.Attribute("ParentName").Value));
        var names = new HashSet<string>(all.Select(e => e.Element("defName")?.Value).Where(n => n != null));
        foreach (XElement def in XDocument.Load(System.IO.Path.Combine(root, files[0])).Root.Elements())
            foreach (XElement reference in def.Descendants().Where(e => new[] { "building", "job", "designation", "appliedHediff", "workEffect", "actorMemory", "targetMemory" }.Contains(e.Name.LocalName)))
                Check("reference " + reference.Value, names.Contains(reference.Value));
    }
    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (string prefix in new[] { "", "Verse.", "RimWorld." })
            {
                Type type = assembly.GetType(prefix + name); if (type != null) return type;
            }
        return null;
    }
    private static void Validate(XElement node, Type type)
    {
        if (type == null || typeof(Def).IsAssignableFrom(type) && !node.Elements().Any()) return;
        if (type.IsEnum) { Check("enum " + type.Name + "=" + node.Value, Enum.TryParse(type, node.Value, out _)); return; }
        if (type == typeof(int)) { Check("integer " + node.Name + "=" + node.Value, int.TryParse(node.Value, out _)); return; }
        if (type.IsPrimitive || type == typeof(string) || type == typeof(Type) || type == typeof(decimal)) return;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type element = type.GetGenericArguments()[0];
            foreach (XElement child in node.Elements())
            {
                Type concrete = child.Attribute("Class") == null ? element : FindType(child.Attribute("Class").Value);
                Check("list class " + (child.Attribute("Class")?.Value ?? element.Name), concrete != null && element.IsAssignableFrom(concrete));
                Validate(child, concrete);
            }
            return;
        }
        // Stat modifiers and cost lists use RimWorld's named-entry XML representation.
        foreach (XElement child in node.Elements())
        {
            if (new[] { "statBases", "costList", "killedLeavings" }.Contains(child.Name.LocalName)) continue;
            FieldInfo field = null;
            for (Type scan = type; scan != null && field == null; scan = scan.BaseType) field = scan.GetField(child.Name.LocalName, Flags);
            Check(type.Name + "." + child.Name.LocalName, field != null);
            if (field != null) Validate(child, field.FieldType);
        }
    }
    public static void Main(string[] args) { try { Run(args); } catch (Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode = 1; } }
    private static void Run(string[] args)
    {
        Current.Game = Raw<Game>(); Current.Game.tickManager = Raw<TickManager>();
        ArchitectThreadDispatch();
        ColonistBarContainment();
        // Only synthetic fixtures bypass expansion initialization; the def allowances remain real.
        new Harmony("XMT.Tests.HiveFixtures").Patch(typeof(ThingFilter).GetMethod("Allows", new[] { typeof(Thing) }),
            prefix: new HarmonyMethod(typeof(HiveStructureChecks), nameof(FixtureFilter)));
        StatDefOf.Mass = new StatDef { defName = "Mass", defaultBaseValue = 1f };
        StatDefOf.Nutrition = new StatDef { defName = "Nutrition", defaultBaseValue = 0f };
        var jelly = Item("Starbeast_Jelly", 0.025f); var resin = Item("Starbeast_Resin", 0.05f); var steel = Item("Steel", 0.5f);
        var resource = new QueenIngestibleResourceDef { defName = "SyntheticOrganicJelly", massBased = true, product = jelly, massFactor = 0.0125f, applyJellySettings = true };
        var makerDef = Item("SyntheticMaker", 1f);
        makerDef.comps = new List<CompProperties> { new CompJellyMakerProperties { conversionRate = 0.5f,
            resources = new List<ResourceConversionEntry> { new ResourceConversionEntry { resourceDef = resource } } } };
        CompJellyMaker split = Maker(makerDef, resource), batch = Maker(makerDef, resource);
        int splitTotal = 0;
        for (int i = 0; i < 79; i++) splitTotal += Convert(split, resource, 0.000625f, true);
        Check("79 resin singles bank without minimum-one output", splitTotal == 0);
        Check("preview sees accumulated credit", Convert(split, resource, 0.000625f, false) == 1);
        Check("repeated preview does not spend credit", Convert(split, resource, 0.000625f, false) == 1);
        splitTotal += Convert(split, resource, 0.000625f, true);
        Check("80th resin emits exactly one jelly", splitTotal == 1);
        Check("batch and split agree", Convert(batch, resource, 0.05f, true) == splitTotal);
        int whole = Convert(Maker(makerDef, resource), resource, 0.000625f * 8000, true);
        int singles = 0; var many = Maker(makerDef, resource);
        for (int i = 0; i < 8000; i++) singles += Convert(many, resource, 0.000625f, true);
        Check("8000-way splitting creates no extra jelly", singles == whole && whole == 100);
        var allowed = new HiveInputSettings(d => d == resin);
        var stockpile = new StorageSettings(); stockpile.filter.SetAllow(resin, true); stockpile.filter.SetAllow(steel, true);
        allowed.GetStoreSettings().CopyFrom(stockpile); allowed.Clamp();
        Check("pasted valid input remains enabled", allowed.GetStoreSettings().filter.Allows(resin));
        Check("pasted invalid input is removed", !allowed.GetStoreSettings().filter.Allows(steel));
        Check("unmodified filter defaults to all valid inputs", new HiveInputSettings(d => d == resin).GetStoreSettings().filter.Allows(resin));
        var acid = new CompProperties_AcidPit();
        Check("default acid charge fits 25kg granite", AcidPitEconomy.Capacity(acid, AcidPitEconomy.CostPerKg(acid)) >= 25d);
        Check("default acid cost exceeds continuous jelly return", AcidPitEconomy.CostPerKg(acid) > 0.25d);
        Type settingsType = typeof(CompJellyMaker).Assembly.GetType("Xenomorphtype.XMTSettings");
        object settings = Activator.CreateInstance(settingsType, true);
        settingsType.GetField("instance", Flags).SetValue(null, settings);
        settingsType.GetField("_jellyMassEfficiency", Flags).SetValue(settings, 4f);
        Check("maximum jelly setting retains a loss", AcidPitEconomy.CostPerKg(acid) > 40d);
        Check("maximum setting scales storage to fit a chunk", AcidPitEconomy.Capacity(acid, AcidPitEconomy.CostPerKg(acid)) >= 1000d);
        Check("high setting needs multiple 25-jelly fuelings", AcidPitEconomy.Capacity(acid, AcidPitEconomy.CostPerKg(acid)) > 25d);
        var oldCredit = Maker(makerDef, resource);
        Convert(oldCredit, resource, 0.025f, true);
        settingsType.GetField("_jellyMassEfficiency", Flags).SetValue(settings, 0.025f);
        Check("settings changes do not revalue old banked credit", Convert(oldCredit, resource, 0.000625f, true) == 0);
        // Exercise the public job-facing path, including payment and successful zero-output conversion.
        Map map = Raw<Map>();
        typeof(Game).GetField("maps", Flags).SetValue(Current.Game, new List<Map> { map });
        var makeJelly = new DesignationDef { defName = "TestMakeJelly" };
        DefDatabase<DesignationDef>.Add(makeJelly);
        map.designationManager = new DesignationManager(map);
        map.reservationManager = new ReservationManager(map);
        map.physicalInteractionReservationManager = new PhysicalInteractionReservationManager();
        typeof(CompJellyMaker).Assembly.GetType("Xenomorphtype.XenoWorkDefOf").GetField("XMT_MakeJelly", Flags)
            .SetValue(null, makeJelly);
        ThingDef tiny = Item("TinyOrganic", 0.000625f);
        tiny.destroyable = true;
        resource.entries = new List<QueenIngestibleResourceEntry> { new QueenIngestibleResourceEntry { thingDef = tiny } };
        CompJellyMaker live = Maker(makerDef, resource);
        var source = new HiveFixtureThing { def = tiny };
        typeof(Thing).GetField("mapIndexOrState", Flags).SetValue(source, (sbyte)0);
        Check("public conversion banks a sub-unit payment", live.ConvertToJelly(source) == 0 && live.LastConversionSucceeded && source.Destroyed);
        Check("public conversion commits fractional credit", ((Dictionary<ThingDef, double>)typeof(CompJellyMaker).GetField("outputRemainders", Flags).GetValue(live))[jelly] > 0d);
        Check("same input cannot be consumed twice", live.ConvertToJelly(source) == 0 && !live.LastConversionSucceeded && source.DestroyCalls == 1);
        // Oversized insert owns the original item and withholds every output until all acid is paid.
        var inorganic = new QueenIngestibleResourceDef { defName = "TestInorganic", massBased = true, product = resin,
            entries = new List<QueenIngestibleResourceEntry> { new QueenIngestibleResourceEntry { thingDef = steel } } };
        var pit = new CompAcidPit { parent = new ThingWithComps { def = makerDef }, props = new CompProperties_AcidPit { inputs = inorganic } };
        typeof(CompAcidPit).GetField("pricedCost", Flags).SetValue(pit, AcidPitEconomy.CostPerKg(pit.Props));
        var steelInput = new HiveFixtureThing { def = steel };
        Check("oversized input is inserted even with empty acid", pit.Insert(steelInput) && pit.Busy);
        Check("steel promises ten resin", Math.Abs((double)typeof(CompAcidPit).GetField("resinUnits", Flags).GetValue(pit) - 10d) < 1e-8);
        Check("busy pit refuses another item", !pit.CanAccept(new HiveFixtureThing { def = steel }));
        double debt = (double)typeof(CompAcidPit).GetField("remainingCost", Flags).GetValue(pit);
        typeof(CompAcidPit).GetField("acid", Flags).SetValue(pit, debt / 2d);
        typeof(CompAcidPit).GetMethod("Digest", Flags).Invoke(pit, null);
        Check("partial digestion spends acid without destroying input", pit.Busy && !steelInput.Destroyed && pit.Acid == 0d);
        Check("partial digestion retains remaining debt", Math.Abs((double)typeof(CompAcidPit).GetField("remainingCost", Flags).GetValue(pit) - debt / 2d) < 1e-8);
        AcidProcessingChecks(pit);
        AcidSearchEarlyOuts(map);
        AcidSprayChecks(map);
        // Force the referenced pipe assembly into the schema resolver.
        _ = typeof(PipeSystem.CompProperties_ResourceStorage).Assembly.GetName();
        if (args.Length > 0) Schema(args[0]);
        Console.WriteLine("ALL " + checks + " CHECKS PASSED");
    }
    public static bool FixtureFilter(ThingFilter __instance, Thing t, ref bool __result)
    {
        if (t is not HiveFixtureThing) return true;
        __result = __instance.Allows(t.def);
        return false;
    }
}
public sealed class HiveFixtureThing : Thing
{
    public int DestroyCalls;
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        DestroyCalls++;
        holdingOwner?.Remove(this);
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, (sbyte)-2);
        stackCount = 0;
    }
}
