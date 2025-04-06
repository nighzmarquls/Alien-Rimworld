using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class RitualObligationTargetWorker_MatureChrysalis : RitualObligationTargetFilter
    {
        public RitualObligationTargetWorker_MatureChrysalis()
        {
        }

        public RitualObligationTargetWorker_MatureChrysalis(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }
        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return "Fully prepared";
        }
        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            IEnumerable<FillableChrysalis> candidates = map.listerBuildings.AllBuildingsColonistOfClass<FillableChrysalis>();
            foreach (FillableChrysalis candidate in candidates)
            {
                if (candidate.Filled)
                {
                    Log.Message("Adding Target for Mature");
                    yield return candidate;
                }
            }
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
      
            if (XMTSettings.LogRituals)
            {
                Log.Message(target.Thing + " being checked for mature ritual.");
            }
            FillableChrysalis targetChrysalis = target.Thing as FillableChrysalis;
            if (targetChrysalis == null)
            {
                return false;
            }

            if (!targetChrysalis.Filled)
            {
                return false;
            }

            return true;
        }
    }
}
