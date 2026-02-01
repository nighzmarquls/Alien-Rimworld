


using RimWorld;
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

        public void PopulateInfluence()
        {
            if(Pawn == null || Pawn.health == null || Pawn.health.hediffSet == null )
            {
                return;
            }

            IEnumerable<BodyPartRecord> allbodyparts =  Pawn.health.hediffSet.GetNotMissingParts();
            float totalCoverage = 0;

            foreach (BodyPartRecord part in allbodyparts)
            {
                totalCoverage += part.coverage;
            }

            Influence = (parent.Part == null)? 1 : parent.Part.coverage / totalCoverage;
            float updatedValue = Pawn.CarboSilicate() + Influence;

            Pawn.UpdateCarboSilicate(updatedValue);

            unInitialized = false;
        }

        public override void CompPostPostRemoved()
        {
            Pawn.UpdateCarboSilicate(Mathf.Max(0, Pawn.CarboSilicate() - Influence));
        }
    }
}
