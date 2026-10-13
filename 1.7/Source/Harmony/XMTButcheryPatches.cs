

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class XMTButcheryPatches
    {
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.ButcherProducts))]
        static class Patch_ITab_Pawn_Gear_IsVisible
        {

            [HarmonyPostfix]
            static void Postfix(Pawn __instance, float efficiency, ref IEnumerable<Thing> __result)
            {
                if (!XMTUtility.IsXenomorph(__instance))
                {
                    float silicate = __instance.CarboSilicate();
                    float exoskeleton = __instance.MesoSkeletonization();
                    if (silicate > 0 || exoskeleton > 0)
                    {
                        List<Thing> meats = new List<Thing>();
                        List<Thing> leathers = new List<Thing>();
                        List<Thing> alteredButchery = new List<Thing>();
                        int totalMeat = 0;
                        int totalLeather = 0;
                        foreach (Thing thing in __result)
                        {
                            if (thing.def == __instance.RaceProps.meatDef)
                            {
                                totalMeat += thing.stackCount;
                                meats.Add(thing);
                            }
                            else if (thing.def == __instance.RaceProps.leatherDef)
                            {
                                totalLeather += thing.stackCount;
                                leathers.Add(thing);
                            }
                            else
                            {
                                alteredButchery.Add(thing);
                            }
                        }
                        int silicateCount = Mathf.CeilToInt(totalMeat * Mathf.Min(1, silicate));
                        int chitinCount = Mathf.CeilToInt(totalLeather * Mathf.Min(1, exoskeleton));

                        foreach (Thing meat in meats)
                        {
                            if (silicateCount <= 0)
                            {
                                alteredButchery.Add(meat);
                            }
                            else if (meat.stackCount > silicateCount)
                            {
                                meat.stackCount -= silicateCount;

                                Thing newSilicateMeat = ThingMaker.MakeThing(InternalDefOf.Starbeast_Flesh_Meat);
                                newSilicateMeat.stackCount = silicateCount;
                                alteredButchery.Add(newSilicateMeat);
                                alteredButchery.Add(meat);
                                silicateCount = 0;
                            }
                            else
                            {
                                Thing newSilicateMeat = ThingMaker.MakeThing(InternalDefOf.Starbeast_Flesh_Meat);
                                newSilicateMeat.stackCount = meat.stackCount;
                                alteredButchery.Add(newSilicateMeat);
                                silicateCount -= newSilicateMeat.stackCount;
                                meat.Discard();
                            }
                        }

                        foreach (Thing leather in leathers)
                        {
                            if (chitinCount <= 0)
                            {
                                alteredButchery.Add(leather);
                            }
                            else if (leather.stackCount > chitinCount)
                            {
                                leather.stackCount -= chitinCount;

                                Thing newChiten = ThingMaker.MakeThing(InternalDefOf.Starbeast_Chitin);
                                newChiten.stackCount = chitinCount;
                                alteredButchery.Add(newChiten);
                                alteredButchery.Add(leather);
                                chitinCount = 0;
                            }
                            else
                            {
                                Thing newChiten = ThingMaker.MakeThing(InternalDefOf.Starbeast_Chitin);
                                newChiten.stackCount = leather.stackCount;
                                alteredButchery.Add(newChiten);
                                silicateCount -= newChiten.stackCount;
                                leather.Discard();
                            }
                        }

                        __result = alteredButchery;
                    }
                }

                Corpse corpse = __instance.Corpse;
                if (XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished != true ||
                    corpse == null || corpse.GetRotStage() != RotStage.Fresh)
                {
                    return;
                }

                int glandCount = 0;
                if (corpse is StarbeastCorpse)
                {
                    glandCount = Math.Max(1, GenMath.RoundRandom(__instance.BodySize * 5f * efficiency));
                }
                else
                {
                    int categoryCount = PheromoneUtility.CategoryCount(__instance.PheromoneProduction());
                    if (categoryCount > 0)
                    {
                        glandCount = Math.Max(1, GenMath.RoundRandom(__instance.BodySize * categoryCount * efficiency));
                    }
                }

                if (glandCount > 0)
                {
                    List<Thing> products = new List<Thing>(__result);
                    int stackLimit = InternalDefOf.XMT_RawPheromone.stackLimit;
                    while (glandCount > 0)
                    {
                        Thing glands = ThingMaker.MakeThing(InternalDefOf.XMT_RawPheromone);
                        glands.stackCount = Math.Min(glandCount, stackLimit);
                        products.Add(glands);
                        glandCount -= glands.stackCount;
                    }
                    __result = products;
                }
            }
        }
    }
}
