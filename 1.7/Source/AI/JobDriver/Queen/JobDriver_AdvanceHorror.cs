using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_AdvanceHorror : JobDriver_QueenAlterationChannel
    {
        private CompGeneManipulator Manipulator => pawn.GetComp<CompGeneManipulator>();

        protected override string ChannelToilName => "PerformHorrorAdvancement";
        protected override bool AllowsRemoteChannel => false;

        protected override AcceptanceReport ValidateSpecificTarget()
        {
            HorrorAdvancementOrder order = Manipulator?.FindHorrorAdvancementOrder(Target);
            HorrorAdvancementOption option = order == null
                ? null
                : HorrorAdvancementUtility.MakeOption(order.direction, order.pawnKind, order.thingDef);
            return HorrorAdvancementUtility.CanExecute(pawn, Target, option, requireAdjacent: true);
        }

        protected override bool CompleteChannel()
        {
            return Manipulator?.TryExecuteHorrorAdvancementOrder(Target, out _) == true;
        }

        protected override void CleanupChannel()
        {
            Manipulator?.CancelHorrorAdvancementOrder(Target);
        }
    }
}
