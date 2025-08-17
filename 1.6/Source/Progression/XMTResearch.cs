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
            return XMTUtility.PlayerXenosOnMap(Find.CurrentMap);
        }

        static bool FinishedResearching(ResearchProjectDef project, ResearchManager researchManager)
        {
            return researchManager.GetProgress(project) >= project.Cost;
        }
        internal static void ProgressMimicTech(int progress, Thing actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            Pawn pawn = actor as Pawn;
            Log.Message(progress + " towards mimicry");
            if (!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Construction, researchManager))
            {
                researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Construction, progress, pawn);
            } else if(!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Sculpture,researchManager))
            {
                researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Sculpture, progress, pawn);
            }
        }

  
        internal static void ProgressEvolutionTech(int progress, Thing actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            Pawn pawn = actor as Pawn;

            if (!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Chrysalis, researchManager))
            {
                researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Chrysalis, progress, pawn);
            }
            else if(!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_JellyTransport,researchManager))
            {
                if (XenoSocialDefOf.XMT_Starbeast_JellyTransport.PrerequisitesCompleted)
                {
                    researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_JellyTransport, progress, pawn);
                }
            }
            else if (!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Eggs, researchManager))
            {
                if (XenoSocialDefOf.XMT_Starbeast_Eggs.PrerequisitesCompleted)
                {
                    researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Eggs, progress, pawn);
                }
            }
        }

        internal static void ProgressResinTech(int progress, Thing actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            Pawn pawn = actor as Pawn;

            if (!FinishedResearching(XenoSocialDefOf.XMT_Starbeast_Reinforcement, researchManager))
            {
                researchManager.AddProgress(XenoSocialDefOf.XMT_Starbeast_Reinforcement, progress, pawn);
            }
        }
        internal static void ProgressCryptobioTech(int progress, Pawn actor)
        {
            ResearchManager researchManager = Find.ResearchManager;

            Pawn pawn = actor as Pawn;

            if (!FinishedResearching(XenoGeneDefOf.XMT_Starbeast_Genetics, researchManager))
            {
                researchManager.AddProgress(XenoGeneDefOf.XMT_Starbeast_Genetics, progress, pawn);
            }
        }
    }
}
