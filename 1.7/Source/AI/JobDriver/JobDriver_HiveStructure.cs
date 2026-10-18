using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_HiveStructure : JobDriver
    {
        private int work;
        private bool completed;
        private HiveStructureUnlockDef Unlock => job.def.GetModExtension<HiveStructureJobExtension>()?.unlock;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref work, "structureWork");
            Scribe_Values.Look(ref completed, "structureCompleted");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Unlock == null || !pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
                return ReservationFailed();
            if (job.targetA.Thing is HiveStructureSite site)
                foreach (IntVec3 cell in GenAdj.OccupiedRect(site.Position, site.Rotation, Unlock.building.size))
                    if (!pawn.Reserve(cell, job, 1, -1, null, errorOnFailed))
                        return ReservationFailed();
            return true;
        }

        private bool ReservationFailed()
        {
            if (job.targetA.Thing is HiveStructureSite site && !site.Destroyed)
                site.Destroy();
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Unlock == null || !HiveStructureUtility.Unlocked(Unlock) || !Unlock.Worker.ActorAllowed(Unlock, pawn) || (Unlock.mode != HiveCreationMode.SelfTransform && !Unlock.Worker.TargetAllowed(Unlock, job.targetA.Thing)));
            AddFinishAction(condition =>
            {
                if (!completed && job.targetA.Thing is HiveStructureSite site && !site.Destroyed)
                    site.Destroy();
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil toil = ToilMaker.MakeToil("HiveStructureCreation");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.tickAction = () =>
            {
                if (++work < Unlock.workTicks)
                    return;
                if (!pawn.CanReachImmediate(job.targetA, PathEndMode.Touch))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Preservation can end the actor's current job; never continue through an initAction after that.
                completed = true;
                if (!Unlock.Worker.Complete(Unlock, pawn, job.targetA.Thing))
                {
                    completed = false;
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (pawn.Spawned && pawn.CurJob == job)
                    ReadyForNextToil();
            };
            toil.WithProgressBar(TargetIndex.A, () => work / (float)Unlock.workTicks);
            if (Unlock.workEffect != null)
                toil.WithEffect(Unlock.workEffect, TargetIndex.A);
            yield return toil;
        }
    }
}
