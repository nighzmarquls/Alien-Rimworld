using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_ExtractPheromone : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn && PheromoneUtility.IsPheromoneProducer(pawn) && base.AvailableOnNow(thing, part);
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            return base.CompletableEver(surgeryTarget) && PheromoneUtility.CanExtractFrom(surgeryTarget);
        }

        public override void CheckForWarnings(Pawn medPawn)
        {
            base.CheckForWarnings(medPawn);
            if (!PheromoneUtility.CanExtractFrom(medPawn))
            {
                Messages.Message("XMT_MessageCannotStartPheromoneExtraction".Translate(medPawn.Named("PAWN")), medPawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!PheromoneUtility.TryExtractGland(pawn, billDoer, pawn.PositionHeld, pawn.MapHeld))
            {
                Messages.Message("XMT_MessagePawnHadNotEnoughToProducePheromone".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
                return;
            }

            NemesisEvidenceReporter.ReportHarvest(pawn, billDoer, recipe, "pheromone surgery");

            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, HistoryEventDefOf.ExtractedHemogenPack);
            }
        }
    }
}
