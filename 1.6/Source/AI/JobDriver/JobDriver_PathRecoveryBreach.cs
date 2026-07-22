using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_PathRecoveryBreach : JobDriver
    {
        private float breachWorkRequired = 1f;
        private float breachWorkLeft = 1f;
        private bool breachWorkInitialized;

        private Thing Blocker => TargetThingA;
        private IntVec3 InteractionCell => job.GetTarget(TargetIndex.B).Cell;
        private IntVec3 GoalCell => job.GetTarget(TargetIndex.C).Cell;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref breachWorkRequired, "breachWorkRequired", 1f);
            Scribe_Values.Look(ref breachWorkLeft, "breachWorkLeft", 1f);
            Scribe_Values.Look(ref breachWorkInitialized, "breachWorkInitialized", false);
        }

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

            yield return BreachEdifice();

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

        private Toil BreachEdifice()
        {
            Toil toil = ToilMaker.MakeToil("BreachEdifice");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                breachWorkRequired = Mathf.Max(1f, Blocker?.HitPoints ?? 1);
                breachWorkLeft = breachWorkRequired;
                breachWorkInitialized = true;
            };
            toil.tickIntervalAction = delegate(int delta)
            {
                float constructionSpeed = Mathf.Max(0.1f, pawn.GetStatValue(StatDefOf.ConstructionSpeed));
                breachWorkLeft -= constructionSpeed * delta;
                if (breachWorkLeft <= 0f)
                {
                    ReadyForNextToil();
                }
            };
            toil.WithProgressBar(TargetIndex.A, () => breachWorkInitialized
                ? Mathf.Clamp01(1f - breachWorkLeft / breachWorkRequired)
                : 0f);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
