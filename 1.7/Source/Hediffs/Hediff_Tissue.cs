using RimWorld;
using Verse;

namespace Xenomorphtype
{
    // Thin native presentation adapters; metadata and wording remain on each HediffDef.
    public class Hediff_Tissue : HediffWithComps
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);
    }

    public class Hediff_TissueAddedPart : Hediff_AddedPart
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);
    }

    public class Hediff_TissueHigh : Hediff_High
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);
    }

    public class Hediff_TissueAddiction : Hediff_Addiction
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);
    }
}
