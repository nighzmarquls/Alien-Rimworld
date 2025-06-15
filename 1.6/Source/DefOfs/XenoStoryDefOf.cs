using RimWorld;
using AlienRace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xenomorphtype
{
    [DefOf]
    internal class XenoStoryDefOf
    {
        static XenoStoryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoStoryDefOf));
        }

        public static AlienBackstoryDef StarbeastChildDeveloped10;
        public static AlienBackstoryDef StarbeastChildPremature15;
        public static AlienBackstoryDef StarbeastChildAlone16;
        public static AlienBackstoryDef StarbeastChildHuman17;
        public static AlienBackstoryDef StarbeastChildHive18;
        
    }
}
