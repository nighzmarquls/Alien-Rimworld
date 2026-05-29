using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_AssimilateQueenItem : JobDriver
    {
        private Thing Target => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, job.count, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                CompQueenAssimilation comp = pawn.GetComp<CompQueenAssimilation>();
                return comp == null || !comp.CanAssimilate(Target, out QueenAssimilationDef _).Accepted;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil assimilate = ToilMaker.MakeToil("AssimilateQueenItem");
            assimilate.defaultCompleteMode = ToilCompleteMode.Delay;
            assimilate.defaultDuration = AssimilationDurationTicks();
            assimilate.handlingFacing = true;
            assimilate.tickAction = delegate
            {
                if (Target != null && Target.Spawned)
                {
                    pawn.rotationTracker.FaceCell(Target.Position);
                }
            };
            assimilate.WithProgressBarToilDelay(TargetIndex.A);
            yield return assimilate;

            yield return Toils_General.Do(delegate
            {
                pawn.GetComp<CompQueenAssimilation>()?.Assimilate(Target, job.playerForced);
            });
        }

        private int AssimilationDurationTicks()
        {
            float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
            return Mathf.Clamp(Mathf.RoundToInt(180f / Mathf.Max(0.1f, speed)), 60, 600);
        }
    }
}
