using System;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public abstract class NemesisSignalWorker
    {
        public abstract NemesisSignalResult Evaluate(NemesisSignalDef def, GameComponent_Nemesis component, int tick);
    }

    public sealed class NemesisSignalWorker_Sum : NemesisSignalWorker
    {
        public override NemesisSignalResult Evaluate(NemesisSignalDef def, GameComponent_Nemesis component, int tick)
        {
            NemesisSignalResult result = new NemesisSignalResult { signal = def };
            float total = 0f;

            foreach (NemesisSignalInput input in def.inputs.Where(value => value != null))
            {
                float raw;
                float confidence;
                string source;
                if (input.evidence != null)
                {
                    raw = component.EvidenceValue(input.evidence, tick);
                    confidence = 1f;
                    source = "evidence:" + input.evidence.defName;
                }
                else
                {
                    NemesisObservationRecord record = component.GetObservation(input.observation);
                    raw = record?.value ?? 0f;
                    confidence = record?.EffectiveConfidenceAt(tick) ?? 0f;
                    source = "observation:" + input.observation.defName;
                }

                if (input.divisorObservation != null)
                {
                    NemesisObservationRecord divisor = component.GetObservation(input.divisorObservation);
                    float divisorValue = Mathf.Max(input.minimumDivisor, divisor?.value ?? 0f);
                    raw /= divisorValue;
                    confidence = Mathf.Min(confidence, divisor?.EffectiveConfidenceAt(tick) ?? 0f);
                    source += " / observation:" + input.divisorObservation.defName;
                }

                float contribution = raw * confidence * input.weight;
                total += contribution;
                result.contributions.Add(new NemesisContribution
                {
                    source = source,
                    rawValue = raw,
                    confidence = confidence,
                    weight = input.weight,
                    contribution = contribution
                });
            }

            result.value = Mathf.Clamp01(total / Mathf.Max(0.0001f, def.saturation));
            return result;
        }
    }

    public static class NemesisSignalEvaluator
    {
        public static NemesisSignalResult Evaluate(NemesisSignalDef def, GameComponent_Nemesis component, int tick)
        {
            try
            {
                NemesisSignalWorker worker = Activator.CreateInstance(def.workerClass) as NemesisSignalWorker;
                return worker?.Evaluate(def, component, tick) ?? new NemesisSignalResult { signal = def };
            }
            catch (Exception exception)
            {
                Verse.Log.Error("[XMT][Nemesis] Could not evaluate signal " + def.defName + ": " + exception);
                return new NemesisSignalResult { signal = def };
            }
        }
    }
}
