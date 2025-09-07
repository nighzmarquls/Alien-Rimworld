using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    public class CompAbilityBloodSpray : CompAbilityEffect
    {
        public override bool CanCast => (base.CanCast && parent?.pawn?.health?.hediffSet?.BleedRateTotal > 0);
        public override bool ShouldHideGizmo => (parent?.pawn?.health?.hediffSet?.BleedRateTotal == 0);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            CompAcidBlood acidBlood = parent.pawn.GetAcidBloodComp();

            if (acidBlood != null)
            {
                acidBlood.TrySplashAcidCell(target.Cell);
            }

            base.Apply(target, dest);
        }
    }

    public class CompAbilityBloodSprayProperties : CompProperties_AbilityEffect
    {
        public CompAbilityBloodSprayProperties()
        {
            this.compClass = typeof(CompAbilityBloodSpray);
        }

    }
}
