using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Xenomorphtype;

namespace Xenomorphtype
{
    internal class XMTWildAnimalPatches
    {
        [HarmonyPatch(typeof(WildAnimalSpawner), nameof(WildAnimalSpawner.SpawnRandomWildAnimalAt))]
        public static class Patch_WildAnimalSpawner_SpawnRandomWildAnimalAt
        {
            private static float CommonalityOfAnimalNow(PawnKindDef def, Map map)
            {
                return ((ModsConfig.BiotechActive && Rand.Value < WildAnimalSpawner.PollutionAnimalSpawnChanceFromPollutionCurve.Evaluate(Find.WorldGrid[map.Tile].pollution)) ? map.Biome.CommonalityOfPollutionAnimal(def) : map.Biome.CommonalityOfAnimal(def)) / def.wildGroupSize.Average;
            }

            [HarmonyPrefix]
            public static bool Prefix(IntVec3 loc, bool __result, WildAnimalSpawner __instance, Map ___map)
            {

                if(XenoformingUtility.XenoformingMeets(1))
                {
                    if(___map.skyManager.CurSkyGlow < 0.45f)
                    {
                        if(Rand.Chance(XenoformingUtility.ChanceByXenoforming(XMTSettings.WildMorphHuntChance)))
                        {
                            IntVec3 loc2 = CellFinder.RandomClosewalkCellNear(loc, ___map, 2);
                            if (XMTSettings.LogWorld)
                            {
                                Log.Message("spawning feral xenomorph ");
                            }
                            GenSpawn.Spawn(PawnGenerator.GeneratePawn(InternalDefOf.XMT_FeralStarbeastKind), loc2, ___map);
                            __result = true;
                            return false;
                        }
                    }

                    if (!___map.Biome.AllWildAnimals.Where((PawnKindDef a) => ___map.mapTemperature.SeasonAcceptableFor(a.race)).TryRandomElementByWeight((PawnKindDef def) => CommonalityOfAnimalNow(def, ___map), out var result))
                    {
                        __result = false;
                        return false;
                    }

                    int randomInRange = result.wildGroupSize.RandomInRange;
                    int radius = Mathf.CeilToInt(Mathf.Sqrt(result.wildGroupSize.max));
                    for (int i = 0; i < randomInRange; i++)
                    {
                        IntVec3 loc2 = CellFinder.RandomClosewalkCellNear(loc, ___map, radius);
                        Pawn animal = GenSpawn.Spawn(PawnGenerator.GeneratePawn(result), loc2, ___map) as Pawn;
                        if (XMTUtility.IsHost(animal))
                        {
                            if (Rand.Chance(XenoformingUtility.ChanceByXenoforming(XMTSettings.WildEmbryoChance)))
                            {
                                if(XMTSettings.LogWorld)
                                {
                                    Log.Message("spawning animal with embryo " + animal);
                                }
                                Hediff embryo = BioUtility.MakeEmbryoPregnancy(animal);
                                animal.health.AddHediff(embryo);
                            }
                        }
                    }

                    __result = true;
                    return false;
                }

                return true;
            }
        }
    }
}
