using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_RemoveOnXenomorph : HediffComp
    {
        HediffCompProperties_RemoveOnXenomorph Props => props as HediffCompProperties_RemoveOnXenomorph;
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }

                if (Props.ifXenomorph)
                {
                    return XMTUtility.IsXenomorph(parent.pawn);
                }

                return !XMTUtility.IsXenomorph(parent.pawn);
            }
        }
    }
    public class HediffCompProperties_RemoveOnXenomorph : HediffCompProperties
    {

        public bool ifXenomorph = false;
        public HediffCompProperties_RemoveOnXenomorph()
        {
            this.compClass = typeof(HediffComp_RemoveOnXenomorph);
        }
    }
}
