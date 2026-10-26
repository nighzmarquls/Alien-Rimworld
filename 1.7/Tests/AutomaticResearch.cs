// Runs the production automatic-award utility against small engine substitutes.
// In-game validation still covers exposure profiles, Scribe, and held pawns.
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Xenomorphtype;

internal static class AutomaticResearch
{
    private static int assertions;
    private static readonly Faction Player = new Faction { IsPlayer = true };
    private static readonly Faction Enemy = new Faction();

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static Pawn Human(Map map, Faction faction = null)
    {
        var pawn = new Pawn { MapHeld = map, Faction = faction ?? Player };
        map.mapPawns.pawns.Add(pawn);
        return pawn;
    }

    private static void Reset()
    {
        Find.ResearchManager = new ResearchManager();
        Faction.OfPlayerSilentFail = Player;
        XMTUtility.hive = false;
        XenoGeneDefOf.XMT_Jelly_Drugs.prerequisites.Clear();
        XenoSocialDefOf.XMT_Starbeast_JellyTransport.prerequisites.Clear();
    }

    public static void Main()
    {
        Reset();
        var map = new Map();
        var human = Human(map);
        var subject = new KnowledgeCategoryDef();
        var profile = new KnowledgeProfileDef { subject = subject };
        ResearchUtility.ApplyWitnessExposure(human, subject, profile, 0.1f, null, 10);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 10, "First discovery must award research");
        ResearchUtility.ApplyWitnessExposure(human, subject, profile, 0.1f, null, 10);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 10, "Repeated related events must not award research");
        Check(human.knowledge[subject] == 0.2f, "Repeated exposure must still teach personal knowledge");
        var restored = Human(map);
        restored.knowledge = new Dictionary<KnowledgeCategoryDef, float>(human.knowledge);
        ResearchUtility.ApplyWitnessExposure(restored, subject, profile, 0.1f, null, 10);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 10, "Restored knowledge must prevent repeat awards");
        var otherSubject = new KnowledgeCategoryDef();
        ResearchUtility.ApplyWitnessExposure(human, otherSubject, new KnowledgeProfileDef { subject = otherSubject }, 0, null, 10);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 10, "Exposure that teaches nothing must not award research");
        ResearchUtility.ApplyWitnessExposure(human, otherSubject, new KnowledgeProfileDef { subject = otherSubject }, 0.1f, null, 10, acid: true);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Acid_Utilization) == 10, "Independent acid discovery must reach the acid project");
        var secondWitness = Human(map);
        ResearchUtility.ApplyWitnessExposure(secondWitness, subject, profile, 0.1f, null, 10);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 20, "Each new witness may contribute once");
        var npc = Human(map, Enemy);
        ResearchUtility.ApplyWitnessExposure(npc, subject, profile, 0.1f, null, 10);
        Check(npc.knowledge[subject] > 0 && Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 20,
            "NPCs must learn without contributing player research");

        Reset();
        var emptyMap = new Map();
        var ownedSource = new Thing { Faction = Player, MapHeld = emptyMap };
        ResearchUtility.ProgressGeneticsTech(50, ownedSource);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 0, "Pure hive map must not earn human tech");
        var animal = Human(emptyMap);
        animal.RaceProps.Humanlike = false;
        ResearchUtility.ProgressGeneticsTech(50, null, emptyMap);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 0, "Animals must not qualify as human presence");
        animal.RaceProps.Humanlike = true;
        animal.morph = new object();
        ResearchUtility.ProgressGeneticsTech(50, null, emptyMap);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 0, "Mature morphs must not qualify");
        var remoteHuman = Human(new Map());
        ResearchUtility.ProgressGeneticsTech(50, null, emptyMap);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 0, "Human presence on another map must not qualify");
        var hybrid = Human(emptyMap);
        XMTUtility.hive = true;
        ResearchUtility.ProgressGeneticsTech(50, ownedSource);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 50, "Humanlike hybrid without a morph comp must enable hive human tech");
        ResearchUtility.ProgressGeneticsTech(10, new Pawn { MapHeld = emptyMap, Faction = Enemy });
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 50, "NPC activity must not contribute despite human presence");
        hybrid.Dead = true;
        ResearchUtility.ProgressGeneticsTech(10, null, emptyMap);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 50, "Dead humans must not qualify");
        hybrid.Dead = false;
        emptyMap.mapPawns.pawns.Remove(hybrid);
        ResearchUtility.ProgressGeneticsTech(10, null, emptyMap);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) == 50, "Departed humans must stop passive progress without removing previous research");

        Reset();
        ResearchUtility.SeedCryptobioTech(25, human);
        ResearchUtility.SeedCryptobioTech(500, human);
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Extraction) == 25, "Spawn and recruitment seeds must share a zero-progress gate");
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 0, "Seeds must not advance later projects");

        Reset();
        var parasite = new Pawn { Faction = Player, morph = new object() }; // Held by the host, map passed explicitly.
        ResearchUtility.ProgressEvolutionTech(250, parasite, map);
        Check(Find.ResearchManager.GetProgress(XenoSocialDefOf.XMT_Starbeast_JellyTransport) == 0, "Pre-queen colonies must not earn behaviour progress");
        XMTUtility.hive = true;
        ResearchUtility.ProgressEvolutionTech(250, parasite, map);
        Check(Find.ResearchManager.GetProgress(XenoSocialDefOf.XMT_Starbeast_JellyTransport) == 250, "Owned held parasite must earn hive progress using the host map");
        parasite.Faction = Enemy;
        ResearchUtility.ProgressEvolutionTech(250, parasite, map);
        Check(Find.ResearchManager.GetProgress(XenoSocialDefOf.XMT_Starbeast_JellyTransport) == 250, "Enemy parasite must not earn hive progress");
        parasite.Faction = null;
        ResearchUtility.ProgressEvolutionTech(250, parasite, map);
        Check(Find.ResearchManager.GetProgress(XenoSocialDefOf.XMT_Starbeast_JellyTransport) == 250, "Wild parasite must not earn hive progress");

        Reset();
        var prerequisite = new ResearchProjectDef { tab = XenoGeneDefOf.XMT_HumanResearchTab, Cost = 15 };
        XenoGeneDefOf.XMT_Jelly_Drugs.prerequisites.Add(prerequisite);
        ResearchUtility.ProgressCryptobioTech(int.MaxValue, human);
        Check(Find.ResearchManager.GetProgress(prerequisite) == 15, "Large awards must clamp to the selected prerequisite cost");
        Check(Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Drugs) == 0, "Clamped awards must not spill into later projects");
        prerequisite.tab = XenoSocialDefOf.XMT_StarbeastResearchTab;
        Find.ResearchManager = new ResearchManager();
        ResearchUtility.ProgressCryptobioTech(10, human);
        Check(Find.ResearchManager.GetProgress(prerequisite) == 0, "Prerequisite traversal must not bypass faction eligibility");
        Console.WriteLine($"Automatic research: {assertions} assertions passed.");
    }
}

