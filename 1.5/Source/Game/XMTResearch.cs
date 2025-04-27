using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class XMTResearch
    {
        public static bool HumanProjectsVisible()
        {
            return Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Starbeast_Genetics) > 0;
        }

        public static bool StarbeastProjectsVisible()
        {
            return XMTUtility.QueenIsPlayer();
        }

        static bool FinishedResearching(ResearchProjectDef project, ResearchManager researchManager)
        {
            return researchManager.GetProgress(project) >= project.Cost;
        }
        internal static void ProgressMimicTech(int progress, Pawn actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            if (!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Construction, researchManager))
            {
                researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Construction, progress, actor);
            }
        }
        internal static void ProgressCryptobioTech(int progress, Pawn actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            if (!FinishedResearching(XenoGeneDefOf.XMT_Starbeast_Genetics, researchManager))
            {
                researchManager.AddProgress(XenoGeneDefOf.XMT_Starbeast_Genetics, progress, actor);
            }
        }
    }
}
