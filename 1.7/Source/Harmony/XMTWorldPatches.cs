using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    public static class XMTWorldPatches
    {
        [HarmonyPatch(typeof(WorldDrawLayerBase), "FinalizeMesh")]
        public static class Patch_FinalizeWorldTerrainMesh
        {
            [HarmonyPrefix]
            public static void Prefix(WorldDrawLayerBase __instance, MeshParts __0)
            {
                XenoformingUtility.ClearEmptyWorldTerrainMeshes(__instance, __0);
            }
        }
    }
}
