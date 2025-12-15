

using HarmonyLib;
using RimWorld;
using VEF.Maps;
using Verse;

namespace Xenomorphtype
{
    internal class XMTDoorPatches
    {

       
        [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
        static class Toils_Building_Door_PawnCanOpen
        {
            [HarmonyPrefix]
            public static bool Prefix(Building_Door __instance, bool __result, Pawn p)
            {
                if(__instance.Faction == null)
                {
                    return true;
                }

                if (p.Faction != null)
                {
                    return true;
                }

                if (!XMTUtility.IsXenomorph(p))
                {
                    return true;
                }

                if (__instance.IsForbidden(__instance.Faction))
                {
                    __result = false;
                    return false;
                }

                if (!__instance.Powered())
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }


        
    }
}
