using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal class XMTLordDutyPatches
    {
        [HarmonyPatch(typeof(LordToil_ExitMap), nameof(LordToil_ExitMap.UpdateAllDuties))]
        public static class Patch_LordToil_ExitMap_UpdateAllDuties
        {
            [HarmonyPostfix]
            public static void Postfix(Lord ___lord)
            {
                for (int i = 0; i < ___lord.ownedPawns.Count; i++)
                {
                    Pawn pawn = ___lord.ownedPawns[i];
                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        if (pawn?.mindState?.duty?.def == DutyDefOf.ExitMapBest)
                        {
                            Log.Message(pawn + " is now trying to leave the map.");
                        }
                    }
                }


            }
        }
    }
}
