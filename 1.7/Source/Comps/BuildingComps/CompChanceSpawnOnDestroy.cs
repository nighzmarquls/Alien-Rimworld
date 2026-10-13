using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Xenomorphtype;

namespace Xenomorphtype
{
    public class ThingSpawnChance
    {
        public ThingDef spawnThing;
        public float probability = 0.01f;
        public float minXenoforming = 0;
    }
    public class CompChanceSpawnOnDestroy : ThingComp
    {
        CompChanceSpawnOnDestroyProperties Props => props as CompChanceSpawnOnDestroyProperties;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            List<ThingSpawnChance> things = Props.thingSpawns;

            if(things.Count == 1)
            {
                if (Rand.Chance(things[0].probability) && XenoformingUtility.XenoformingMeets(things[0].minXenoforming))
                {
                    Thing spawnedThing = GenSpawn.Spawn(things[0].spawnThing, parent.Position, previousMap, parent.Rotation);
                    if(spawnedThing is Ovomorph spawnedOvomorph)
                    {
                        spawnedOvomorph.ForceProgress();
                    }
                    FilthMaker.TryMakeFilth(parent.Position, previousMap, InternalDefOf.Starbeast_Filth_Resin);
                }
                return;
            }

            List<IntVec3> spawnpoints = GenRadial.RadialCellsAround(parent.Position, GenRadial.RadiusOfNumCells(things.Count), true).ToList();
            for (int i = 0; i < spawnpoints.Count; i++)
            {
                if (i < spawnpoints.Count - 1)
                {
                    if (Rand.Chance(things[i].probability) && XenoformingUtility.XenoformingMeets(things[i].minXenoforming))
                    {
                        IntVec3 SpawnPosition = spawnpoints[i];
                        Thing spawnedThing = GenSpawn.Spawn(things[i].spawnThing, SpawnPosition, previousMap, parent.Rotation);
                        if (spawnedThing is Ovomorph spawnedOvomorph)
                        {
                            spawnedOvomorph.ForceProgress();
                        }
                        FilthMaker.TryMakeFilth(SpawnPosition, previousMap, InternalDefOf.Starbeast_Filth_Resin);
                    }
                }
            }
        }
    }
    public class CompChanceSpawnOnDestroyProperties : CompProperties
    {
        public List<ThingSpawnChance> thingSpawns;
        public CompChanceSpawnOnDestroyProperties()
        {
            this.compClass = typeof(CompChanceSpawnOnDestroy);
        }
    }
}
