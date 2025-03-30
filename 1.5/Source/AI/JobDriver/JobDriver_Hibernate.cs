using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_Hibernate : JobDriver
    {
        float workDone;
        CompMatureMorph pawnMorph;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {

            yield return Toils_Goto.GotoCell(TargetA.Cell, PathEndMode.OnCell);
            yield return FormCocoon();
        }

        private Toil FormCocoon()
        {
            pawnMorph = pawn.GetComp<CompMatureMorph>();
            Toil toil = ToilMaker.MakeToil("FormCocoon");
            toil.atomicWithPrevious = true;
            toil.tickAction = delegate
            {

                workDone += 0.0004f;
                if (workDone > 1)
                {

                    pawnMorph.EnterHiberation();
                    ReadyForNextToil();
                }

            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            toil.PlaySustainerOrSound(() => SoundDefOf.Vomit);
            return toil;
        }
    }
}
