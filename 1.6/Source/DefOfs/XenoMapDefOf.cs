using RimWorld;
using Verse;


namespace Xenomorphtype
{
    [DefOf]
    internal class XenoMapDefOf
    {
        static XenoMapDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoIncidentDefOf));
        }
        public static GenStepDef XMT_AttackAftermath;
    }
    
}
