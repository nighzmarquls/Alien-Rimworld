using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_SurgeryInspectableEmbryo : HediffComp_SurgeryInspectable
    {

        public new HediffCompProperties_SurgeryInspectableEmbryo Props => (HediffCompProperties_SurgeryInspectableEmbryo)props;

        public bool Identified => identified;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref identified, "identified", false);
        }

        private bool identified;

        public override SurgicalInspectionOutcome DoSurgicalInspection(Pawn surgeon)
        {
            if (Identified) return SurgicalInspectionOutcome.Nothing;
            identified = true;
            KnowledgeUtility.ApplyExposure(surgeon, Props.knowledgeProfile, Props.knowledgeMagnitude, KnowledgeAcquisition.ControlledExperience, Pawn);
            XMTMedicalExaminationUtility.NotifyConditionDiscovered(surgeon, Props.cryptobioResearch);
            return SurgicalInspectionOutcome.Detected;
        }
    }

    public class HediffCompProperties_SurgeryInspectableEmbryo : HediffCompProperties_SurgeryInspectable
    {
        public KnowledgeProfileDef knowledgeProfile;
        public float knowledgeMagnitude = 0.25f;
        public int cryptobioResearch = 10;
        public HediffCompProperties_SurgeryInspectableEmbryo()
        {
            compClass = typeof(HediffComp_SurgeryInspectableEmbryo);
        }
    }
    
}
