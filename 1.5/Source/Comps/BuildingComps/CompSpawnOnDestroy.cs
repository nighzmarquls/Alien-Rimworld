using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class CompSpawnOnDestroy : ThingComp
    {
        CompSpawnOnDestroyProperties Props => props as CompSpawnOnDestroyProperties;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            IntVec3 SpawnPosition = parent.Position;
            Thing spawnedBuilding = GenSpawn.Spawn(Props.spawnThing,SpawnPosition,previousMap);
            FilthMaker.TryMakeFilth(SpawnPosition, previousMap, InternalDefOf.Starbeast_Filth_Resin);
        }
    }
    public class CompSpawnOnDestroyProperties : CompProperties
    {
        public ThingDef spawnThing;
        public CompSpawnOnDestroyProperties()
        {
            this.compClass = typeof(CompSpawnOnDestroy);
        }
    }
}
