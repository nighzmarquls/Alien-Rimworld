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

        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static IssueDef XMT_Reproduction;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static IssueDef XMT_Cryptobio;

        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Parasite_Revered;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Parasite_Reincarnation;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Parasite_Abhorrent;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Parasite_OtherFaction;

        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Biomorph_Hunt;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Biomorph_Study;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Biomorph_Worship;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static PreceptDef XMT_Biomorph_Abhorrent;

        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Larvae_Killed;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Ovomorph_Destroyed;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Parasite_Attached;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Parasite_Birth;

        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Cryptobio_Killed;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Ovomorph_Hatched;
        [MayRequire("Ludeon.RimWorld.Ideology")]
        public static HistoryEventDef XMT_Ovomorph_Laid;


    }
}
