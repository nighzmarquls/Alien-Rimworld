using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;


namespace Xenomorphtype
{
    public class HediffComp_CureOnSeverity : HediffComp
    {
        HediffCompProperties_CureOnSeverity Props => props as HediffCompProperties_CureOnSeverity;

        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }
                return parent.Severity >= Props.cureSeverity;
            }
        }

    }



    public class HediffCompProperties_CureOnSeverity : HediffCompProperties
    {
        public float cureSeverity;
        public HediffCompProperties_CureOnSeverity()
        {
            compClass = typeof(HediffComp_CureOnSeverity);
        }
    }
}
