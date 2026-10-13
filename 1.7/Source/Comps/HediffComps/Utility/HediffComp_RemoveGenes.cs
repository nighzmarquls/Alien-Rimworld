
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_RemoveGenes : HediffComp
    {
        HediffCompProperties_RemoveGenes Props => props as HediffCompProperties_RemoveGenes;
        int lastGeneCount = -1;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastGeneCount, "lastGeneCount", 0);
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if(Pawn.IsHashIntervalTick(Props.checkInterval))
            {
                CheckGenes();
            }
        }

        protected void CheckGenes()
        {
            if (Pawn.genes != null)
            {
                if (lastGeneCount == Pawn.genes.GenesListForReading.Count )
                {
                    return;
                }

                if (Pawn.genes.GenesListForReading.Count == 0)
                {
                    lastGeneCount = 0;
                    return;
                }

                List<Gene> genesToRemove = new List<Gene>();

                foreach (Gene gene in Pawn.genes.GenesListForReading)
                {
                    bool marked = false;
                    if (Props.removedCategoryTags != null && gene.def.exclusionTags != null)
                    {
                        foreach (string tag in gene.def.exclusionTags)
                        {
                            if (Props.removedCategoryTags.Contains(tag))
                            {
                                genesToRemove.Add(gene);
                                marked = true;
                                break;
                            }
                        }
                    }

                    if (marked)
                    {
                        continue;
                    }

                    if (Props.removedGenes == null)
                    {
                        continue;
                    }

                    foreach (GeneDef removeDef in Props.removedGenes)
                    {
                        if (gene.def == removeDef)
                        {
                            genesToRemove.Add(gene);
                            break;
                        }
                    }
                }

                foreach (Gene gene in genesToRemove)
                {
                    Pawn.genes.RemoveGene(gene);
                }

                lastGeneCount = Pawn.genes.GenesListForReading.Count;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            CheckGenes();
        }
    }
    public class HediffCompProperties_RemoveGenes : HediffCompProperties
    {
        public int checkInterval = 600;
        public List<GeneDef> removedGenes = null;
        public List<string>  removedCategoryTags = null;

        public HediffCompProperties_RemoveGenes()
        {
            compClass = typeof(HediffComp_RemoveGenes);
        }
    }
}
