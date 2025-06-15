using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobDriver_Metamorphosis : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Do(delegate
            {
                CompMatureMorph matureMorph = pawn.GetComp<CompMatureMorph>();
                if (matureMorph != null)
                {
                    matureMorph.TryMetamorphosis();
                }
                ReadyForNextToil();
            });
        }
    }
}
