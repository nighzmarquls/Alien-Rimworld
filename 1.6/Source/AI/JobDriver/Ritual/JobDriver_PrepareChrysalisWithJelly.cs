
using RimWorld;
using Verse;
using Verse.AI;
using System.Linq;

namespace Xenomorphtype
{
    internal class JobDriver_PrepareChrysalisWithJelly : JobDriver_GotoAndStandSociallyActive
    {
        public override Toil StandToil
        {
            get
            {
                Toil toil = base.StandToil.WithEffect(InternalDefOf.ResinBuild, () => pawn.Position + pawn.Rotation.FacingCell);
                toil.AddPreInitAction(delegate
                {
                    Thing thing =  pawn.inventory.innerContainer.FirstOrDefault((Thing t) => t.def == job.thingDefToCarry && t.stackCount >= job.count);
                    if (thing != null)
                    {
                        pawn.carryTracker.TryStartCarry(thing, job.count);
                    }
                });
                return toil;
            }
        }
    }
}
