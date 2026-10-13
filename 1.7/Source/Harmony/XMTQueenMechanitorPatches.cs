using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.GetGizmos))]
    internal static class XMTQueenMechanitorPatches
    {
        private static void Postfix(Pawn_MechanitorTracker __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance?.Pawn?.GetComp<CompQueenAssimilation>() == null)
            {
                return;
            }

            __result = __result.Where(gizmo => gizmo?.GetType().Name != "Command_CallBossgroup");
        }
    }
}
