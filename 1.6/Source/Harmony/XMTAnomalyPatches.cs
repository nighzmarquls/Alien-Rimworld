

using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class XMTAnomalyPatches
    {
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.DoSurgicalInspection))]
        static class Patch_Pawn_DoSurgicalInspection
        {
            static bool Prepare()
            {
                return ModsConfig.AnomalyActive;
            }

            [HarmonyPrefix]
            static void Prefix(Pawn __instance, out List<Hediff> __state)
            {
                __state = XMTMedicalExaminationUtility.HiddenMutations(__instance);
            }

            [HarmonyPostfix]
            static void Postfix(Pawn __instance, Pawn surgeon, ref string desc,
                ref SurgicalInspectionOutcome __result, List<Hediff> __state)
            {
                string findings = XMTMedicalExaminationUtility.RevealMutations(__instance, surgeon, __state);
                if (findings.NullOrEmpty())
                {
                    return;
                }

                desc = desc.NullOrEmpty() ? findings : desc + "\n\n" + findings;
                __result = SurgicalInspectionOutcome.Detected;
            }
        }

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
                if (selPawn == null)
                {
                    selPawn = XMTContainmentUtility.HeldPawn(selected);
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
