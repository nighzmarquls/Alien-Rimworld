
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_ExtractJelly : Recipe_Surgery
    {


        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            if (pawn != null && !XMTUtility.IsXenomorph(pawn))
            {
                return false;
            }

            return base.AvailableOnNow(thing, part);
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            return base.AvailableReport(thing, part);
        }

        public override bool CompletableEver(Pawn surgeryTarget)
        {
            if (base.CompletableEver(surgeryTarget))
            {
                return BioUtility.PawnHasEnoughForExtraction(surgeryTarget);
            }

            return false;
        }

        public override void CheckForWarnings(Pawn medPawn)
        {
            base.CheckForWarnings(medPawn);
            if (!BioUtility.PawnHasEnoughForExtraction(medPawn))
            {
                Messages.Message("XMT_MessageCannotStartJellyExtraction".Translate(medPawn.Named("PAWN")), medPawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!BioUtility.PawnHasEnoughForExtraction(pawn))
            {
                Messages.Message("XMT_MessagePawnHadNotEnoughToProduceJelly".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
                return;
            }

            BioUtility.ExtractMetabolicCostFromPawn(pawn);

            OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, HistoryEventDefOf.ExtractedHemogenPack);
            }
        }

        protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Thing jelly = ThingMaker.MakeThing(InternalDefOf.Starbeast_Jelly);
            jelly.stackCount = 5;
            XMTUtility.GiveInteractionMemory(pawn, ThoughtDefOf.HarmedMe, billDoer);
            ResearchUtility.ProgressCryptobioTech(5, billDoer);

            if (!GenPlace.TryPlaceThing(jelly, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near))
            {
                Log.Error("Could not drop jelly near " + pawn.PositionHeld.ToString());
            }
        }
    }
}
