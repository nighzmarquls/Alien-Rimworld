using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class CompSterilizingDoorwayCleanliness : ThingComp
    {
        private CompPowerTrader powerTrader;
        private CompRefuelable refuelable;
        private bool active;

        private CompProperties_SterilizingDoorwayCleanliness Props => (CompProperties_SterilizingDoorwayCleanliness)props;
        private CompPowerTrader PowerTrader => powerTrader ??= parent.GetComp<CompPowerTrader>();
        private CompRefuelable Refuelable => refuelable ??= parent.GetComp<CompRefuelable>();
        private bool ActiveNow => PowerTrader?.PowerOn == true && Refuelable?.HasFuel == true;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            active = ActiveNow;
        }

        public override void CompTick()
        {
            base.CompTick();

            bool activeNow = ActiveNow;
            if (activeNow == active)
            {
                return;
            }

            active = activeNow;
            StatDefOf.Cleanliness.Worker.ClearCacheForThing(parent);
            NotifyAdjacentRoomsChanged();
        }

        public override float GetStatOffset(StatDef stat)
        {
            float offset = base.GetStatOffset(stat);
            if (stat == StatDefOf.Cleanliness && ActiveNow)
            {
                offset += Props.cleanlinessOffset;
            }
            return offset;
        }

        private void NotifyAdjacentRoomsChanged()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }

            HashSet<Room> notifiedRooms = new HashSet<Room>();
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(parent))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                Room room = cell.GetRoom(map);
                if (room != null && notifiedRooms.Add(room))
                {
                    room.Notify_BedTypeChanged();
                }
            }
        }
    }

    public class CompProperties_SterilizingDoorwayCleanliness : CompProperties
    {
        public float cleanlinessOffset = 4f;

        public CompProperties_SterilizingDoorwayCleanliness()
        {
            compClass = typeof(CompSterilizingDoorwayCleanliness);
        }
    }
}
