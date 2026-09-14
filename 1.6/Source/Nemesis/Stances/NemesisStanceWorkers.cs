using System;
using System.Collections.Generic;
using System.Linq;

namespace Xenomorphtype
{
    public abstract class NemesisStanceWorker
    {
        public abstract NemesisStanceResult Evaluate(NemesisStanceDef def, IReadOnlyDictionary<NemesisSignalDef, NemesisSignalResult> signals);
    }

    public sealed class NemesisStanceWorker_Weighted : NemesisStanceWorker
    {
        public override NemesisStanceResult Evaluate(NemesisStanceDef def, IReadOnlyDictionary<NemesisSignalDef, NemesisSignalResult> signals)
        {
            NemesisStanceResult result = new NemesisStanceResult
            {
                stance = def,
                score = def.baseScore,
                eligible = true
            };

            foreach (NemesisStanceCriterion criterion in def.criteria.Where(value => value?.signal != null))
            {
                float value = signals.TryGetValue(criterion.signal, out NemesisSignalResult signal) ? signal.value : 0f;
                if (value < criterion.minimum)
                {
                    result.eligible = false;
                    result.failureReasons.Add(criterion.signal.defName + "=" + value.ToString("0.###")
                        + " is below minimum " + criterion.minimum.ToString("0.###"));
                }

                float contribution = value * criterion.weight;
                result.score += contribution;
                result.contributions.Add(new NemesisContribution
                {
                    source = "signal:" + criterion.signal.defName,
                    rawValue = value,
                    confidence = 1f,
                    weight = criterion.weight,
                    contribution = contribution
                });
            }
            return result;
        }
    }

    public static class NemesisStanceEvaluator
    {
        public static NemesisStanceResult Evaluate(NemesisStanceDef def, IReadOnlyDictionary<NemesisSignalDef, NemesisSignalResult> signals)
        {
            try
            {
                NemesisStanceWorker worker = Activator.CreateInstance(def.workerClass) as NemesisStanceWorker;
                return worker?.Evaluate(def, signals) ?? new NemesisStanceResult { stance = def, eligible = false };
            }
            catch (Exception exception)
            {
                Verse.Log.Error("[XMT][Nemesis] Could not evaluate stance " + def.defName + ": " + exception);
                return new NemesisStanceResult { stance = def, eligible = false };
            }
        }
    }
}
