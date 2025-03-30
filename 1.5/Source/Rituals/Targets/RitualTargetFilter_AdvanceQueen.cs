using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class RitualTargetFilter_AdvanceQueen : RitualTargetFilter
    {
        public RitualTargetFilter_AdvanceQueen()
        {
        }

        public RitualTargetFilter_AdvanceQueen(RitualTargetFilterDef def)
        {
            this.def = def;
        }

        public override TargetInfo BestTarget(TargetInfo initiator, TargetInfo selectedTarget)
        {
            if (XMTUtility.IsXenomorph(initiator.Thing))
            {
                if (!XMTUtility.IsQueen(initiator.Thing as Pawn))
                {
                    return null;
                }
            }
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
                if (XMTUtility.IsQueen(initiator.Thing as Pawn))
                {
                    rejectionReason = "Is a Queen";
                    return true;
                }

                rejectionReason = "Not a Queen!";
                return false;
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
