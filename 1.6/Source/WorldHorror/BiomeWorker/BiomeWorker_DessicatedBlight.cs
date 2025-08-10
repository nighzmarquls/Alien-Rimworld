
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

            if (tile.rainfall >= 340f)
            {
                return 0f;
            }

            if(!XenoformingUtility.XenoformingMeets(15))
            {
                return 0f;
            }

            return XenoformingUtility.ChanceByXenoforming(1) * ( tile.temperature * 2.7f - 13f - tile.rainfall * 0.14f);
        }
    }
}
