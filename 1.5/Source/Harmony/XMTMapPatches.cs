using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace Xenomorphtype
{ 
    public class XMTMapPatches
    {
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
        public static class Patch_Pawn_ExitMap
        {
            [HarmonyPostfix]
            public static void Postfix(bool allowedToJoinOrCreateCaravan, Rot4 exitDir, Pawn __instance)
            {
                if(XMTSettings.LogWorld)
                {
                    Log.Message(__instance + " is leaving the map");
                }

                XenoformingUtility.HandleXenoformingImpact(__instance);
               
                return;
            }
        }

        [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreKidnapped))]
        public static class Patch_Pawn_PreKidnapped
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn kidnapper, Pawn __instance)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(__instance + " is being kidnapped");
                }
                XenoformingUtility.HandleXenoformingImpact(__instance);
            }
        }
    }
}
