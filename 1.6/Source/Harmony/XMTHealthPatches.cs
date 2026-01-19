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
        [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
        public static class Patch_TendUtility_DoTend
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn doctor, Pawn patient, Medicine medicine)
            {
                if (medicine == null)
                {
                    if (XMTUtility.IsXenomorph(patient))
                    {
                        return;
                    }
                    if (XMTUtility.IsXenomorph(doctor))
                    {
                        BioUtility.TryMutatingPawn(ref patient);
                    }
                    return;
                }
                else
                {

                    if (medicine.Destroyed)
                    {
                        return;
                    }

                    if (XMTUtility.IsXenomorph(patient))
                    {
                        return;
                    }

                    if (medicine.def == InternalDefOf.Starbeast_Jelly || XMTUtility.IsXenomorph(doctor))
                    {
                        BioUtility.TryMutatingPawn(ref patient);
                    }
                }
            }
        }
        [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
        public static class Patch_HediffSet_AddDirect
        {
            [HarmonyPrefix]
            public static bool Prefix(Hediff hediff, HediffSet __instance)
            {
                if(hediff is Hediff_MissingPart missingPart)
                {
                    IEnumerable<HediffComp_PawnAttachement> attachments = __instance.GetHediffComps<HediffComp_PawnAttachement>();
                    foreach(HediffComp_PawnAttachement attachment in attachments)
                    { 
                        if(attachment.parent.Part != missingPart.Part)
                        {
                            continue;
                        }
                        attachment.PawnRelease();
                        return true;
                    }
                }

                if (__instance.pawn != null && hediff.def == HediffDefOf.CoveredInFirefoam)
                {
                    if(__instance.pawn.Info() is CompPawnInfo info)
                    {
                        info.CleanPheramones(1);
                    }
                }
                return true;
            }

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
                    CompPawnInfo info = pawn.Info();

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
                    CompPawnInfo info = ___pawn.Info();

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
