using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class ThoughtWorker_TraumatizedBy : ThoughtWorker
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

            if (XMTUtility.IsXenomorph(p))
            {
                return false;
            }

            CompPawnInfo info = p.Info();

            if(info.IsObsessed())
            {
                return false;
            }

            float Awareness = info.HorrorAwareness;

            if (Awareness <= 0)
            {
                return false;
            }

            if (XMTUtility.IsXenomorph(other))
            {
                if (Awareness > 0f)
                {
                    return ThoughtState.ActiveAtStage(0);
                }

                if (Awareness >= 0.25f)
                {
                    return ThoughtState.ActiveAtStage(1);
                }

                if (Awareness >= 0.5f)
                {
                    return ThoughtState.ActiveAtStage(2);
                }

                if (Awareness >= 0.75f)
                {
                    return ThoughtState.ActiveAtStage(3);
                }
            }

            return ThoughtState.Inactive;
        }
    }
}
