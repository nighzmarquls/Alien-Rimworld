using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class XMTInstallBlueprintPatches { 


        [HarmonyPatch(typeof(Blueprint_Install), nameof(Blueprint_Install.TryReplaceWithSolidThing))]
        public static class Patch_Blueprint_Install_TryReplaceWithSolidThing
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn workerPawn, ref Thing createdThing)
            {
                if(createdThing is Ovamorph egg)
                {
                    if (XMTUtility.IsXenomorph(workerPawn))
                    {
                        Pawn queen = XMTUtility.GetQueen();

                        if (queen == null)
                        {
                            egg.LayEgg(workerPawn);
                        }
                    }
                }
            }
        }
    }
}
