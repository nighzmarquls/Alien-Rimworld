


using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_CarboSilicateMeat : HediffComp
    {
        float Influence = 0f;
        bool unInitialized = true;
        public override void CompExposeData()
        {

        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (unInitialized)
            {
                PopulateInfluence();
            }

        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            PopulateInfluence();
        }

        public void PopulateInfluence()
        {
            IEnumerable<BodyPartRecord> allbodyparts =  Pawn.health.hediffSet.GetNotMissingParts();
            float totalCoverage = 0;
            foreach (BodyPartRecord part in allbodyparts)
            {
                totalCoverage += part.coverage;
            }
            
            Influence = parent.Part.coverage / totalCoverage;

            Pawn.UpdateCarboSilicate(Pawn.CarboSilicate() + Influence);
            Influence = parent.Part.coverage;
            unInitialized = false;
        }

        public override void CompPostPostRemoved()
        {
            Pawn.UpdateCarboSilicate(Mathf.Max(0, Pawn.CarboSilicate() - Influence));
        }
    }
}
