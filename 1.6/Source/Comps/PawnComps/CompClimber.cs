

using System;
using Verse;
using Verse.AI;
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
        public bool lastClimb => climbStep >= (climbParameters.TraversalLegs?.Count ?? 0);
        int climbStep = 0;
        private int activeClimbJobLoadId = -1;

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

        public TraversalLeg CurrentTraversalLeg => lastClimb ? climbParameters.TraversalLegs?.LastOrDefault() : climbParameters.TraversalLegs[climbStep];
        public IntVec3 EndClimbCell => CurrentTraversalLeg?.end ?? IntVec3.Invalid;
        public IntVec3 StartClimbCell => CurrentTraversalLeg?.start ?? IntVec3.Invalid;
        public bool CurrentLegIsInfiltration => CurrentTraversalLeg?.IsInfiltration ?? false;

        public void MarkClimbToilActive(Job job)
        {
            activeClimbJobLoadId = job?.loadID ?? -1;
        }

        public bool HasActiveClimbToilFor(Job job)
        {
            return job != null && activeClimbJobLoadId >= 0 && activeClimbJobLoadId == job.loadID;
        }

        public void RestoreLoadedClimber(PawnClimber loadedClimber, Job detachedJob)
        {
            pawnClimber = loadedClimber;

            if (activeClimbJobLoadId < 0 && detachedJob != null)
            {
                MarkClimbToilActive(detachedJob);
            }
        }

        public void ClearClimberData()
        {
            climbParameters = new ClimbParameters
            {
                NoWallToClimb = true,
                FinalGoalCell = IntVec3.Invalid,
                FinalGoalTarget = LocalTargetInfo.Invalid,
                TraversalLegs = new List<TraversalLeg>(),
                ClimbStarts = new List<IntVec3>(),
                ClimbEnds = new List<IntVec3>(),
                Tunneling = false
            };

            pawnClimber = null;
            startedClimb = false;
            _finishedClimb = false;
            climbStep = 0;
            activeClimbJobLoadId = -1;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            ClimbParameters parameters = climbParameters;
            Scribe_Values.Look(ref parameters.FinalGoalCell, "xmtClimbFinalGoalCell", IntVec3.Invalid);
            Scribe_TargetInfo.Look(ref parameters.FinalGoalTarget, "xmtClimbFinalGoalTarget");
            Scribe_Values.Look(ref parameters.NoWallToClimb, "xmtClimbNoWall", true);
            Scribe_Values.Look(ref parameters.Tunneling, "xmtClimbTunneling", false);
            Scribe_Collections.Look(ref parameters.TraversalLegs, "xmtTraversalLegs", LookMode.Deep);
            Scribe_Collections.Look(ref parameters.ClimbStarts, "xmtClimbStarts", LookMode.Value);
            Scribe_Collections.Look(ref parameters.ClimbEnds, "xmtClimbEnds", LookMode.Value);
            climbParameters = parameters;

            Scribe_References.Look(ref pawnClimber, "xmtPawnClimber");
            Scribe_Values.Look(ref startedClimb, "xmtClimbStarted", false);
            Scribe_Values.Look(ref _finishedClimb, "xmtClimbFinished", false);
            Scribe_Values.Look(ref climbStep, "xmtClimbStep", 0);
            Scribe_Values.Look(ref activeClimbJobLoadId, "xmtClimbJobLoadId", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                climbParameters.ClimbStarts ??= new List<IntVec3>();
                climbParameters.ClimbEnds ??= new List<IntVec3>();
                climbParameters.TraversalLegs ??= new List<TraversalLeg>();

                if (climbParameters.ClimbStarts.Count != climbParameters.ClimbEnds.Count)
                {
                    climbParameters.ClimbStarts.Clear();
                    climbParameters.ClimbEnds.Clear();
                    climbStep = 0;
                    startedClimb = false;
                    _finishedClimb = false;
                }

                if (climbParameters.TraversalLegs.Count == 0 && climbParameters.ClimbStarts.Count == climbParameters.ClimbEnds.Count)
                {
                    for (int i = 0; i < climbParameters.ClimbStarts.Count; i++)
                    {
                        climbParameters.TraversalLegs.Add(TraversalLeg.WallClimb(climbParameters.ClimbStarts[i], climbParameters.ClimbEnds[i]));
                    }
                }

                climbParameters.TraversalLegs.RemoveAll(leg => leg == null || !leg.start.IsValid || !leg.end.IsValid);
                if (climbParameters.TraversalLegs.Count > 0)
                {
                    climbStep = Math.Max(0, Math.Min(climbStep, climbParameters.TraversalLegs.Count));
                }
            }
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
