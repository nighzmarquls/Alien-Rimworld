using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Recipe_MedicalExamination : Recipe_Surgery
    {
        private const int AnestheticTicks = 60000;

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer,
            List<Thing> ingredients, Bill bill)
        {
            if (pawn == null || pawn.Dead || CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
            {
                return;
            }

            string findings = XMTMedicalExaminationUtility.ExamineWithoutAnomaly(pawn, billDoer);
            TaggedString text = findings.NullOrEmpty()
                ? "XMT_MedicalExaminationNothing".Translate(billDoer.Named("DOCTOR"), pawn.Named("PATIENT"))
                : "XMT_MedicalExaminationDetected".Translate(billDoer.Named("DOCTOR"), pawn.Named("PATIENT"),
                    findings.Named("FINDINGS"));
            Find.LetterStack.ReceiveLetter("XMT_MedicalExaminationLetterLabel".Translate(), text,
                LetterDefOf.NeutralEvent, pawn);

            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            pawn.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, 4f));
            HediffComp_Disappears anesthetic = pawn.health.AddHediff(HediffDefOf.Anesthetic)
                .TryGetComp<HediffComp_Disappears>();
            if (anesthetic != null)
            {
                anesthetic.disappearsAfterTicks = AnestheticTicks;
                anesthetic.ticksToDisappear = AnestheticTicks;
            }
        }
    }
}
