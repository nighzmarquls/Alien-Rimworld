using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class RitualObligationTargetWorker_HiveSpot : RitualObligationTargetFilter
    {
        public RitualObligationTargetWorker_HiveSpot()
        {
        }

        public RitualObligationTargetWorker_HiveSpot(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }
        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return "test";
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            yield return HiveUtility.GetNestSpot(map);
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            NestSpot targetSpot = target.Thing as NestSpot;
            if (targetSpot == null)
            {
                return false;
            }

            return true;
        }
    }
}
