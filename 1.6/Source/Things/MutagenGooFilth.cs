using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class MutagenGooFilth : Filth
    {
        int sitTicks = 0;
        int maxSitTicks = 100;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sitTicks, "sitTicks", 0);

        }

        protected virtual float TouchChance(Pawn p)
        {
            float num = 1f - p.GetStatValue(StatDefOf.ToxicEnvironmentResistance);
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }

            if (XMTUtility.IsInorganic(p))
            {
                return 0f;
            }

            num *= this.GetStatValue(StatDefOf.TrapSpringChance) * p.GetStatValue(StatDefOf.PawnTrapSpringChance);
            return Mathf.Clamp01(num);
        }
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
       
            if (GenTicks.IsTickInterval(125) && Spawned)
            {

                Pawn pawn = Position.GetFirstPawn(Map);

                if (pawn != null)
                {
                    if(Rand.Chance(TouchChance(pawn)))
                    {
                        BioUtility.TryMutatingPawn(ref pawn);
                        DeSpawn();
                    }
                }

                sitTicks++;
                if (sitTicks > maxSitTicks)
                {
                    DeSpawn();
                }
            }
        }
    }
}
