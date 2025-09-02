using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class XMTThoughtPatch
    {
        //TryGainMemory(ThoughtDef def, Pawn otherPawn = null, Precept sourcePrecept = null)

        [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new Type[] { typeof(ThoughtDef), typeof(Pawn), typeof(Precept) })]
        public static class Patch_MemoryThoughtHandler_TryGainMemoryFast
        {
            [HarmonyPostfix]
            public static void Postfix(ThoughtDef def, Pawn otherPawn, Precept sourcePrecept, MemoryThoughtHandler __instance)
            {
                if (def == ThoughtDefOf.SoakingWet)
                {
                    //Log.Message(" patching SoakingWet Thought");
                    if (__instance.pawn != null)
                    {
                        CompPawnInfo info = __instance.pawn.Info();
                        if(info != null)
                        {
                            info.CleanPheramones();
                        }
                    }
                }
            }
        }
    }
}
