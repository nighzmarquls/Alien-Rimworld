using RimWorld;
using AlienRace;


namespace Xenomorphtype
{
    [DefOf]
    internal class XenoStoryDefOf
    {
        static XenoStoryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoStoryDefOf));
        }

        public static XMT_BackstorySet XMT_ObsessedBackstories;

        public static AlienBackstoryDef StarbeastChildDeveloped10;
        public static AlienBackstoryDef StarbeastChildPremature15;
        public static AlienBackstoryDef StarbeastChildAlone16;
        public static AlienBackstoryDef StarbeastChildHuman17;
        public static AlienBackstoryDef StarbeastChildHive18;
        
    }
}
