using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public static class JuggernautAbilityUtility
    {
        public const float ReferenceBodySize = 3f;
        public const float MaximumScaledBodySize = 10f;
        public const float BaseChargeRange = 10f;
        public const float BaseEffectRadius = 1.5f;
        public const float BaseDirectPawnRadius = 0.5f;

        public static float BodyScale(Pawn pawn)
        {
            float bodySize = Mathf.Clamp(pawn?.BodySize ?? ReferenceBodySize, ReferenceBodySize, MaximumScaledBodySize);
            return Mathf.Lerp(1f, 2f, Mathf.InverseLerp(ReferenceBodySize, MaximumScaledBodySize, bodySize));
        }

        public static float MovementScale(Pawn pawn)
        {
            if (pawn?.def == null)
            {
                return 1f;
            }

            float baseMoveSpeed = pawn.def.GetStatValueAbstract(StatDefOf.MoveSpeed);
            if (baseMoveSpeed <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0f, pawn.GetStatValue(StatDefOf.MoveSpeed) / baseMoveSpeed);
        }

        public static float TrampleRange(Pawn pawn)
        {
            return BaseChargeRange * MovementScale(pawn);
        }

        public static float OverrunRange(Pawn pawn)
        {
            return BaseChargeRange * BodyScale(pawn) * MovementScale(pawn);
        }

        public static float OverrunEffectRadius(Pawn pawn)
        {
            return BaseEffectRadius * BodyScale(pawn);
        }

        public static float OverrunDirectPawnRadius(Pawn pawn)
        {
            return BaseDirectPawnRadius * BodyScale(pawn);
        }

        public static float DefensiveStatureRadius(Pawn pawn)
        {
            return BaseEffectRadius * BodyScale(pawn);
        }
    }

    public class Verb_CastTrample : Verb_CastAbility
    {
        public override float EffectiveRange => JuggernautAbilityUtility.TrampleRange(CasterPawn);

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (CasterPawn != null)
            {
                GenDraw.DrawRadiusRing(CasterPawn.Position, EffectiveRange);
            }

            Ability?.DrawEffectPreviews(target);
        }
    }

    public class Verb_CastOverrun : Verb_CastAbility
    {
        public override float EffectiveRange => JuggernautAbilityUtility.OverrunRange(CasterPawn);

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (CasterPawn != null)
            {
                GenDraw.DrawRadiusRing(CasterPawn.Position, EffectiveRange);
            }

            Ability?.DrawEffectPreviews(target);
        }
    }
}
