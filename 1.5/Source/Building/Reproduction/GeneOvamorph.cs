using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class GeneOvamorph : XMTBase_Building
    {
        public int TimeLaid;

        private CompHiveGeneHolder geneHolder;

        List<string> tmpGeneLabelsDesc = new List<string>();
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            geneHolder = this.GetComp<CompHiveGeneHolder>();

            if (geneHolder != null)
            {
                HiveUtility.AddGeneOvamorph(this, map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref TimeLaid, "timeLaidTick");
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            HiveUtility.RemoveGeneOvamorph(this, Map);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            BioUtility.SpawnJellyHorror(PositionHeld, MapHeld, 1);
            base.Destroy(mode);
        }
        public override string GetInspectString()
        {
            string description = base.GetInspectString(); ;
            string text = description;


            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                return text;
            }


            if (geneHolder.genes == null || !geneHolder.genes.GenesListForReading.Any())
            {
                return text;
            }


            if (!text.NullOrEmpty())
            {
                text += "\n\n";
            }


            tmpGeneLabelsDesc.Clear();

            for (int i = 0; i < geneHolder.genes.GenesListForReading.Count; i++)
            {
                tmpGeneLabelsDesc.Add(geneHolder.genes.GenesListForReading[i].label);
            }

            return text + ("Genes".Translate().CapitalizeFirst() + ":\n" + tmpGeneLabelsDesc.ToLineList("  - ", capitalizeItems: true));
        }

        protected void RecieveGenesFrom(Pawn pawn)
        {
            List<GeneDef> hostGenes = BioUtility.GetExtraHostGenes(pawn);
            if(hostGenes != null)
            {
                if (hostGenes.Count > 0)
                {
                    BioUtility.ExtractGenesToGeneset(ref geneHolder.genes, hostGenes);
                }
            }

            if(pawn.genes != null)
            {
                BioUtility.ExtractGenesToGeneset(ref geneHolder.genes, pawn.genes.GenesListForReading);
            }
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            RecieveGenesFrom(pawn);
            if (pawn.BodySize > 1)
            {
                int remainingBody = Mathf.FloorToInt(pawn.BodySize - 1);

                if (remainingBody > 0)
                {

                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, remainingBody, false);

                    foreach (IntVec3 cell in cells)
                    {
                        if (remainingBody <= 0)
                        {
                            break;
                        }
                        GeneOvamorph egg = GenSpawn.Spawn(def, cell, Map, WipeMode.VanishOrMoveAside) as GeneOvamorph;


                        egg.RecieveGenesFrom(pawn);
                        remainingBody -= 1;
                    }
                }
            }
        }
    }
}
