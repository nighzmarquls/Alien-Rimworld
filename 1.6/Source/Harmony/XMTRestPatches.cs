using HarmonyLib;
using RimWorld;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class XMTRestPatches
    {
        [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.ShouldWakeUpToAutofeedUrgent))]
        static class Toils_ChildcareUtility_ShouldWakeUpToAutofeedUrgent
        {
            [HarmonyPrefix]
            public static bool Prefix(bool __result, Pawn feeder)
            {
                if(feeder == null || feeder.Faction == null)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
        static class Toils_LayDown_ApplyBedThoughts
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn actor, Building_Bed bed)
            {

                if(bed is JellyPod pod)
                {
                    if (pod.SoakInJelly(actor))
                    {

                    }
                }

                CompPawnInfo info = actor.Info();
                if (info != null)
                {
                    if (KnowledgeUtility.IsObsessed(actor))
                    {
                        return;
                    }

                    float totalTrauma = KnowledgeUtility.GetTrauma(actor);

                    if (totalTrauma <= 0)
                    {
                        return;
                    }

                    if (totalTrauma >= 4.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 3);
                    }
                    else if (totalTrauma >= 2.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 2);
                    }
                    else if (totalTrauma >= 1.0f)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 1);
                    }
                    else if (totalTrauma > 0)
                    {
                        XMTUtility.GiveMemory(actor, HorrorMoodDefOf.VictimNightmareMood, stage: 0);
                    }

                    
                }
                return;
            }
        }
    }
}
