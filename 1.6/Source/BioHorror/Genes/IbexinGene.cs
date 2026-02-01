
using Verse;

namespace Xenomorphtype
{
    public class IbexinGene : XenomorphGene
    {
        public override void PostAdd()
        {
            BodyPartRecord skull = pawn.health.hediffSet.GetBodyPartRecord(InternalDefOf.StarbeastSkull);
            if (skull == null)
            {
                skull = pawn.health.hediffSet.GetBodyPartRecord(ExternalDefOf.Skull);
            }
            Hediff horn = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_IbexHorns, pawn, skull);
            pawn.health.AddHediff(horn, skull);

            base.PostAdd();
        }

        public override void PostRemove()
        {
            if (pawn.health.hediffSet.TryGetHediff(XenoGeneDefOf.XMT_IbexHorns, out Hediff horn))
            {
                pawn.health.RemoveHediff(horn);
            }

            base.PostRemove();
        }
    }
}
