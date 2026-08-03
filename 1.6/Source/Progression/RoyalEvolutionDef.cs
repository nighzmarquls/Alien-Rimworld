using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class RoyalEvolutionDef : Def
    {
        public int evoPointCost;

        public HediffDef evolutionHediff;

        public BodyPartDef targetBodyPart;

        public List<AbilityDef> unlockedAbilities;
        public List<GeneDef> unlockedGenes;

        public List<RoyalEvolutionDef> replaces;
        public List<RoyalEvolutionDef> prerequisites;
        public List<RoyalEvolutionDef> incompatible;

        // Replacement advancements normally replace all logic and physical adaptations.
        // These independent opt-ins let replacement defs retain either category.
        public bool preserveReplacedFeatures = false;
        public bool preserveHediff = false;

        public ConceptDef tutorialConcept;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if ((preserveReplacedFeatures || preserveHediff) && replaces.NullOrEmpty())
            {
                yield return $"{defName} enables replacement preservation but does not define any replaced advancements.";
            }
        }

        public bool AvailableForPawn(Pawn pawn)
        {
            CompQueen compQueen = pawn.GetComp<CompQueen>();
            if(compQueen == null)
            {
                return false;
            }

            if (compQueen.AvailableEvoPoints < evoPointCost)
            {
                return false;
            }

            return true;
        }

        public bool IsPrerequisiteOfHeldPermit(Pawn pawn)
        {
            return false;
        }
    }


    public class RoyalEvolutionSet : Def
    {
        public List<RoyalEvolutionDef> evolutions;
    }
}
