using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public sealed class NemesisStrain : IExposable
    {
        public string id;
        public string source;
        public string donorId;
        public List<GeneDef> genes = new List<GeneDef>();
        public List<GeneDef> removedDefaults = new List<GeneDef>();
        public int donorComplexity;
        public int availableTick;
        public bool acquired;
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id"); Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref donorId, "donorId"); Scribe_Values.Look(ref donorComplexity, "donorComplexity");
            Scribe_Values.Look(ref availableTick, "availableTick"); Scribe_Values.Look(ref acquired, "acquired");
            Scribe_Collections.Look(ref genes, "genes", LookMode.Def);
            Scribe_Collections.Look(ref removedDefaults, "removedDefaults", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                genes ??= new List<GeneDef>(); removedDefaults ??= new List<GeneDef>();
                genes.RemoveAll(gene => gene == null); removedDefaults.RemoveAll(gene => gene == null);
            }
        }
        public HorrorGenePayload Payload => new HorrorGenePayload(genes, "XMT_NemesisStrainLabel".Translate(), removedDefaults);
        internal static string Signature(IEnumerable<GeneDef> genes) => string.Join(",", genes.Where(gene => gene != null)
            .Distinct().OrderBy(gene => gene.defName).Select(gene => gene.defName));
    }

    public sealed class NemesisFactionSample : IExposable
    {
        public Faction faction;
        public List<XenotypeDef> xenotypes = new List<XenotypeDef>();
        public List<int> remaining = new List<int>();
        public int sampled;
        public void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref xenotypes, "xenotypes", LookMode.Def);
            Scribe_Collections.Look(ref remaining, "remaining", LookMode.Value);
            Scribe_Values.Look(ref sampled, "sampled");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { xenotypes ??= new List<XenotypeDef>(); remaining ??= new List<int>(); }
        }
    }

    public sealed class NemesisGeneFitness : IExposable
    {
        public GeneDef gene;
        public NemesisMissionDef profile; // null = generic, shared across all adult missions
        public float mean;
        public int trials;
        public int failedProbes;
        public int retryAfterTick;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref gene, "gene"); Scribe_Defs.Look(ref profile, "profile");
            Scribe_Values.Look(ref mean, "mean"); Scribe_Values.Look(ref trials, "trials");
            Scribe_Values.Look(ref failedProbes, "failedProbes"); Scribe_Values.Look(ref retryAfterTick, "retryAfterTick");
        }
        internal void Observe(float value) { trials++; mean += (value - mean) / System.Math.Min(trials, 12); }
    }

    public sealed class NemesisGeneticMember : IExposable
    {
        public Pawn pawn;
        public string pawnId;
        public List<GeneDef> genes = new List<GeneDef>();
        public int kills;
        public int extracted;
        public float injury;
        public bool escaped;
        public bool terminal;
        public IntVec3 lastCell = IntVec3.Invalid;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn"); Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Collections.Look(ref genes, "genes", LookMode.Def);
            Scribe_Values.Look(ref kills, "kills"); Scribe_Values.Look(ref extracted, "extracted");
            Scribe_Values.Look(ref injury, "injury"); Scribe_Values.Look(ref escaped, "escaped");
            Scribe_Values.Look(ref terminal, "terminal"); Scribe_Values.Look(ref lastCell, "lastCell", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) genes ??= new List<GeneDef>();
        }
    }

    public sealed class NemesisDefenseSnapshot : IExposable
    {
        public Thing target;
        public int hitPoints;
        public float weight;
        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target"); Scribe_Values.Look(ref hitPoints, "hitPoints");
            Scribe_Values.Look(ref weight, "weight", 1f);
        }
    }

    public sealed class NemesisGeneticTrial : IExposable
    {
        public int id;
        public int cycle;
        public NemesisMissionDef mission;
        public int mapId;
        public int startedTick;
        public int settleAfterTick = -1;
        public string strainId;
        public GeneDef added;
        public GeneDef removed;
        public bool probe;
        public float baseline;
        public float baselineGeneric;
        public float genericScore;
        public float specialistScore;
        public bool operationEnded;
        public bool finalized;
        public bool successful;
        public bool followUpPending;
        public bool followUpReported;
        public List<NemesisGeneticMember> members = new List<NemesisGeneticMember>();
        public List<NemesisDefenseSnapshot> defenses = new List<NemesisDefenseSnapshot>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id"); Scribe_Values.Look(ref cycle, "cycle"); Scribe_Defs.Look(ref mission, "mission");
            Scribe_Values.Look(ref mapId, "mapId"); Scribe_Values.Look(ref startedTick, "startedTick");
            Scribe_Values.Look(ref settleAfterTick, "settleAfterTick", -1); Scribe_Values.Look(ref strainId, "strainId");
            Scribe_Defs.Look(ref added, "added"); Scribe_Defs.Look(ref removed, "removed");
            Scribe_Values.Look(ref probe, "probe"); Scribe_Values.Look(ref baseline, "baseline");
            Scribe_Values.Look(ref baselineGeneric, "baselineGeneric");
            Scribe_Values.Look(ref genericScore, "genericScore"); Scribe_Values.Look(ref specialistScore, "specialistScore");
            Scribe_Values.Look(ref operationEnded, "operationEnded"); Scribe_Values.Look(ref finalized, "finalized");
            Scribe_Values.Look(ref successful, "successful");
            Scribe_Values.Look(ref followUpPending, "followUpPending"); Scribe_Values.Look(ref followUpReported, "followUpReported");
            Scribe_Collections.Look(ref members, "members", LookMode.Deep);
            Scribe_Collections.Look(ref defenses, "defenses", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { members ??= new List<NemesisGeneticMember>(); defenses ??= new List<NemesisDefenseSnapshot>(); }
        }
    }

    public sealed class NemesisRecoveryRecord : IExposable
    {
        public Pawn pawn;
        public int readyTick;
        public void ExposeData() { Scribe_References.Look(ref pawn, "pawn"); Scribe_Values.Look(ref readyTick, "readyTick"); }
    }

    public sealed class NemesisCapabilityDef : Def
    {
        public float minimumXenoforming;
        public RoyalEvolutionDef requiredEvolution;
        public bool Available(GameComponent_Nemesis component) => XenoformingUtility.GetXenoforming() >= minimumXenoforming
            && (requiredEvolution == null || component.GeneticFeature(requiredEvolution));
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (minimumXenoforming < 0f || minimumXenoforming > 100f) yield return defName + ": invalid xenoforming threshold.";
        }
    }
}
