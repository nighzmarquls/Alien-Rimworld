

using UnityEngine;
using Verse;


namespace Xenomorphtype
{
    internal class HediffComp_XenoSocialGiver : HediffComp
    {
        float Influence = 0f;
        float LastUpdatedInfluence = 0f;
        HediffCompProperties_XenoSocialGiver Props => props as HediffCompProperties_XenoSocialGiver;
        public override void CompExposeData()
        {
        }


        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (Props.socialInfluence != Influence)
            {
                PopulateInfluence();
            }
        }

        public void PopulateInfluence()
        {

            LastUpdatedInfluence = Influence;

            Influence = Props.socialInfluence;

            float InfluenceDelta = Influence - LastUpdatedInfluence;

            Pawn.UpdateXenoSocial(Mathf.Max(0, Pawn.XenoSocial() + InfluenceDelta));

        }

        public override void CompPostPostRemoved()
        {
            Pawn.UpdateXenoSocial(Mathf.Max(0, Pawn.XenoSocial() - Influence));
        }
    }

    public class HediffCompProperties_XenoSocialGiver : HediffCompProperties
    {
        public float socialInfluence = 1f;
        public HediffCompProperties_XenoSocialGiver()
        {
            compClass = typeof(HediffComp_XenoSocialGiver);
        }
    }
}


