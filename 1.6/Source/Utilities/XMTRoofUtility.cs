
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTRoofUtility
    {
        public static bool RoofIsBreakable(RoofDef roof)
        {
            return true; //roof == RoofDefOf.RoofRockThin || roof == RoofDefOf.RoofConstructed;
        }
    }
}
