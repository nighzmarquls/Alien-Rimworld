using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [HarmonyLib.HarmonyPatch(typeof(DesignationCategoryDef), "ResolveDesignators")]
    public static class HiveArchitectCommandsPatch
    {
        public static void Postfix(DesignationCategoryDef __instance)
        {
            if (__instance?.defName == "XMT_Hive")
                LongEventHandler.ExecuteWhenFinished(() => HiveStructureUtility.InstallArchitectCommands(__instance));
        }
    }
}
