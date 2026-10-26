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
                    ResearchUtility.ProgressGeneticsTech(100, ingester);
                }
            }

            base.PostIngested(ingester);
        }


        public override void PostMake()
        {
            base.PostMake();

            geneSet = new GeneSet();
            foreach (GeneDef gene in XenoGeneDefOf.XMT_UnknownGenes.genes)
            {
                geneSet.AddGene(gene);
            }
        }
        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            Map researchMap = MapHeld;
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (HitPoints <= 0)
            {
                Map mapHeld = base.MapHeld;

                ResearchUtility.ProgressGeneticsTech(50, null, researchMap);

                BioUtility.SpawnJellyHorror(this.PositionHeld, mapHeld, Potency);
            }
        }

    }
}
