using System;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public enum NemesisObservationUpdateMode
    {
        ExplicitCensus,
        EventImmediate,
        Hybrid
    }

    public enum NemesisTraversalCategory
    {
        PlayerBuildings,
        StoredItems,
        SpawnedPawns,
        HeldPawns,
        Caravans,
        WorldPawns,
        Terrain,
        RoomsAndRegions,
        PowerNets,
        CryptimorphCorpses
    }

    public class NemesisSettingsDef : Def
    {
        public int missionIntervalTicks = 60000;
        public float missionOpportunityChance;
        public float missionOpportunityChanceAtActivation = 0.95f;
        public float missionOpportunityChanceExponent = 2f;
        public float activationXenoforming = 10f;
        public float initialCensusConfidence = 0.65f;
        public int recentEvidenceCapacity = 128;
        public float abductedXenotypeProcessingDays = 3f;
        public float stanceCommitmentDays = 7f;
        public float challengerMargin = 0.15f;
        public int maxSpatialContactsPerObservation = 256;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (missionOpportunityChance < 0f || missionOpportunityChance > 1f
                || missionOpportunityChanceAtActivation < missionOpportunityChance
                || missionOpportunityChanceAtActivation > 1f)
                yield return defName + " requires mission opportunity chances between zero and one, with activation chance at least the baseline.";
            if (missionOpportunityChanceExponent <= 0f)
                yield return defName + " requires a positive mission opportunity chance exponent.";
            if (activationXenoforming <= 0f) yield return defName + " requires positive activation xenoforming.";
        }
    }

    public class NemesisEvidenceTagDef : Def
    {
    }

    public class NemesisEvidenceDef : Def
    {
        public float defaultAmount = 1f;
        public float halfLifeDays = 30f;
        public List<NemesisEvidenceTagDef> tags = new List<NemesisEvidenceTagDef>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (halfLifeDays < 0f)
            {
                yield return defName + " has a negative evidence half-life.";
            }
        }
    }

    public class NemesisObservationDef : Def
    {
        public Type workerClass;
        public string metric;
        public NemesisObservationUpdateMode updateMode = NemesisObservationUpdateMode.ExplicitCensus;
        public List<NemesisTraversalCategory> traversalCategories = new List<NemesisTraversalCategory>();
        public float baseConfidence = 0.75f;
        public float staleHalfLifeDays = 30f;
        public float minimumConfidence = 0.2f;
        public bool recordsSpatialContacts;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null || !typeof(NemesisObservationWorker).IsAssignableFrom(workerClass))
            {
                yield return defName + " requires a workerClass derived from NemesisObservationWorker.";
            }

            if (metric.NullOrEmpty())
            {
                yield return defName + " requires a metric identifier.";
            }

            if (baseConfidence < 0f || baseConfidence > 1f)
            {
                yield return defName + " baseConfidence must be between zero and one.";
            }

            if (minimumConfidence < 0f || minimumConfidence > 1f)
            {
                yield return defName + " minimumConfidence must be between zero and one.";
            }
        }
    }

    public class NemesisSignalInput
    {
        public NemesisEvidenceDef evidence;
        public NemesisObservationDef observation;
        public NemesisObservationDef divisorObservation;
        public float minimumDivisor = 1f;
        public float weight = 1f;

        public IEnumerable<string> ConfigErrors(string owner)
        {
            if ((evidence == null) == (observation == null))
            {
                yield return owner + " signal input must define exactly one evidence or observation source.";
            }
            if (minimumDivisor <= 0f)
            {
                yield return owner + " signal input minimumDivisor must be greater than zero.";
            }
        }
    }

    public class NemesisSignalDef : Def
    {
        public Type workerClass = typeof(NemesisSignalWorker_Sum);
        public List<NemesisSignalInput> inputs = new List<NemesisSignalInput>();
        public float saturation = 1f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null || !typeof(NemesisSignalWorker).IsAssignableFrom(workerClass))
            {
                yield return defName + " requires a workerClass derived from NemesisSignalWorker.";
            }

            if (inputs.NullOrEmpty())
            {
                yield return defName + " requires at least one input.";
            }
            else
            {
                foreach (NemesisSignalInput input in inputs)
                {
                    if (input == null)
                    {
                        yield return defName + " contains a null signal input.";
                        continue;
                    }

                    foreach (string error in input.ConfigErrors(defName))
                    {
                        yield return error;
                    }
                }
            }

            if (saturation <= 0f)
            {
                yield return defName + " saturation must be greater than zero.";
            }
        }
    }

    public class NemesisStanceCriterion
    {
        public NemesisSignalDef signal;
        public float weight = 1f;
        public float minimum;
    }

    public class NemesisStanceDef : Def
    {
        public Type workerClass = typeof(NemesisStanceWorker_Weighted);
        public List<NemesisStanceCriterion> criteria = new List<NemesisStanceCriterion>();
        public RoyalEvolutionSet evolutionSet;
        public bool isFallback;
        public float baseScore;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (workerClass == null || !typeof(NemesisStanceWorker).IsAssignableFrom(workerClass))
            {
                yield return defName + " requires a workerClass derived from NemesisStanceWorker.";
            }

            if (!isFallback && criteria.NullOrEmpty())
            {
                yield return defName + " requires criteria or must be marked as the fallback stance.";
            }

            if (criteria != null)
            {
                foreach (NemesisStanceCriterion criterion in criteria)
                {
                    if (criterion?.signal == null)
                    {
                        yield return defName + " contains a criterion without a signal.";
                    }
                }
            }
        }
    }
}
