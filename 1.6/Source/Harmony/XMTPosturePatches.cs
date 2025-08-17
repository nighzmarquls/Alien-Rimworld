using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class XMTPosturePatches
    {

        [HarmonyPatch(typeof(Pawn), "TicksPerMove")]
        public static class Patch_Pawn_TicksPerMove
        {
            [HarmonyPrefix]
            public static bool Prefix(ref float __result, Pawn __instance, bool diagonal)
            {
                if (!XMTUtility.IsXenomorph(__instance))
                {
                    return true;
                }

                float speed = __instance.GetStatValue(StatDefOf.MoveSpeed);
                if (__instance.Crawling)
                {
                    speed = __instance.GetStatValue(StatDefOf.CrawlSpeed);
                }

                if (RestraintsUtility.InRestraints(__instance))
                {
                    speed *= 0.35f;
                }

                if (__instance.carryTracker?.CarriedThing != null && __instance.carryTracker.CarriedThing.def.category == ThingCategory.Pawn)
                {
                    speed *= 0.6f;
                }

                float speedDelta = speed / 60f;
                float output;
                if (speedDelta == 0f)
                {
                    output = 450f;
                }
                else
                {
                    output = 1f / speedDelta;
                    if (__instance.Spawned && !__instance.Map.roofGrid.Roofed(__instance.Position))
                    {
                        output /= __instance.Map.weatherManager.CurMoveSpeedMultiplier;
                    }

                    if (diagonal)
                    {
                        output *= 1.41421f;
                    }
                }

                output = Mathf.Clamp(output, 1f, 450f);
                if (__instance.debugMaxMoveSpeed)
                {
                    __result = 1f;
                    return false;
                }

                __result = output;
                return false;
            }
        }

        [HarmonyPatch(typeof(Pawn), "CanAttackWhileCrawling", MethodType.Getter)]
        public static class Patch_Pawn_CanAttackWhileCrawling
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn __instance)
            {
                if (!XMTUtility.IsXenomorph(__instance))
                {
                    return true;
                }

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Pawn), "Crawling", MethodType.Getter)]
        public static class Patch_Pawn_Crawling
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn __instance)
            {
                if (__instance.Downed)
                {
                    return true;
                }

                if (!XMTUtility.IsXenomorph(__instance))
                {
                    return true;
                }

                if (!__instance.health.CanCrawl)
                {
                    __result = false;
                    return false;
                }

                if (!__instance.ageTracker.Adult)
                {
                    __result = true;
                    __instance.jobs.posture = PawnPosture.LayingOnGroundNormal;
                    return false;
                }

                CompCrawler compCrawler = __instance.GetComp<CompCrawler>();

                if (compCrawler != null)
                {
                    __result = compCrawler.Crawling;
                    if (__result)
                    {
                        __instance.jobs.posture = PawnPosture.LayingOnGroundNormal;
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), "CanCrawl", MethodType.Getter)]
        public static class Patch_Pawn_HealthTracker_CanCrawl
        {
            public static void Postfix(ref bool __result, Pawn ___pawn, Pawn_HealthTracker __instance)
            {
                if (__result)
                {
                    return;
                }

                if (!XMTUtility.IsXenomorph(___pawn))
                {
                    return;
                }

                if (__instance.capacities.GetLevel(PawnCapacityDefOf.Manipulation) < 0.15f)
                {
                    return;
                }

                if (__instance.hediffSet.AnyHediffPreventsCrawling)
                {
                    return;
                }

                __result = true;
            }
        }
        
    }
}
