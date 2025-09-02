
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_NoHeal : HediffComp
    {
        float lastSeverity = 0;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastSeverity, "lastSeverity", 0);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if(parent.Severity < lastSeverity)
            {
                parent.Severity = lastSeverity;
            }
            else
            {
                lastSeverity = parent.Severity;
            }
        }
    }
}
