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

        public static IssueDef XMT_Reproduction;
        public static IssueDef XMT_Cryptobio;

        public static PreceptDef XMT_Parasite_Revered;
        public static PreceptDef XMT_Parasite_Reincarnation;
        public static PreceptDef XMT_Parasite_Abhorrent;
        public static PreceptDef XMT_Parasite_OtherFaction;

        public static PreceptDef XMT_Biomorph_Hunt;
        public static PreceptDef XMT_Biomorph_Study;
        public static PreceptDef XMT_Biomorph_Worship;
        public static PreceptDef XMT_Biomorph_Abhorrent;

        public static HistoryEventDef XMT_Larvae_Killed;
        public static HistoryEventDef XMT_Ovomorph_Destroyed;
        public static HistoryEventDef XMT_Parasite_Attached;
        public static HistoryEventDef XMT_Parasite_Birth;

        public static HistoryEventDef XMT_Cryptobio_Killed;
        public static HistoryEventDef XMT_Ovomorph_Hatched;
        public static HistoryEventDef XMT_Ovomorph_Laid;


    }
}
