﻿
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

            CompPawnInfo info = pawn.GetComp<CompPawnInfo>();
            if (info != null)
            {
                if(info.IsObsessed())
                {
                    return false;
                }

                if(info.TotalHorrorAwareness() > 0.5f)
                {
                    return base.BreakCanOccur(pawn);
                }
            }
            return false;
        }
    }
}
