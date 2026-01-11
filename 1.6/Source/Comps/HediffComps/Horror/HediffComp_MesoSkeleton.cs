

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_MesoSkeleton : HediffComp
    {
        float Influence = 0f;
        float LastUpdatedInfluence = 0f;
        public override void CompExposeData()
        {
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if(parent.Severity != Influence)
            {
                PopulateInfluence();
            }

        }

        public void PopulateInfluence()
        {

            LastUpdatedInfluence = Influence;

            Influence = (parent.Severity*10);

            float InfluenceDelta = Influence - LastUpdatedInfluence;

            Pawn.UpdateMesoSkeletonization(Mathf.Max(0, Pawn.MesoSkeletonization() + InfluenceDelta));
            
        }

        public override void CompPostPostRemoved()
        {
            Pawn.UpdateMesoSkeletonization(Mathf.Max(0, Pawn.MesoSkeletonization() - Influence));
        }
    }
}
