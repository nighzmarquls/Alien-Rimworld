
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_ExtractResin : Recipe_Surgery
    {
        private const float MetabolicLossSeverity = 0.10f;

        private const float MinMetabolicLossSeverity = 0.10f;

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
                return PawnHasEnoughForExtraction(surgeryTarget);
            }

            return false;
        }

        public override void CheckForWarnings(Pawn medPawn)
        {
            base.CheckForWarnings(medPawn);
            if (!PawnHasEnoughForExtraction(medPawn))
            {
                Messages.Message("XMT_MessageCannotStartResinExtraction".Translate(medPawn.Named("PAWN")), medPawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (!PawnHasEnoughForExtraction(pawn))
            {
                Messages.Message("XMT_MessagePawnHadNotEnoughToProduceResin".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent);
                return;
            }

            if (pawn.needs != null && pawn.needs.food != null)
            {
                pawn.needs.food.CurLevel -= MetabolicLossSeverity;
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
                hediff.Severity = MetabolicLossSeverity;
                pawn.health.AddHediff(hediff);
            }

            OnSurgerySuccess(pawn, part, billDoer, ingredients, bill);
            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -1, HistoryEventDefOf.ExtractedHemogenPack);
            }
        }

        protected override void OnSurgerySuccess(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Thing jelly = ThingMaker.MakeThing(InternalDefOf.Starbeast_Resin);
            jelly.stackCount = 10;

            XMTResearch.ProgressCryptobioTech(2, billDoer);

            if (!GenPlace.TryPlaceThing(jelly, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near))
            {
                Log.Error("Could not drop resin near " + pawn.PositionHeld.ToString());
            }
        }

        private bool PawnHasEnoughForExtraction(Pawn pawn)
        {
            if(pawn.needs != null && pawn.needs.food != null)
            {
                return pawn.needs.food.CurLevel > MinMetabolicLossSeverity;
            }
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (firstHediffOfDef != null)
            {
                return firstHediffOfDef.Severity < MinMetabolicLossSeverity;
            }

            return true;
        }
    }
}
