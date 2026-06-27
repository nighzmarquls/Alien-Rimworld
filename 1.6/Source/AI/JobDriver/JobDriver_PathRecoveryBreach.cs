using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_PathRecoveryBreach : JobDriver
    {
        private const int BreachTicks = 220;

        private Thing Blocker => TargetThingA;
        private IntVec3 InteractionCell => job.GetTarget(TargetIndex.B).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Blocker == null || !InteractionCell.IsValid || !FeralJobUtility.IsThingAvailableForJobBy(pawn, Blocker))
            {
                return false;
            }

            if (!FeralJobUtility.ReservePlaceForJob(pawn, job, InteractionCell))
            {
                return false;
            }

            FeralJobUtility.ReserveThingForJob(pawn, job, Blocker);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFailCondition(() => !CompMatureMorph.IsPathRecoveryBreachCandidate(pawn, Blocker as Building, out IntVec3 _, requireAvailability: false));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            Toil breach = Toils_General.Wait(BreachTicks);
            breach.WithProgressBarToilDelay(TargetIndex.A);
            breach.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            yield return breach;

            yield return Toils_General.Do(delegate
            {
                Building blocker = Blocker as Building;
                Map map = pawn.Map;
                if (blocker == null || map == null || !blocker.Spawned || blocker.Map != map)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                IntVec3 breachCell = blocker.Position;
                XMT_BreachReplacementPair replacement = null;
                bool hasReplacement = XMTBreachReplacementUtility.TryGetReplacement(blocker.def, out replacement);

                blocker.Destroy(DestroyMode.Deconstruct);

                if (hasReplacement && replacement.replacementFloorThing != null)
                {
                    GenSpawn.Spawn(replacement.replacementFloorThing, breachCell, map, WipeMode.VanishOrMoveAside);
                }

                ThingDef passageDef = hasReplacement && replacement.replacementThing != null
                    ? replacement.replacementThing
                    : XenoBuildingDefOf.HiveWebbing;

                if (passageDef != null)
                {
                    GenSpawn.Spawn(passageDef, breachCell, map, WipeMode.VanishOrMoveAside);
                }

                pawn.GetMorphComp()?.ClearPathRecovery();
                EndJobWith(JobCondition.Succeeded);
            });
        }
    }
}
