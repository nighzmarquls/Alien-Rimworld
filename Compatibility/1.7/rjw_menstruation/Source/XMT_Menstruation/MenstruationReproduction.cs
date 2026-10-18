using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Xenomorphtype;
using XMT_RJW;

namespace XMT_Menstruation
{
    [StaticConstructorOnStartup]
    public static class MenstruationReproduction
    {
        static MenstruationReproduction()
        {
            if (!MenstruationApiShim.Ready)
            {
                Log.Warning("[XMT][Menstruation] The upstream womb API did not load. Check the legacy Unity forwarders and the installed RJW/Menstruation versions.");
                return;
            }
            new Harmony("XMT.Menstruation.Reproduction").PatchAll(typeof(MenstruationReproduction).Assembly);
        }

        [HarmonyPatch(typeof(RJWReproduction), nameof(RJWReproduction.TransferWomb))]
        private static class PreserveWomb
        {
            private static bool Prefix(Hediff source, Hediff replacement)
            { MenstruationApiShim.TransferWomb(source, replacement); return false; }
        }

        [HarmonyPatch(typeof(RJWReproduction), nameof(RJWReproduction.TryCreate))]
        private static class CreateInWomb
        {
            private static bool Prefix(Pawn recipient, Pawn donor, float essence, ref bool __result)
            {
                HediffComp womb = MenstruationApiShim.Wombs(recipient).FirstOrDefault(w => MenstruationApiShim.Pregnancy(w) == null);
                if (womb == null) { __result = false; return false; }
                Hediff pregnancy = RJWApiShim.CreateNative(recipient, donor);
                if (pregnancy != null)
                {
                    MenstruationApiShim.Assign(womb, pregnancy);
                    __result = pregnancy.TryGetComp<HediffComp_CorruptedPregnancy>()?.Corrupt(essence) == true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.UseVanillaGeneInspection))]
        private static class UseWombInterface
        {
            private static bool Prefix(ref bool __result) { __result = false; return false; }
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
        private static class ReleasedPregnancy
        {
            private static void Prefix(Hediff hediff, out List<HediffComp> __state)
            {
                __state = hediff?.TryGetComp<HediffComp_CorruptedPregnancy>() == null ? null :
                    MenstruationApiShim.Wombs(hediff.pawn).Where(w => MenstruationApiShim.Pregnancy(w) == hediff).ToList();
            }
            private static void Postfix(Hediff hediff, List<HediffComp> __state)
            {
                if (__state == null || __state.Count == 0) return;
                HediffDef next = hediff is Hediff_Pregnant ? RimWorld.HediffDefOf.PregnancyLabor :
                    hediff is Hediff_Labor ? RimWorld.HediffDefOf.PregnancyLaborPushing : null;
                Hediff successor = next == null ? null : hediff.pawn.health.hediffSet.hediffs.LastOrDefault(h => h.def == next);
                foreach (HediffComp womb in __state) MenstruationApiShim.Assign(womb, successor);
            }
        }

        [HarmonyPatch(typeof(HorrorPregnancyUtility), nameof(HorrorPregnancyUtility.TryHandleNativePregnancy))]
        private static class AdditionalWomb
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Pawn recipient, Pawn donor, float essence, bool existingOnly,
                ref bool result, ref bool __result)
            {
                if (existingOnly || donor == null || !RJWReproduction.NativePregnancies(recipient).Any()) return true;
                result = RJWReproduction.ConvertExisting(recipient, essence);
                if (BioUtility.CanGiveHorrorBirth(recipient) &&
                    MenstruationApiShim.Wombs(recipient).Any(w => MenstruationApiShim.Pregnancy(w) == null) &&
                    Rand.Chance(HorrorPregnancyUtility.Tuning.Chance(essence, false)))
                    result |= RJWReproduction.TryCreate(recipient, donor, essence);
                __result = true;
                return false;
            }
        }

        [HarmonyPatch]
        private static class SharedFetusGraphics
        {
            private static MethodBase TargetMethod() => AccessTools.Method(
                AccessTools.TypeByName("RJW_Menstruation.MenstruationUtility"), "GetSingleBabyImage");
            private static bool Prepare() => TargetMethod() != null;
            private static bool Prefix(Hediff pregnancy, ref string __result)
            {
                HediffComp_CorruptedPregnancy comp = pregnancy?.TryGetComp<HediffComp_CorruptedPregnancy>();
                if (comp?.corrupted != true) return true;
                if (!comp.discovered)
                {
                    Pawn baby = RJWApiShim.Babies(pregnancy)?.Cast<Pawn>().FirstOrDefault();
                    ThingDef race = baby == null ? pregnancy.pawn.def : comp.OriginalRace(baby) ?? pregnancy.pawn.def;
                    __result = race.modExtensions?.Select(e => RJWApiShim.Read(e, "fetusTexPath"))
                        .OfType<string>().FirstOrDefault() ?? "Fetus/Fetus_Default";
                    return false;
                }
                string path = HorrorPregnancyUtility.Tuning.fetusTexPath;
                // Art is optional while the user illustrates it; avoid missing-texture error spam.
                if (ContentFinder<Texture2D>.Get(path + "00", false) == null) return true;
                __result = path;
                return false;
            }
        }

        [HarmonyPatch]
        private static class DisguisedBabyInfo
        {
            private static MethodBase TargetMethod() => AccessTools.Method(
                AccessTools.TypeByName("RJW_Menstruation.PregnancyCommon"), "GetBabyInfo");
            private static bool Prepare() => TargetMethod() != null;
            private static bool Prefix(IEnumerable<Pawn> babies, ref string __result)
            {
                if (babies == null) return true;
                List<ThingDef> races = new List<ThingDef>();
                bool disguised = false;
                foreach (Pawn baby in babies)
                {
                    Pawn mother = baby.relations?.DirectRelations.FirstOrDefault(r =>
                        r.def == RimWorld.PawnRelationDefOf.ParentBirth)?.otherPawn;
                    HediffComp_CorruptedPregnancy comp = RJWReproduction.NativePregnancies(mother)
                        .Select(h => h.TryGetComp<HediffComp_CorruptedPregnancy>())
                        .FirstOrDefault(c => c.corrupted && !c.discovered && c.OriginalRace(baby) != null);
                    ThingDef race = comp?.OriginalRace(baby);
                    disguised |= race != null;
                    races.Add(race ?? baby.def);
                }
                if (!disguised) return true;
                __result = string.Join(", ", races.GroupBy(r => r).Select(g => g.Count() + " " + g.Key.LabelCap));
                return false;
            }
        }
    }
}
