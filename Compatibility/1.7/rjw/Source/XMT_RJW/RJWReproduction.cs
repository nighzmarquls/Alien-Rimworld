using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace XMT_RJW
{
    [StaticConstructorOnStartup]
    public static class RJWReproduction
    {
        static RJWReproduction()
        {
            if (!RJWApiShim.Ready)
            {
                Log.Warning("[XMT][RJW] No compatible RJW pregnancy API loaded. The XMT adapter requires a working RJW build for this game version.");
                return;
            }
            foreach (HediffDef def in DefDatabase<HediffDef>.AllDefsListForReading.Where(RJWApiShim.Supports))
            {
                def.comps ??= new List<HediffCompProperties>();
                if (!def.comps.Any(c => c is HediffCompProperties_CorruptedPregnancy))
                    def.comps.Add(new HediffCompProperties_CorruptedPregnancy());
                def.modExtensions ??= new List<DefModExtension>();
                if (!def.modExtensions.OfType<MutationTissueExtension>().Any())
                    def.modExtensions.Add(RJWReproductionDef.Current.corruptedPregnancyPresentation);
            }
            new Harmony("XMT.RJW.Reproduction").PatchAll(typeof(RJWReproduction).Assembly);
        }

        public static IEnumerable<Hediff> NativePregnancies(Pawn pawn) =>
            pawn?.health?.hediffSet?.hediffs.Where(h => h.TryGetComp<HediffComp_CorruptedPregnancy>() != null)
                ?? Enumerable.Empty<Hediff>();

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.TryAdvanceNativePregnancy))]
        private static class MutatorPregnancyProgress
        {
            private static bool Prefix(Hediff pregnancy, float progress, ref bool __result)
            {
                __result = RJWApiShim.AdvanceProgress(pregnancy, progress, Find.TickManager.TicksGame);
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void TransferWomb(Hediff source, Hediff replacement) { }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static bool TryCreate(Pawn recipient, Pawn donor, float essence)
        {
            Hediff created = RJWApiShim.CreateNative(recipient, donor);
            return created?.TryGetComp<HediffComp_CorruptedPregnancy>()?.Corrupt(essence) == true;
        }

        public static bool ConvertExisting(Pawn recipient, float essence)
        {
            bool result = false;
            foreach (Hediff pregnancy in NativePregnancies(recipient).ToList())
            {
                HediffComp_CorruptedPregnancy comp = pregnancy.TryGetComp<HediffComp_CorruptedPregnancy>();
                if (comp.corrupted || Rand.Chance(HorrorPregnancyUtility.Tuning.Chance(essence, true)))
                    result |= comp.Corrupt(essence);
            }
            return result;
        }

        [HarmonyPatch(typeof(BioUtility), nameof(BioUtility.CanGiveHorrorBirth))]
        private static class Anatomy
        {
            private static bool Prefix(Pawn pawn, ref bool __result)
            {
                __result = RJWApiShim.HasFemaleParts(pawn);
                return false;
            }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.TryHandleNativePregnancy))]
        private static class ExistingPregnancy
        {
            private static bool Prefix(Pawn recipient, Pawn donor, float essence, bool existingOnly,
                ref bool result, ref bool __result)
            {
                if (NativePregnancies(recipient).Any()) result = ConvertExisting(recipient, essence);
                else if (!existingOnly && BioUtility.CanGiveHorrorBirth(recipient) &&
                    Rand.Chance(HorrorPregnancyUtility.Tuning.Chance(essence, false)))
                    result = TryCreate(recipient, donor, essence);
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.ShouldDeferMotherTransformation))]
        private static class ProtectMother
        {
            private static bool Prefix(Pawn pawn, ref bool __result)
            {
                // Includes all active native pregnancies, not only already-corrupted ones.
                __result = NativePregnancies(pawn).Any();
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static bool HasRetainedFluidAdapter(Pawn pawn) => false;

        [HarmonyPatch(typeof(CryptimorphicInfluence), nameof(CryptimorphicInfluence.UsesRetainedFluids))]
        private static class ExplicitDeposits
        {
            private static bool Prefix(ref bool __result) { __result = true; return false; }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.ApplyReproductiveMutation))]
        private static class LovinMutation
        {
            private static bool Prefix(Pawn recipient, Pawn partner)
            {
                RJWReproductionDef tuning = RJWReproductionDef.Current;
                float essence = HorrorPregnancyUtility.EssenceLoad(recipient, partner);
                if (essence <= 0f || !Rand.Chance(tuning.lovinMutationChance)) return false;
                BodyPartRecord site = RJWApiShim.Genitals(recipient);
                if (site == null || recipient.health.hediffSet.PartIsMissing(site)) return false;
                Hediff existing = recipient.health.hediffSet.hediffs.FirstOrDefault(h => h.def == tuning.reproductiveTissue && h.Part == site);
                if (existing != null) existing.Severity = Mathf.Min(1f, existing.Severity + tuning.repeatedLovinGrowth);
                else
                {
                    MutationHealth mutation = BioUtility.AllMutationsForSet(DefDatabase<XMT_MutationsHealthSet>.GetNamed("XMT_LovinMutationSet"))
                        .FirstOrDefault(m => m.horror == tuning.reproductiveTissue);
                    if (mutation != null) BioUtility.TryApplyMutation(recipient, mutation, out _, feedback: false);
                }
                return false;
            }
        }

        [HarmonyPatch]
        private static class NativeBirth
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                DefDatabase<HediffDef>.AllDefsListForReading.Where(RJWApiShim.Supports)
                    .Select(d => AccessTools.Method(d.hediffClass, "GiveBirth"))
                    .Where(m => m != null && !m.IsAbstract).Distinct();

            private static bool Prefix(Hediff __instance)
            {
                HediffComp_CorruptedPregnancy comp = __instance.TryGetComp<HediffComp_CorruptedPregnancy>();
                if (comp?.corrupted != true) return true;
                // 1.7 SHIM: legacy human delivery assumes human story/guest trackers. Horror
                // babies need the common release helper instead; retain the source until all release.
                comp.Deliver();
                return false;
            }
        }

        [HarmonyPatch]
        private static class FluidExposure
        {
            private static MethodBase TargetMethod() => AccessTools.Method(
                AccessTools.TypeByName("rjw.SexUtility"), "IngestFluids");
            private static bool Prepare() => TargetMethod() != null;
            private static void Prefix(object[] __args, out float __state)
            {
                // 1.7 SHIM: capture the amount before upstream deducts donor nutrition.
                Hediff source = __args[2] as Hediff ?? RJWApiShim.Read(__args[2], "AsHediff") as Hediff;
                HediffComp part = (source as HediffWithComps)?.comps?.FirstOrDefault(c => c.GetType().Name == "HediffComp_SexPart");
                object amount = RJWApiShim.Read(part, "FluidAmount");
                __state = amount == null ? 0f : Convert.ToSingle(amount);
                object cost = RJWApiShim.Read(RJWApiShim.Read(part, "Fluid"), "baseNutritionCost");
                float nutritionCost = cost == null ? 0f : Convert.ToSingle(cost);
                var food = (__args[1] as Pawn)?.needs?.food;
                if (nutritionCost > 0f && food != null)
                    __state = Mathf.Min(__state * nutritionCost, food.CurLevel) / nutritionCost;
            }
            private static void Postfix(object[] __args, float __state)
            {
                Pawn receiver = __args[0] as Pawn;
                Pawn giver = __args[1] as Pawn;
                object fromPart = __args[2];
                object toPart = __args[3];
                // Genital/internal transfer only; oral ingestion is a separate gameplay system.
                if (receiver == null || toPart == null || __state <= 0f || !HorrorPregnancyUtility.Enabled) return;
                Hediff source = fromPart as Hediff;
                HediffComp part = (source as HediffWithComps)?.comps?.FirstOrDefault(c => c.GetType().Name == "HediffComp_SexPart");
                Def fluid = RJWApiShim.Read(part, "Fluid") as Def;
                CorruptingFluid contribution = RJWReproductionDef.Current.fluids.FirstOrDefault(f => f.fluid == fluid?.defName);
                if (contribution == null || contribution.essence <= 0f) return;
                if (HasRetainedFluidAdapter(receiver)) return;
                // RJW without retained-fluid simulation uses the core strongest persistent exposure.
                float strength = __state / CryptimorphicInfluenceDef.Current.normalDepositMl *
                    contribution.essence * CryptimorphicInfluence.DonorPotency(giver);
                if (strength <= 0f) return;
                var exposure = CryptimorphicInfluence.Exposure(receiver, true);
                exposure.persistentInfluence = Mathf.Max(exposure.persistentInfluence, strength);
                HorrorPregnancyUtility.TryApply(receiver, giver);
            }
        }
    }
}
