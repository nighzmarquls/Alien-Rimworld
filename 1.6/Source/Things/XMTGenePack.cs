using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class XMTGenePack : Genepack
    {
        public float Potency => 0.1f * geneSet.ComplexityTotal;
        protected override void PostIngested(Pawn ingester)
        {

            if(ingester.genes != null)
            {
                if(!XMTUtility.IsXenomorph(ingester))
                {
                    BioUtility.InsertGenesetToPawn(geneSet, ref ingester, forbidUnknown: false);
                    BioUtility.TryMutatingPawn(ref ingester);
                    Find.ResearchManager.AddProgress(XenoGeneDefOf.XMT_Starbeast_Genetics, 100, ingester);
                }
            }

            base.PostIngested(ingester);
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (HitPoints <= 0)
            {
                Map mapHeld = base.MapHeld;

                Find.ResearchManager.AddProgress(XenoGeneDefOf.XMT_Starbeast_Genetics, 50, null);

                BioUtility.SpawnJellyHorror(this.PositionHeld, mapHeld, Potency);
            }
        }

    }
}
