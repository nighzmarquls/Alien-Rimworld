using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class JobDriver_SubvertTurret : JobDriver
    {
        private Building_TurretGun Turret => job.GetTarget(TargetIndex.A).Thing as Building_TurretGun;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            return "XMT_SubvertTurretReport".Translate();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !XMT_IFFUtility.IsValidSubverterTurretTarget(pawn, Turret, pawn.MapHeld));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            Toil subvert = ToilMaker.MakeToil("SubvertTurret");
            subvert.initAction = delegate
            {
                Building_TurretGun turret = Turret;
                if (!XMT_IFFUtility.TrySubvertTurret(pawn, turret))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                NemesisMissionUtility.NotifyTurretSubverted(pawn, turret);
                pawn.Destroy(DestroyMode.Vanish);
            };
            subvert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return subvert;
        }
    }
}
