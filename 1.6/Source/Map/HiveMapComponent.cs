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
            /*
            Scribe_Collections.Look(ref InfiltrationEntries, "InfiltrationEntries", LookMode.Reference);
            Scribe_Collections.Look(ref Vents, "Vents", LookMode.Reference);
            Scribe_Collections.Look(ref VEPipeEntries, "VEPipeEntries", LookMode.Reference);
            Scribe_Collections.Look(ref DubPipeEntries, "DubPipeEntries", LookMode.Reference);
            Scribe_Values.Look(ref lastNonColonistBuildings, "lastNonColonistBuildings", 0);
            Scribe_Values.Look(ref lastColonistBuildings, "lastColonistBuildings", 0);
            */
        }

        
    }
}
