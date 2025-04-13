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

        public static PreceptDef XMT_Parasite_Reincarnation;
        public static PreceptDef XMT_Biomorph_Study;
        public static PreceptDef XMT_Biomorph_Worship;

        public static HistoryEventDef XMT_Larvae_Killed;
        public static HistoryEventDef XMT_Ovamorph_Destroyed;
        public static HistoryEventDef XMT_Parasite_Attached;
        public static HistoryEventDef XMT_Parasite_Birth;

        public static HistoryEventDef XMT_Cryptobio_Killed;
        
    }
}
