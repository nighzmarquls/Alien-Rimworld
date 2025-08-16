

using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTAnomalyPatches
    {
        [HarmonyPatch(typeof(CompHoldingPlatformTarget), "StudiedAtHoldingPlatform", MethodType.Getter)]
        static class Patch_CompHoldingPlatformTarget_StudiedAtHoldingPlatform
        {

            [HarmonyPostfix]
            static void Postfix(CompHoldingPlatformTarget __instance, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                if (__instance.parent is Pawn pawn)
                {
                    if (!pawn.Downed)
                    {
                        return;
                    }

                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        __result = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CompHoldingPlatformTarget), "CanBeCaptured", MethodType.Getter)]
        static class Patch_CompHoldingPlatformTarget_CanBeCaptured
        {

            [HarmonyPostfix]
            static void Postfix(CompHoldingPlatformTarget __instance, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                if (__instance.parent is Pawn pawn)
                {
                    if(!pawn.Downed)
                    {
                        return;
                    }

                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        __result = true;
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(RaceProperties), "IsAnomalyEntity", MethodType.Getter)]
        static class Patch_RaceProperties_IsAnomalyEntity
        {

            [HarmonyPostfix]
            static void Postfix(RaceProperties __instance, ref bool __result)
            {
                if (__result)
                {
                    return;
                }
                
                if(__instance.FleshType == InternalDefOf.StarbeastFlesh)
                {
                    __result = true;
                }
            }
        }
    }
}
