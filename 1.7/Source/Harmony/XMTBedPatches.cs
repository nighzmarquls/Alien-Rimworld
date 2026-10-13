using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class XMTBedPatches
    {
        [HarmonyPatch(typeof(Building_Bed), "FindPreferredInteractionCell")]
        static class Patch_Building_FindPreferredInteractionCell
        {

            [HarmonyPrefix]
            static bool Prefix(IntVec3 occupantLocation,Building_Bed __instance, ref IntVec3? __result)
            {
                if (__instance is QueenBed)
                {
                    List<IntVec3> cells = GenRadial.RadialCellsAround(occupantLocation, 1, false).ToList();
                    foreach (IntVec3 cell in cells)
                    {
                        if (cell.Walkable(__instance.Map))
                        {
                            __result = cell;
                            return false;
                        }
                        
                    }
                    __result = null;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Building_Bed), "SleepingSlotsCount", MethodType.Getter)]
        static class Patch_Building_Bed_GetSleepingSlotCount
        {

            [HarmonyPrefix]
            static bool Prefix(Building_Bed __instance, ref int __result)
            {
                if(__instance is QueenBed)
                {
                    __result = 1;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Building_Bed), "GetSleepingSlotPos")]
        static class Patch_Building_Bed_GetSleepingSlotPos
        {
            [HarmonyPrefix]
            static bool Prefix(Building_Bed __instance, ref IntVec3 __result, int index)
            {
                if (__instance is QueenBed)
                {
                    __result = __instance.Position;
                    return false;
                }
                return true;
            }
        }
    }
}
