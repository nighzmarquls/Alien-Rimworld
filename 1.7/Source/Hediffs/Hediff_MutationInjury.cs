using Verse;

namespace Xenomorphtype
{
    // Retain vanilla wound rendering without making altered tissue an ordinary healing injury.
    public class Hediff_MutationInjury : Hediff_Injury
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);

        public override void Heal(float amount) { }

        public override bool TryMergeWith(Hediff other) => MergeMutation(this, other);

        public static bool IsMutationInjury(Hediff injury) => injury is Hediff_MutationInjury ||
            (injury is Hediff_Injury && injury.def?.hediffClass != null &&
             typeof(Hediff_MutationInjury).IsAssignableFrom(injury.def.hediffClass));

        // The def check also protects vanilla injury instances retained by older saves.
        public static bool MergeMutation(Hediff_Injury injury, Hediff other)
        {
            if (!(other is Hediff_Injury) || other.def != injury.def || other.Part != injury.Part ||
                injury.def.injuryProps?.canMerge != true) return false;

            injury.Severity = typeof(Hediff_Protofluid).IsAssignableFrom(injury.def.hediffClass)
                ? injury.def.maxSeverity : UnityEngine.Mathf.Min(injury.def.maxSeverity, injury.Severity + other.Severity);
            injury.ageTicks = 0;
            if (injury.comps != null)
                foreach (HediffComp comp in injury.comps) comp.CompPostMerged(other);
            return true;
        }
    }

    // Exposure clears through its XML severity decay, never through ordinary wound healing.
    public class Hediff_Protofluid : Hediff_MutationInjury { }
}
