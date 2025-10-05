

using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTAnomalyPatches
    {
        [HarmonyPatch(typeof(ITab_Pawn_Social), "IsVisible", MethodType.Getter)]
        static class Patch_ITab_Pawn_Social_IsVisible
        {
            [HarmonyPostfix]
            static void Postfix(ITab_Entity __instance, ref bool __result)
            {

                if (__result)
                {
                    return;
                }
                Thing selected = Find.Selector.SingleSelectedThing;

                Pawn selPawn = selected as Pawn;

                if (XMTUtility.IsXenomorph(selPawn))
                {
                    __result = true;
                }

            }
        }

        [HarmonyPatch(typeof(ITab_Entity), "IsVisible", MethodType.Getter)]
        static class Patch_ITab_Entity_IsVisible
        {
            [HarmonyPostfix]
            static void Postfix(ITab_Entity __instance, ref bool __result)
            {
                
                if(!__result)
                {
                    return;
                }
                Thing selected = Find.Selector.SingleSelectedThing;

                Pawn selPawn = selected as Pawn;
                if (selPawn == null && selected is Building_HoldingPlatform building_HoldingPlatform)
                {
                    selPawn = building_HoldingPlatform.HeldPawn;
                }

                if (XMTUtility.IsXenomorph(selPawn))
                {
                    __result = false;
                }
                
            }
        }

        [HarmonyPatch(typeof(ITab_Pawn_Gear), "IsVisible", MethodType.Getter)]
        static class Patch_ITab_Pawn_Gear_IsVisible
        {

            [HarmonyPostfix]
            static void Postfix(ITab_Pawn_Gear __instance, ref bool __result)
            {
                if (__result)
                {
                    return;
                }

                Thing selected = Find.Selector.SingleSelectedThing;

                Pawn selectedPawn = selected as Pawn;

                if(selectedPawn == null)
                {
                    if(selected is Corpse corpse)
                    {
                        selectedPawn = corpse.InnerPawn;
                    }
                }

                if (selectedPawn == null)
                {
                    return;
                }

                if(!XMTUtility.IsXenomorph(selectedPawn))
                {
                    return;
                }

                if (selectedPawn.apparel == null)
                {
                    if (selectedPawn.equipment == null)
                    {
                        return;
                    }
                }

                __result = true;
            }
        }


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
