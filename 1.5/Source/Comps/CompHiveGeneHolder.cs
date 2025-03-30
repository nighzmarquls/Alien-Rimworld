using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompHiveGeneHolder : ThingComp
    {
        public GeneSet genes;
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Deep.Look(ref genes, "genes");
        }
    }
 
}
