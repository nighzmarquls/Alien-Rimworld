
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_ExtractAcid : Recipe_Surgery
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
                return BioUtility.PawnHasEnoughForExtraction(surgeryTarget, false);
            }

            return false;
        }

        public override void CheckForWarnings(Pawn medPawn)
        {
            base.CheckForWarnings(medPawn);
            if (!BioUtility.PawnHasEnoughForExtraction(medPawn, false))
            {
                Messages.Message("XMT_MessageCannotStartResinExtraction".Translate(medPawn.Named("PAWN")), medPawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!BioUtility.PawnHasEnoughForExtraction(pawn, false))
            {
                Messages.Message("XMT_MessagePawnHadNotEnoughToProduceResin".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
                return;
            }

            BioUtility.ExtractMetabolicCostFromPawn(pawn, false);
           
            OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, HistoryEventDefOf.ExtractedHemogenPack);
            }
        }

        protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Thing acid = ThingMaker.MakeThing(InternalDefOf.XMT_Acid);
            acid.stackCount = 2;
            XMTUtility.GiveInteractionMemory(pawn, ThoughtDefOf.HarmedMe, billDoer);
            ResearchUtility.ProgressAcidTech(2, billDoer);

            if (!GenPlace.TryPlaceThing(acid, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near))
            {
                Log.Error("Could not drop acid near " + pawn.PositionHeld.ToString());
            }
        }


    }
}
