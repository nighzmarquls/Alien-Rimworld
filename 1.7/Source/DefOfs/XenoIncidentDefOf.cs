using RimWorld;


namespace Xenomorphtype
{
    [DefOf]
    internal class XenoIncidentDefOf
    {
        static XenoIncidentDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoIncidentDefOf));
        }

        public static IncidentDef XMT_HuntingPack;
    }
}
