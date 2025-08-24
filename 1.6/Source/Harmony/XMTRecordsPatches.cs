
using AlienRace;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTRecordsPatches
    {
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
            }

        }
    }
}
