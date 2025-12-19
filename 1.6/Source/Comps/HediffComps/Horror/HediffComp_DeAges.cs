
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_DeAges :HediffComp
    {
        int nextProgressTick = -1;
        HediffCompProperties_DeAges Props => props as HediffCompProperties_DeAges;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            List<Hediff> removableList = parent.pawn.health.hediffSet.hediffs;
            foreach (Hediff hediff in removableList)
            {
                bool cured = false;
                foreach (HediffDef illness in Props.curedAgeIllness)
                {
                    if(hediff.def == illness)
                    {
                        parent.pawn.health.RemoveHediff(hediff);
                        cured = true;
                        break;
                    }
                }
                if(cured)
                {
                    break;
                }
            }

            if(!XMTUtility.IsXenomorph(parent.pawn))
            {
                int num = (int)(3600000f * parent.pawn.ageTracker.AdultAgingMultiplier*Props.yearsDeAged);
                long val = (long)(3600000f * parent.pawn.ageTracker.AdultMinAge);
                parent.pawn.ageTracker.AgeBiologicalTicks = Math.Max(val, parent.pawn.ageTracker.AgeBiologicalTicks - num);
                parent.pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.ViaTreatment);
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (parent.pawn == null)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick > nextProgressTick)
            {

                nextProgressTick = tick + Mathf.CeilToInt(2500);
                List<Hediff> removableList = parent.pawn.health.hediffSet.hediffs;
                foreach (Hediff hediff in removableList)
                {
                    if(hediff.def == InternalDefOf.Undeveloped)
                    {
                        hediff.Severity+= (0.1f);
                    }
                }
            }
        }
    }

    public class HediffCompProperties_DeAges : HediffCompProperties
    {
        public List<HediffDef> curedAgeIllness;
        public float yearsDeAged = 1f;
        public float promotionSeverity = 0.75f;
        public HediffCompProperties_DeAges()
        {
            compClass = typeof(HediffComp_DeAges);
        }
    }
}
