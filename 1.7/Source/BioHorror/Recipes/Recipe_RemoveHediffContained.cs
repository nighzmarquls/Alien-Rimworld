using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_RemoveHediffContained : Recipe_RemoveHediff
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Thing outerContainer = billDoer?.CurJob?.source as Thing;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(recipe.removesHediff);
            HediffComp_LarvalAttachment larva = hediff?.TryGetComp<HediffComp_LarvalAttachment>();
            HediffComp_EmbryoPregnancy embryo = hediff?.TryGetComp<HediffComp_EmbryoPregnancy>();

            if (larva != null)
            {
                larva.pendingBioContainer = outerContainer;
            }
            if (embryo != null)
            {
                embryo.pendingBioContainer = outerContainer;
            }

            try
            {
                base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            }
            finally
            {
                if (larva != null)
                {
                    larva.pendingBioContainer = null;
                }
                if (embryo != null)
                {
                    embryo.pendingBioContainer = null;
                }
            }
        }
    }
}
