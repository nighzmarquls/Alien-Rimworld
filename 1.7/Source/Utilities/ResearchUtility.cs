using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class ResearchUtility
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

        internal static bool IsHumanResearchContributor(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.RaceProps.Humanlike &&
                pawn.GetMorphComp() == null && pawn.Faction != null && pawn.Faction.IsPlayer;
        }

        private static bool CanAutomaticallyResearch(ResearchProjectDef project, Thing actor, Map map)
        {
            if (project == null || Find.ResearchManager == null || map == null) return false;
            if (actor is Pawn pawn && pawn.Dead) return false;
            if (actor?.Faction != null && !actor.Faction.IsPlayer) return false;

            if (project.tab == XenoSocialDefOf.XMT_StarbeastResearchTab)
            {
                return XMTUtility.PlayerFactionIsCryptimorph() && actor?.Faction?.IsPlayer == true;
            }

            if (project.tab != XenoGeneDefOf.XMT_HumanResearchTab) return false;
            if (actor is Pawn contributor)
            {
                if (contributor.Faction?.IsPlayer != true) return false;
                if (contributor.MapHeld == map && IsHumanResearchContributor(contributor)) return true;
            }

            // Use the faction's native spawned-pawn index, only for sources that cannot
            // themselves establish human presence (for example a destroyed genepack).
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null) return false;
            foreach (Pawn candidate in map.mapPawns.SpawnedPawnsInFaction(player))
            {
                if (IsHumanResearchContributor(candidate)) return true;
            }
            return false;
        }

        internal static void ApplyWitnessExposure(Pawn witness, KnowledgeCategoryDef subject,
            KnowledgeProfileDef profile, float strength, Thing source, int progress, bool acid = false)
        {
            bool knewSubject = KnowledgeUtility.GetEffectiveKnowledge(witness, subject) > 0f;
            KnowledgeUtility.ApplyExposure(witness, profile, strength, source: source);
            if (knewSubject || !IsHumanResearchContributor(witness) ||
                KnowledgeUtility.GetEffectiveKnowledge(witness, subject) <= 0f) return;
            if (acid) ProgressAcidTech(progress, witness);
            else ProgressCryptobioTech(progress, witness);
        }

        internal static void SeedCryptobioTech(int progress, Pawn actor)
        {
            if (Find.ResearchManager == null || HumanProjectsVisible() || !IsHumanResearchContributor(actor)) return;
            ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Jelly_Extraction);
        }

        internal static void ProgressGeneticsTech(int progress, Thing actor, Map map = null)
        {
            ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Starbeast_Genetics, map);
        }

        static bool FinishedResearching(ResearchProjectDef project, ResearchManager researchManager)
        {
            return researchManager.GetProgress(project) >= project.Cost;
        }

        static bool ProgressTechProjectOrPrerequisites(int progress, Thing actor, ResearchProjectDef project, Map map = null)
        {
            map ??= actor?.MapHeld;
            if (progress <= 0 || !CanAutomaticallyResearch(project, actor, map)) return false;
            ResearchManager researchManager = Find.ResearchManager;
            if (FinishedResearching(project, researchManager))
            {
                return false;
            }

            ResearchProjectDef targetProjectDef = project;
            bool stillSearching = true;

            while (targetProjectDef.prerequisites != null && targetProjectDef.prerequisites.Count > 0 && stillSearching)
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

            if (targetProjectDef != project && !CanAutomaticallyResearch(targetProjectDef, actor, map)) return false;
            Pawn pawn = actor as Pawn;
            float researchedProgress = researchManager.GetProgress(targetProjectDef);
            float remaining = targetProjectDef.Cost - researchedProgress;
            if (remaining <= 0f) return false;
            researchManager.AddProgress(targetProjectDef, System.Math.Min(progress, remaining), progress <= remaining ? pawn : null);

            return true;
        }
        internal static void ProgressMimicTech(int progress, Thing actor)
        {
            ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_Sculpture);
        }

        internal static void ProgressEvolutionTech(int progress, Thing actor, Map map = null)
        {
            if(ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_JellyTransport, map))
            {
                return;
            }
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoSocialDefOf.XMT_Starbeast_Eggs, map))
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

        internal static void ProgressAcidTech(int progress, Pawn actor)
        {
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Acid_Utilization))
            {
                return;
            }

            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Jelly_Drugs))
            {
                return;
            }
        }
        internal static void ProgressCryptobioTech(int progress, Pawn actor)
        {
            //Log.Message(progress + " being added to cryptobiotech by " + actor);
            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Jelly_Drugs))
            {
                return;
            }

            if (ProgressTechProjectOrPrerequisites(progress, actor, XenoGeneDefOf.XMT_Mutation_Targeted))
            {
                return;
            }
        }
    }
}
