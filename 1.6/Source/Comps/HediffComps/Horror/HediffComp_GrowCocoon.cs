
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_GrowCocoon : HediffComp
    {
        HediffCompProperties_GrowCocoon Props => props as HediffCompProperties_GrowCocoon;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if(!Pawn.Spawned)
            {
                return;
            }

            if(Pawn.IsHashIntervalTick(Props.intervalCheck))
            {
                if(Pawn.InBed())
                {
                    return;
                }


                List<Thing> things = Pawn.PositionHeld.GetThingList(Pawn.MapHeld);

                foreach (Thing thing in things)
                {
                    if(thing is CocoonBase)
                    {
                        return;
                    }
                }

                TryGrowNewBed();
            }
        }

        protected void TryGrowNewBed()
        {
            if(Pawn.Map == null)
            {
                return;
            }

            IntVec3 cell = Pawn.Position;

            if(cell.GetAffordances(Pawn.Map).Contains(Props.activateOnAffordance))
            {
               CocoonBase cocoon = HiveUtility.TryPlaceCocoonBase(cell, Pawn) as CocoonBase;
                if (cocoon != null)
                {
                    Pawn.jobs.Notify_TuckedIntoBed(cocoon);
                }
            }

        }
    }

    public class HediffCompProperties_GrowCocoon : HediffCompProperties
    {
        public int intervalCheck = 600;
        public TerrainAffordanceDef activateOnAffordance;
        public HediffCompProperties_GrowCocoon()
        {
            compClass = typeof(HediffComp_GrowCocoon);
        }
    }
}
