using RimWorld;
using System.Collections.Generic;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobDriver_StarbeastSnuggle : JobDriver
    {
        private float TicksFinish = 120;
        private float Ticks = 0;
        private float Progress = 0;
        private Job recipientWaitJob;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Ticks, "snuggleTicks", 0f);
            Scribe_References.Look(ref recipientWaitJob, "snuggleRecipientWaitJob");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Progress = Ticks / TicksFinish;
            }
        }

        private bool IsValidRecipient(Pawn recipient)
        {
            return recipient != null && !recipient.Dead && recipient.Spawned &&
                   pawn.Spawned && !pawn.Dead && recipient.Map == pawn.Map &&
                   pawn.CanReachImmediate(recipient, PathEndMode.Touch);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private Toil AttemptSnuggle()
        {
            Toil toil = ToilMaker.MakeToil("AttemptSnuggle");
            toil.atomicWithPrevious = true;
            toil.socialMode = RandomSocialMode.Off;
            toil.initAction = delegate
            {
                Ticks = 0;
                Progress = 0;
                Pawn recipient = job.targetA.Thing as Pawn;
                if (!IsValidRecipient(recipient))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Roll once, before replacing the recipient's job with a paired wait.
                Job previousJob = recipient.CurJob;
                XMTUtility.TraumaResponse(pawn, recipient.Info(), fromInterruption: true);
                if (!IsValidRecipient(recipient) || recipient.InMentalState || recipient.CurJob != previousJob)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                PawnUtility.ForceWait(recipient, (int)TicksFinish + 1, pawn,
                    maintainPosture: true, maintainSleep: true);
                recipientWaitJob = recipient.CurJob;
            };
            toil.tickAction = delegate
            {
                Pawn recipient = job.targetA.Thing as Pawn;
                if (!IsValidRecipient(recipient) || recipient.InMentalState ||
                    recipientWaitJob == null || recipient.CurJob != recipientWaitJob)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                Ticks += 1;
                Progress = (Ticks / TicksFinish);
                if (Ticks < TicksFinish)
                {
                    return;
                }

                pawn.needs?.joy?.GainJoy(0.1f * Progress, ExternalDefOf.Social);
                if (recipient.relations != null)
                {
                    CompPawnInfo pawnInfo = recipient.Info();
                    if (pawnInfo != null)
                    {
                        if (pawnInfo.IsObsessed() || recipient.relations.OpinionOf(pawn) > 0)
                        {
                            XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimSnuggledHappy, pawn);
                            if (recipient.needs?.joy != null)
                            {
                                recipient.needs.joy.GainJoy(0.12f, JoyKindDefOf.Social);
                                pawnInfo.GainObsession(0.12f);
                            }
                        }
                        else
                        {
                            XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimSnuggledScared, pawn);
                        }
                    }
                    else
                    {
                        XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimSnuggledScared, pawn);
                    }
                }
                XMTUtility.GiveInteractionMemory(pawn, HorrorMoodDefOf.SnuggledVictim, recipient);

                CompPawnInfo info = recipient.Info();

                if (info != null)
                {
                    info.ApplyFriendlyPheromone(pawn, 0.25f*Progress);
                }

                ReadyForNextToil();
            };
            toil.AddFinishAction(delegate
            {
                Pawn recipient = job.targetA.Thing as Pawn;
                // Only release the wait owned by this interaction, never a replacement job.
                if (recipientWaitJob != null && recipient?.CurJob == recipientWaitJob)
                {
                    recipient.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                recipientWaitJob = null;
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => job.targetA.Thing is not Pawn recipient || recipient.Dead || recipient.Map != pawn.Map);
            this.FailOnNotCasualInterruptible(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
            yield return AttemptSnuggle();
        }
    }
}
