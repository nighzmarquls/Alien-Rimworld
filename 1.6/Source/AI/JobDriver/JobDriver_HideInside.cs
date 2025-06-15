using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class JobDriver_HideInside : JobDriver
    {
        float workDone;
        CompMatureMorph pawnMorph;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {

            yield return Toils_Goto.GotoCell(TargetA.Cell, PathEndMode.ClosestTouch);
            yield return HideInside();
        }

        private Toil HideInside()
        {
            pawnMorph = pawn.GetComp<CompMatureMorph>();
            Toil toil = ToilMaker.MakeToil("HideInside");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {

                workDone += 0.017f;
                if (workDone > 1)
                {
                    pawnMorph.EnterHidingSpot(pawn.Position);
                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: false);
            return toil;
        }
    }
}
