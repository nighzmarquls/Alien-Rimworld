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
            float progress = Find.ResearchManager.GetProgress(XenoGeneDefOf.XMT_Jelly_Extraction);
            return progress > 0;
        }

        public static bool StarbeastProjectsVisible()
        {
            return XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
        }

        static bool FinishedResearching(ResearchProjectDef project, ResearchManager researchManager)
        {
            return researchManager.GetProgress(project) >= project.Cost;
        }

        static bool ProgressTechProjectOrPrerequisites(int progress, Thing actor, ResearchProjectDef project)
        {
            ResearchManager researchManager = Find.ResearchManager;
            if (FinishedResearching(project, researchManager))
            {
                return false;
            }

            ResearchProjectDef targetProjectDef = project;
            bool stillSearching = true;

            while (targetProjectDef.prerequisites.Count > 0 && stillSearching)
            {
                ResearchProjectDef lastProjectFound = targetProjectDef;
                foreach (ResearchProjectDef prerequisite in targetProjectDef.prerequisites)
                {
                    if (FinishedResearching(prerequisite, researchManager))
                    {
                        continue;
                    }

                    targetProjectDef = prerequisite;
                }
                if(lastProjectFound == targetProjectDef)
                {
                    stillSearching = false;
                }
            }

            Pawn pawn = actor as Pawn;
            researchManager.AddProgress(targetProjectDef, progress, pawn);
            return true;
        }
        internal static void ProgressMimicTech(int progress, Thing actor)
        {
            ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_Sculpture);
        }

        internal static void ProgressEvolutionTech(int progress, Thing actor)
        {
            if(ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_JellyTransport))
            {
                return;
            }
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_Eggs))
            {
                return;
            }
        }

        internal static void ProgressResinTech(int progress, Thing actor)
        {
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_Reinforcement))
            {
                return;
            }
        }

        internal static void ProgressCryptobioTech(int progress, Pawn actor)
        {
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Jelly_Drugs))
            {
                return;
            }

            /*if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Mutation_Targeted))
            {
                return;
            }*/
        }
    }
}
