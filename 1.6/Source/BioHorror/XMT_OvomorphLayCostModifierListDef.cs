using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class OvomorphLayCostModifier
    {
        public GeneDef gene;
        public HediffDef hediff;
        public TraitDef trait;
        public float factor = 1f;

        public bool AppliesTo(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (gene != null && pawn.genes != null)
            {
                foreach (Gene pawnGene in pawn.genes.GenesListForReading)
                {
                    if (pawnGene.def == gene && pawnGene.Active)
                    {
                        return true;
                    }
                }
            }

            if (hediff != null && pawn.health?.hediffSet?.HasHediff(hediff) == true)
            {
                return true;
            }

            if (trait != null && pawn.story?.traits?.HasTrait(trait) == true)
            {
                return true;
            }

            return false;
        }
    }

    public class XMT_OvomorphLayCostModifierListDef : Def
    {
        private static readonly List<OvomorphLayCostModifier> CostModifiers = new List<OvomorphLayCostModifier>();

        public List<OvomorphLayCostModifier> costModifiers;

        public override void ResolveReferences()
        {
            base.ResolveReferences();

            if (costModifiers == null)
            {
                return;
            }

            CostModifiers.AddRange(costModifiers);
        }

        public static float CostFactorFor(Pawn pawn)
        {
            float factor = 1f;

            foreach (OvomorphLayCostModifier modifier in CostModifiers)
            {
                if (modifier.AppliesTo(pawn))
                {
                    factor *= modifier.factor;
                }
            }

            return factor;
        }
    }
}
