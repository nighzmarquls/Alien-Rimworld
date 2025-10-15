
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_ReplaceGenes : HediffComp
    {
        HediffCompProperties_ReplaceGenes Props => props as HediffCompProperties_ReplaceGenes;
        bool finished = false;
        public override bool CompShouldRemove => finished;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if(Pawn.genes == null)
            {
                finished = true;
                return;
            }

            BioUtility.ClearGenes(ref parent.pawn);
            GeneSet newGenes = new GeneSet();
            BioUtility.ExtractGenesToGeneset(ref newGenes, Props.genes);
            BioUtility.InsertGenesetToPawn(newGenes, ref parent.pawn);

            Pawn.genes.xenotypeName = Props.xenotype;
            finished = true;
        }
    }

    public class HediffCompProperties_ReplaceGenes : HediffCompProperties
    {
        public List<GeneDef> genes;
        public string xenotype = "baseliner";
        public HediffCompProperties_ReplaceGenes()
        {
            compClass = typeof(HediffComp_ReplaceGenes);
        }
    }
}
