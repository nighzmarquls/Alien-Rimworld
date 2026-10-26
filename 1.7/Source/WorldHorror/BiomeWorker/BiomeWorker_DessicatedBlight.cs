
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype {
    internal class BiomeWorker_DessicatedBlight : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
        {
            return XenoformingUtility.XenoformingBiomeScore(biome, tile);
        }
    }
}
