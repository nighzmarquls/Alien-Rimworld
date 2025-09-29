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

        public static InteractionDef XMT_AdvancedTameAttempt;

        // xeno research tab
        public static ResearchTabDef XMT_StarbeastResearchTab;

        // xeno researchdefs
        // mimicry
        public static ResearchProjectDef XMT_Starbeast_Construction;
        public static ResearchProjectDef XMT_Starbeast_Sculpture;
        // resin
        public static ResearchProjectDef XMT_Starbeast_Reinforcement;
        // evo
        public static ResearchProjectDef XMT_Starbeast_Chrysalis;
        public static ResearchProjectDef XMT_Starbeast_JellyTransport;
        public static ResearchProjectDef XMT_Starbeast_Eggs;

    }
}
