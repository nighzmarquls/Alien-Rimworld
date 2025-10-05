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
                if(createdThing is Ovomorph egg)
                {
                    if (XMTUtility.IsXenomorph(workerPawn))
                    {
                        Pawn queen = XMTUtility.GetQueen();

                        if (queen == null)
                        {
                            egg.LayEgg(workerPawn);
                        }
                        else
                        {
                            egg.SetFaction(queen.Faction);
                        }
                    }
                    else
                    {
                        bool cannotPlaceOvomorphs = true;

                        if (ModsConfig.IdeologyActive)
                        {
                            if (workerPawn.mechanitor != null)
                            {
                                if (workerPawn.mechanitor.Pawn.Ideo is Ideo workerIdeo)
                                {
                                    if(workerIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation))
                                    {
                                        cannotPlaceOvomorphs = false;
                                    }
                                }
                            }
                            else if (workerPawn.Ideo is Ideo workerIdeo)
                            {
                                if (workerIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation))
                                {
                                    cannotPlaceOvomorphs = false;
                                }
                            }
                        }

                        if (cannotPlaceOvomorphs)
                        {
                            egg.SetFaction(null);
                        }
                        else
                        {
                            egg.SetFaction(workerPawn.Faction);
                        }
                    }
                }
            }
        }
    }
}
