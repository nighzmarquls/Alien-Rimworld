using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    public class ChemfuelGene : XenomorphGene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            Gene deathless = pawn.genes.GetGene(GeneDefOf.Deathless);
            if (deathless != null)
            {
                pawn.genes.RemoveGene(deathless);
            }
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            float explosionRadius = (pawn.ageTracker.CurLifeStageIndex == 0) ? 1.9f : ((pawn.ageTracker.CurLifeStageIndex != 1) ? 4.9f : 2.9f);
            GenExplosion.DoExplosion(radius: explosionRadius, center: pawn.PositionHeld, map: pawn.MapHeld, damType: DamageDefOf.Flame, instigator: pawn);

            CompAcidBlood compAcidBlood = pawn.GetComp<CompAcidBlood>();

            if (compAcidBlood != null)
            {
                compAcidBlood.CreateAcidExplosion(explosionRadius);
            }
        }
    }
}
