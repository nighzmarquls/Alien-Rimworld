

using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class MentalStateWorker_XMT_MurderousRage : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn))
            {
                return false;
            }

            return XMTMentalStateUtility.FindPawnToKill(pawn) != null;
        }
    }
}
