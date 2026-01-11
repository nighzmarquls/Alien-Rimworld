
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    internal class GenStep_QueenSpawn : GenStep
    {
        private int eggCount = 1;

        private int hostCount = 0;

        private int gaurdians = 0;
        private int entrances = 2;

        private float chamberRadius = 10f;

        public override int SeedPart => 42172136;

        public override void Generate(Map map, GenStepParams parms)
        {
            float chamberAdjacentWallRadius = chamberRadius - 3f;
            float innerChamberRadius = chamberAdjacentWallRadius - 1f;
            IEnumerable<IntVec3> queenChamberCells = GenRadial.RadialCellsAround(map.Center, innerChamberRadius, true);
            IEnumerable<IntVec3> wallCells = GenRadial.RadialCellsAround(map.Center, chamberAdjacentWallRadius, chamberRadius);
            IEnumerable<IntVec3> hostCells = GenRadial.RadialCellsAround(map.Center, innerChamberRadius, chamberAdjacentWallRadius);

            int actualEntrances = entrances > 4 ? 4 : entrances;
            int actualEggCount = eggCount;
            int actualGuardians = gaurdians;

            Pawn queen = XMTUtility.GetQueen();
            if (queen != null)
            {
                if (queen.Faction != null)
                {
                    if (queen.Faction.IsPlayer)
                    {
                        return;
                    }
                }
               
            }
            else
            {
                queen = XenoformingUtility.GenerateFeralQueen();
                XMTUtility.DeclareQueen(queen);
            }

            if(queen == null)
            {
                return;
            }

            foreach (IntVec3 wallCell in wallCells)
            {
                GenSpawn.Spawn(XenoBuildingDefOf.Hivemass, wallCell, map);
            }

            bool spawnThrone = false;

            if(queen.GetComp<CompQueen>() is CompQueen comp)
            {
                foreach (RoyalEvolutionDef evo in comp.ChosenEvolutions)
                {
                    if (evo == RoyalEvolutionDefOf.Evo_OvoThrone)
                    {
                        actualEggCount += 8;
                        actualGuardians += 6;
                        spawnThrone = true;
                    }
                    
                }
                if(queen.health.hediffSet.HasHediff(RoyalEvolutionDefOf.XMT_Fertility))
                {
                    actualEggCount += 3;
                    actualGuardians += 3;
                }
               
            }

            int spawnedEggs = 0;
            int spawnedGaurdians = 0;

            
            Rot4 Direction = Rot4.Random;
            for (int i = 0; i < actualEntrances; i++)
            {
                float steps = 0;
                IntVec3 cell = map.Center;
                
                
                do
                {
                    if (cell.GetEdifice(map) is Building edifice)
                    {
                        edifice.Destroy();
                        GenSpawn.Spawn(XenoBuildingDefOf.HiveWebbing, cell, map);

                        if (Rand.Chance(0.25f))
                        {
                            Rot4 guardianDirection = Direction.Rotated(Rand.Bool ? RotationDirection.Counterclockwise : RotationDirection.Clockwise);
                            IntVec3 gaurdianCell = cell + guardianDirection.FacingCell;

                            if(gaurdianCell.GetEdifice(map) is Building nook)
                            {
                                nook.Destroy();
                            }

                            GenSpawn.Spawn(XenoBuildingDefOf.XMT_AmbushSpot, gaurdianCell, map, guardianDirection);
                            spawnedGaurdians++;
                        }
                    }
                    cell += Direction.FacingCell;
                    steps += 1;
                } while (steps < chamberRadius+2);

                Direction = Direction.Rotated(RotationDirection.Clockwise);
            }
            
            foreach(IntVec3 cell in queenChamberCells)
            {
                if (cell.GetEdifice(map) is Building edifice)
                {
                    if (edifice.def == XenoBuildingDefOf.Hivemass)
                    {
                        continue;
                    }

                    edifice.Destroy();
                }

                if (Rand.Chance(0.25f))
                {
                    if (spawnedEggs < actualEggCount)
                    {
                        Ovomorph egg = GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovomorph, cell, map) as Ovomorph;
                        egg.ForceProgress(Rand.Range(0.8f, 1f));
                        spawnedEggs++;
                    }
                }
                else if (Rand.Chance(0.25f))
                {
                    if (spawnedGaurdians < gaurdians)
                    {
                        Pawn gaurdian = XenoformingUtility.GenerateFeralXenomorph();
                        GenSpawn.Spawn(gaurdian, cell, map);
                        spawnedGaurdians++;
                    }
                }
                map.terrainGrid.SetTerrain(cell, InternalDefOf.HeavyHiveFloor);
            }



            int spawnedHosts = 0;
            foreach (IntVec3 cell in hostCells)
            {
                if (spawnedHosts >= hostCount)
                {
                    break;
                }

                if (Rand.Chance(0.25f))
                {
                    PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.Drifter);

                    Pawn host = PawnGenerator.GeneratePawn(request);
                    host = GenSpawn.Spawn(host, cell, map) as Pawn;
                    Hediff hediff = HediffMaker.MakeHediff(InternalDefOf.StarbeastCocoon, host);
                    host.health.AddHediff(hediff);

                    CocoonBase cocoonBase = XMTHiveUtility.TryPlaceCocoonBase(cell, host) as CocoonBase;
                    if (cocoonBase != null)
                    {
                        host.jobs.Notify_TuckedIntoBed(cocoonBase);
                    }
                    spawnedHosts++;
                } 
            }

            if (spawnThrone)
            {
                GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovothrone, map.Center, map, Rot4.Random);
                XMTHiveUtility.ForceNestPosition(map.Center+(Rot4.Random.FacingCell*5), map);
            }
            GenSpawn.Spawn(queen, map.Center, map);
        }
    }
}
