using HarmonyLib;
using Verse;

namespace Xenomorphtype
{
    /*
    internal class XMTWildmanPatch
    {
        [HarmonyPatch(typeof(WildManUtility), nameof(WildManUtility.IsWildMan))]
        public static class Patch_WildManUtility_IsWildMan
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn p, ref bool __result)
            {
                if (XMTUtility.IsXenomorph(p))
                {
                    if(p.Faction == null)
                    {
                        __result = true;
                    }
                
                    return;
                }
            }
        }
    }
    */
}
