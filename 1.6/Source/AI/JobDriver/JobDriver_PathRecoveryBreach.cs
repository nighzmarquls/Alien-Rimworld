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
        private IntVec3 GoalCell => job.GetTarget(TargetIndex.C).Cell;

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
            bool breachedByJob = false;
            IntVec3 passageDestination = IntVec3.Invalid;

            AddFailCondition(() => !breachedByJob && (Blocker == null || Blocker.Destroyed || !MatureMorphPathRecovery.IsPathRecoveryBreachCandidate(pawn, Blocker as Building, out IntVec3 _, requireAvailability: false)));
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
                CellRect blockerRect = blocker.OccupiedRect();
                XMT_SabotageReplacementPair replacement = null;
                bool hasReplacement = XMTSabotageReplacementUtility.TryGetReplacement(blocker.def, out replacement);

                blocker.Destroy(DestroyMode.Deconstruct);

                if (hasReplacement && replacement.replacementFloorThing != null)
                {
                    GenSpawn.Spawn(replacement.replacementFloorThing, breachCell, map, WipeMode.VanishOrMoveAside);
                    map.terrainGrid.SetTerrain(breachCell, InternalDefOf.HiveFloor);
                }
                else if (InternalDefOf.HiveFloor != null && breachCell.InBounds(map) && !PathRecoveryJobUtility.IsUnsafeBreachDestination(map, breachCell))
                {
                    map.terrainGrid.SetTerrain(breachCell, InternalDefOf.HiveFloor);
                }

                breachedByJob = true;
                pawn.GetMorphComp()?.ClearPathRecovery();
                PathRecoveryJobUtility.TryFindPassageDestination(pawn, blockerRect, InteractionCell, requireSafeExit: false, goalCell: GoalCell, out passageDestination);
            });

            yield return PathRecoveryJobUtility.GotoCellIfValid(() => passageDestination);
        }
    }
}
