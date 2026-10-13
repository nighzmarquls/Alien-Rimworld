using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class IdeologyResinArtFrame : Building
    {
        public ThingDef TargetThingDef;
        public ThingDef StuffDef;
        public ThingStyleDef TargetStyleDef;

        public override string Label
        {
            get
            {
                if (TargetThingDef != null)
                {
                    return "unfinished " + TargetThingDef.label;
                }

                return base.Label;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref TargetThingDef, "targetThingDef");
            Scribe_Defs.Look(ref StuffDef, "stuffDef");
            Scribe_Defs.Look(ref TargetStyleDef, "styleDef");
        }
    }
}
