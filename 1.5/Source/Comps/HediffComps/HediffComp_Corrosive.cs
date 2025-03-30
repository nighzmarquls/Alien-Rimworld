using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype { 
    public class HediffComp_Corrosive : HediffComp
    {
        HediffCompProperties_Corrosive Props => props as HediffCompProperties_Corrosive;
        int tickInterval = 2500;

        public override bool CompShouldRemove => base.CompShouldRemove || parent.Severity <= parent.def.minSeverity;
        public override void CompPostMake()
        {
            base.CompPostMake();
            tickInterval = Mathf.CeilToInt(Props.damageIntervalHours * 2500);
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);


            if(parent.pawn.IsHashIntervalTick(tickInterval))
            {
                BodyPartRecord targetPart = parent.Part;
                if (parent.pawn.health.hediffSet.HasBodyPart(targetPart) || XMTUtility.GetPartAttachedToPartOnPawn(parent.pawn, ref targetPart))
                {
                    if (!targetPart.IsCorePart)
                    {
                        FleckMaker.ThrowSmoke(parent.pawn.DrawPos, parent.pawn.MapHeld, 1);
                        parent.pawn.TakeDamage(new DamageInfo(Props.damageType, parent.Severity * Props.damageMultiplier, 999, -1, null, targetPart));
                    }
                }
            }
        }
    }

    public class HediffCompProperties_Corrosive : HediffCompProperties
    {
        
        public DamageDef damageType;
        public float damageMultiplier = 0.125f;
        public float damageIntervalHours;
        public HediffCompProperties_Corrosive()
        {
            compClass = typeof(HediffComp_Corrosive);
        }
    }
}