namespace Verse
{
    public class Thing { public Faction Faction; public Map MapHeld; public bool Destroyed; }
    public class Pawn : Thing
    {
        public bool Dead;
        public RaceProperties RaceProps = new RaceProperties();
        public object morph;
        public Dictionary<KnowledgeCategoryDef, float> knowledge = new Dictionary<KnowledgeCategoryDef, float>();
    }
    public class RaceProperties { public bool Humanlike = true; }
    public class Map { public MapPawns mapPawns = new MapPawns(); }
    public class MapPawns
    {
        public List<Pawn> pawns = new List<Pawn>();
        public IEnumerable<Pawn> SpawnedPawnsInFaction(Faction faction) => pawns.FindAll(p => p.Faction == faction);
    }
    public class ResearchProjectDef
    {
        public ResearchTabDef tab;
        public float Cost = 1000;
        public List<ResearchProjectDef> prerequisites = new List<ResearchProjectDef>();
    }
    public static class Find { public static ResearchManager ResearchManager; public static Map CurrentMap; }
}
namespace RimWorld
{
    public class Faction { public bool IsPlayer; public static Faction OfPlayerSilentFail; }
    public class ResearchTabDef { }
    public class ResearchManager
    {
        private readonly Dictionary<ResearchProjectDef, float> progress = new Dictionary<ResearchProjectDef, float>();
        public float GetProgress(ResearchProjectDef project) => progress.TryGetValue(project, out float value) ? value : 0;
        public void AddProgress(ResearchProjectDef project, float amount, Pawn pawn) => progress[project] = GetProgress(project) + amount;
    }
}
namespace Xenomorphtype
{
    public static class XMTUtility
    {
        public static bool hive;
        public static bool PlayerFactionIsCryptimorph() => hive;
        public static bool PlayerXenosOnMap(Map map) => hive;
        public static object GetMorphComp(this Pawn pawn) => pawn.morph;
    }
    public class KnowledgeCategoryDef { }
    public class KnowledgeProfileDef { public KnowledgeCategoryDef subject; }
    public static class KnowledgeUtility
    {
        public static float GetEffectiveKnowledge(Pawn pawn, KnowledgeCategoryDef subject) =>
            pawn.knowledge.TryGetValue(subject, out float value) ? value : 0;
        public static void ApplyExposure(Pawn pawn, KnowledgeProfileDef profile, float strength, Thing source = null)
        {
            if (profile != null && strength > 0)
                pawn.knowledge[profile.subject] = GetEffectiveKnowledge(pawn, profile.subject) + strength;
        }
    }
    public static class XenoGeneDefOf
    {
        public static readonly ResearchTabDef XMT_HumanResearchTab = new ResearchTabDef();
        public static readonly ResearchProjectDef XMT_Jelly_Extraction = HumanProject();
        public static readonly ResearchProjectDef XMT_Starbeast_Genetics = HumanProject();
        public static readonly ResearchProjectDef XMT_Acid_Utilization = HumanProject();
        public static readonly ResearchProjectDef XMT_Jelly_Drugs = HumanProject();
        public static readonly ResearchProjectDef XMT_Mutation_Targeted = HumanProject();
        private static ResearchProjectDef HumanProject() => new ResearchProjectDef { tab = XMT_HumanResearchTab };
    }
    public static class XenoSocialDefOf
    {
        public static readonly ResearchTabDef XMT_StarbeastResearchTab = new ResearchTabDef();
        public static readonly ResearchProjectDef XMT_Starbeast_Sculpture = HiveProject();
        public static readonly ResearchProjectDef XMT_Starbeast_JellyTransport = HiveProject();
        public static readonly ResearchProjectDef XMT_Starbeast_Eggs = HiveProject();
        public static readonly ResearchProjectDef XMT_Starbeast_Reinforcement = HiveProject();
        private static ResearchProjectDef HiveProject() => new ResearchProjectDef { tab = XMT_StarbeastResearchTab };
    }
}
