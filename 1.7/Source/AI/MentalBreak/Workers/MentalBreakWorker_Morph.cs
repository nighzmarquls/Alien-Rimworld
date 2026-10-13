
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class MentalBreakWorker_Morph : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn)
        {
            if (XMTUtility.IsXenomorph(pawn))
            {
                return base.BreakCanOccur(pawn);
            }
            return false;
        }
    }
}
