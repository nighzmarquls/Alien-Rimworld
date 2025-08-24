using RimWorld;
using System.Collections.Generic;
using Verse.AI;
using Verse;


namespace Xenomorphtype
{
    public class JobDriver_ClimbToPosition : JobDriver
    {
        protected virtual IntVec3 FinalGoalCell => TargetA.Cell;
       
        
       
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
           return true;
        }

       
        protected override IEnumerable<Toil> MakeNewToils()
        {

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.ClosestTouch);
    
        }
    }
}
