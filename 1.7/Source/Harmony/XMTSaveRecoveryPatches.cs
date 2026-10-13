using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.ExposeData))]
    internal static class XMTSaveRecoveryPatches
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Pawn_AbilityTracker __instance)
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || __instance?.abilities == null)
            {
                return;
            }

            int removedCount = __instance.abilities.RemoveAll(ability => ability?.def == null);
            if (removedCount <= 0)
            {
                return;
            }

            __instance.Notify_TemporaryAbilitiesChanged();
            Log.Warning("[Alien | RimWorld] Removed " + removedCount + " invalid saved "
                + (removedCount == 1 ? "ability" : "abilities") + " from "
                + (__instance.pawn?.ThingID ?? "an unknown pawn") + ".");
        }
    }
}
