using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class XMTBioContainerBillPatch
    {
        [HarmonyPatch]
        private static class WorkGiver_DoBill_TryStartNewDoBillJob_Patch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(WorkGiver_DoBill), "TryStartNewDoBillJob", new[]
                {
                    typeof(Pawn), typeof(Bill), typeof(IBillGiver), typeof(List<ThingCount>),
                    typeof(Job).MakeByRefType(), typeof(bool)
                });
            }

            private static void Postfix(Pawn pawn, Bill bill, ref Job __result)
            {
                if (__result?.def != JobDefOf.DoBill || bill?.recipe?.Worker is not Recipe_RemoveHediffContained)
                {
                    return;
                }

                if (!BioContainerUtility.TryFindSurgicalContainer(pawn, bill.billStack.billGiver as Thing,
                    out Thing container, out bool noInstallSpace))
                {
                    JobFailReason.Is((noInstallSpace ? "XMT_NoBioContainerSurgerySpace" : "XMT_NoEmptyBioContainer").Translate());
                    __result = null;
                    return;
                }

                __result.source = container;
                __result.def = XenoWorkDefOf.XMT_DoContainedBill;
                __result.count = 1;
            }
        }
    }
}
