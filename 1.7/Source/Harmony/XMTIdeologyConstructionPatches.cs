using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTIdeologyConstructionPatches
    {
        [HarmonyPatch(typeof(Blueprint_Build), nameof(Blueprint_Build.TotalMaterialCost))]
        public static class Patch_BlueprintBuild_TotalMaterialCost
        {
            [HarmonyPrefix]
            public static bool Prefix(Blueprint_Build __instance, ref List<ThingDefCountClass> __result)
            {
                if (!XMTIdeologyConstructionUtility.IsRegisteredResinBuild(__instance))
                {
                    return true;
                }

                __result = XMTIdeologyConstructionUtility.RequiredMaterialCost(__instance.BuildDef as ThingDef);
                return false;
            }
        }

        [HarmonyPatch(typeof(Blueprint_Build), "MakeSolidThing")]
        public static class Patch_BlueprintBuild_MakeSolidThing
        {
            [HarmonyPostfix]
            public static void Postfix(Blueprint_Build __instance, Thing __result)
            {
                if (!XMTIdeologyConstructionUtility.IsRegisteredResinBuild(__instance))
                {
                    return;
                }

                XMTIdeologyConstructionUtility.UnregisterResinBuild(__instance);
                if (__result is Frame frame)
                {
                    XMTIdeologyConstructionUtility.RegisterResinBuild(frame);
                }
            }
        }

        [HarmonyPatch(typeof(Frame), nameof(Frame.TotalMaterialCost))]
        public static class Patch_Frame_TotalMaterialCost
        {
            [HarmonyPrefix]
            public static bool Prefix(Frame __instance, ref List<ThingDefCountClass> __result)
            {
                if (!XMTIdeologyConstructionUtility.IsRegisteredResinBuild(__instance))
                {
                    return true;
                }

                __result = XMTIdeologyConstructionUtility.RequiredMaterialCost(__instance.BuildDef as ThingDef);
                return false;
            }
        }

        [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
        public static class Patch_Frame_CompleteConstruction
        {
            [HarmonyPostfix]
            public static void Postfix(Frame __instance)
            {
                XMTIdeologyConstructionUtility.UnregisterResinBuild(__instance);
            }
        }

        [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy), typeof(DestroyMode))]
        public static class Patch_Thing_Destroy
        {
            [HarmonyPostfix]
            public static void Postfix(Thing __instance)
            {
                XMTIdeologyConstructionUtility.UnregisterResinBuild(__instance);
            }
        }

        [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool), typeof(JobDef))]
        public static class Patch_GenConstruct_CanConstruct
        {
            [HarmonyPostfix]
            public static void Postfix([HarmonyArgument(0)] Thing t, [HarmonyArgument(1)] Pawn pawn, ref bool __result)
            {
                if (!__result && XMTIdeologyConstructionUtility.IsRegisteredResinBuild(t) && XMTUtility.IsXenomorph(pawn))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct), typeof(Thing), typeof(Pawn), typeof(WorkTypeDef), typeof(bool), typeof(JobDef))]
        public static class Patch_GenConstruct_CanConstruct_WorkType
        {
            [HarmonyPostfix]
            public static void Postfix([HarmonyArgument(0)] Thing t, [HarmonyArgument(1)] Pawn pawn, ref bool __result)
            {
                if (!__result && XMTIdeologyConstructionUtility.IsRegisteredResinBuild(t) && XMTUtility.IsXenomorph(pawn))
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), nameof(WorkGiver_ConstructFinishFrames.JobOnThing))]
        public static class Patch_WorkGiverConstructFinishFrames_JobOnThing
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, Thing t, ref Job __result)
            {
                if (__result != null && XMTIdeologyConstructionUtility.IsRegisteredResinBuild(t) && !XMTUtility.IsXenomorph(pawn))
                {
                    __result = null;
                }
            }
        }
    }
}
