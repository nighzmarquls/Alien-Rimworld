

using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class CompJellyDrug : CompDrug
    {
        public override void PrePostIngested(Pawn ingester)
        {
            if(XMTUtility.IsXenomorph(ingester))
            {
                return;
            }

            base.PrePostIngested(ingester);
        }
    }
}
