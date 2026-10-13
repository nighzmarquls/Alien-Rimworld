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
        struct TerrainSpreadingFrame
        {
            public CompSpreadTerrainProperties properties;
            public Frame Frame;
        }
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
                TerrainDef terrainAt = parent.Map.terrainGrid.TerrainAt(item);
                List<Thing> things = parent.Position.GetThingList(parent.Map);
                List<TerrainSpreadingFrame> terrainSpreaders = new List<TerrainSpreadingFrame>();
                foreach(Thing thing in things)
                {
                    if(thing is Frame frame)
                    {
                        if (frame.BuildDef != null)
                        {
                            foreach (CompProperties comp in frame.BuildDef.comps)
                            {
                                if(comp is CompSpreadTerrainProperties terrainSpreader)
                                {
                                    TerrainSpreadingFrame newSpreader = new() { Frame = frame, properties = terrainSpreader};
                                    terrainSpreaders.Add(newSpreader);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (terrainAt != ExternalDefOf.EmptySpace )
                {
                    if(Props.upgradeTerrain != null)
                    {
                        if(terrainAt == Props.spreadTerrain)
                        {
                            foreach (TerrainSpreadingFrame spreader in terrainSpreaders)
                            {
                                if (spreader.properties.radius > 0)
                                {
                                    continue;
                                }
                                if (spreader.properties.spreadTerrain == Props.upgradeTerrain || spreader.properties.spreadTerrain == Props.spreadTerrain)
                                {
                                    spreader.Frame.Destroy();
                                }
                            }
                            parent.Map.terrainGrid.SetTerrain(item, Props.upgradeTerrain);
                        }
                        else if(terrainAt != Props.upgradeTerrain)
                        {
                            parent.Map.terrainGrid.SetTerrain(item, Props.spreadTerrain);
                            foreach (TerrainSpreadingFrame spreader in terrainSpreaders)
                            {
                                if (spreader.properties.radius > 0)
                                {
                                    continue;
                                }
                                if (spreader.properties.spreadTerrain == Props.spreadTerrain)
                                {
                                    spreader.Frame.Destroy();
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach(TerrainSpreadingFrame spreader in terrainSpreaders)
                        {
                            if(spreader.properties.radius > 0)
                            {
                                continue;
                            }
                            if (spreader.properties.spreadTerrain == Props.spreadTerrain)
                            {
                                spreader.Frame.Destroy();
                            }
                        }
                        parent.Map.terrainGrid.SetTerrain(item, Props.spreadTerrain);
                    }
                    
                }
            }
        }
    }

    public class CompSpreadTerrainProperties : CompProperties
    {
        public TerrainDef spreadTerrain = null;
        public TerrainDef upgradeTerrain = null;

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
