using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_AdvanceHorror : JobDriver
    {
        private const int WorkTicks = 350;
        private int ticksWorked;

        private Thing Target => job.GetTarget(TargetIndex.A).Thing;
        private CompGeneManipulator Manipulator => pawn.GetComp<CompGeneManipulator>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOn(() => Find.TickManager.TicksGame > startTick + 5000 &&
                    (job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return PerformAdvancement();
        }

        private Toil PerformAdvancement()
        {
            Toil toil = ToilMaker.MakeToil("PerformHorrorAdvancement");
            toil.atomicWithPrevious = true;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.initAction = delegate
            {
                ticksWorked = 0;
                if (Target is Pawn targetPawn)
                {
                    PawnUtility.ForceWait(targetPawn, WorkTicks, pawn);
                }
            };
            toil.tickAction = delegate
            {
                HorrorAdvancementOrder order = Manipulator?.FindHorrorAdvancementOrder(Target);
                HorrorAdvancementOption option = order == null
                    ? null
                    : HorrorAdvancementUtility.MakeOption(order.direction, order.pawnKind, order.thingDef);
                AcceptanceReport report = HorrorAdvancementUtility.CanExecute(pawn, Target, option);
                if (!report.Accepted)
                {
                    if (pawn.Faction == Faction.OfPlayer)
                    {
                        Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                    }
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                ticksWorked++;
                if (ticksWorked < WorkTicks)
                {
                    return;
                }

                bool success = Manipulator.TryExecuteHorrorAdvancementOrder(Target, out bool foundOrder);
                pawn.jobs.EndCurrentJob(success ? JobCondition.Succeeded : JobCondition.Incompletable);
            };
            toil.AddFinishAction(() => Manipulator?.CancelHorrorAdvancementOrder(Target));
            toil.WithProgressBar(TargetIndex.A, () => Mathf.Clamp01((float)ticksWorked / WorkTicks));
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            return toil;
        }
    }
}
