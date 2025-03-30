using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class XenomorphGene : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            BioUtility.TryMutatingPawn(ref pawn);
        }

        public override void PostRemove()
        {
            base.PostRemove();
        }
    }
}
