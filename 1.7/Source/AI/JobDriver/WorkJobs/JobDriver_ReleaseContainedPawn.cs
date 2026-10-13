using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_ReleaseContainedPawn : JobDriver
    {
        private const TargetIndex SourceIndex = TargetIndex.A;
        private const TargetIndex PawnIndex = TargetIndex.B;

        private Thing Source => job.GetTarget(SourceIndex).Thing;
        private Pawn Occupant => job.GetTarget(PawnIndex).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Source, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(SourceIndex);
            yield return Toils_Goto.GotoThing(SourceIndex, PathEndMode.Touch);

            Toil wait = Toils_General.WaitWith(SourceIndex, 120, useProgressBar: true);
            wait.FailOn(() => XMTContainmentUtility.HeldPawn(Source) != Occupant);
            yield return wait;

            Toil release = ToilMaker.MakeToil("ReleaseContainedPawn");
            release.initAction = delegate
            {
                if (XMTContainmentUtility.HeldPawn(Source) != Occupant ||
                    !XMTContainmentUtility.Eject(Occupant))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            release.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return release;
        }
    }
}
