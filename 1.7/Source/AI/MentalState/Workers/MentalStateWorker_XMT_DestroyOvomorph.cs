
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class MentalStateWorker_XMT_DestroyOvomorph : MentalStateWorker
    {
        private static List<Thing> tmpThings = new List<Thing>();

        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn))
            {
                return false;
            }


            bool result = XMTHiveUtility.GetOvomorph(pawn.MapHeld, false) != null;
        
            return result;
        }
    }
}
