using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTInteractionPatches
    {
        [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance))]
        public static class Patch_InteractionWorker_RomanceAttempt_SuccessChance
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn initiator, Pawn recipient, float baseChance, float __result)
            {
                bool recipientXenomorph = XMTUtility.IsXenomorph(recipient);
                bool initiatorXenomorph = XMTUtility.IsXenomorph(initiator);
                if (!recipientXenomorph && !initiatorXenomorph)
                {
                    return;
                }

                float influence = 0;

                if (!initiatorXenomorph && recipientXenomorph)
                {

                }

                if (initiatorXenomorph && !recipientXenomorph)
                {

                }

                __result += influence;

            }
        }

        [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
        internal static class Patch_JobDriver_Cleanup
        {
            //RomanceDiversified lovin
            //not very good solution, some other mod can have same named jobdrivers, but w/e
            private readonly static Type JobDriverDoLovinCasual = AccessTools.TypeByName("JobDriver_DoLovinCasual");

            //Nightmare Incarnation
            //not very good solution, some other mod can have same named jobdrivers, but w/e
            private readonly static Type JobDriverNIManaSqueeze = AccessTools.TypeByName("JobDriver_NIManaSqueeze");

            //Highmate Expanded lovin
            //not very good solution, some other mod can have same named jobdrivers, but w/e
            private readonly static Type JobDriverHighMateLovin = AccessTools.TypeByName("JobDriver_InitiateLovin");

            //vanilla lovin
            private readonly static Type JobDriverLovin = typeof(JobDriver_Lovin);

            //vanilla mate
            private readonly static Type JobDriverMate = typeof(JobDriver_Mate);

            //RJW sex
            //not very good solution, some other mod can have same named jobdrivers, but w/e
            private readonly static Type JobDriverSex = AccessTools.TypeByName("JobDriver_Sex");


            

            [HarmonyPrefix]
            private static void Prefix(JobDriver __instance, JobCondition condition)
            {
                if (__instance == null)
                    return;

                if (condition == JobCondition.Succeeded)
                {
                    Pawn pawn = __instance.pawn;
                    Pawn partner = null;
                    

                    var any_ins = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                    //Vanilla loving
                    if (__instance.GetType() == JobDriverLovin)
                    {
                        partner = (Pawn)(__instance.GetType().GetProperty("Partner", any_ins).GetValue(__instance, null));

                    }
                    //Vanilla mating
                    else if (__instance.GetType() == JobDriverMate)
                    {
                        partner = (Pawn)(__instance.GetType().GetProperty("Female", any_ins).GetValue(__instance, null));

                    }
                    //Highmate loving
                    else if (__instance.GetType() == JobDriverHighMateLovin)
                    {
                        partner = (Pawn)(__instance.GetType().GetProperty("Partner", any_ins).GetValue(__instance, null));
                    }
                    else
                    {
                        if ((object)JobDriverSex != null)
                        {
                            if (__instance.GetType().IsSubclassOf(JobDriverSex))
                            {
                                partner = (Pawn)(__instance.GetType().GetProperty("Partner", any_ins).GetValue(__instance, null));
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    BioUtility.ApplySexEffects(pawn, partner);
                    
                }
                return;
            }
        }
    }
}
