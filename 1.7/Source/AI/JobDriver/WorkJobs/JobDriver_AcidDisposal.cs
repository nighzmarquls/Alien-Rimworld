using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{

    public class JobDriver_AcidDisposal : JobDriver
    {
        private CompAcidPit Pit => job.targetB.Thing?.TryGetComp<CompAcidPit>();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, job.count, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => Pit == null || !Pit.AcceptingMaterial || Pit.parent.Faction != pawn.Faction);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, subtractNumTakenFromJobCount: true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried == null || !pawn.CanReachImmediate(job.targetB, PathEndMode.Touch) || !Pit.Insert(carried))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.needs?.joy?.GainJoy(0.05f, InternalDefOf.NestTending);
            });
        }
    }
}
