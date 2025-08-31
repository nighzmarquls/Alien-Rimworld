using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_Obsession : HediffComp
    {
        HediffCompProperties_Obsession Props => props as HediffCompProperties_Obsession;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            CompPawnInfo info = Pawn.GetComp<CompPawnInfo>();
            if (info != null)
            {
               
                info.GainObsession(Props.obsessionGain);
                
            }
        }
    }

    public class HediffCompProperties_Obsession : HediffCompProperties
    {
        public float obsessionGain = 0.1f;
        public HediffCompProperties_Obsession()
        {
            compClass = typeof(HediffComp_Obsession);
        }
    }
}
