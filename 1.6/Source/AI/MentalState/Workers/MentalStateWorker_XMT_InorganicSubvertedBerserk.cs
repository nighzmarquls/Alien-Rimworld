using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class MentalStateWorker_XMT_InorganicSubvertedBerserk : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            return base.StateCanOccur(pawn)
                && XMTUtility.IsInorganic(pawn)
                && XMTMentalStateUtility.FindInorganicSubversionTargetToKill(pawn) != null;
        }
    }
}
