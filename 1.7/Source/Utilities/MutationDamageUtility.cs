using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public enum AcidTissueHazard { None, Toxic, Corrosive }

    public static class MutationDamageUtility
    {
        public static bool AllowsActivityInjury(bool downed, bool resting, bool canMove, bool canManipulate,
            HediffCompProperties_GiveInjury props) =>
            (!downed || props.injureIfDowned) && (!resting || props.injureWhileResting) &&
            (canMove || props.injureWithoutMoving) && (canManipulate || props.injureWithoutManipulation);

        public static float EffectiveInjuryDamage(HediffCompProperties_GiveInjury props, float severity, float protection)
        {
            if (props.damageDef == null || props.chancebySeverity == null || props.chancebySeverity.Evaluate(severity) <= 0f) return 0f;
            return Mathf.Max(0f, props.maxDamage) * (1f - Mathf.Clamp01(protection));
        }

        public static float InjuryDamage(Hediff tissue)
        {
            var props = tissue.TryGetComp<HediffComp_GiveInjury>()?.props as HediffCompProperties_GiveInjury;
            return props == null ? 0f : EffectiveInjuryDamage(props, tissue.Severity,
                props.latticeProtection ? tissue.pawn.MesoSkeletonization() : 0f);
        }

        public static float UnprotectedAcidLoad(float acid, float lattice, float silicate) =>
            Mathf.Max(0f, acid - lattice - silicate / 2f);

        public static AcidTissueHazard AcidHazard(float load, float bloodFullness, float severity, HediffCompProperties_AcidBloodGiver props)
        {
            if (load <= 0f || bloodFullness <= 0f || severity <= 0f) return AcidTissueHazard.None;
            if (load < props.burnSeverityThreshold && props.toxinBuildup > 0f) return AcidTissueHazard.Toxic;
            if (load > props.burnSeverityThreshold && props.damagePerSeverity > 0f) return AcidTissueHazard.Corrosive;
            return AcidTissueHazard.None;
        }

        public static AcidTissueHazard AcidHazard(Hediff tissue)
        {
            var props = tissue.TryGetComp<HediffComp_AcidBloodGiver>()?.props as HediffCompProperties_AcidBloodGiver;
            Pawn pawn = tissue.pawn;
            return props == null || pawn.AcidBloodValue() <= 0f ? AcidTissueHazard.None : AcidHazard(
                UnprotectedAcidLoad(pawn.AcidBloodValue(), pawn.MesoSkeletonization(), pawn.CarboSilicate()),
                AcidUtility.GetBloodFullness(pawn, false), tissue.Severity, props);
        }

    }
}
