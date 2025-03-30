using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompSpreadTerrain : ThingComp
    {
        CompSpreadTerrainProperties Props => props as CompSpreadTerrainProperties;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
            {
                return;
            }
            if(Props.spreadTerrain == null)
            {
                return;
            }

            foreach (IntVec3 item in GenRadial.RadialCellsAround(parent.Position, Props.radius, useCenter: true))
            {
                if (!item.InBounds(parent.Map))
                {
                    continue;
                }

                if(parent.Map.terrainGrid.TerrainAt(item) != ExternalDefOf.EmptySpace)
                {
                    parent.Map.terrainGrid.SetTerrain(item,Props.spreadTerrain);
                }
            }
        }
    }

    public class CompSpreadTerrainProperties : CompProperties
    {
        public TerrainDef spreadTerrain = null;

        public float radius = 1;

        public CompSpreadTerrainProperties()
        {
            this.compClass = typeof(CompSpreadTerrain);
        }

        public CompSpreadTerrainProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
