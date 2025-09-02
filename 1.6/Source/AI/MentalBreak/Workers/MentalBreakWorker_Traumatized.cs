
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
                if(info.IsObsessed())
                {
                    return false;
                }

                if(info.TotalHorrorExperience() > 1f && HiveUtility.XenosOnMap(pawn.Map))
                {
                    return base.BreakCanOccur(pawn);
                }
            }
            return false;
        }
    }
}
