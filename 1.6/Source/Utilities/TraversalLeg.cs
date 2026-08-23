using Verse;

namespace Xenomorphtype
{
    public enum TraversalLegType : byte
    {
        WallClimb,
        Infiltration
    }

    public class TraversalLeg : IExposable
    {
        public TraversalLegType type;
        public IntVec3 start = IntVec3.Invalid;
        public IntVec3 end = IntVec3.Invalid;
        public string providerKey;
        public string categoryKey;
        public int entryThingId = -1;
        public int exitThingId = -1;

        public bool IsInfiltration => type == TraversalLegType.Infiltration;

        public TraversalLeg()
        {
        }

        public static TraversalLeg WallClimb(IntVec3 start, IntVec3 end)
        {
            return new TraversalLeg
            {
                type = TraversalLegType.WallClimb,
                start = start,
                end = end
            };
        }

        internal static TraversalLeg Infiltration(IntVec3 start, IntVec3 end, string providerKey, string categoryKey,
            int entryThingId, int exitThingId)
        {
            return new TraversalLeg
            {
                type = TraversalLegType.Infiltration,
                start = start,
                end = end,
                providerKey = providerKey,
                categoryKey = categoryKey,
                entryThingId = entryThingId,
                exitThingId = exitThingId
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type", TraversalLegType.WallClimb);
            Scribe_Values.Look(ref start, "start", IntVec3.Invalid);
            Scribe_Values.Look(ref end, "end", IntVec3.Invalid);
            Scribe_Values.Look(ref providerKey, "providerKey");
            Scribe_Values.Look(ref categoryKey, "categoryKey");
            Scribe_Values.Look(ref entryThingId, "entryThingId", -1);
            Scribe_Values.Look(ref exitThingId, "exitThingId", -1);
        }

        public override string ToString()
        {
            return type + " " + start + " -> " + end +
                (IsInfiltration ? " [" + providerKey + "/" + categoryKey + ", " + entryThingId + " -> " + exitThingId + "]" : string.Empty);
        }
    }
}
