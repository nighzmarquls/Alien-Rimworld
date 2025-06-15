using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static AlienRace.AlienPartGenerator;

namespace Xenomorphtype
{
    public class HediffComp_HeadOffset : HediffComp
    {
        HediffCompProperties_HeadOffset Props => props as HediffCompProperties_HeadOffset;
        public DirectionalOffset Offset => Props.headOffset;
    }

    public class HediffCompProperties_HeadOffset : HediffCompProperties
    {
        public DirectionalOffset headOffset = null;
        public HediffCompProperties_HeadOffset()
        {
            compClass = typeof(HediffComp_HeadOffset);
        }
    }
}
