using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    // Save migration for explicitly complete anatomy that formerly inherited .001 severity.
    public class HediffComp_CompleteAnatomy : HediffComp
    {
        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit && parent.Severity < 1f) parent.Severity = 1f;
        }
    }
    public class HediffCompProperties_CompleteAnatomy : HediffCompProperties
    {
        public HediffCompProperties_CompleteAnatomy() { compClass = typeof(HediffComp_CompleteAnatomy); }
    }
}
