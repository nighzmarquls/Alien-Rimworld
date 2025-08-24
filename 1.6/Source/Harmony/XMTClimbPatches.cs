
using HarmonyLib;
using System;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTClimbPatches
    {
        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoThing), new Type[] { typeof(TargetIndex), typeof(PathEndMode), typeof(bool) })]
        public static class Patch_Toils_Goto_GotoThing
        {
            [HarmonyPrefix]
            public static bool Prefix(TargetIndex ind, PathEndMode peMode, bool canGotoSpawnedParent, ref Toil __result)
            {
                __result = ClimbUtility.GotoThing( ind, peMode, canGotoSpawnedParent);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoThing), new Type[] { typeof(TargetIndex), typeof(IntVec3) })]
        public static class Patch_Toils_Goto_GotoThingExact
        {
            [HarmonyPrefix]
            public static bool Prefix(TargetIndex ind, IntVec3 exactCell, ref Toil __result)
            {
                __result = ClimbUtility.GotoThing( ind, exactCell);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), new Type[] { typeof(TargetIndex), typeof(PathEndMode) })]
        public static class Patch_Toils_Goto_GotoCell
        {
            [HarmonyPrefix]
            public static bool Prefix(TargetIndex ind, PathEndMode peMode, ref Toil __result)
            {
                __result = ClimbUtility.GotoCell( ind, peMode);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), new Type[] { typeof(IntVec3), typeof(PathEndMode) })]
        public static class Patch_Toils_Goto_GotoCell_IntVec3
        {
            [HarmonyPrefix]
            public static bool Prefix(IntVec3 cell, PathEndMode peMode, ref Toil __result)
            {
                __result = ClimbUtility.GotoCell( cell, peMode);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.CarryHauledThingToCell))]
        public static class Patch_Toils_Haul_CarryHauledThingToCell
        {
            [HarmonyPrefix]
            public static bool Prefix(TargetIndex squareIndex, PathEndMode pathEndMode, ref Toil __result)
            {
                __result = ClimbUtility.CarryHauledThingToCell(squareIndex, pathEndMode);
                return false;
            }
        }
    }
}
