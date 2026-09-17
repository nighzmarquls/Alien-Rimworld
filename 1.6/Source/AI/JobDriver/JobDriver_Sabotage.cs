using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public class JobDriver_Sabotage : JobDriver_ClimbToPosition
    {
        private bool approachStarted;
        private bool sabotageCompleted;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref approachStarted, "approachStarted");
            Scribe_Values.Look(ref sabotageCompleted, "sabotageCompleted");
        }

        internal void NotifyCleanup(JobCondition condition)
        {
            Thing target = TargetThingA;
            if (condition == JobCondition.Incompletable && approachStarted && !sabotageCompleted
                && target != null && !target.Destroyed && target.Spawned)
                (pawn.GetLord()?.LordJob as LordJob_NemesisMission)?.Notify_SabotageTargetInaccessible(pawn, target);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing target = TargetThingA;
            if (target == null || !FeralJobUtility.IsThingAvailableForJobBy(pawn, target))
            {
                return false;
            }

            FeralJobUtility.ReserveThingForJob(pawn, job, target);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            approach.AddPreInitAction(delegate { approachStarted = true; });
            yield return approach;
            yield return Toils_General.Do(delegate
            {
                Thing target = pawn.CurJob.targetA.Thing;
                if (target != null)
                {
                    LordJob_NemesisMission mission = pawn.GetLord()?.LordJob as LordJob_NemesisMission;
                    XMTUtility.SabotageThing(target, pawn);
                    sabotageCompleted = true;
                    mission?.Notify_SabotageTargetResolved(pawn, target);
                }
            });
        }
    }
}
