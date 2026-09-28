using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(MinifiedThing), nameof(MinifiedThing.GetGizmos))]
    internal static class Patch_MinifiedThing_GetGizmos_XMTBioContainerTransfer
    {
        [HarmonyPostfix]
        private static void Postfix(MinifiedThing __instance, ref IEnumerable<Gizmo> __result)
        {
            Building_BioContainer container = BioContainerUtility.Resolve(__instance);
            if (container?.Faction != Faction.OfPlayer || container.ContainedThing is not Pawn)
            {
                return;
            }

            __result = AppendTransfer(__instance, __result);
        }

        private static IEnumerable<Gizmo> AppendTransfer(MinifiedThing source, IEnumerable<Gizmo> original)
        {
            foreach (Gizmo gizmo in original)
            {
                yield return gizmo;
            }

            Command_Action transfer = XMTContainedPawnTransferUtility.MakeTransferCommand(source);
            if (transfer != null)
            {
                yield return transfer;
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_HoldingPlatform_GetGizmos_XMTTransfer
    {
        private const string HoldingPlatformTypeName = "RimWorld.Building_HoldingPlatform";

        private static bool Prepare()
        {
            return ModsConfig.AnomalyActive && AccessTools.TypeByName(HoldingPlatformTypeName) != null;
        }

        private static MethodBase TargetMethod()
        {
            System.Type platformType = AccessTools.TypeByName(HoldingPlatformTypeName);
            return platformType == null ? null : AccessTools.Method(platformType, "GetGizmos");
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, ref IEnumerable<Gizmo> __result)
        {
            Thing platform = __instance as Thing;
            Pawn heldPawn = XMTContainmentUtility.HeldPawn(platform);
            if (!XMTUtility.IsXenomorph(heldPawn))
            {
                return;
            }

            __result = ReplaceTransferCommand(platform, heldPawn, __result);
        }

        private static IEnumerable<Gizmo> ReplaceTransferCommand(Thing platform, Pawn heldPawn,
            IEnumerable<Gizmo> original)
        {
            string vanillaLabel = "TransferEntity".Translate(heldPawn).ToString();
            foreach (Gizmo gizmo in original)
            {
                if (gizmo is Command command && command.defaultLabel != null &&
                    command.defaultLabel.StartsWith(vanillaLabel))
                {
                    continue;
                }
                yield return gizmo;
            }

            Command_Action transfer = XMTContainedPawnTransferUtility.MakeTransferCommand(platform);
            if (transfer != null)
            {
                yield return transfer;
            }
        }
    }
}
