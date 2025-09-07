using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_BloodDrainer : HediffComp
    {
        int nextDrainTick;

        HediffCompProperties_BloodDrainer Props => props as HediffCompProperties_BloodDrainer;
        HediffComp_PawnAttachement attachment => parent.GetComp<HediffComp_PawnAttachement>();

        Pawn Feeder => attachment != null ? attachment.attachedPawn : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref nextDrainTick, "nextDrainTick", -1);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (parent.pawn == null)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick > nextDrainTick)
            {
                
                nextDrainTick = tick + Mathf.CeilToInt(2500);
                DrainBlood();
            }
        }

        protected void DrainBlood()
        {
            float targetBloodloss = Props.bloodLossPerHour;
            if (parent.pawn.IsBloodfeeder())
            {
                Gene_Hemogen gene_Hemogen = parent.pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
                if (gene_Hemogen.Value > Props.bloodLossPerHour)
                {
                    GeneUtility.OffsetHemogen(parent.pawn, 0f - Props.bloodLossPerHour);
                    return;
                }
                else
                {
                    targetBloodloss = targetBloodloss - gene_Hemogen.Value;
                }
            }

            if (parent.pawn.health != null)
            {
                Hediff bloodloss = parent.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodloss != null)
                {
                    bloodloss.Severity += Props.bloodLossPerHour;
                }
                else
                {
                    bloodloss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, parent.pawn);

                    bloodloss.Severity = Props.bloodLossPerHour;

                    parent.pawn.health.AddHediff(bloodloss);
                }
            }

            if (Feeder != null)
            {
                if(Feeder?.needs?.food != null)
                {
                    Feeder.needs.food.CurLevel += Props.bloodLossPerHour;
                }
            }
        }
    }
    public class HediffCompProperties_BloodDrainer : HediffCompProperties
    {
        public float bloodLossPerHour = 0.01f;
        public HediffCompProperties_BloodDrainer()
        {
            compClass = typeof(HediffComp_BloodDrainer);
        }
    }
}
