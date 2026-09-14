using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VefAbility = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(Psycast), nameof(Psycast.Activate), new System.Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    internal static class NemesisVanillaPsycastPatch
    {
        private static void Postfix(Psycast __instance, bool __result)
        {
            if (__result)
            {
                NemesisEvidenceReporter.ReportVanillaPsycast(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(VefAbility), nameof(VefAbility.Cast), new System.Type[] { typeof(GlobalTargetInfo[]) })]
    internal static class NemesisVefPsycastPatch
    {
        internal sealed class CastState
        {
            internal NemesisVpePsycastDescriptor descriptor;
            internal float psyfocus;
            internal float entropy;
        }

        private static void Prefix(VefAbility __instance, out CastState __state)
        {
            __state = null;
            if (__instance?.pawn?.Faction != Faction.OfPlayer
                || !NemesisVpeAdapter.TryDescribePsycast(__instance, out NemesisVpePsycastDescriptor descriptor))
            {
                return;
            }

            __state = new CastState
            {
                descriptor = descriptor,
                psyfocus = __instance.pawn.psychicEntropy?.CurrentPsyfocus ?? 0f,
                entropy = __instance.pawn.psychicEntropy?.EntropyValue ?? 0f
            };
        }

        private static void Postfix(VefAbility __instance, CastState __state)
        {
            if (__state != null)
            {
                NemesisEvidenceReporter.ReportVpePsycast(__instance, __state.descriptor,
                    __state.psyfocus, __state.entropy, "Committed");
            }
        }
    }
}
