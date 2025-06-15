using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    public class ThoughtWorker_PheromoneOpinion : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (!p.RaceProps.Humanlike)
            {
                return false;
            }

            if (!other.RaceProps.Humanlike)
            {
                return false;
            }

            if (!RelationsUtility.PawnsKnowEachOther(p, other))
            {
                return false;
            }

            if (!XMTUtility.SmellsPheromones(p))
            {
                return false;
            }

            if (XMTUtility.IsXenomorph(other))
            {
                return false;
            }

            CompPawnInfo info = other.GetComp<CompPawnInfo>();
            float PheromoneValue = info.XenomorphPheromoneValue();
            if (PheromoneValue < 0f)
            {
                return ThoughtState.ActiveAtStage(0);
            }

            if (PheromoneValue >= 0.75f)
            {
                return ThoughtState.ActiveAtStage(3);
            }

            if (PheromoneValue >= 0.25f)
            {
                return ThoughtState.ActiveAtStage(2);
            }

            if (PheromoneValue > 0.0f)
            {
                return ThoughtState.ActiveAtStage(1);
            }

            return ThoughtState.Inactive;
        }
    }
}
