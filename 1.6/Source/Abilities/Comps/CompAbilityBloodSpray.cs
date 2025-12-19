using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{ 
    public class CompAbilityBloodSpray : CompAbilityEffect
    {
        public override bool CanCast => (base.CanCast && parent?.pawn?.health?.hediffSet?.BleedRateTotal > 0);
        public override bool ShouldHideGizmo => (parent?.pawn?.health?.hediffSet?.BleedRateTotal == 0);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            List<IntVec3> splashZone = GenRadial.RadialCellsAround(target.Cell, 1.5f, true).ToList();

            splashZone.Shuffle();
            FilthMaker.TryMakeFilth(target.Cell, parent.pawn.Map, parent.pawn.RaceProps.BloodDef);
            int max = 3;
            foreach (IntVec3 splashCell in splashZone)
            {
                if(max < 0)
                {
                    break;
                }
                FilthMaker.TryMakeFilth(splashCell, parent.pawn.Map, parent.pawn.RaceProps.BloodDef);
                max--;
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
