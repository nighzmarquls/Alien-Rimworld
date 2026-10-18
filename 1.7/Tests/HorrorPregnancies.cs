using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Verse;
using Xenomorphtype;
using XMT_RJW;
using XMT_Menstruation;

public static class HorrorPregnancyChecks
{
    private static int checks;
    private static void Check(string name, bool result)
    {
        if (!result) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }
    private static SimpleCurve Curve(XElement element)
    {
        SimpleCurve curve = new SimpleCurve();
        foreach (XElement point in element.Element("points").Elements("li"))
        {
            string[] values = point.Value.Trim('(', ')').Split(',');
            curve.Add(float.Parse(values[0], CultureInfo.InvariantCulture), float.Parse(values[1], CultureInfo.InvariantCulture));
        }
        return curve;
    }
    public static void Main()
    {
        typeof(UnityData).GetField("mainThreadId", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(null, Environment.CurrentManagedThreadId);
        XElement reproductionXml = XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "RJWReproduction.xml")).Root;
        Check("new RJW defs use fields supported by the current runtime", reproductionXml.Elements().All(def =>
        {
            Type runtimeType = def.Name.LocalName == "HediffDef" ? typeof(HediffDef) : typeof(RJWReproductionDef);
            return def.Elements().All(field => runtimeType.GetField(field.Name.LocalName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null);
        }));
        Check("fluid exposure bookkeeping remains hidden", !new Hediff_FluidEssence().Visible);
        XElement paletteXml = XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "CryptimorphPalette.xml")).Root.Elements().Single();
        CryptimorphPaletteDef palette = new CryptimorphPaletteDef
        {
            enabled = bool.Parse(paletteXml.Element("enabled").Value),
            neutralizeWombTint = bool.Parse(paletteXml.Element("neutralizeWombTint").Value),
            states = paletteXml.Element("states").Elements("li").Select(s => new CryptimorphStateColor
            {
                stage = s.Element("stage").Value,
                saturationFactor = float.Parse(s.Element("saturationFactor").Value, CultureInfo.InvariantCulture),
                brightnessFactor = float.Parse(s.Element("brightnessFactor").Value, CultureInfo.InvariantCulture),
                minimumBrightness = float.Parse(s.Element("minimumBrightness").Value, CultureInfo.InvariantCulture)
            }).ToList()
        };
        Check("palette XML fields match their concrete runtime consumers", paletteXml.Elements().All(f => typeof(CryptimorphPaletteDef).GetField(f.Name.LocalName) != null) &&
            paletteXml.Element("states").Elements("li").All(s => s.Elements().All(f => typeof(CryptimorphStateColor).GetField(f.Name.LocalName) != null)));
        UnityEngine.Color blood = new UnityEngine.Color(0.35f, 0.8f, 0.1f);
        UnityEngine.Color.RGBToHSV(blood, out float bloodHue, out _, out _);
        Check("every stage retains the native blood hue", palette.states.All(s =>
        {
            UnityEngine.Color.RGBToHSV(s.Evaluate(blood), out float hue, out _, out _);
            return Math.Abs(hue - bloodHue) < 0.0001f;
        }));
        Check("XML tuning distinguishes ovulation from bleeding", palette.State("Ovulatory").Evaluate(blood).g > palette.State("Bleeding").Evaluate(blood).g);
        Check("dark blood colors retain the configured readability floor", palette.states.All(s =>
        {
            UnityEngine.Color.RGBToHSV(s.Evaluate(UnityEngine.Color.black), out _, out _, out float value);
            return Math.Abs(value - s.minimumBrightness) < 0.0001f;
        }));
        Check("unlisted cycle states use the XML fallback", ReferenceEquals(palette.State("Infertile"), palette.State("Default")));
        UnityEngine.Color pink = new UnityEngine.Color(1f, 0.47f, 0.47f, 1f);
        Check("human womb tint stays unchanged while cryptimorph tint becomes neutral", CryptimorphPalette.WombTint(pink, false, palette) == pink &&
            CryptimorphPalette.WombTint(pink, true, palette) == UnityEngine.Color.white);
        palette.enabled = false;
        Check("disabled palette restores the upstream panel tint", CryptimorphPalette.WombTint(pink, true, palette) == pink);
        palette.enabled = true;
        Check("ever fertile reserve restores an exhausted womb", EverFertile.HealthyReserve(0, 20) == 20);
        Check("ever fertile reserve preserves surplus eggs and handles a zero threshold", EverFertile.HealthyReserve(40, 20) == 40 && EverFertile.HealthyReserve(-2, 0) == 1);
        XElement xml = XDocument.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "HorrorPregnancyTuning.xml")).Root.Elements().Single();
        HorrorPregnancyDef tuning = new HorrorPregnancyDef
        {
            defName = "Fixture", essenceCurve = Curve(xml.Element("essenceCurve")),
            conceptionChance = Curve(xml.Element("conceptionChance")), corruptionChance = Curve(xml.Element("corruptionChance"))
        };
        Check("zero essence cannot conceive or corrupt", tuning.Chance(0f, false) == 0f && tuning.Chance(0f, true) == 0f);
        Check("XML curves interpolate partial essence", Math.Abs(tuning.Chance(0.25f, false) - 0.25f) < 0.0001f);
        Check("high essence saturates probability", tuning.Chance(8f, false) == 1f && tuning.Chance(8f, true) == 1f);
        tuning.essenceCurve = new SimpleCurve { new CurvePoint(0f, 0f), new CurvePoint(4f, 2f) };
        Check("tuning curve changes both pregnancy decisions", tuning.Chance(1f, false) == 0.5f && tuning.Chance(1f, true) == 0.5f);
        Check("all twelve race templates migrated", xml.Element("templates").Elements("li").Count() == 12);
        Check("every template has a valid gestation and certain fallback", xml.Element("templates").Elements("li").All(t =>
            t.Element("babies").Elements("li").All(c => float.Parse(c.Element("gestationFactor").Value, CultureInfo.InvariantCulture) > 0f) &&
            t.Element("babies").Elements("li").Any(c => c.Element("probability").Value == "1.0")));

        GeneDef first = new GeneDef { defName = "First" }, second = new GeneDef { defName = "Second" };
        GeneSet source = new GeneSet(); source.AddGene(first);
        GeneSet snapshot = HorrorPregnancyUtility.CopyGenes(source); source.AddGene(second);
        Check("conversion genes are an independent snapshot", snapshot.GenesListForReading.Count == 1 && snapshot.GenesListForReading[0] == first);
        Check("unknown donor genes are safely represented", HorrorPregnancyUtility.CopyGenes(null).GenesListForReading.Count == 0);
        // ThingDef's constructor initializes renderer assets; this fixture only needs race data.
        ThingDef race = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));
        race.race = new RaceProperties();
        CryptimorphTextureSetDef textures = new CryptimorphTextureSetDef
        {
            prefix = "XMT/Menstruation/Cryptimorph/",
            defaultPrefix = "XMT/Menstruation/Default/",
            sourcePaths = new List<string> { "Womb/Womb", "Fetus/Horror/00" },
            derivedGenes = new List<GeneDef> { first }
        };
        Check("ordinary pawns retain upstream textures", !CryptimorphTextures.MatchesLineage(race, null, new[] { second }, false, textures));
        Check("derived genes select cryptimorph textures", CryptimorphTextures.MatchesLineage(race, null, new[] { first }, false, textures));
        race.modExtensions = new List<DefModExtension> { new XMT_HorrorPawnExtension() };
        Check("cryptimorph races select cryptimorph textures", CryptimorphTextures.MatchesLineage(race, null, null, false, textures));
        race.race.lifeStageAges = new List<LifeStageAge> { new LifeStageAge { minAge = 0, def = new LifeStageDef { reproductive = true } } };
        Pawn fertilePawn = new Pawn { def = race };
        fertilePawn.ageTracker = new Pawn_AgeTracker(fertilePawn);
        // Use the fixture's only life stage without invoking the running game's growth/tick manager.
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(fertilePawn.ageTracker, 0);
        Check("mature cryptimorphs qualify for age-independent fertility", EverFertile.Applies(fertilePawn));
        race.race.lifeStageAges[0].def.reproductive = false;
        Check("ever fertile does not bypass reproductive maturity", !EverFertile.Applies(fertilePawn));
        race.race.lifeStageAges[0].def.reproductive = true;
        race.modExtensions.Clear();
        Check("ordinary pawns keep native fertility aging", !EverFertile.Applies(fertilePawn));
        Check("ordinary pawns do not acquire the blood-color palette", !CryptimorphPalette.Applies(fertilePawn, palette));
        Check("eligible anatomy paths use independent texture keys", CryptimorphTextures.Remap("Womb/Womb", true, textures) == textures.prefix + "Womb/Womb");
        Check("shine masks resolve only for known cryptimorph textures",
            CryptimorphShine.MaskPath(textures.prefix + "Womb/Womb", textures) == textures.shineMaskPrefix + "Womb/Womb" &&
            CryptimorphShine.MaskPath("Womb/Womb", textures) == null &&
            CryptimorphShine.MaskPath(textures.defaultPrefix + "Womb/Womb", textures) == null &&
            CryptimorphShine.MaskPath(textures.prefix + "Custom/Womb", textures) == null);
        Check("horror stages follow maternal lineage in the relocated hierarchy",
            CryptimorphTextures.Remap(textures.defaultPrefix + "Fetus/Horror/00", true, textures) == textures.prefix + "Fetus/Horror/00" &&
            CryptimorphTextures.Remap(textures.defaultPrefix + "Fetus/Horror/00", false, textures) == textures.defaultPrefix + "Fetus/Horror/00");
        Check("human and custom texture paths remain unchanged", CryptimorphTextures.Remap("Womb/Womb", false, textures) == "Womb/Womb" &&
            CryptimorphTextures.Remap("Custom/Womb", true, textures) == "Custom/Womb");
        FieldInfo textureScope = typeof(CryptimorphTextures).GetField("grayscale", BindingFlags.NonPublic | BindingFlags.Static);
        bool outerScope = CryptimorphTextures.Enter(true);
        bool innerScope = CryptimorphTextures.Enter(false);
        bool humanScope = !(bool)textureScope.GetValue(null);
        CryptimorphTextures.Restore(innerScope);
        bool restoredCryptimorphScope = (bool)textureScope.GetValue(null);
        CryptimorphTextures.Restore(outerScope);
        Check("nested pawn scopes restore without leaking grayscale", humanScope && restoredCryptimorphScope && !(bool)textureScope.GetValue(null));
        Check("no litter curve means one spawn", HorrorPregnancyUtility.LitterSize(race) == 1);
        race.race.litterSizeCurve = new SimpleCurve { new CurvePoint(2.5f, 0f), new CurvePoint(3f, 1f), new CurvePoint(3.5f, 0f) };
        Rand.PushState(874);
        try { Check("race litter curve produces multiple core spawns", Enumerable.Range(0, 20).All(_ => HorrorPregnancyUtility.LitterSize(race) == 3)); }
        finally { Rand.PopState(); }
        race.comps = new List<CompProperties> { new CompProperties_EggLayer() };
        Check("egg layers override the litter curve with one horror", HorrorPregnancyUtility.LitterSize(race) == 1);

        // Minimal fixture API exercises the isolated compatibility shim without starting a game.
        rjw.Hediff_BasePregnancy pregnancy = new rjw.Hediff_BasePregnancy { p_start_tick = 100f, p_end_tick = 1100f };
        RJWApiShim.Advance(pregnancy, 50, 0.5f);
        Check("native acceleration preserves duration while advancing progress", pregnancy.p_start_tick == 50f && pregnancy.p_end_tick == 1050f);
        RJWApiShim.Advance(pregnancy, 50, 1f);
        Check("normal native gestation is unchanged", pregnancy.p_start_tick == 50f);
        Pawn oldBaby = new Pawn { thingIDNumber = 1, def = race },
            replacement = new Pawn { thingIDNumber = 2, def = race },
            sibling = new Pawn { thingIDNumber = 3, def = race };
        pregnancy.babies.Add(oldBaby);
        pregnancy.enzygoticSiblings[oldBaby] = sibling;
        pregnancy.enzygoticSiblings[sibling] = oldBaby;
        RJWApiShim.ReplaceSiblingReferences(pregnancy, oldBaby, replacement);
        Check("identical-sibling references follow converted offspring", pregnancy.enzygoticSiblings[replacement] == sibling && pregnancy.enzygoticSiblings[sibling] == replacement);
        Check("native baby ownership is retained", ReferenceEquals(RJWApiShim.Babies(pregnancy), pregnancy.babies));

        MethodInfo bridge = AccessTools.Method(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.TryHandleNativePregnancy));
        Check("core extension point cannot inline away", (bridge.GetMethodImplementationFlags() & MethodImplAttributes.NoInlining) != 0);
        Harmony harmony = new Harmony("XMT.HorrorPregnancyChecks");
        harmony.Patch(bridge, prefix: new HarmonyMethod(typeof(HorrorPregnancyChecks), nameof(BridgePrefix)));
        try
        {
            bool handled = HorrorPregnancyUtility.TryHandleNativePregnancy(null, null, 2f, true, out bool result);
            Check("optional project prefix supplies out result and skips core stub", handled && result);
        }
        finally { harmony.Unpatch(bridge, HarmonyPatchType.All, harmony.Id); }
        Console.WriteLine("ALL " + checks + " PREGNANCY CHECKS PASSED");
    }
    private static bool BridgePrefix(float essence, bool existingOnly, ref bool result, ref bool __result)
    { result = essence == 2f && existingOnly; __result = true; return false; }
}

namespace rjw
{
    public class Hediff_BasePregnancy : HediffWithComps
    {
        public float p_start_tick, p_end_tick;
        public List<Pawn> babies = new List<Pawn>();
        public Dictionary<Pawn, Pawn> enzygoticSiblings = new Dictionary<Pawn, Pawn>();
    }
}
