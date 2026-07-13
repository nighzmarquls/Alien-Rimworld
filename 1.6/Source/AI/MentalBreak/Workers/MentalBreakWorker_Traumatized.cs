
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class MentalBreakWorker_Traumatized : MentalBreakWorker
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
                    return false;
                }

                if(KnowledgeUtility.GetTrauma(pawn) > 1f && XMTHiveUtility.XenosOnMap(pawn.Map))
                {
                    return base.BreakCanOccur(pawn);
                }
            }
            return false;
        }
    }
}
