using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class CompTargetEffect_ApplyRawPheromone : CompTargetEffect
    {
        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (target is not Pawn targetPawn)
            {
                return;
            }

            PheromoneUtility.RandomTopicalDose(out float aggregation, out float reproductive, out float alarm);
            if (PheromoneUtility.TryApplyArtificial(targetPawn, aggregation, reproductive, alarm))
            {
                parent.SplitOff(1).Destroy();
            }
            else
            {
                Messages.Message("XMT_PheromoneApplicationFailed".Translate(targetPawn.Named("PAWN")), targetPawn, MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    public class CompProperties_TargetEffectApplyRawPheromone : CompProperties
    {
        public CompProperties_TargetEffectApplyRawPheromone()
        {
            compClass = typeof(CompTargetEffect_ApplyRawPheromone);
        }
    }
}
