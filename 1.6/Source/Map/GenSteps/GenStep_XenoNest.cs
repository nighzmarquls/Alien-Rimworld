
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Noise;


namespace Xenomorphtype
{
    internal class GenStep_XenoNest : GenStep
    {
        private float nestFrequency = 0.3f;

        private float nestThreshold = 0.2f;

        private int eggCount = 1;

        private int hostCount = 0;

        private int gaurdians = 0;

        private float entranceChance = 0.1f;

        private float nestRadius = 10f;

        private float eggRadius = 10f;

        private int noiseOctaves = 6;

        public override int SeedPart => 9872136;

        public override void Generate(Map map, GenStepParams parms)
        {
            Log.Message("generating cryptimorph nest on " + map);
            Perlin perlin = new Perlin(nestFrequency, 2.0, 0.5, noiseOctaves, normalized: true, invert: true, seed: Rand.Int, quality: QualityMode.Medium);

            int spawnedHosts = 0;
            int spawnedEggs = 0;
            int spawnedGuardians = 0;

            List<IntVec3> nestCells = new List<IntVec3>();

            List<IntVec3> wallCells = new List<IntVec3>();

            foreach (IntVec3 allCell in map.AllCells)
            {
                Building edifice = allCell.GetEdifice(map);

                float nestPerlin = (float)perlin.GetValue(allCell.x, 0.0, allCell.z);

                float distance = allCell.DistanceTo(map.Center);
                float adjustment = Mathf.Max(0, 1f - distance / nestRadius);

                nestPerlin *= adjustment;

                if (nestPerlin >= nestThreshold)
                {
                    if (edifice != null)
                    {
                        edifice.Destroy();
                    }
                    nestCells.Add(allCell);
                    XMTHiveUtility.TryPlaceResinFloor(allCell, map);
                }
            }

            foreach (IntVec3 nestCell in nestCells)
            {
                IntVec3 testCell = nestCell;
                bool wallAdjacent = false;
                IntVec3 eggCell = nestCell;
                for (int x = -1; x <= 1; x++)
                {
                    for(int z = -1; z <= 1; z++)
                    { 
                        testCell.x = nestCell.x + x;
                        testCell.z = nestCell.z + z;

                        if (testCell == nestCell)
                        {
                            continue;
                        }

                        if (testCell.GetTerrain(map) != InternalDefOf.HiveFloor)
                        {
                            wallAdjacent = true;
                            wallCells.Add(testCell);
                        }
                        else
                        {
                            eggCell = testCell;
                        }
                    }
                }

                if(wallAdjacent)
                {
                    float distance = nestCell.DistanceTo(map.Center);
                    if (distance < eggRadius)
                    {
                        if (Rand.Chance(0.25f))
                        {
                            if (spawnedHosts < hostCount)
                            {
                                PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.Drifter);

                                Pawn host = PawnGenerator.GeneratePawn(request);
                                host = GenSpawn.Spawn(host, nestCell, map) as Pawn;
                                Hediff hediff = HediffMaker.MakeHediff(InternalDefOf.StarbeastCocoon, host);
                                host.health.AddHediff(hediff);

                                CocoonBase cocoonBase = XMTHiveUtility.TryPlaceCocoonBase(nestCell, host) as CocoonBase;
                                if (cocoonBase != null)
                                {
                                    host.jobs.Notify_TuckedIntoBed(cocoonBase);
                                }

                                spawnedHosts++;

                                if(eggCell != nestCell && spawnedEggs < eggCount)
                                {
                                    Ovomorph egg = GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovomorph, eggCell, map) as Ovomorph;
                                    egg.ForceProgress(Rand.Range(0.8f,1f));
                                    spawnedEggs++;
                                }
                            }
                            else if (spawnedEggs < eggCount)
                            {
                                Ovomorph egg = GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovomorph, nestCell, map) as Ovomorph;
                                egg.ForceProgress();
                                spawnedEggs++;
                            }
                            else if (spawnedGuardians < gaurdians)
                            {
                                Pawn gaurdian = XenoformingUtility.GenerateFeralXenomorph();
                                GenSpawn.Spawn(gaurdian, nestCell, map);
                                spawnedGuardians++;
                            }
                        }
                    }
                }

                map.roofGrid.SetRoof(nestCell, RoofDefOf.RoofRockThin);

                map.fogGrid.Refog(CellRect.CenteredOn(nestCell, 1));
            }

            foreach(IntVec3 wallCell in wallCells)
            {
                if (Rand.Chance(entranceChance))
                {
                    GenSpawn.Spawn(XenoBuildingDefOf.HiveWebbing, wallCell, map);
                }
                else
                {
                    GenSpawn.Spawn(XenoBuildingDefOf.Hivemass, wallCell, map);
                }
                map.roofGrid.SetRoof(wallCell, RoofDefOf.RoofRockThin);
            }

            XMTHiveUtility.ForceNestPosition(map.Center, map);
        }
    }
}
