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
    public class JobDriver_Sabotage : JobDriver_ClimbToPosition
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, pawn.CurJob) && base.TryMakePreToilReservations(errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            if (NoWallToClimb)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else
            {
                yield return ClimbOverWall();
            }
            yield return Toils_General.Do(delegate
            {
                Thing target = pawn.CurJob.targetA.Thing;
                if (target != null)
                {
                    XMTUtility.SabotageThing(target, pawn);
                   

                }
            });
        }
    }
}
