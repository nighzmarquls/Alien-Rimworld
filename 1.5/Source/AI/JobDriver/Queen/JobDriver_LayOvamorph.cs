using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_LayOvamorph : JobDriver
    {
        float workDone;
        CompOvamorphLayer ovamorphLayer;
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
            yield return LayOvamorph();
        }

        
        private Toil LayOvamorph()
        {
            ovamorphLayer = pawn.GetComp<CompOvamorphLayer>();
            Toil toil = ToilMaker.MakeToil("LayOvamorph");
            toil.atomicWithPrevious = true;
            toil.handlingFacing = true;
            toil.initAction = delegate
            {
                pawn.Rotation = ovamorphLayer.GetLayingFacing(TargetA.Cell);
            };
            toil.tickAction = delegate
            {

                pawn.Rotation = ovamorphLayer.GetLayingFacing(TargetA.Cell);
                workDone += 0.0017f;
                if (workDone > 1)
                {
                    GeneOvamorph laidGenes = ovamorphLayer.LayOvamorph(TargetA.Cell) as GeneOvamorph;
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
