using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Xenomorphtype;
using XMT_RJW;

namespace XMT_Menstruation
{
    public class MenstruationContaminationDef : Def
    {
        public float childDerivedProgress = 0.5f;
        public static MenstruationContaminationDef Current => DefDatabase<MenstruationContaminationDef>.GetNamed("XMT_MenstruationContamination");
    }

    // 1.7 SHIM: all Cum/retained-fluid assumptions target the installed 1.6 API.
    // The native entries retain donor references, but potency is saved only on our receiver hediff.
    public static class MenstruationContamination
    {
        private static IEnumerable<object> Fluids(HediffComp womb) =>
            (AccessTools.Method(MenstruationApiShim.WombType, "GetFluids", Type.EmptyTypes)?.Invoke(womb, null) as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
        private static float Volume(object fluid) => Convert.ToSingle(RJWApiShim.Read(fluid, "Volume") ?? 0f);
        private static bool IsSemen(object fluid) => !(bool)(RJWApiShim.Read(fluid, "notcum") ?? true);
        private static string Key(HediffComp womb, object fluid) => womb.parent.GetUniqueLoadID() + ":" +
            ((RJWApiShim.Read(fluid, "pawn") as Pawn)?.GetUniqueLoadID() ?? "unknown");
        private static ContaminationDeposit Record(Hediff_CryptimorphicExposure exposure, string key)
        {
            var record = exposure.deposits.FirstOrDefault(d => d.key == key);
            if (record == null) { record = new ContaminationDeposit { key = key }; exposure.deposits.Add(record); }
            return record;
        }
        public static void Refresh(Pawn pawn)
        {
            var exposure = CryptimorphicInfluence.Exposure(pawn);
            if (exposure == null) return;
            // Read only this receiver's wombs; old material without a record is deliberately inert.
            var present = new HashSet<string>();
            foreach (HediffComp womb in MenstruationApiShim.Wombs(pawn))
                foreach (object fluid in Fluids(womb).Where(IsSemen))
                {
                    string key = Key(womb, fluid);
                    present.Add(key);
                    var record = exposure.deposits.FirstOrDefault(d => d.key == key);
                    if (record != null) record.volume = Volume(fluid);
                }
            exposure.deposits.RemoveAll(d => !present.Contains(d.key));
        }

        public sealed class DepositContext
        {
            public HediffComp womb;
            public Pawn donor;
            public float potency;
            public HashSet<object> before;
            public DepositContext previous;
        }
        [ThreadStatic] private static DepositContext current;

        [HarmonyPatch]
        private static class Deposit
        {
            private static MethodBase TargetMethod() => AccessTools.Method(MenstruationApiShim.WombType, "CumIn", new[] { typeof(Pawn), typeof(float), typeof(float), typeof(bool) });
            private static bool Prepare() => MenstruationApiShim.Ready && TargetMethod() != null;
            private static void Prefix(object __instance, Pawn cummer, out DepositContext __state)
            {
                var womb = (HediffComp)__instance;
                __state = new DepositContext { womb = womb, donor = cummer, potency = CryptimorphicInfluence.DonorPotency(cummer),
                    before = new HashSet<object>(Fluids(womb)), previous = current };
                current = __state;
            }
            private static void Postfix(DepositContext __state)
            {
                current = __state.previous;
                Pawn receiver = __state.womb.Pawn;
                if (__state.potency > 0)
                    foreach (object fluid in Fluids(__state.womb).Where(f => !__state.before.Contains(f) && IsSemen(f) &&
                        RJWApiShim.Read(f, "pawn") == __state.donor))
                    {
                        var record = Record(CryptimorphicInfluence.Exposure(receiver, true), Key(__state.womb, fluid));
                        record.potency = __state.potency;
                        record.volume = Volume(fluid);
                    }
                Refresh(receiver);
                // Evaluate only after the native method accepted/merged its material. A pull-out cannot add exposure.
                if (CryptimorphicInfluence.Contamination(receiver) > 0)
                    HorrorPregnancyUtility.TryApply(receiver, __state.donor);
            }
            private static Exception Finalizer(Exception __exception, DepositContext __state)
            {
                if (__state != null) current = __state.previous;
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class Merge
        {
            private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("RJW_Menstruation.Cum"), "MergeWithCum");
            private static bool Prepare() => TargetMethod() != null;
            private static void Prefix(object __instance, float volumein)
            {
                DepositContext context = current;
                if (context == null || volumein <= 0 || !IsSemen(__instance)) return;
                Pawn receiver = context.womb.Pawn;
                var exposure = CryptimorphicInfluence.Exposure(receiver);
                if (exposure == null && context.potency <= 0) return;
                exposure ??= CryptimorphicInfluence.Exposure(receiver, true);
                var record = Record(exposure, Key(context.womb, __instance));
                record.potency = CryptimorphicInfluence.BlendPotency(Volume(__instance), record.potency, volumein, context.potency);
            }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.AdditionalEssence))]
        private static class ReadExposure
        {
            private static void Prefix(Pawn recipient) { if (recipient != null) Refresh(recipient); }
        }
        [HarmonyPatch(typeof(RJWReproduction), nameof(RJWReproduction.HasRetainedFluidAdapter))]
        private static class Retention
        {
            private static bool Prefix(Pawn pawn, ref bool __result)
            { __result = pawn != null && MenstruationApiShim.Wombs(pawn).Any(); return false; }
        }
        [HarmonyPatch(typeof(CryptimorphicInfluence), nameof(CryptimorphicInfluence.UseChildTransformation))]
        private static class ChildBranch
        {
            private static bool Prefix(Pawn mother, Hediff pregnancy, ref bool __result)
            {
                bool owned = MenstruationApiShim.Wombs(mother).Any(w => MenstruationApiShim.Pregnancy(w) == pregnancy);
                // StageProgress is intentionally not used: discovery/UI settings can disguise it.
                float progress = Convert.ToSingle(RJWApiShim.Read(pregnancy, "GestationProgress") ?? pregnancy.Severity);
                __result = owned && progress >= MenstruationContaminationDef.Current.childDerivedProgress;
                return false;
            }
        }
        [HarmonyPatch]
        private static class Clearance
        {
            private static MethodBase TargetMethod() => AccessTools.Method(MenstruationApiShim.WombType, "CompPostTick");
            private static bool Prepare() => MenstruationApiShim.Ready && TargetMethod() != null;
            private static void Postfix(HediffComp __instance)
            {
                if (__instance.Pawn.IsHashIntervalTick(250)) Refresh(__instance.Pawn);
            }
        }
    }
}
