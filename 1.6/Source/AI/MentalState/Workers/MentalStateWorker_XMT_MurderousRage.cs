

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

            if (XMTUtility.IsXenomorph(pawn) || pawn.Info().IsObsessed())
            {
                return XMTMentalStateUtility.FindXenoEnemyToKill(pawn) != null;
            }
            else
            {
                return XMTMentalStateUtility.FindXenoToKill(pawn) != null;
            }
        }
    }
}
