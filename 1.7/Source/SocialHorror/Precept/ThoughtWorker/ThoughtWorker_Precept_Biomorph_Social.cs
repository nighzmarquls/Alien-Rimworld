using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class ThoughtWorker_Precept_Biomorph_Social : ThoughtWorker_Precept_Social
    {
        protected override ThoughtState ShouldHaveThought(Pawn p, Pawn otherPawn)
        {
            if (XMTUtility.IsXenomorph(p))
            {
                return false;
            }

            if (XMTUtility.IsXenomorph(otherPawn))
            {
                if (XMTUtility.IsQueen(otherPawn))
                {
                    return ThoughtState.ActiveAtStage(2);
                }
                else
                {
                    return ThoughtState.ActiveAtStage(1);
                }
            }
            else if (XMTUtility.IsXenomorphFriendly(otherPawn))
            {
                return ThoughtState.ActiveAtStage(0);
            }

            return false;
        }
    }
}
