
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype {
    internal class BiomeWorker_DessicatedBlight : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
        {
            if (tile.WaterCovered)
            {
                return -100f;
            }

            if(!XenoformingUtility.XenoformingMeets(10))
            {
                return 0f;
            }
            float baseScore = XenoformingUtility.ChanceByXenoforming(1) + (tile.temperature * 2.7f);

            if(Find.TickManager.TicksGame <= 0)
            {
                baseScore *= (XenoformingUtility.GetXenoforming()/100);
            }

            return baseScore;
        }
    }
}
