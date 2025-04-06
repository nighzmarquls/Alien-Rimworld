using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xenomorphtype
{
    [DefOf]
   
    internal class XenoPreceptDefOf
    {
        static XenoPreceptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoPreceptDefOf));
        }

        public static HistoryEventDef XMT_Cryptobio_Killed;
    }
}
