using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_LayOvomorph : JobDriver
    {
        float workDone;
        bool completedFromStock;
        CompOvomorphLayer OvomorphLayer;
        IntVec3 LayingTarget;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            OvomorphLayer = pawn.GetComp<CompOvomorphLayer>();
            if (OvomorphLayer?.HyperFertilityActive == true && TryFindIntegratedLayingTarget(out LayingTarget))
            {
                return true;
            }

            List<IntVec3> adjacent = GenRadial.RadialCellsAround(TargetA.Cell, 1f, false).ToList();
            adjacent.Shuffle();
            foreach (IntVec3 adjacentCell in adjacent)
            {
                if (adjacentCell.Standable(pawn.Map))
                {
                    LayingTarget = adjacentCell;
                    break;
                }
            }
            return true;
        }

        private bool TryFindIntegratedLayingTarget(out IntVec3 layingTarget)
        {
            int preferredDistance = OvomorphLayer.PreferredLayingDistance;
            IntVec3[] directions = { IntVec3.East, IntVec3.West, IntVec3.North, IntVec3.South };

            for (int distance = preferredDistance; distance >= 2; distance--)
            {
                IntVec3 bestCell = IntVec3.Invalid;
                float bestDistance = float.MaxValue;
                foreach (IntVec3 direction in directions)
                {
                    IntVec3 candidate = TargetA.Cell + direction * distance;
                    if (!candidate.InBounds(pawn.Map) || !candidate.Standable(pawn.Map) || !pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))
                    {
                        continue;
                    }

                    float distanceFromPawn = pawn.Position.DistanceToSquared(candidate);
                    if (distanceFromPawn < bestDistance)
                    {
                        bestDistance = distanceFromPawn;
                        bestCell = candidate;
                    }
                }

                if (bestCell.IsValid)
                {
                    layingTarget = bestCell;
                    return true;
                }
            }

            layingTarget = IntVec3.Invalid;
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {

            yield return Toils_Goto.GotoCell(LayingTarget, PathEndMode.ClosestTouch);
            yield return LayOvomorph();
        }

        
        private Toil LayOvomorph()
        {
            OvomorphLayer = pawn.GetComp<CompOvomorphLayer>();
            Toil toil = ToilMaker.MakeToil("LayOvomorph");
            toil.atomicWithPrevious = true;
            toil.handlingFacing = true;
            toil.initAction = delegate
            {
                pawn.Rotation = OvomorphLayer.GetLayingFacing(TargetA.Cell);
                if (OvomorphLayer.HyperFertilityActive && OvomorphLayer.StoredEggs > 0)
                {
                    completedFromStock = true;
                    OvomorphLayer.LayOvomorph(TargetA.Cell);
                    ReadyForNextToil();
                }
            };
            toil.tickAction = delegate
            {
                if (completedFromStock)
                {
                    return;
                }

                pawn.Rotation = OvomorphLayer.GetLayingFacing(TargetA.Cell);
                workDone += 0.0017f;
                if (workDone > 1)
                {
                    OvomorphLayer.LayOvomorph(TargetA.Cell);
                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            return toil;
        }
    }
}
