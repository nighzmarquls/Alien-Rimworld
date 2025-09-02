using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace Xenomorphtype
{
    internal class XMTOffsetPatches
    {
        [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
        public static class Patch_Pawn_DrawTracker_DrawPos
        {
            public static void Postfix(ref Vector3 __result, Pawn ___pawn)
            {
                if (___pawn.jobs?.curDriver != null)
                {
                    __result.y += ___pawn.jobs.curDriver.ForcedBodyOffset.y;
                }
                return;
            }
        }

        [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BaseHeadOffsetAt))]
        public static class Patch_PawnRenderer_BaseHeadOffsetAt
        {
            public static void Postfix(ref Vector3 __result, Rot4 rotation, Pawn ___pawn)
            {
                CompPawnInfo info = ___pawn.Info();

                if (info != null)
                {
                   
                    if (!(rotation == Rot4.South))
                    {
                        if (!(rotation == Rot4.North))
                        {
                            if (!(rotation == Rot4.East))
                            {
                                __result += info.headOffset_west;
                                return;
                            }
                            __result += info.headOffset_east;
                            return;
                        }
                        __result += info.headOffset_north;
                        return;
                    }
                    __result += info.headOffset_south;
                    return ;
                    
                }
            }
        }

        }
}
