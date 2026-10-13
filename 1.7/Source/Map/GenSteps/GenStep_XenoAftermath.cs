
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    internal class GenStep_XenoAftermath : GenStep
    {
        private float damageFrequency = 0.3f;

        private float damageThreshold = 0.2f;

        private float burnTerrainThreshold = 0.1f;

        private float aftermathFalloffRadius = -1f;

        private int noiseOctaves = 6;

        private float bloodFrequency = 0.3f;

        private float bloodThreshold = 0.2f;
        public override int SeedPart => 9872136;

        public override void Generate(Map map, GenStepParams parms)
        {
            Perlin perlin = new Perlin(damageFrequency, 2.0, 0.5, noiseOctaves, normalized:true , invert:true, seed:Rand.Int, quality: QualityMode.Medium);
            Perlin perlin2 = new Perlin(bloodFrequency, 2.0, 0.5, noiseOctaves, Rand.Int, QualityMode.Medium);
            MapGenFloatGrid caves = MapGenerator.Caves;
            foreach (IntVec3 allCell in map.AllCells)
            {
                if (map.generatorDef.isUnderground && caves[allCell] <= 0f)
                {
                    continue;
                }

                Building edifice = allCell.GetEdifice(map);
                if (edifice == null || (!edifice.def.building.isNaturalRock))
                {
                    float damagePerlin = (float)perlin.GetValue(allCell.x, 0.0, allCell.z);

                    float bloodPerlin = (float)perlin2.GetValue(allCell.x, 0.0, allCell.z);
                    if (aftermathFalloffRadius > 0f)
                    {
                        float distance = allCell.DistanceTo(map.Center);
                        float adjustment = 1f - Mathf.Clamp01((distance - aftermathFalloffRadius) / ((float)map.Size.x / 2f - aftermathFalloffRadius));
                        damagePerlin *= adjustment;
                        bloodPerlin  *= adjustment;
                    }

                    if (damagePerlin >= damageThreshold)
                    {
                        if (edifice != null)
                        {
                            edifice.Destroy();
                            FilthMaker.TryMakeFilth(allCell, map, ThingDefOf.Filth_RubbleBuilding);
                        }

                        if(map.terrainGrid.CanRemoveTopLayerAt(allCell))
                        {
                            map.terrainGrid.RemoveTopLayer(allCell);
                        }

                        if(map.terrainGrid.CanRemoveFoundationAt(allCell))
                        {
                            map.terrainGrid.RemoveFoundation(allCell);
                        }
                    }

                    if (damagePerlin >= burnTerrainThreshold)
                    {
                        if (damagePerlin > (burnTerrainThreshold + 0.025f))
                        {
                            map.terrainGrid.SetTerrain(allCell, Rand.Bool ? InternalDefOf.MediumAcidBurned : InternalDefOf.AcidBurned);
                        }
                        else
                        {
                            map.terrainGrid.SetTerrain(allCell,InternalDefOf.LightAcidBurned);
                        }
                    }

                   
                    if (bloodPerlin > bloodThreshold)
                    {
                        ThingDef filthDef = (Rand.Bool ? InternalDefOf.Starbeast_Filth_Resin : ThingDefOf.Filth_Blood);
                        FilthMaker.TryMakeFilth(allCell, map, filthDef);
                    }
                }
            }
        }
    }
}
