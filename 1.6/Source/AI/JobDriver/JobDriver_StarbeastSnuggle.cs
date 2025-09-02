using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobDriver_StarbeastSnuggle : JobDriver
    {
        private float TicksFinish = 120;
        private float Ticks = 0;
        private float Progress = 0;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private Toil AttemptSnuggle()
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
                Pawn recipient = (Pawn)pawn.CurJob.targetA.Thing;

                pawn.needs.joy.GainJoy(0.1f * Progress, ExternalDefOf.Social);
                if (recipient.relations != null)
                {
                    CompPawnInfo pawnInfo = recipient.Info();
                    if (pawnInfo != null)
                    {
                        if (pawnInfo.IsObsessed() || recipient.relations.OpinionOf(pawn) > 0)
                        {
                            XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimTrophallaxisHappy, pawn);
                            if (recipient.needs.joy != null)
                            {
                                recipient.needs.joy.GainJoy(0.12f, JoyKindDefOf.Gluttonous);
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
            yield return AttemptSnuggle();
        }
    }
}
