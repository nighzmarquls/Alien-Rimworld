using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTSlavePatch
    {
        [HarmonyPatch(typeof(GenGuest), nameof(GenGuest.SlaveRelease))]
        public static class Patch_GenGuest_SlaveRelease
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn p)
            {
                if (XMTUtility.IsXenomorph(p))
                {
                    CompMatureMorph morph = p.GetMorphComp();

                    if(morph.Integrated)
                    {
                        return;
                    }

                    p.SetFaction(null);

                    return;
                }
            }
        }
    }
    /*
     * public static void SlaveRelease(Pawn p)
    {
        if ((p.Faction == Faction.OfPlayer || p.IsWildMan()) && p.needs.mood != null)
        {
            p.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.WasEnslaved);
        }

        GuestRelease(p);
    }
    
    */
}
