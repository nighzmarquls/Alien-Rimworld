using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class WorkGiver_XMT_HaulToGeneBank : WorkGiver_HaulToGeneBank
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(XenoGeneDefOf.XMT_Genepack);
    }
}
