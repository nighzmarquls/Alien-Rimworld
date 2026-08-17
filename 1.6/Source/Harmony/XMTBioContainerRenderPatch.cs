using HarmonyLib;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class XMTBioContainerRenderPatch
    {
        private static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if (!__result.Portrait && ___pawn?.ParentHolder is Building_BioContainer container)
            {
                float scale = container.BioContainerComp.OccupantDrawScale;
                if (scale != 1f)
                {
                    __result.matrix *= Matrix4x4.Scale(new Vector3(scale, 1f, scale));
                }
            }
        }
    }
}
