
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    internal class XenoPawnKindDefOf
    {
        static XenoPawnKindDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoPawnKindDefOf));
        }
        public static PawnKindDef XMT_StarbeastKind;
        public static PawnKindDef XMT_RoyaltyKind;
        public static PawnKindDef XMT_Droplet;
        public static PawnKindDef XMT_FeralStarbeastKind;
        public static PawnKindDef XMT_Larva;
    }

}
