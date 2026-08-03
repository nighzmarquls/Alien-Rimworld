using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_MutateTarget : JobDriver_QueenAlterationChannel
    {
        protected override string ChannelToilName => "AttemptMutationAlteration";

        protected override AcceptanceReport ValidateSpecificTarget()
        {
            return Target is Pawn target && !target.Dead && target.health != null
                ? AcceptanceReport.WasAccepted
                : "XMT_SovereignAlterationInvalidTarget".Translate();
        }

        protected override bool CompleteChannel()
        {
            if (!(Target is Pawn target))
            {
                return false;
            }

            CompGeneManipulator manipulator = pawn.GetComp<CompGeneManipulator>();
            bool foundOrder = false;
            if (manipulator != null && manipulator.TryExecuteMutationOrder(target, out foundOrder))
            {
                return true;
            }

            if (foundOrder)
            {
                return false;
            }

            BioUtility.TryMutatingPawn(ref target, BioUtility.GetFallbackMutationSet(target), 1);
            return true;
        }
    }
}
