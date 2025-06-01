using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTManhunterPatches
    {

        [HarmonyPatch(typeof(JobGiver_Manhunter), "FindPawnTarget")]
        public static class Patch_JobGiver_Manhunter_FindPawnTarget
        {
            private static bool IsValidTarget(Pawn x)
            {
                return !(XMTUtility.NotPrey(x)) && (int)x.def.race.intelligence >= 1;
            }
            [HarmonyPrefix]
            public static bool PreFix( Pawn pawn,ref Pawn __result)
            {

                if(XMTUtility.IsXenomorph(pawn))
                {
                    Log.Message(pawn + " is manhunter looking for target");
                    __result = (Pawn)AttackTargetFinder.BestAttackTarget(pawn, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, (Thing x) => x is Pawn p && IsValidTarget(p) , 0f, 9999f, default(IntVec3), float.MaxValue, true, canTakeTargetsCloserThanEffectiveMinRange: true, true);
                    Log.Message(__result + " picked as target");
                    return false;
                }
                return true;
            }

        }


    }
}
