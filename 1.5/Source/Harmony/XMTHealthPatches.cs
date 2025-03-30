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
    internal class XMTHealthPatches
    {
        [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
        public static class Patch_HediffSet_AddDirect
        {
            [HarmonyPostfix]
            public static void Postfix(Hediff hediff, DamageInfo? dinfo, DamageWorker.DamageResult damageResult, HediffSet __instance)
            {
                Pawn pawn = __instance.pawn;
                if (pawn != null)
                {
                    CompPerfectOrganism perfectOrganism = pawn.GetComp<CompPerfectOrganism>();
                    if (perfectOrganism != null)
                    {
                        if (hediff.def.pregnant && hediff.def != XenoGeneDefOf.XMT_HorrorPregnant)
                        {
                            pawn.health.RemoveHediff(hediff);
                            pawn.health.AddHediff(XenoGeneDefOf.XMT_HorrorPregnant);
                        }
                        perfectOrganism.RemoveImperfection(hediff);
                    }
                    CompPawnInfo info = pawn.GetComp<CompPawnInfo>();

                    if (info != null)
                    {
                        if (hediff.def == ExternalDefOf.WetCold || hediff.def == ExternalDefOf.Washing)
                        {
                            info.CleanPheramones();
                        }

                        HediffComp_HeadOffset hediffOffset = hediff.TryGetComp<HediffComp_HeadOffset>();
                        if (hediffOffset != null)
                        {
                            info.headOffset = hediffOffset.Offset;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
        public static class Patch_Pawn_HealthTracker_RemoveHediff
        {
            [HarmonyPostfix]
            public static void Postfix(Hediff hediff, Pawn ___pawn)
            {
                if (___pawn != null)
                {
                    CompPawnInfo info = ___pawn.GetComp<CompPawnInfo>();

                    if (info != null)
                    {
                        HediffComp_HeadOffset offset = hediff.TryGetComp<HediffComp_HeadOffset>();
                        if (offset != null)
                        {
                            info.headOffset = null;
                        }
                    }
                }
            }
        }
    }
}
