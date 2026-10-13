

using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_PheromonePump :HediffComp
    {
        internal HediffCompProperties_PheromonePump Props => props as HediffCompProperties_PheromonePump;
        internal PheromoneProductionType ProductionTypes
        {
            get
            {
                PheromoneProductionType types = PheromoneProductionType.None;
                if (Props == null)
                {
                    return types;
                }
                if (Props.friendPheromone > 0f) types |= PheromoneProductionType.Aggregation;
                if (Props.loverPheromone > 0f) types |= PheromoneProductionType.Reproductive;
                if (Props.threatPheromone > 0f) types |= PheromoneProductionType.Alarm;
                return types;
            }
        }
        int lastGeneCount = -1;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastGeneCount, "lastGeneCount", 0);
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (Pawn.IsHashIntervalTick(Props.pumpInterval))
            {
                PumpPheromones();
            }
        }

        protected void PumpPheromones()
        {
            Pawn.Info().ApplyFriendlyPheromone(Pawn, Props.friendPheromone);
            Pawn.Info().ApplyThreatPheromone(Pawn, Props.threatPheromone);
            Pawn.Info().ApplyLoverPheromone(Pawn, Props.loverPheromone);
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            PumpPheromones();
        }
    }
    public class HediffCompProperties_PheromonePump : HediffCompProperties
    {
        public float friendPheromone = 0;
        public float threatPheromone = 0;
        public float loverPheromone = 0;

        public int pumpInterval = 2500;

        public HediffCompProperties_PheromonePump()
        {
            compClass = typeof(HediffComp_PheromonePump);
        }
    }
}
