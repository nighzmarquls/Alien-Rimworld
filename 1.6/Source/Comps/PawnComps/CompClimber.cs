

using System;
using Verse;
using System.Collections.Generic;
using static Xenomorphtype.ClimbUtility;
using System.Linq;

namespace Xenomorphtype
{
    public class CompClimber : ThingComp
    {
        public ClimbUtility.ClimbParameters climbParameters;
        public PawnClimber pawnClimber = null;
        public bool startedClimb = false;
        bool _finishedClimb = false;
        public bool finishedClimb
        {
            get { return _finishedClimb; }
            set { 
                _finishedClimb = value;
                if (value)
                {
                    climbStep++;
                }
            }
        }
        public bool lastClimb => climbStep == climbParameters.ClimbStarts.Count;
        int climbStep = 0;

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

        public IntVec3 EndClimbCell => lastClimb ? climbParameters.ClimbEnds.Last() : climbParameters.ClimbEnds[climbStep];
        public IntVec3 StartClimbCell => lastClimb ? climbParameters.ClimbStarts.Last() : climbParameters.ClimbStarts[climbStep];

        public void ClearClimberData()
        {
            climbParameters = new ClimbParameters
            {
                NoWallToClimb = true,
                FinalGoalCell = IntVec3.Invalid,
                ClimbStarts = new List<IntVec3>(),
                ClimbEnds = new List<IntVec3>(),
                Tunneling = false
            };

            pawnClimber = null;
            startedClimb = false;
            _finishedClimb = false;
            climbStep = 0;
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
