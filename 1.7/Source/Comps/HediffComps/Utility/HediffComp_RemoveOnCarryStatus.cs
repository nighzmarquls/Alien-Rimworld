using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_RemoveOnCarryStatus : HediffComp
    {
        HediffCompProperties_RemoveOnCarryStatus Props => props as HediffCompProperties_RemoveOnCarryStatus;
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }

                if (Props.RemoveIfCarried)
                {
                    if (Props.XenomorphsExempt)
                    {
                        return parent.pawn.CarriedBy != null &&  !XMTUtility.IsXenomorph(parent.pawn.CarriedBy);
                    }
                    else
                    {
                        return parent.pawn.CarriedBy != null;
                    }
                }

                return parent.pawn.CarriedBy == null;
            }
        }
    }
    public class HediffCompProperties_RemoveOnCarryStatus : HediffCompProperties
    {
        public bool RemoveIfCarried = true;
        public bool XenomorphsExempt = false;
        public HediffCompProperties_RemoveOnCarryStatus()
        {
            this.compClass = typeof(HediffComp_RemoveOnCarryStatus);
        }
    }
}
