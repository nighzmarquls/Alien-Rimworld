using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTHealthPatches
    {
        [HarmonyPatch(typeof(HediffComp_SeverityPerDay), nameof(HediffComp_SeverityPerDay.CompExposeData))]
        public static class Patch_ProtofluidSavedDecay
        {
            public static void Postfix(HediffComp_SeverityPerDay __instance, ref float ___severityPerDay)
            {
                // Vanilla saves the calculated rate, so pre-update exposures otherwise retain -1/day.
                if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                    __instance.parent.def.hediffClass != null &&
                    typeof(Hediff_Protofluid).IsAssignableFrom(__instance.parent.def.hediffClass))
                    ___severityPerDay = ((HediffCompProperties_SeverityPerDay)__instance.props).severityPerDay;
            }
        }

        [HarmonyPatch]
        public static class Patch_MutationInjuryHealingEligibility
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(HediffUtility), nameof(HediffUtility.CanHealNaturally));
                yield return AccessTools.Method(typeof(HediffUtility), nameof(HediffUtility.CanHealFromTending));
            }

            public static bool Prefix(Hediff_Injury __0, ref bool __result)
            {
                if (!Hediff_MutationInjury.IsMutationInjury(__0)) return true;
                __result = false;
                return false;
            }
        }

        // Saved injuries retain their serialized class. Protect them until naturally replaced,
        // without rebuilding hediffs and losing their saved promotion/comp state.
        [HarmonyPatch(typeof(Hediff_Injury), nameof(Hediff_Injury.Heal))]
        public static class Patch_LegacyMutationInjuryHealing
        {
            public static bool Prefix(Hediff_Injury __instance) => !Hediff_MutationInjury.IsMutationInjury(__instance);
        }

        [HarmonyPatch(typeof(Hediff_Injury), nameof(Hediff_Injury.TryMergeWith))]
        public static class Patch_LegacyMutationInjuryMerge
        {
            public static bool Prefix(Hediff_Injury __instance, Hediff __0, ref bool __result)
            {
                if (!Hediff_MutationInjury.IsMutationInjury(__instance)) return true;
                __result = Hediff_MutationInjury.MergeMutation(__instance, __0);
                return false;
            }
        }

        [HarmonyPatch]
        public static class Patch_Toils_Tend_FinalizeTend
        {
            public static MethodBase TargetMethod()
            {
                // FinalizeTend's factory only creates this callback; the DoTend call is in its body.
                return typeof(Toils_Tend).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    .Single(method => method.Name.StartsWith("<FinalizeTend>"));
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                MethodInfo doTend = AccessTools.Method(typeof(TendUtility), nameof(TendUtility.DoTend));
                MethodInfo guardedTend = AccessTools.Method(typeof(Patch_Toils_Tend_FinalizeTend), nameof(TendAndKeepJob));
                foreach (CodeInstruction instruction in instructions)
                {
                    if (!instruction.Calls(doTend))
                    {
                        yield return instruction;
                        continue;
                    }

                    instruction.opcode = OpCodes.Call;
                    instruction.operand = guardedTend;
                    yield return instruction;
                    Label continueTending = generator.DefineLabel();
                    yield return new CodeInstruction(OpCodes.Brtrue, continueTending);
                    yield return new CodeInstruction(OpCodes.Ret);
                    CodeInstruction continuation = new CodeInstruction(OpCodes.Nop);
                    continuation.labels.Add(continueTending);
                    yield return continuation;
                }
            }

            public static bool TendAndKeepJob(Pawn doctor, Pawn patient, Medicine medicine)
            {
                Job job = doctor?.CurJob;
                JobDriver driver = doctor?.jobs?.curDriver;
                TendUtility.DoTend(doctor, patient, medicine);
                // Damage/removal callbacks can end self-tending or interrupt the doctor.
                // The old callback must not read or modify a replacement job's targets.
                return job != null && doctor.CurJob == job && doctor.jobs.curDriver == driver;
            }
        }

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
                if (__instance.pawn == null)
                {
                    return true;
                }

                if (hediff is Hediff_MissingPart missingPart)
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

                if (XMTUtility.IsXenomorph(__instance.pawn))
                {
                    if(hediff.Part == null)
                    {

                    }
                }

                if (hediff.def == HediffDefOf.CoveredInFirefoam)
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
                        // Convert on the pawn tick: parents/genes may be assigned after AddDirect.
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

                    PawnCacheWrapper.Recache(pawn);
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

                    PawnCacheWrapper.Recache(___pawn);
                }
            }
        }
    }
}
