using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public sealed class NemesisXenotypeRecord : IExposable
    {
        public XenotypeDef xenotype;
        public string label;
        public List<GeneDef> genes = new List<GeneDef>();
        public string signature;
        public string origin;
        public int acquiredTick;
        public int availableTick;
        public int sampleCount = 1;
        public int lastSampledTick;
        public int abductedCount;

        public bool AvailableAt(int tick)
        {
            return tick >= availableTick;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref xenotype, "xenotype");
            Scribe_Values.Look(ref label, "label");
            Scribe_Collections.Look(ref genes, "genes", LookMode.Def);
            Scribe_Values.Look(ref signature, "signature");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref acquiredTick, "acquiredTick", 0);
            Scribe_Values.Look(ref availableTick, "availableTick", 0);
            Scribe_Values.Look(ref sampleCount, "sampleCount", 1);
            Scribe_Values.Look(ref lastSampledTick, "lastSampledTick", 0);
            Scribe_Values.Look(ref abductedCount, "abductedCount", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                genes ??= new List<GeneDef>();
            }
        }

        public static NemesisXenotypeRecord FromPawn(Pawn pawn, string origin, int acquiredTick, int availableTick)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return null;
            }

            List<GeneDef> genes = pawn.genes.GenesListForReading
                .Where(gene => gene?.def != null)
                .Select(gene => gene.def)
                .Distinct()
                .OrderBy(gene => gene.defName)
                .ToList();
            XenotypeDef xenotype = pawn.genes.Xenotype;
            bool custom = pawn.genes.CustomXenotype != null;
            string signature = !custom && xenotype != null
                ? "def:" + xenotype.defName
                : "custom:" + string.Join(",", genes.Select(gene => gene.defName));
            return new NemesisXenotypeRecord
            {
                xenotype = xenotype,
                label = pawn.genes.XenotypeLabel,
                genes = genes,
                signature = signature,
                origin = origin,
                acquiredTick = acquiredTick,
                availableTick = availableTick,
                lastSampledTick = acquiredTick
            };
        }
    }

    public sealed class NemesisSpatialContact : IExposable
    {
        public NemesisObservationDef observation;
        public string observedThingId;
        public string subjectId;
        public string defName;
        public string label;
        public int mapId = -1;
        public string mapLabel;
        public IntVec3 cell = IntVec3.Invalid;
        public int observedTick = -1;
        public float confidence;
        public float threat;
        public List<string> tags = new List<string>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref observation, "observation");
            Scribe_Values.Look(ref observedThingId, "observedThingId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref mapLabel, "mapLabel");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref observedTick, "observedTick", -1);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref threat, "threat", 0f);
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tags ??= new List<string>();
            }
        }

        public float EffectiveConfidenceAt(int tick)
        {
            if (observation == null || observedTick < 0)
            {
                return 0f;
            }

            if (observation.staleHalfLifeDays <= 0f)
            {
                return Mathf.Clamp01(confidence);
            }

            float elapsedDays = Mathf.Max(0, tick - observedTick) / 60000f;
            return Mathf.Clamp01(Mathf.Max(observation.minimumConfidence,
                confidence * Mathf.Pow(0.5f, elapsedDays / observation.staleHalfLifeDays)));
        }

        public static NemesisSpatialContact FromThing(NemesisObservationDef observation, Thing thing,
            int tick, float confidence, float threat, params string[] tags)
        {
            Map map = thing?.MapHeld;
            if (observation == null || thing == null || map == null || !thing.PositionHeld.IsValid)
            {
                return null;
            }

            return new NemesisSpatialContact
            {
                observation = observation,
                observedThingId = thing.GetUniqueLoadID(),
                subjectId = thing.GetUniqueLoadID(),
                defName = thing.def?.defName,
                label = thing.LabelCap,
                mapId = map.uniqueID,
                mapLabel = map.Parent?.LabelCap,
                cell = thing.PositionHeld,
                observedTick = tick,
                confidence = confidence,
                threat = threat,
                tags = tags?.Where(tag => !tag.NullOrEmpty()).Distinct().ToList() ?? new List<string>()
            };
        }
    }

    public sealed class NemesisEvidenceAggregate : IExposable
    {
        public NemesisEvidenceDef evidence;
        public float value;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref value, "value", 0f);
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }

        public float ValueAt(int tick)
        {
            if (value <= 0f || lastUpdatedTick < 0 || evidence == null || evidence.halfLifeDays <= 0f)
            {
                return Mathf.Max(0f, value);
            }

            float elapsedDays = Mathf.Max(0, tick - lastUpdatedTick) / 60000f;
            return value * Mathf.Pow(0.5f, elapsedDays / evidence.halfLifeDays);
        }

        public void Add(float amount, int tick)
        {
            value = ValueAt(tick) + amount;
            lastUpdatedTick = tick;
        }
    }

    public sealed class NemesisMapEvidenceAggregate : IExposable
    {
        public NemesisEvidenceDef evidence;
        public int mapId = -1;
        public string mapLabel;
        public float value;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref mapLabel, "mapLabel");
            Scribe_Values.Look(ref value, "value", 0f);
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }

        public float ValueAt(int tick)
        {
            if (value <= 0f || lastUpdatedTick < 0 || evidence == null || evidence.halfLifeDays <= 0f)
                return Mathf.Max(0f, value);
            float elapsedDays = Mathf.Max(0, tick - lastUpdatedTick) / 60000f;
            return value * Mathf.Pow(0.5f, elapsedDays / evidence.halfLifeDays);
        }

        public void Add(float amount, int tick)
        {
            value = ValueAt(tick) + amount;
            lastUpdatedTick = tick;
        }
    }

    public sealed class NemesisEvidenceEvent : IExposable
    {
        public NemesisEvidenceDef evidence;
        public int tick;
        public float amount;
        public string actorId;
        public string subjectId;
        public string sourceDefName;
        public string detail;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref amount, "amount", 0f);
            Scribe_Values.Look(ref actorId, "actorId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref sourceDefName, "sourceDefName");
            Scribe_Values.Look(ref detail, "detail");
        }
    }

    public sealed class NemesisObservationRecord : IExposable
    {
        public NemesisObservationDef observation;
        public float value;
        public int observedTick = -1;
        public float confidence;
        public float coverage = 1f;
        public string source;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref observation, "observation");
            Scribe_Values.Look(ref value, "value", 0f);
            Scribe_Values.Look(ref observedTick, "observedTick", -1);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref coverage, "coverage", 1f);
            Scribe_Values.Look(ref source, "source");
        }

        public float EffectiveConfidenceAt(int tick)
        {
            if (observation == null || observedTick < 0)
            {
                return 0f;
            }

            if (observation.staleHalfLifeDays <= 0f)
            {
                return Mathf.Clamp01(confidence * coverage);
            }

            float elapsedDays = Mathf.Max(0, tick - observedTick) / 60000f;
            float aged = confidence * Mathf.Pow(0.5f, elapsedDays / observation.staleHalfLifeDays);
            return Mathf.Clamp01(Mathf.Max(observation.minimumConfidence, aged) * coverage);
        }
    }

    public sealed class NemesisContribution
    {
        public string source;
        public float rawValue;
        public float confidence = 1f;
        public float weight = 1f;
        public float contribution;
    }

    public sealed class NemesisSignalResult
    {
        public NemesisSignalDef signal;
        public float value;
        public readonly List<NemesisContribution> contributions = new List<NemesisContribution>();
    }

    public sealed class NemesisStanceResult
    {
        public NemesisStanceDef stance;
        public float score;
        public bool eligible = true;
        public readonly List<NemesisContribution> contributions = new List<NemesisContribution>();
        public readonly List<string> failureReasons = new List<string>();
    }

    public sealed class NemesisAssessment
    {
        public int tick;
        public readonly List<NemesisSignalResult> signals = new List<NemesisSignalResult>();
        public readonly List<NemesisStanceResult> stances = new List<NemesisStanceResult>();
        public NemesisStanceResult recommended;
        public string commitmentExplanation;
    }
}
