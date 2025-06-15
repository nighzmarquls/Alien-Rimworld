using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class CompInfiltrationPoint : ThingComp
    {
        CompInfiltrationPointProperties Props => props as CompInfiltrationPointProperties;

        public override void CompTick()
        {
            base.CompTick();
        }
    
        public override void PostExposeData()
        {
            base.PostExposeData();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            Log.Message(parent + " made with infiltration point");
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            Log.Message(parent + " destroyed with infiltration point");
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            Log.Message(parent + " despawned with infiltration point");
        }


        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            Log.Message(parent + " spawned with infiltration point");
        }
    }

    public class CompInfiltrationPointProperties : CompProperties
    {
        public CompInfiltrationPointProperties()
        {
            this.compClass = typeof(CompInfiltrationPoint);
        }

    }
}
