using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_AlterGenes : JobDriver_QueenAlterationChannel
    {
        protected override string ChannelToilName => "AttemptGeneAlteration";

        protected override AcceptanceReport ValidateSpecificTarget()
        {
            return BioUtility.HasAlterableGenes(Target, pawn)
                ? AcceptanceReport.WasAccepted
                : "XMT_SovereignAlterationInvalidTarget".Translate();
        }

        protected override bool CompleteChannel()
        {
            CompGeneManipulator manipulator = pawn.GetComp<CompGeneManipulator>();
            if (manipulator == null)
            {
                return false;
            }

            manipulator.AlterGenes(Target);
            return true;
        }
    }
}
