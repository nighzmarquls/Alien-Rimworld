using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_BuildHiveRoof : JobDriver
    {
        private const float BaseWorkAmount = 170f;

        private float workLeft = BaseWorkAmount;
        private bool roofCompleted;
        private bool workStarted;

        private IntVec3 RoofCell => job.GetTarget(TargetIndex.A).Cell;

        internal void NotifyCleanup(JobCondition condition)
        {
            if (condition == JobCondition.Incompletable && !workStarted && !roofCompleted)
            {
                pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(RoofCell), job);
                XMTNestBuildingUtility.NotifyHiveBuildJobFailed(pawn, RoofCell, null, NestBuildStage.RoofEnclosedRoom);
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            IntVec3 roofCell = RoofCell;
            if (pawn.MapHeld != null && pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, roofCell))
            {
                return true;
            }

            return FeralJobUtility.ReservePlaceForJob(pawn, job, roofCell);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => XMTNestBuildingUtility.HiveRoofBatchCells(pawn?.Map, RoofCell, pawn).Count == 0);

            if (!pawn.Position.AdjacentTo8WayOrInside(RoofCell))
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            }

            yield return BuildRoofToil();
        }

        private Toil BuildRoofToil()
        {
            Toil toil = ToilMaker.MakeToil("BuildHiveRoof");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                workStarted = true;
                workLeft = BaseWorkAmount;
            };
            toil.tickIntervalAction = delegate(int delta)
            {
                workLeft -= pawn.GetStatValue(StatDefOf.ConstructionSpeedFactor) * delta;
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Construction, 0.085f * delta);
                }

                if (!BioUtility.PerformBioconstructionCost(pawn))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (workLeft <= 0f)
                {
                    CompleteRoof();
                }
            };
            toil.WithProgressBar(TargetIndex.A, () => 1f - (workLeft / BaseWorkAmount));
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private void CompleteRoof()
        {
            if (roofCompleted)
            {
                return;
            }

            roofCompleted = true;
            Map map = pawn?.Map;
            IntVec3 cell = RoofCell;
            List<IntVec3> roofCells = XMTNestBuildingUtility.HiveRoofBatchCells(map, cell, pawn);
            if (roofCells.Count == 0)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            foreach (IntVec3 roofCell in roofCells)
            {
                map.roofGrid.SetRoof(roofCell, RoofDefOf.RoofConstructed);
                // TODO: Replace this with a hive-specific temporary roof texture for added verisimilitude.
                MoteMaker.PlaceTempRoof(roofCell, map);
            }

            XMTNestBuildingUtility.NotifyHiveRoofCompleted(map, cell);
            XMTNestBuildingUtility.NotifyHiveBuildJobSucceeded(pawn, cell, null, NestBuildStage.RoofEnclosedRoom);
            EndJobWith(JobCondition.Succeeded);
        }
    }
}
