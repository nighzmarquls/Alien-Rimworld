using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class ThoughtWorker_Precept_Biomorph : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
            if (XMTUtility.IsXenomorph(p))
            {
                if(XMTUtility.IsQueen(p))
                {
                    return ThoughtState.ActiveAtStage(2);
                }
                else
                {
                    return ThoughtState.ActiveAtStage(1);
                }    
            }
            else if (XMTUtility.IsXenomorphFriendly(p))
            {
                return ThoughtState.ActiveAtStage(0);
            }

            return false;
        }
    }
}
