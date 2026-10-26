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
                if (TryResolveCorrosiveDamagePart(parent.pawn, ref targetPart))
                {
                    if (!targetPart.IsCorePart)
                    {
                        if (Props.throwSmoke)
                        {
                            FleckMaker.ThrowSmoke(parent.pawn.DrawPos, parent.pawn.MapHeld, 1);
                        }
                        parent.pawn.TakeDamage(new DamageInfo(Props.damageType, parent.Severity * Props.damageMultiplier, 999, -1, null, targetPart));
                    }
                }

            }
        }

        private bool TryResolveCorrosiveDamagePart(Pawn pawn, ref BodyPartRecord targetPart)
        {
            return DamageTargets(pawn, targetPart).TryRandomElement(out targetPart);
        }

        public static IEnumerable<BodyPartRecord> DamageTargets(Pawn pawn, BodyPartRecord originalPart)
        {
            if (pawn == null || originalPart == null)
            {
                yield break;
            }

            if (pawn.health.hediffSet.PartIsMissing(originalPart))
            {
                yield break;
            }

            if (originalPart.coverageAbs > 0f && originalPart.depth == BodyPartDepth.Outside)
            {
                if (!originalPart.IsCorePart) yield return originalPart;
                yield break;
            }

            foreach (BodyPartRecord part in originalPart.GetPartAndAllChildParts())
                if (part != originalPart && !part.IsCorePart && part.coverageAbs > 0f && part.depth == BodyPartDepth.Outside &&
                    !pawn.health.hediffSet.PartIsMissing(part)) yield return part;
        }
    }

    public class HediffCompProperties_Corrosive : HediffCompProperties
    {
        
        public DamageDef damageType;
        public float damageMultiplier = 0.125f;
        public float damageIntervalHours;
        public bool throwSmoke = true;
        public HediffCompProperties_Corrosive()
        {
            compClass = typeof(HediffComp_Corrosive);
        }
    }
}
