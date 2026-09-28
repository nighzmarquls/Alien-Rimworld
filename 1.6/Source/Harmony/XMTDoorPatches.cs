

using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTDoorPatches
    {

       
        [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
        static class Toils_Building_Door_PawnCanOpen
        {
            [HarmonyPrefix]
            public static bool Prefix(Building_Door __instance, ref bool __result, Pawn p)
            {
                if(__instance.Faction == null)
                {
                    return true;
                }

                if (__instance is PassableRoomborder)
                {
                    __result = true;
                    return false;
                }

                if (p == null || !XMTUtility.IsXenomorph(p))
                {
                    return true;
                }

                bool conventionalResistanceApplies = p.IsPrisoner || p.Faction == null || p.Faction != __instance.Faction;
                if (!conventionalResistanceApplies)
                {
                    return true;
                }

                if (__instance.IsForbidden(__instance.Faction))
                {
                    if(__instance.Open)
                    {
                        return true;
                    }
                    __result = false;
                    return false;
                }

                if (XMTUtility.IsSpace(p.MapHeld))
                {
                    __result = true;
                    return false;
                }

                if (!XMTDoorUtility.HasPoweredResistance(__instance))
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Setter)]
        static class CompPowerTrader_PowerOn
        {
            [HarmonyPostfix]
            public static void Postfix(CompPowerTrader __instance)
            {
                if (__instance?.parent is Building_Door door && door.Spawned)
                {
                    door.Map.reachability.ClearCache();
                }
            }
        }


        
    }
}
