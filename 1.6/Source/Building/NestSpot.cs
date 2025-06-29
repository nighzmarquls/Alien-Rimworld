using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using UnityEngine;

namespace Xenomorphtype
{
    public class NestSpot : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if(map == null)
            {
                return;
            }

            IEnumerable<NestSpot> NestSpots = map.listerBuildings.allBuildingsNonColonist.OfType<NestSpot>().Concat(map.listerBuildings.AllBuildingsColonistOfClass<NestSpot>());

            if (NestSpots != null && NestSpots.Count() > 1)
            {
                List<NestSpot> oldNestSpots = NestSpots.ToList();

                if (oldNestSpots.Count > 1)
                {
                    for (int i = 0; i < oldNestSpots.Count - 1; i++)
                    {
                        oldNestSpots[0].DeSpawn();
                    }
                }

                //Log.Message(this + " has reset nest spot to " + Position);
            }
            HiveUtility.ForceNestPosition(this.Position, map);

        }
    }
}
