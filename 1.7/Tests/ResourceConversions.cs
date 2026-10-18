// Standalone regression checks against the built 1.7 assembly. Synthetic defs avoid loading a save.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;
using RimWorld;
using Xenomorphtype;
public static class Program
{
    static int checks;
    static int serial;
    static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static ThingDef Def(string name, float mass = 1f, float nutrition = 0f)
    {
        var d = Raw<ThingDef>(); d.defName = name + ++serial; d.category = ThingCategory.Item; d.thingClass = typeof(Thing);
        d.virtualDefs = new List<ThingDef>(); d.ingestible = new IngestibleProperties(); d.statBases = new List<StatModifier> { new StatModifier { stat = StatDefOf.Mass, value = mass }, new StatModifier { stat = StatDefOf.Nutrition, value = nutrition } };
        typeof(IngestibleProperties).GetField("parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(d.ingestible, d); return d;
    }
    static ThingFilter Filter(params ThingDef[] defs)
    {
        var f = new ThingFilter(); foreach (var d in defs) f.SetAllow(d, true); return f;
    }
    static IngredientCount Ingredient(float count, params ThingDef[] defs)
    {
        var i = new IngredientCount(); i.SetBaseCount(count); i.filter = Filter(defs); return i;
    }
    static RecipeDef Recipe(ThingDef output, int count, params IngredientCount[] ingredients)
    {
        var r = new RecipeDef { defName = "CheckRecipe" + ++serial, fixedIngredientFilter = null,
            products = new List<ThingDefCountClass> { new ThingDefCountClass(output, count) }, ingredients = ingredients.ToList() };
        DefDatabase<RecipeDef>.Add(r); return r;
    }
    static QueenIngestibleResourceDef Resource(params ThingDef[] inputs)
        => new QueenIngestibleResourceDef { defName = "CheckResource" + ++serial, usesStorage = false, massBased = true, product = Def("Output"), entries = inputs.Select(d => new QueenIngestibleResourceEntry { thingDef = d }).ToList() };
    static Thing Item(ThingDef d, int count = 1, ThingDef stuff = null)
    {
        var t = new Thing { def = d, stackCount = count }; if (stuff != null) t.SetStuffDirect(stuff); return t;
    }
    static List<ResourceConversionEntry> Entries(params QueenIngestibleResourceDef[] resources)
        => resources.Select(r => new ResourceConversionEntry { resourceDef = r }).ToList();
    static void Near(string name, float actual, float expected)
    {
        if (Math.Abs(actual - expected) > 0.0002f) throw new Exception(name + ": expected " + expected + ", got " + actual);
        Console.WriteLine("PASS " + name); checks++;
    }
    static void Yes(string name, bool pass) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
    static float Fraction(ThingDef d, QueenIngestibleResourceDef r, ThingDef stuff = null) => ResourceHelperUtility.EligibleFraction(d, stuff, r);
    public static void Main() { try { Run(); } catch (Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode = 1; } }
    private static void Run()
    {
        Current.Game = Raw<Game>(); Current.Game.tickManager = Raw<TickManager>();
        StatDefOf.Mass = new StatDef { defName = "Mass", defaultBaseValue = 1f };
        StatDefOf.Nutrition = new StatDef { defName = "Nutrition", defaultBaseValue = 0f };
        var wood = Def("Wood", 2f, .1f); var steel = Def("Steel"); var stone = Def("Stone");
        var organic = Resource(wood); var metal = Resource(steel);
        var mixed = Def("Mixed", 100f); Recipe(mixed, 1, Ingredient(5, wood), Ingredient(10, steel));
        var batch = Def("Batch", 3f); Recipe(batch, 2, Ingredient(5, wood), Ingredient(10, steel));
        var alternate = Def("Alternate", 100f); Recipe(alternate, 1, Ingredient(5, wood)); Recipe(alternate, 1, Ingredient(10, steel));
        var choices = Def("Choices"); Recipe(choices, 1, Ingredient(5, wood, steel));
        var noRecipe = Def("NoRecipe");
        var nutritionRecipe = Def("Nutritional", 100f); var nr = Recipe(nutritionRecipe, 1, Ingredient(.5f, wood), Ingredient(10, steel)); typeof(RecipeDef).GetField("ingredientValueGetterInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(nr, new IngredientValueGetter_Nutrition());
        // Nutrition-based recipes need a nutritious second ingredient too.
        steel.statBases.First(s => s.stat == StatDefOf.Nutrition).value = 1f;
        var constrained = Def("Constrained"); var cr = Recipe(constrained, 1, Ingredient(5, wood, steel)); cr.fixedIngredientFilter = Filter(wood);
        ResourceHelperUtility.ClearCaches();
        Near("direct acceptance", Fraction(wood, organic), 1f);
        Near("final item mass fraction", Fraction(mixed, organic), .5f);
        Near("no recipe early out", Fraction(noRecipe, organic), 0f);
        Near("alternate zero included", Fraction(alternate, organic), 0f);
        Near("ingredient choice zero included", Fraction(choices, organic), 0f);
        Near("fixed recipe filter respected", Fraction(constrained, organic), 1f);
        Near("nutrition ingredient count normalized", Fraction(nutritionRecipe, organic), .5f);
        Yes("definition targeting uses recipe union", ResourceHelperUtility.CanConvertDefinition(alternate, Entries(organic, metal), null));
        Near("shared budget for overlapping outputs", ResourceHelperUtility.Allocate(Item(mixed), Entries(organic, metal), null).Sum(a => a.mass), 100f);
        Near("union before alternative minimization", ResourceHelperUtility.Allocate(Item(alternate), Entries(organic, metal), null).Sum(a => a.mass), 100f);
        Near("finished mass rather than raw recipe counts", ResourceHelperUtility.Allocate(Item(mixed), Entries(organic), null).Sum(a => a.mass), 50f);
        organic.nutritionToMass = 20f;
        Near("nutrition converted into mass equivalent", ResourceHelperUtility.ValueFor(Item(wood), organic), 2f);
        var lightFood = Def("LightFood", .01f, 2f); organic.entries.Add(new QueenIngestibleResourceEntry { thingDef = lightFood });
        Near("nutrition dominates through mass equivalent", ResourceHelperUtility.ValueFor(Item(lightFood), organic), 40f);
        Near("integer partial stack quantity", ResourceHelperUtility.ConversionQuantity(Item(wood, 3), .5f), 1f);
        Near("partial source valued at committed quantity", ResourceHelperUtility.Allocate(Item(wood, 3), Entries(organic), null, 1).Sum(a => a.mass), 2f);
        Near("zero fraction produces no source quantity", ResourceHelperUtility.ConversionQuantity(Item(wood, 3), 0f), 0f);
        var tiny = new ResourceHelperUtility.Allocation { resource = organic, mass = .00001f };
        Near("positive allocation minimum one item", tiny.ItemCount, 1f);
        tiny.mass = 0f; Near("zero allocation no minimum item", tiny.ItemCount, 0f);
        var stuffCategory = new StuffCategoryDef { defName = "CheckStuff" };
        wood.stuffProps = new StuffProperties { categories = new List<StuffCategoryDef> { stuffCategory } };
        steel.stuffProps = new StuffProperties { categories = new List<StuffCategoryDef> { stuffCategory } };
        var table = Def("Table", 100f); table.stuffCategories = new List<StuffCategoryDef> { stuffCategory }; table.costStuffCount = 10;
        Recipe(table, 1, Ingredient(10, wood, steel));
        var composite = Def("Composite", 12f); Recipe(composite, 1, Ingredient(1, table));
        ResourceHelperUtility.ClearCaches();
        Near("actual convertible stuff", Fraction(table, organic, wood), 1f);
        Near("actual nonconvertible stuff distinct", Fraction(table, organic, steel), 0f);
        Near("unspecified root stuff conservative", Fraction(table, organic), 0f);
        Near("recursive unknown stuff forgiven", Fraction(composite, organic), 1f);
        Near("recursive forgiveness does not pollute actual stuff", Fraction(table, organic, steel), 0f);
        var jellyXml = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "Resource_Conversions.xml"));
        var jellyFilter = jellyXml.Root.Elements("Xenomorphtype.QueenIngestibleResourceDef")
            .Single(e => (string)e.Attribute("Name") == "XMT_JellyResourceBase").Element("inputFilter");
        Yes("shared organic jelly XML accepts wool category", jellyFilter.Element("categories").Elements("li").Any(e => e.Value == "Wools"));
        var wools = new ThingCategoryDef { defName = "Wools" };
        var fabric = new StuffCategoryDef { defName = "Fabric" };
        var wool = Def("WoolMegasloth", .03f); wool.thingCategories = new List<ThingCategoryDef> { wools };
        var devilstrand = Def("DevilstrandCloth", .03f); devilstrand.defName = "DevilstrandCloth";
        var synthetic = Def("SyntheticFabric", .03f);
        foreach (var material in new[] { wool, devilstrand, synthetic }) material.stuffProps = new StuffProperties { categories = new List<StuffCategoryDef> { fabric } };
        var mask = Def("ClothMask", .04f); mask.stuffCategories = new List<StuffCategoryDef> { fabric }; mask.costStuffCount = 10;
        Recipe(mask, 1, Ingredient(10, wool, devilstrand, synthetic));
        var textileResource = Resource();
        foreach (var category in jellyFilter.Element("categories").Elements("li"))
            if (category.Value == wools.defName) textileResource.entries.Add(new QueenIngestibleResourceEntry { thingCategory = wools });
        foreach (var allowed in jellyFilter.Element("thingDefs").Elements("li"))
            if (allowed.Value == devilstrand.defName) textileResource.entries.Add(new QueenIngestibleResourceEntry { thingDef = devilstrand });
        Near("raw megasloth wool qualifies", Fraction(wool, textileResource), 1f);
        Near("wool face mask uses actual wool stuff", Fraction(mask, textileResource, wool), 1f);
        Near("devilstrand face mask remains eligible", Fraction(mask, textileResource, devilstrand), 1f);
        Near("unlisted synthetic fabric remains ineligible", Fraction(mask, textileResource, synthetic), 0f);
        Near("wool face mask yields from finished mass", ResourceHelperUtility.Allocate(Item(mask, 1, wool), Entries(textileResource), null).Sum(a => a.mass), .04f);
        var coreApparel = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "CoreApparel_Various.xml"));
        var fixedCloth = Def("FlakCloth", .028f);
        var fixedSteel = Def("FlakSteel", .5f);
        var component = Def("FlakComponent", .6f);
        var flakIngredients = new Dictionary<string, ThingDef> { ["Cloth"] = fixedCloth, ["Steel"] = fixedSteel, ["ComponentIndustrial"] = component };
        var clothResource = Resource(fixedCloth);
        var clothUnits = new QueenIngestibleResourceDef { defName = "ClothUnits", entries = new List<QueenIngestibleResourceEntry> {
            new QueenIngestibleResourceEntry { thingDef = fixedCloth, valuePerUnit = 2f } } };
        MethodInfo setIngredients = typeof(RecipeDefGenerator).GetMethod("SetIngredients", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (string armorName in new[] { "Apparel_FlakPants", "Apparel_FlakVest" })
        {
            var armorXml = coreApparel.Root.Elements("ThingDef").Single(e => (string)e.Element("defName") == armorName);
            var armor = Def(armorName, (float)armorXml.Element("statBases").Element("Mass"));
            armor.costList = armorXml.Element("costList").Elements().Select(e => new ThingDefCountClass(flakIngredients[e.Name.LocalName], (int)e)).ToList();
            var generatedRecipe = new RecipeDef { defName = "NativeGenerated" + armorName, products = new List<ThingDefCountClass> { new ThingDefCountClass(armor, 1) } };
            setIngredients.Invoke(null, new object[] { generatedRecipe, armor, 1 });
            DefDatabase<RecipeDef>.Add(generatedRecipe);
            Yes(armorName + " native recipe leaves broad filter empty", generatedRecipe.fixedIngredientFilter.AllowedDefCount == 0);
            Yes(armorName + " vanilla accepts fixed cloth slot", generatedRecipe.IsIngredient(fixedCloth));
            float clothMass = armor.costList.Where(c => c.thingDef == fixedCloth).Sum(c => c.count * .028f);
            float totalMass = armor.costList.Sum(c => c.count * c.thingDef.GetStatValueAbstract(StatDefOf.Mass));
            float armorFraction = clothMass / totalMass;
            Near(armorName + " fixed ingredient mass fraction", Fraction(armor, clothResource), armorFraction);
            Yes(armorName + " eligible for architect targeting", ResourceHelperUtility.CanConvertDefinition(armor, Entries(clothResource), null));
            Near(armorName + " conversion values only cloth share of finished mass",
                ResourceHelperUtility.Allocate(Item(armor), Entries(clothResource), null).Sum(a => a.mass), 4f * armorFraction);
            Near(armorName + " queen recipe units retain fixed cloth", ResourceHelperUtility.RecipeUnits(generatedRecipe, clothUnits), 60f);
        }
        var flexibleProduct = Def("FlexibleWithEmptyWideFilter");
        var flexibleRecipe = Recipe(flexibleProduct, 1, Ingredient(10, fixedCloth, synthetic));
        flexibleRecipe.fixedIngredientFilter = new ThingFilter();
        Near("flexible slots still require recipe-wide permission", Fraction(flexibleProduct, clothResource), 0f);
        var numeric = new QueenIngestibleResourceDef { defName = "Numeric", entries = new List<QueenIngestibleResourceEntry> {
            new QueenIngestibleResourceEntry { thingDef = wood, valuePerUnit = 2f }, new QueenIngestibleResourceEntry { thingDef = steel, valuePerUnit = 3f } } };
        Near("queen recipe normalized per product batch", ResourceHelperUtility.DefinitionUnits(batch, numeric), 20f);
        Near("queen recipe total remains whole batch", ResourceHelperUtility.RecipeUnits(DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.products.Any(p => p.thingDef == batch)), numeric), 40f);
        var directOnly = new QueenIngestibleResourceDef { defName = "DirectOnly", entries = new List<QueenIngestibleResourceEntry> {
            new QueenIngestibleResourceEntry { thingDef = wood, valuePerUnit = 2f, countsInDerivedCosts = false } } };
        Near("direct-only entry warm cache", ResourceHelperUtility.DefinitionUnits(wood, directOnly), 2f);
        Near("direct-only entry excluded during recursion", ResourceHelperUtility.DefinitionUnits(constrained, directOnly), 0f);
        var a = Def("CycleA"); var b = Def("CycleB"); Recipe(a, 1, Ingredient(1, b)); Recipe(b, 1, Ingredient(1, a));
        ResourceHelperUtility.ClearCaches(); Near("cycles terminate", Fraction(a, organic), 0f);
        var resourceCaches = (IDictionary)typeof(ResourceHelperUtility).GetField("caches", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var cache = resourceCaches[organic]; var fractionCache = (IDictionary)cache.GetType().GetField("fractions").GetValue(cache);
        Yes("cycle abort not globally cached", fractionCache.Count == 0);
        var corpseDef = Def("MechCorpse"); corpseDef.thingClass = typeof(Corpse);
        var race = Def("MechRace"); race.category = ThingCategory.Pawn; race.race = new RaceProperties { corpseDef = corpseDef };
        race.butcherProducts = new List<ThingDefCountClass> { new ThingDefCountClass(steel, 10) };
        Yes("metal alone cannot permit mech corpse", !ResourceHelperUtility.CorpseRaceAllowed(race, metal));
        var explicitCorpse = Resource(); explicitCorpse.corpseFilter = Filter(corpseDef);
        Yes("explicit corpse race validity", ResourceHelperUtility.CorpseRaceAllowed(race, explicitCorpse));
        ResourceHelperUtility.ClearCaches();
        Yes("runtime cache reset clears resource cache", resourceCaches.Count == 0);
        var temp = new ResourceTemperature { enabled = false, celsiusPerInterval = 0f };
        organic.temperature = temp; Near("disabled temperature early out", ResourceHelperUtility.TemperatureYield(organic, IntVec3.Invalid, null), 0f);
        numeric.derivedIngredientFactor = .8f;
        Near("queen raw recipe costs unaffected by recovery efficiency", ResourceHelperUtility.DefinitionUnits(batch, numeric), 20f);
        Near("queen derived efficiency applied once", ResourceHelperUtility.ValueFor(Item(batch), numeric), 16f);
        var outer = Def("NestedUnits"); Recipe(outer, 1, Ingredient(1, batch));
        Near("queen nested efficiency not compounded", ResourceHelperUtility.ValueFor(Item(outer), numeric), 16f);
        StatDefOf.MeatAmount = new StatDef { defName = "MeatAmount", workerClass = typeof(FixtureStatWorker) };
        StatDefOf.MaxHitPoints = new StatDef { defName = "MaxHitPoints", defaultBaseValue = 100f, workerClass = typeof(FixtureStatWorker) };
        var organicCorpseDef = Def("OrganicCorpse"); organicCorpseDef.thingClass = typeof(Corpse); organicCorpseDef.useHitPoints = true;
        organicCorpseDef.statBases.Add(new StatModifier { stat = StatDefOf.MaxHitPoints, value = 100f });
        var organicRace = Def("OrganicRace"); organicRace.category = ThingCategory.Pawn;
        organicRace.race = new RaceProperties { corpseDef = organicCorpseDef, meatDef = wood, intelligence = Intelligence.Humanlike };
        organicRace.statBases.Add(new StatModifier { stat = StatDefOf.MeatAmount, value = 100f });
        var pawn = Raw<Pawn>(); pawn.def = organicRace;
        var corpse = new Corpse { def = organicCorpseDef, HitPoints = 100 };
        ((ThingOwner<Pawn>)corpse.GetDirectlyHeldThings()).InnerListForReading.Add(pawn);
        var corpseResource = Resource(organicCorpseDef);
        Near("corpse live full HP yield", ResourceHelperUtility.ValueFor(corpse, corpseResource), 200f);
        corpse.HitPoints = 50;
        Near("corpse HP changes not cached", ResourceHelperUtility.ValueFor(corpse, corpseResource), 100f);
        organicRace.statBases.First(s => s.stat == StatDefOf.MeatAmount).value = 50f;
        Near("corpse pawn stats changes not cached", ResourceHelperUtility.ValueFor(corpse, corpseResource), 50f);
        float corpseQuantity = ResourceHelperUtility.ConversionQuantity(corpse, .45f);
        Near("non-rotting corpse amount quantized before valuation", corpseQuantity, .46f);
        float corpseBefore = ResourceHelperUtility.ValueFor(corpse, corpseResource);
        ResourceHelperUtility.Consume(corpse, corpseQuantity, preserveCorpseRemainder: true);
        Near("non-rotting corpse retains exact unconverted value", ResourceHelperUtility.ValueFor(corpse, corpseResource), corpseBefore * (1f - corpseQuantity));
        corpse.HitPoints = 50;
        // Exercise worn-item ownership separately from graphics notifications: the fixture
        // apparel's Destroy substitutes only those engine side effects, not conversion logic.
        pawn.apparel = new Pawn_ApparelTracker(pawn);
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(pawn, (sbyte)-1);
        typeof(Thing).GetField("holdingOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(pawn, corpse.GetDirectlyHeldThings());
        mask.thingClass = typeof(FixtureApparel); mask.apparel = new ApparelProperties(); mask.destroyable = true;
        var wearable = new FixtureApparel { def = mask, stackCount = 1 };
        wearable.SetStuffDirect(wool);
        var unconvertible = new FixtureApparel { def = mask, stackCount = 1 };
        unconvertible.SetStuffDirect(synthetic);
        var clothingOwner = (ThingOwner<Apparel>)pawn.apparel.GetDirectlyHeldThings();
        foreach (Apparel clothing in new[] { wearable, unconvertible })
        {
            clothingOwner.InnerListForReading.Add(clothing);
            typeof(Thing).GetField("holdingOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(clothing, clothingOwner);
        }
        var dressedResource = Resource(organicCorpseDef);
        dressedResource.entries.AddRange(textileResource.entries);
        var dressedEntries = Entries(dressedResource);
        float bodyOnly = ResourceHelperUtility.Allocate(corpse, dressedEntries, null, .5f).Sum(a => a.mass);
        var clothedPlan = ResourceHelperUtility.PlanConversion(corpse, dressedEntries, null, .5f);
        Near("corpse plan adds full eligible worn apparel to partial body yield", clothedPlan.Allocations.Sum(a => a.mass), bodyOnly + .04f);
        Yes("corpse preview leaves worn apparel intact", !wearable.Destroyed && pawn.apparel.WornApparel.Count == 2);
        Near("worn apparel cannot authorize an ineligible corpse", ResourceHelperUtility.PlanConversion(corpse, Entries(textileResource), null).Allocations.Count, 0f);
        Near("zero conversion does not collect worn apparel", ResourceHelperUtility.PlanConversion(corpse, dressedEntries, null, 0f).Allocations.Count, 0f);
        var stalePlan = ResourceHelperUtility.PlanConversion(corpse, dressedEntries, null, .5f);
        Yes("corpse apparel conversion commits", clothedPlan.TryConsume());
        Yes("converted apparel is removed and destroyed once", wearable.Destroyed && wearable.DestroyCalls == 1 && !pawn.apparel.WornApparel.Contains(wearable));
        Yes("ineligible worn apparel remains on partial corpse", !unconvertible.Destroyed && pawn.apparel.WornApparel.Contains(unconvertible));
        Yes("partial body remains after clothing conversion", !corpse.Destroyed && corpse.HitPoints == 25);
        Yes("conversion plan cannot commit twice", !clothedPlan.TryConsume());
        Yes("stale apparel snapshot cannot pay a second time", !stalePlan.TryConsume() && corpse.HitPoints == 25);
        Near("later corpse conversion does not recount removed apparel", ResourceHelperUtility.PlanConversion(corpse, dressedEntries, null, .5f).Allocations.Sum(a => a.mass),
            ResourceHelperUtility.Allocate(corpse, dressedEntries, null, ResourceHelperUtility.ConversionQuantity(corpse, .5f)).Sum(a => a.mass));
        corpse.HitPoints = 50;
        var rot = new CompRottable { parent = corpse, props = new CompProperties_Rottable { daysToRotStart = 1f, daysToDessicated = 5f } };
        typeof(ThingWithComps).GetField("comps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(corpse, new List<ThingComp> { rot });
        typeof(CompRottable).GetField("rotProgressInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rot, (float)rot.PropsRot.TicksToRotStart);
        Near("corpse rot modifies yield live", ResourceHelperUtility.ValueFor(corpse, corpseResource), 40f);
        typeof(CompRottable).GetField("rotProgressInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rot, (float)rot.PropsRot.TicksToDessicated);
        Near("dessication gate rejects despite warmed race cache", ResourceHelperUtility.ValueFor(corpse, corpseResource), 0f);
        corpseResource.corpseRotStages.Add(RotStage.Dessicated);
        Near("feeding can permit dessicated corpse", ResourceHelperUtility.ValueFor(corpse, corpseResource), 12.5f);
        var weighted = Entries(organic, metal); weighted[0].allocationWeight = 3f;
        var weightedOutput = ResourceHelperUtility.Allocate(Item(mixed), weighted, null);
        Near("allocation weights divide one budget", weightedOutput.First(a => a.resource == organic).mass, 75f);
        weighted[0].allocationWeight = 0f;
        Near("zero allocation weight disables a resource", ResourceHelperUtility.Allocate(Item(mixed), weighted, null).Count, 1f);
        Near("consumer caches stay independent", Fraction(noRecipe, Resource(noRecipe)), 1f);
        var zeroMass = Def("ZeroMass", 0f); var zeroProduct = Def("ZeroProduct"); Recipe(zeroProduct, 1, Ingredient(1, zeroMass));
        Near("zero-mass recipe cannot create a full fraction", Fraction(zeroProduct, Resource(zeroMass)), 0f);
        var barren = Raw<TerrainDef>(); barren.defName = "Barren"; barren.fertility = 0f;
        var fertile = Raw<TerrainDef>(); fertile.defName = "Fertile"; fertile.fertility = 1f;
        var terrain1 = new ResourceTerrainDef { defName = "Terrain1", minFertility = .1f, yield = 2f, depletedTerrain = barren, depletionPriority = 1 };
        var terrain2 = new ResourceTerrainDef { defName = "Terrain2", minFertility = .1f, yield = 3f, depletedTerrain = barren, depletionPriority = 1 };
        DefDatabase<TerrainDef>.Add(fertile);
        var terrainResource = Resource(); terrainResource.terrainProfiles = new List<ResourceTerrainDef> { terrain1, terrain2 };
        Near("overlapping terrain profiles stack", ResourceHelperUtility.EvaluateTerrain(new[] { terrainResource }, fertile).values[terrainResource], 5f);
        var replacement = Raw<TerrainDef>(); replacement.defName = "Replacement";
        var priority = new ResourceTerrainDef { defName = "Priority", minFertility = .1f, yield = 1f, depletedTerrain = replacement, depletionPriority = 5 };
        terrainResource.terrainProfiles.Add(priority);
        var terrainEvaluation = ResourceHelperUtility.EvaluateTerrain(new[] { terrainResource }, fertile);
        Near("terrain yield includes all profiles before depletion", terrainEvaluation.values[terrainResource], 6f);
        Yes("explicit highest depletion priority", terrainEvaluation.depletion == priority);
        var conflicting = new ResourceTerrainDef { defName = "Conflicting", minFertility = .1f, yield = 1f, depletedTerrain = replacement, depletionPriority = 1 };
        Yes("equal conflicting terrain priorities report config error", ResourceHelperUtility.TerrainConfigErrors(new[] { terrain1, conflicting }).Any());
        Yes("equal matching terrain priorities accepted", !ResourceHelperUtility.TerrainConfigErrors(new[] { terrain1, terrain2 }).Any());
        terrainResource.terrainProfiles.Clear();
        Near("no-material temperature uses base one", ResourceHelperUtility.EvaluateTerrain(new[] { terrainResource }, fertile, r => .25f).values[terrainResource], .25f);
        terrainResource.terrainProfiles.Add(terrain1);
        Near("material temperature uses material basis", ResourceHelperUtility.EvaluateTerrain(new[] { terrainResource }, fertile, r => .25f).values[terrainResource], 2.5f);
        Yes("no temperature at rest gives no free unit", ResourceHelperUtility.EvaluateTerrain(new[] { Resource() }, fertile, r => 0f).values.Count == 0);
        Console.WriteLine("ALL " + checks + " CHECKS PASSED");
    }
}public class FixtureStatWorker : StatWorker
{
    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        => ((ThingDef)req.Def).statBases?.FirstOrDefault(s => s.stat == stat)?.value ?? stat.defaultBaseValue;
    public override void FinalizeValue(StatRequest req, ref float value, bool applyPostProcess) { }
}
public sealed class FixtureApparel : Apparel
{
    public int DestroyCalls;
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        DestroyCalls++;
        var owner = (ThingOwner<Apparel>)Wearer.apparel.GetDirectlyHeldThings();
        owner.InnerListForReading.Remove(this);
        typeof(Thing).GetField("holdingOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(this, null);
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(this, (sbyte)-2);
        stackCount = 0;
    }
}
