using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class HiveMapComponent : MapComponent
    {
        bool Uninitialized = true;
        public HiveMapComponent(Map map) : base(map)
        {
            
        }

        public override void MapRemoved()
        {
            base.MapRemoved();

            XMTHiveUtility.DeregisterNestMap(map);
        }
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if(Uninitialized)
            {
                Initialize();
            }
        }


        protected void Initialize()
        {
            foreach (Room room in map.regionGrid.AllRooms)
            {
                XMTHiveUtility.NotifyHiveRoomCompleted(room);
            }
            Uninitialized = false;
        }
        public override void ExposeData()
        {

        }

        
    }
}
