using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_SurgeryInspectableEmbryo : HediffComp_SurgeryInspectable
    {

        public new HediffCompProperties_SurgeryInspectableEmbryo Props => (HediffCompProperties_SurgeryInspectableEmbryo)props;

        public override SurgicalInspectionOutcome DoSurgicalInspection(Pawn surgeon)
        {
            KnowledgeUtility.ApplyExposure(surgeon, Props.knowledgeProfile, Props.knowledgeMagnitude, KnowledgeAcquisition.ControlledExperience, Pawn);
            return SurgicalInspectionOutcome.Nothing;
           

        }
    }

    public class HediffCompProperties_SurgeryInspectableEmbryo : HediffCompProperties_SurgeryInspectable
    {
        public KnowledgeProfileDef knowledgeProfile;
        public float knowledgeMagnitude = 0.25f;
        public HediffCompProperties_SurgeryInspectableEmbryo()
        {
            compClass = typeof(HediffComp_SurgeryInspectableEmbryo);
        }
    }
    
}
