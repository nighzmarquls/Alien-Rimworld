using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class QueenAssimilationDef : Def
    {
        public RoyalEvolutionDef requiredEvolution;
        public List<QueenAssimilationDef> prerequisiteAssimilations;
        public ThingDef thingDef;
        public int consumeCount = 1;
        public bool oncePerQueen = true;
        public HediffDef implantHediff;
        public BodyPartDef implantBodyPart;
        public List<HediffDef> hediffsToAdd;
        public ResearchProjectDef researchToFinish;
        public QuestScriptDef questToTrigger;
        public string questLetterLabelKey;
        public string questLetterTextKey;
        public string completedMessageKey;

        public bool HasPrerequisites(CompQueenAssimilation comp)
        {
            if (comp == null)
            {
                return false;
            }

            if (requiredEvolution != null && !comp.HasEvolution(requiredEvolution))
            {
                return false;
            }

            if (prerequisiteAssimilations != null)
            {
                foreach (QueenAssimilationDef prerequisite in prerequisiteAssimilations)
                {
                    if (!comp.HasAssimilated(prerequisite))
                    {
                        return false;
                    }
                }
            }

            if (oncePerQueen && comp.HasAssimilated(this))
            {
                return false;
            }

            return true;
        }
    }
}
