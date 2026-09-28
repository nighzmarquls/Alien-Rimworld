using Verse;

namespace Xenomorphtype
{
    public class CompBioContainer : ThingComp
    {
        private float occupantDrawScale = 1f;
        private Thing transferTarget;

        public CompProperties_BioContainer Props => (CompProperties_BioContainer)props;
        public Thing TransferTarget => transferTarget;
        public ContainmentHarnessDrawOffsets DrawOffsetsFor(Pawn pawn)
        {
            return pawn?.RaceProps?.Humanlike == true ? Props.humanlike : Props.nonHumanlike;
        }

        public float DrawScaleFor(Pawn pawn)
        {
            return pawn?.RaceProps?.Humanlike == true ? Props.humanlikeDrawScale : occupantDrawScale;
        }

        public float DrawAngleFor(Pawn pawn)
        {
            return DrawOffsetsFor(pawn)?.pawnRotationOffset ?? 0f;
        }

        public bool CanContain(Pawn pawn, bool medicalExtraction = false)
        {
            float maximumSize = medicalExtraction ? Props.maxMedicalExtractionBodySize : Props.maxBodySize;
            return pawn != null && !pawn.Dead && pawn.BodySize <= maximumSize;
        }

        public void Notify_Accepted(Pawn pawn, bool medicalExtraction)
        {
            occupantDrawScale = medicalExtraction && pawn.BodySize > Props.maxBodySize
                ? Props.medicalExtractionDrawScale
                : Props.nonHumanlikeDrawScale;
        }

        public void Notify_Emptied()
        {
            occupantDrawScale = Props.nonHumanlikeDrawScale;
            transferTarget = null;
        }

        public void SetTransferTarget(Thing target)
        {
            transferTarget = target;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref occupantDrawScale, "occupantDrawScale", Props.nonHumanlikeDrawScale);
            Scribe_References.Look(ref transferTarget, "transferTarget");
        }

        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            props = parent.def.GetCompProperties<CompProperties_BioContainer>();
            if (parent is Building_BioContainer container && container.ContainedThing is Pawn pawn)
            {
                occupantDrawScale = pawn.BodySize > Props.maxBodySize
                    ? Props.medicalExtractionDrawScale
                    : Props.nonHumanlikeDrawScale;
            }
            else
            {
                occupantDrawScale = Props.nonHumanlikeDrawScale;
            }
        }
    }

    public class CompProperties_BioContainer : CompProperties
    {
        public float maxBodySize = 0.35f;
        public float maxMedicalExtractionBodySize = 2f;
        public float nonHumanlikeDrawScale = 1f;
        public float medicalExtractionDrawScale = 0.35f;
        public float humanlikeDrawScale = 0.25f;
        public ContainmentHarnessDrawOffsets nonHumanlike = new ContainmentHarnessDrawOffsets();
        public ContainmentHarnessDrawOffsets humanlike = new ContainmentHarnessDrawOffsets();
        public bool suspendContents = true;

        public CompProperties_BioContainer()
        {
            compClass = typeof(CompBioContainer);
        }
    }
}
