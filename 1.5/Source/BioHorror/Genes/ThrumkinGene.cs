using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class ThrumkinGene : XenomorphGene
    {
        public override void PostAdd()
        {
            BodyPartRecord skull = pawn.health.hediffSet.GetBodyPartRecord(InternalDefOf.StarbeastSkull);
            if(skull == null)
            {
                skull = pawn.health.hediffSet.GetBodyPartRecord(ExternalDefOf.Skull);
            }
            Hediff horn = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_ThrumboHorn, pawn, skull);
            pawn.health.AddHediff(horn, skull);

            base.PostAdd();
        }

        public override void PostRemove()
        {
            if (pawn.health.hediffSet.TryGetHediff(XenoGeneDefOf.XMT_ThrumboHorn, out Hediff horn))
            {
                pawn.health.RemoveHediff(horn);
            }
          
            base.PostRemove();
        }
    }
}
