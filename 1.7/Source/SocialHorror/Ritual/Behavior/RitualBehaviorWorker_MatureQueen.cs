using RimWorld;
using Verse;


namespace Xenomorphtype
{
    internal class RitualBehaviorWorker_MatureQueen : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_MatureQueen()
        {
        }

        public RitualBehaviorWorker_MatureQueen(RitualBehaviorDef def)
            : base(def)
        {
        }
        public override void Tick(LordJob_Ritual ritual)
        {
            base.Tick(ritual);
            if(ritual.Progress > 0.25f)
            {
                FillableChrysalis targetChrysalis = ritual.selectedTarget.Thing as FillableChrysalis;

                if (targetChrysalis != null)
                {
                    Pawn queen = ritual.PawnWithRole("queen");

                    if(queen != null)
                    {
                        JobDriver_Ritual_Metamorphosis ritualJobDriver = queen.jobs.curDriver as JobDriver_Ritual_Metamorphosis;
                        if(ritualJobDriver != null)
                        {
                            
                            ritualJobDriver.DynamicBodyOffset.y = -100;
                        }
                    }
                    targetChrysalis.CloseChrysalis();
                }
            }
        }
    }
}
