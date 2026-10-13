using UnityEngine;

namespace Xenomorphtype
{
    public enum EvolutionScalingCurve
    {
        LinearDelta,
        Proportional,
        Inverse
    }

    public enum EvolutionScalingRounding
    {
        None,
        Round,
        Floor,
        Ceiling
    }

    public readonly struct EvolutionScalingFactor
    {
        public readonly float current;
        public readonly float reference;
        public readonly float weight;

        public EvolutionScalingFactor(float current, float reference, float weight = 1f)
        {
            this.current = current;
            this.reference = reference;
            this.weight = weight;
        }
    }

    public static class EvolutionScalingUtility
    {
        public static float NormalizedPower(params EvolutionScalingFactor[] factors)
        {
            float power = 1f;
            if (factors == null)
            {
                return power;
            }

            foreach (EvolutionScalingFactor factor in factors)
            {
                if (factor.reference <= 0f)
                {
                    return 0f;
                }

                power *= Mathf.Pow(Mathf.Max(0f, factor.current) / factor.reference, factor.weight);
            }

            return power;
        }

        public static float Scale(float baseValue, float response, float minimum, float maximum,
            EvolutionScalingCurve curve, EvolutionScalingRounding rounding, params EvolutionScalingFactor[] factors)
        {
            float normalizedPower = NormalizedPower(factors);
            float value;
            switch (curve)
            {
                case EvolutionScalingCurve.Proportional:
                    value = baseValue * normalizedPower;
                    break;
                case EvolutionScalingCurve.Inverse:
                    value = baseValue / Mathf.Max(normalizedPower, 0.0001f);
                    break;
                default:
                    value = baseValue + (normalizedPower - 1f) * response;
                    break;
            }

            value = Mathf.Clamp(value, minimum, maximum);
            switch (rounding)
            {
                case EvolutionScalingRounding.Round:
                    return Mathf.Round(value);
                case EvolutionScalingRounding.Floor:
                    return Mathf.Floor(value);
                case EvolutionScalingRounding.Ceiling:
                    return Mathf.Ceil(value);
                default:
                    return value;
            }
        }
    }
}
