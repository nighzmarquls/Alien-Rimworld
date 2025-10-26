using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_EnemyMark : JobDriver
    {
        private float TicksFinish = 120;
        private float Ticks = 0;
        private float Progress = 0;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private Toil AttemptMark()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {
                Ticks += 1;
                Progress = (Ticks / TicksFinish);
                if (Ticks >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (pawn.CurJob == null || pawn.CurJob.targetA == null)
                {
                    return;
                }

                Pawn recipient = (Pawn)pawn.CurJob.targetA.Thing;
               
                if (recipient == null)
                {
                    return;
                }

                pawn.needs.joy.GainJoy(0.1f * Progress, ExternalDefOf.Social);
                XMTUtility.GiveInteractionMemory(pawn, HorrorMoodDefOf.SnuggledVictim, recipient);

                CompPawnInfo info = recipient.Info();

                if (info != null)
                {
                    info.ApplyThreatPheromone(pawn, 0.25f * Progress);
                }

                if (recipient.relations != null)
                {
                    if(recipient.needs == null)
                    {
                        return;
                    }

                    if (info != null)
                    {
                        if (info.IsObsessed() || recipient.relations.OpinionOf(pawn) > 0)
                        {
                            XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimSnuggledHappy, pawn);
                            if (recipient.needs.joy != null)
                            {
                                recipient.needs.joy.GainJoy(0.12f, JoyKindDefOf.Social);
                                info.GainObsession(0.12f);
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

            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnNotCasualInterruptible(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
            Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).socialMode = RandomSocialMode.Off;
            yield return AttemptMark();
        }
    }
}
