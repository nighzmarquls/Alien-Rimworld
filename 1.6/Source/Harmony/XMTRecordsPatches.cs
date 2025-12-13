
using AlienRace;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTRecordsPatches
    {
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Discard))]
        public static class Patch_Pawn_Discard
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn __instance)
            {
                PawnCacheWrapper.Cleanup(__instance);
            }
        }
        [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_PawnKilled))]
        public static class Patch_RecordsUtility_Notify_PawnKilled
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn killed, Pawn killer)
            {
                if (XMTUtility.IsXenomorph(killer))
                {
                    if(killer.needs == null)
                    {
                        return;
                    }

                    if(killer.needs.TryGetNeed(out Need_KillThirst need))
                    {
                        need.CurLevel = 1f;
                    }

                    if(killer.needs.joy != null)
                    {
                        killer.needs.joy.GainJoy(0.15f, ExternalDefOf.Gaming_Dexterity);
                    }
                }
                else 
                {
                    if (XMTUtility.IsXenomorph(killed))
                    {
                        CompPawnInfo info = killer.Info();

                        if (info != null)
                        {
                            float relief = 1;

                            if (XMTUtility.IsQueen(killed))
                            {
                                relief += 4;
                            }

                            info.GainRelief(relief);

                        }
                    }
                }
            }

        }
    }
}
