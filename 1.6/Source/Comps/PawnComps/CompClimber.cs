

using System;
using Verse;
using static Xenomorphtype.ClimbUtility;

namespace Xenomorphtype
{
    public class CompClimber : ThingComp
    {
        public ClimbUtility.ClimbParameters climbParameters;
        public PawnClimber pawnClimber = null;
        public bool startedClimb = false;
        public bool finishedClimb = false;

        public bool climbing
        {
            get
            {
                if(pawnClimber == null)
                {
                    return false;
                }

                if(pawnClimber.Spawned)
                {
                    return true;
                }

                return false;
            }
        }
        public void ClearClimberData()
        {
            climbParameters = new ClimbParameters
            {
                StartClimbCell = IntVec3.Invalid,
                EndClimbCell = IntVec3.Invalid,
                NoWallToClimb = true,
                FinalGoalCell = IntVec3.Invalid,
                Tunneling = false
            };

            pawnClimber = null;
            startedClimb = false;
            finishedClimb = false;
        }
    }

    public class CompClimberProperties : CompProperties
    {
        public CompClimberProperties()
        {
            this.compClass = typeof(CompClimber);
        }
        public CompClimberProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
