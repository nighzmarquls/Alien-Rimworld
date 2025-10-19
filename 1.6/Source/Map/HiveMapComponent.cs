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

        public HiveMapComponent(Map map) : base(map)
        {
        }

        public override void MapRemoved()
        {
            base.MapRemoved();

            HiveUtility.DeregisterNestMap(map);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
        }

        public override void ExposeData()
        {

        }

        
    }
}
