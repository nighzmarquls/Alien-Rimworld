using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class XMTResearchPatches
    {
        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.TabInfoVisible) )]
        public static class Patch_ResearchManager_TabInfoVisible
        {
            [HarmonyPostfix]
            public static void Postfix(ResearchTabDef tab, ref bool __result, ResearchManager __instance, DefMap<ResearchTabDef, bool> ___tabInfoVisibility)
            {
                if(tab == XenoGeneDefOf.XMT_HumanResearchTab && !___tabInfoVisibility[XenoGeneDefOf.XMT_HumanResearchTab])
                {
                    if (ResearchUtility.HumanProjectsVisible())
                    {
                        ___tabInfoVisibility[XenoGeneDefOf.XMT_HumanResearchTab] = true;
                        __result = true;
                        return;
                    }
                }

                if(tab == XenoSocialDefOf.XMT_StarbeastResearchTab && !___tabInfoVisibility[XenoSocialDefOf.XMT_StarbeastResearchTab])
                {
                    if(ResearchUtility.StarbeastProjectsVisible())
                    {
                        ___tabInfoVisibility[XenoSocialDefOf.XMT_StarbeastResearchTab] = true;
                        __result = true;
                        return;
                    }
                }
            }
        }
    }
}
