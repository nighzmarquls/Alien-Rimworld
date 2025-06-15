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
    public class HediffComp_Spreading : HediffComp
    {
        HediffCompProperties_Spreading Props => props as HediffCompProperties_Spreading;
        int tickInterval = 2500;

        public override void CompPostMake()
        {
            base.CompPostMake();
            tickInterval = Mathf.CeilToInt(Props.spreadIntervalHours * 2500);
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn.IsHashIntervalTick(tickInterval))
            {
                TrySpread(Props.spreadEfficiency);
            }
        }

        private void TrySpread(float spreadIntensity = 1.0f)
        {
            if (parent.Severity * Props.spreadEfficiency * spreadIntensity > parent.def.minSeverity)
            {
                BodyPartRecord targetPart = parent.Part;
                if (XMTUtility.GetPartAttachedToPartOnPawn(parent.pawn,ref targetPart))
                {
                    //Messages.Message(parent + " is spreading to " + targetPart, MessageTypeDefOf.NeutralEvent);
                    Hediff spreadingHediff = HediffMaker.MakeHediff(parent.def, parent.pawn, targetPart);
                    spreadingHediff.Severity = parent.Severity * spreadIntensity;
                    parent.pawn.health.AddHediff(spreadingHediff, targetPart);
                }

            }
        }
    }

    public class HediffCompProperties_Spreading : HediffCompProperties
    {
        public float spreadIntervalHours = 1f;
        public float spreadEfficiency = 0.5f;
        public HediffCompProperties_Spreading()
        {
            compClass = typeof(HediffComp_Spreading);
        }
    }
}
