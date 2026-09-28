using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class XMTBioContainerRenderPatch
    {
        private static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if (__result.Portrait)
            {
                return;
            }

            if (___pawn?.ParentHolder is Building_BioContainer container)
            {
                float scale = container.BioContainerComp.DrawScaleFor(___pawn);
                if (scale != 1f)
                {
                    __result.matrix *= Matrix4x4.Scale(new Vector3(scale, 1f, scale));
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.AltitudeFor))]
    internal static class XMTBioContainerNodeDepthPatch
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref float __result)
        {
            if (parms.Portrait || parms.pawn?.ParentHolder is not Building_BioContainer container)
            {
                return;
            }

            if (XMTContainmentRenderUtility.IsInHeadBranch(node))
            {
                ContainmentHarnessDrawOffsets offsets = container.BioContainerComp?.DrawOffsetsFor(parms.pawn);
                __result += offsets?.headDepthOffset ?? 0f;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.AltitudeFor))]
    internal static class XMTContainmentHarnessHeadDepthPatch
    {
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref float __result)
        {
            if (!parms.Portrait &&
                XMTContainmentRenderUtility.IsInHeadBranch(node) &&
                parms.pawn?.ParentHolder is Building_ContainmentHarness harness)
            {
                ContainmentHarnessDrawOffsets offsets =
                    harness.ContainmentComp?.Props.DrawOffsetsFor(harness.Rotation);
                if (offsets != null)
                {
                    __result += offsets.headDepthOffset;
                }
            }
        }

    }

    internal static class XMTContainmentRenderUtility
    {
        public static bool IsInHeadBranch(PawnRenderNode node)
        {
            for (PawnRenderNode current = node; current != null; current = current.parent)
            {
                if (current.Props.tagDef == PawnRenderNodeTagDefOf.Head)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
