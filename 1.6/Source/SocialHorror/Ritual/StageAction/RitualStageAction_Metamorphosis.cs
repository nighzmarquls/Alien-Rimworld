using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class RitualStageAction_Metamorphosis : RitualStage
    {
        public static readonly SimpleCurve ProgressPerParticipantCurve = new SimpleCurve
        {
            new CurvePoint(1f, 1f),
            new CurvePoint(2f, 1f),
            new CurvePoint(4f, 1f),
            new CurvePoint(6f, 1f)
        };

        public override TargetInfo GetSecondFocus(LordJob_Ritual ritual)
        {
            return ritual.selectedTarget;
        }

        public override float ProgressPerTick(LordJob_Ritual ritual)
        {
            int num = 1;
            foreach (Pawn item in ritual.assignments.SpectatorsForReading)
            {
                if (ritual.IsParticipating(item))
                {
                    num++;
                }
            }

            return ProgressPerParticipantCurve.Evaluate(num);
        }
    }
}
