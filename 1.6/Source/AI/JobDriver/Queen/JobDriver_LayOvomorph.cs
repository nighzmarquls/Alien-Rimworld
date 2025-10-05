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
        CompOvomorphLayer OvomorphLayer;
        IntVec3 LayingTarget;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
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
            };
            toil.tickAction = delegate
            {

                pawn.Rotation = OvomorphLayer.GetLayingFacing(TargetA.Cell);
                workDone += 0.0017f;
                if (workDone > 1)
                {
                    GeneOvomorph laidGenes = OvomorphLayer.LayOvomorph(TargetA.Cell) as GeneOvomorph;
                    if (laidGenes != null)
                    {
                        Find.WindowStack.Add(new Dialogue_GeneExpression(laidGenes));
                    }
                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            return toil;
        }
    }
}
