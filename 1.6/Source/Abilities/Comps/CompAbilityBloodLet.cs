using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompAbilityBloodLet : CompAbilityEffect
    {
        public override bool CanCast => (base.CanCast && parent?.pawn?.health?.hediffSet?.BleedRateTotal == 0);
        public override bool ShouldHideGizmo => (parent?.pawn?.health?.hediffSet?.BleedRateTotal != 0);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            parent.pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 5, 9999, -1, parent.pawn));
            base.Apply(target, dest);
        }

    }

    public class CompAbilityBloodLetProperties : CompProperties_AbilityEffect
    {
        public CompAbilityBloodLetProperties()
        {
            this.compClass = typeof(CompAbilityBloodLet);
        }

    }
    
}
