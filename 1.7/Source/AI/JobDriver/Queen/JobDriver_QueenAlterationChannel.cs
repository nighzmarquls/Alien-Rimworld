using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal abstract class JobDriver_QueenAlterationChannel : JobDriver
    {
        private int ticksWorked;
        private int workTicks;
        private IntVec3 casterStartPosition;
        private IntVec3 targetStartPosition;

        protected Thing Target => job.GetTarget(TargetIndex.A).Thing;
        protected bool RemoteChannel { get; private set; }

        protected abstract string ChannelToilName { get; }
        protected virtual bool AllowsRemoteChannel => true;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            RemoteChannel = AllowsRemoteChannel && SovereignAlterationUtility.IsRemote(pawn);
            if (!RemoteChannel)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                    .FailOn(() => Find.TickManager.TicksGame > startTick + 5000
                        && (job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            }

            yield return ChannelToil();
        }

        protected virtual AcceptanceReport ValidateSpecificTarget()
        {
            return AcceptanceReport.WasAccepted;
        }

        protected abstract bool CompleteChannel();

        protected virtual void CleanupChannel()
        {
        }

        private Toil ChannelToil()
        {
            Toil toil = ToilMaker.MakeToil(ChannelToilName);
            toil.atomicWithPrevious = !RemoteChannel;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.initAction = delegate
            {
                ticksWorked = 0;
                workTicks = SovereignAlterationUtility.WorkTicks(pawn, RemoteChannel);
                casterStartPosition = pawn.Position;
                targetStartPosition = Target.Position;
                if (Target is Pawn targetPawn)
                {
                    PawnUtility.ForceWait(targetPawn, workTicks, pawn);
                }
            };
            toil.tickAction = delegate
            {
                AcceptanceReport report = SovereignAlterationUtility.CanContinue(
                    pawn, Target, RemoteChannel, casterStartPosition, targetStartPosition);
                if (report.Accepted)
                {
                    report = ValidateSpecificTarget();
                }

                if (!report.Accepted)
                {
                    if (pawn.Faction == Faction.OfPlayer && !report.Reason.NullOrEmpty())
                    {
                        Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                    }
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                ticksWorked++;
                if (ticksWorked >= workTicks)
                {
                    pawn.jobs.EndCurrentJob(CompleteChannel() ? JobCondition.Succeeded : JobCondition.Incompletable);
                }
            };
            toil.AddFinishAction(CleanupChannel);
            toil.WithProgressBar(TargetIndex.A, () => workTicks > 0 ? Mathf.Clamp01((float)ticksWorked / workTicks) : 0f);
            AddTargetCenteredEffect(toil, InternalDefOf.ResinBuild);
            return toil;
        }

        private void AddTargetCenteredEffect(Toil toil, EffecterDef effecterDef)
        {
            Effecter effecter = null;
            toil.AddPreTickAction(delegate
            {
                Thing target = Target;
                if (effecterDef == null || target == null || target.Destroyed || !target.Spawned)
                {
                    return;
                }

                TargetInfo targetInfo = target;
                if (effecter == null)
                {
                    pawn.rotationTracker.FaceTarget(target);
                    effecter = effecterDef.Spawn();
                    effecter.Trigger(targetInfo, targetInfo);
                }
                else
                {
                    effecter.EffectTick(targetInfo, targetInfo);
                }
            });
            toil.AddFinishAction(delegate
            {
                effecter?.Cleanup();
                effecter = null;
            });
        }
    }
}
