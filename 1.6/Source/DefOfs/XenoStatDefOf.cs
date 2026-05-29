using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    public static class XenoStatDefOf
    {
        static XenoStatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoStatDefOf));
        }

        public static StatDef XMT_GrappleStrength;
        public static StatDef XMT_Stealth;
        public static StatDef XMT_StealthDetection;
    }
}
