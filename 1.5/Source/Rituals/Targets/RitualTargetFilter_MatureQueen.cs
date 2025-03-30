using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    internal class RitualTargetFilter_MatureQueen : RitualTargetFilter
    {
        public RitualTargetFilter_MatureQueen()
        {
        }

        public RitualTargetFilter_MatureQueen(RitualTargetFilterDef def)
        {
            this.def = def;
        }

        public override TargetInfo BestTarget(TargetInfo initiator, TargetInfo selectedTarget)
        {
            FillableChrysalis targetChrysalis = selectedTarget.Thing as FillableChrysalis;
            if (targetChrysalis != null)
            {
                return targetChrysalis;
            }
            return null;
        }

        public override bool CanStart(TargetInfo initiator, TargetInfo selectedTarget, out string rejectionReason)
        {

            if (XMTUtility.IsXenomorph(initiator.Thing))
            {
                if(XMTUtility.IsQueen(initiator.Thing as Pawn))
                {
                    rejectionReason = "Is already a Queen";
                    return false;
                }

                rejectionReason = "Can ascend!";
                return true;
            }

            rejectionReason = "Cannot Ascend";
            return false;
        }

        public override IEnumerable<string> GetTargetInfos(TargetInfo initiator)
        {
            yield return "test";
        }
    }
}
