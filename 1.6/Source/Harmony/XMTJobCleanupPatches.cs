using HarmonyLib;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTJobCleanupPatches
    {
        [HarmonyPatch(typeof(JobDriver), "Cleanup")]
        public static class Patch_JobDriver_Cleanup
        {
            [HarmonyPostfix]
            public static void Postfix(JobDriver __instance, JobCondition condition)
            {
                if (__instance is JobDriver_BuildXenomorphStructure hiveBuild)
                {
                    hiveBuild.NotifyCleanup(condition);
                    return;
                }

                if (__instance is JobDriver_BuildHiveRoof hiveRoof)
                {
                    hiveRoof.NotifyCleanup(condition);
                }
            }
        }
    }
}
