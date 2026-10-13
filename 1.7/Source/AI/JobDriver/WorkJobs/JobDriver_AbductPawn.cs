using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public abstract class JobDriver_AbductPawn : JobDriver_ClimbToPosition
    {
        private const float GrabTicksFinish = 30f;
        private float grabTicks;
        private bool failedGrab;
        private bool grabChecked;
        private bool grabbed;
        public Pawn Victim => job.GetTarget(TargetIndex.A).Pawn;
        protected bool FailedGrab => failedGrab;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref grabTicks, "grabTicks");
            Scribe_Values.Look(ref failedGrab, "failedGrab");
            Scribe_Values.Look(ref grabChecked, "grabChecked");
            Scribe_Values.Look(ref grabbed, "grabbed");
        }

        protected Toil AttemptGrab()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                if (grabChecked) return;
                grabChecked = true;
                CompMatureMorph matureMorph = pawn.GetMorphComp();
                failedGrab = !CanGrabNow() || matureMorph == null || !matureMorph.InitiateGrabCheck(Victim);
            };
            toil.tickAction = delegate
            {
                if (failedGrab || !CanGrabNow()) { EndJobWith(JobCondition.Incompletable); return; }
                grabTicks++;
                if (grabTicks < GrabTicksFinish) return;
                CompMatureMorph matureMorph = pawn.GetMorphComp();
                if (matureMorph != null && !grabbed) { grabbed = true; matureMorph.TryGrab(Victim); }
                ReadyForNextToil();
            };
            toil.WithProgressBar(TargetIndex.A, () => grabTicks / GrabTicksFinish);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private bool CanGrabNow() => pawn.Spawned && !pawn.Dead && Victim != null && !Victim.Dead
            && Victim.Spawned && Victim.Map == pawn.Map && pawn.CanReachImmediate(Victim, PathEndMode.Touch);
    }
}
