
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class MentalBreakWorker_Obsession : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn)
        {
            if(XMTUtility.IsXenomorph(pawn))
            {
                return false;
            }

            CompPawnInfo info = pawn.Info();
            if (info != null)
            {
                if(KnowledgeUtility.IsObsessed(pawn))
                {
                    return base.BreakCanOccur(pawn);
                }
            }
            return false;
        }
    }
}
