using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class XMT_HediffByPart
    {
        public HediffDef hediffDef;
        public BodyPartDef bodyPartDef;
    }
    public class HediffComp_PerPartPromoter : HediffComp
    {
        HediffCompProperties_PerPartPromoter Props => props as HediffCompProperties_PerPartPromoter;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if(parent.Severity >= Props.promotionSeverity)
            {
                BodyPartRecord part = parent.Part;

                if (part != null)
                {
                    foreach (XMT_HediffByPart promotion in Props.HediffPromotionsByPart)
                    {
                        if (promotion.bodyPartDef == part.def)
                        {
                            PromoteIntoHediff(promotion.hediffDef);
                        }
                    }
                }
            }
        }

        private void PromoteIntoHediff(HediffDef hediffDef)
        {

        }
    }
    public class HediffCompProperties_PerPartPromoter : HediffCompProperties
    {
        public List<XMT_HediffByPart> HediffPromotionsByPart;
        public float promotionSeverity = 1.0f;
        public HediffCompProperties_PerPartPromoter()
        {
            compClass = typeof(HediffComp_PerPartPromoter);
        }
    }
}
