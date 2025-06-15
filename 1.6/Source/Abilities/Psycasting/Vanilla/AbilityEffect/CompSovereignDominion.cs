using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class CompSovereignDominion : CompAbilityEffect
    {
        Pawn caster => parent.pawn;


        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.HasThing)
            {
                base.Apply(target, dest);
                if(target.Thing == caster)
                {
                    return;
                }
                if (target.Thing is Pawn subject)
                {
                    bool xenomorphCaster = XMTUtility.IsXenomorph(caster);
                    bool xenomorphTarget = XMTUtility.IsXenomorph(subject);
                    HivecastUtility.EnactDominion(subject, caster, xenomorphTarget, xenomorphCaster);
                }
            }
        }
    }
}
