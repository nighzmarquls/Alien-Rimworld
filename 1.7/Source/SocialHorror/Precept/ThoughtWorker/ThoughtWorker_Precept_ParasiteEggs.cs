using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class ThoughtWorker_Precept_ParasiteEggs : ThoughtWorker_Precept
    {
        protected override ThoughtState ShouldHaveThought(Pawn p)
        {
               if(XMTHiveUtility.HaveEggs(p.Map))
               {
                    return ThoughtState.ActiveAtStage(1);
               }
               else
               {
                    return ThoughtState.ActiveAtStage(0);
               }
        }
    }
}
