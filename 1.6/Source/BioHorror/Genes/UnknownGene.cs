using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;
using Verse;

namespace Xenomorphtype
{ 
    public class UnknownGene : XenomorphGene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            BioUtility.AddHybridGene(pawn);
            if(XenoGeneDefOf.XMT_Starbeast_Genetics.IsHidden)
            {
                ResearchUtility.ProgressCryptobioTech(100, pawn);
            }

            if(pawn.genes != null) {
                pawn.genes.RemoveGene(this);
            }
        }
        public override void PostRemove()
        {
            base.PostRemove();
        }
    }
}
