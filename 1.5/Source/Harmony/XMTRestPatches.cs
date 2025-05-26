using HarmonyLib;
using RimWorld;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class XMTRestPatches
    {
        [HarmonyPatch(typeof(RestUtility), "WakeThreshold")]
        static class RestUtility_WakeThreshold
        {
            public static void Postfix(ref float __result, Pawn p)
            {
                if (XMTUtility.IsXenomorph(p))
                {
                    return;
                }

                CompPawnInfo info = p.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    if(info.IsObsessed())
                    {
                        return;
                    }

                    __result -= info.TotalHorrorExperience()/6;
                }
                return;
            }
        }

        [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
        static class RestUtility_WakeUp
        {
            public static void Postfix(Pawn actor, Building_Bed bed)
            {
                if(XMTUtility.IsXenomorph(actor))
                {
                    return;
                }

                CompPawnInfo info = actor.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    if (info.IsObsessed())
                    {
                        return;
                    }

                    float totalExperience = info.TotalHorrorExperience();

                    if (totalExperience <= 0)
                    {
                        return;
                    }

                    if (totalExperience >= 4.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 3);
                    }
                    else if (totalExperience >= 2.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 2);
                    }
                    else if (totalExperience >= 1.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 1);
                    }
                    else if (totalExperience > 0)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 0);
                    }
                }
                return;
            }
        }
    }
}
