using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    public class XenoMentalStateDefOf
    {
        static XenoMentalStateDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoMentalStateDefOf));
        }

        // Generic
        public static MentalStateDef XMT_MurderousRage;
        public static MentalStateDef XMT_SadisticRage;

        // Non-Xenomorph Only
        public static MentalStateDef XMT_DestroyOvamorph;

    }
}
