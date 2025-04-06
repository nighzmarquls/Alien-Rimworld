using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xenomorphtype
{
    [DefOf]
    internal class HiveRitualDefOf
    {
        static HiveRitualDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HiveRitualDefOf));
        }
        public static PreceptDef XMT_QueenAscension;
    }
}
