using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_LarvaImplant : JobDriver
    {
        public Pawn Prey
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing as Pawn;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedOrNull(TargetIndex.A)
                .FailOn(() => Prey == null || pawn == null || pawn.Dead)
                .FailOn(() => NemesisMissionUtility.IsSwarmMember(pawn)
                    && pawn.GetComp<CompHostHunter>()?.IsValidMissionTarget(Prey) != true)
                .FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return AttemptAttachment();
        }

        private Toil AttemptAttachment()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                Pawn prey = Prey;
                if (prey == null || pawn == null || pawn.Dead)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                CompHostHunter HostHunter = pawn.GetComp<CompHostHunter>();
                if(HostHunter != null)
                {
                    if (NemesisMissionUtility.IsSwarmMember(pawn)
                        && !HostHunter.IsValidMissionTarget(prey))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    FinishAttachmentAttempt(prey, HostHunter.AttemptAttachToHost(prey));
                    return;
                }

                EndJobWith(JobCondition.Incompletable);

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private void FinishAttachmentAttempt(Pawn prey, AttachmentAttemptResult result)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message($"[XMT][Attachment] {pawn} -> {prey}: {result}; attacker spawned={pawn.Spawned}, position={pawn.PositionHeld}, host position={prey.PositionHeld}, same map={pawn.MapHeld == prey.MapHeld}.");
            }
            // Containment stops the attacker's jobs. Do not finish a replacement job.
            if (pawn.jobs.curDriver == this)
            {
                EndJobWith(result == AttachmentAttemptResult.Attached
                    ? JobCondition.Succeeded : JobCondition.Incompletable);
            }
        }
    }
}
