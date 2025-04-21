using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    [DefOf]
    internal class XenoSocialDefOf
    {
        static XenoSocialDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(XenoSocialDefOf));
        }
        public static PreceptDef XMT_QueenAscension;

        // human xen biotech researchdefs
        public static ResearchTabDef XMT_StarbeastResearchTab;

        // xeno biotech researchdefs
        public static ResearchProjectDef XMT_Starbeast_Construction;
    }
}
