using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompBioContainer : ThingComp
    {
        private float occupantDrawScale = 1f;

        public CompProperties_BioContainer Props => (CompProperties_BioContainer)props;
        public float OccupantDrawScale => occupantDrawScale;

        public bool CanContain(Pawn pawn, bool medicalExtraction = false)
        {
            float maximumSize = medicalExtraction ? Props.maxMedicalExtractionBodySize : Props.maxBodySize;
            return pawn != null && !pawn.Dead && pawn.BodySize <= maximumSize;
        }

        public void Notify_Accepted(Pawn pawn, bool medicalExtraction)
        {
            occupantDrawScale = medicalExtraction && pawn.BodySize > Props.maxBodySize
                ? Props.medicalExtractionDrawScale
                : Props.occupantDrawScale;
        }

        public void Notify_Emptied()
        {
            occupantDrawScale = Props.occupantDrawScale;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref occupantDrawScale, "occupantDrawScale", Props.occupantDrawScale);
        }

        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            props = parent.def.GetCompProperties<CompProperties_BioContainer>();
            if (parent is Building_BioContainer container && container.ContainedThing is Pawn pawn)
            {
                occupantDrawScale = pawn.BodySize > Props.maxBodySize
                    ? Props.medicalExtractionDrawScale
                    : Props.occupantDrawScale;
            }
            else
            {
                occupantDrawScale = Props.occupantDrawScale;
            }
        }
    }

    public class CompProperties_BioContainer : CompProperties
    {
        public float maxBodySize = 0.35f;
        public float maxMedicalExtractionBodySize = 2f;
        public float occupantDrawScale = 1f;
        public float medicalExtractionDrawScale = 0.35f;
        public Vector3 occupantDrawOffset = new Vector3(0f, 0f, -0.05f);
        public float occupantDrawAngle;
        public bool suspendContents = true;

        public CompProperties_BioContainer()
        {
            compClass = typeof(CompBioContainer);
        }
    }
}
