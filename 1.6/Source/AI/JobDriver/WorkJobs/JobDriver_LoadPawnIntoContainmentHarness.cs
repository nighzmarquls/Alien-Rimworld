using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_LoadPawnIntoContainmentHarness : JobDriver
    {
        private const TargetIndex PawnIndex = TargetIndex.A;
        private const TargetIndex HarnessIndex = TargetIndex.B;

        private Pawn TargetPawn => job.GetTarget(PawnIndex).Pawn;
        private Building_ContainmentHarness Harness => job.GetTarget(HarnessIndex).Thing as Building_ContainmentHarness;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, errorOnFailed: errorOnFailed) &&
                pawn.Reserve(Harness, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(PawnIndex);
            this.FailOnDestroyedOrNull(HarnessIndex);
            this.FailOn(() => Harness.ContainedThing != null ||
                Harness.ContainmentComp?.CanContain(TargetPawn, requireDowned: false) != true);

            yield return Toils_Goto.GotoThing(PawnIndex, PathEndMode.Touch);

            Toil arrest = ToilMaker.MakeToil("ArrestForContainmentHarness");
            arrest.initAction = delegate
            {
                Pawn actor = arrest.actor;
                Pawn target = TargetPawn;
                if (target == null || target.Downed || target.IsPrisonerOfColony)
                {
                    return;
                }

                if (!GenAI.CanBeArrestedBy(target, actor))
                {
                    Messages.Message("XMT_CannotArrestForHarness".Translate(target.Named("PAWN")), target,
                        MessageTypeDefOf.RejectInput);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!target.CheckAcceptArrest(actor))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                XMTContainmentUtility.RegisterPrisoner(target, actor);
            };
            arrest.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return arrest;

            Toil registerCapture = ToilMaker.MakeToil("RegisterHarnessCapture");
            registerCapture.initAction = delegate
            {
                Pawn target = TargetPawn;
                if (target?.Faction != Faction.OfPlayer && !target.IsPrisonerOfColony)
                {
                    XMTContainmentUtility.RegisterPrisoner(target, pawn);
                }
            };
            registerCapture.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return registerCapture;

            yield return Toils_Haul.StartCarryThing(PawnIndex);
            yield return Toils_Goto.GotoThing(HarnessIndex, PathEndMode.Touch);

            Toil insert = ToilMaker.MakeToil("LoadPawnIntoContainmentHarness");
            insert.initAction = delegate
            {
                if (Harness.TryAcceptPawn(TargetPawn))
                {
                    return;
                }

                if (pawn.carryTracker.CarriedThing == TargetPawn)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _, null);
                }
                EndJobWith(JobCondition.Incompletable);
            };
            insert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insert;
        }

    }
}
