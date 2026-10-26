using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public static class CryptimorphicInfluence
    {
        public static bool IsHybrid(Pawn pawn) => pawn?.def?.defName == "XMT_Hybrid_AlienRace";
        public static bool CanMutate(Pawn pawn) => pawn != null && !pawn.Dead && !pawn.IsInorganic() &&
            pawn.GetMorphComp() == null && (!pawn.IsHorror() || IsHybrid(pawn));
        public static float ExposureScale(float bodySize) => 1f / Mathf.Max(0.01f, float.IsNaN(bodySize) ? 1f : bodySize);
        public static float DonorPotency(Pawn donor) => donor == null ? 0f : donor.GetMorphComp() != null ? 1f :
            Mathf.Clamp01(BioUtility.GetXenomorphInfluence(donor) / CryptimorphicInfluenceDef.Current.perfectOrganismOffset);
        public static float BlendPotency(float oldVolume, float oldPotency, float addedVolume, float addedPotency) =>
            oldVolume + addedVolume <= 0 ? 0 : (oldVolume * oldPotency + addedVolume * addedPotency) / (oldVolume + addedVolume);
        public static Hediff_CryptimorphicExposure Exposure(Pawn pawn, bool create = false)
        {
            if (pawn?.health == null) return null;
            var hediff = pawn.health.hediffSet.hediffs.OfType<Hediff_CryptimorphicExposure>().FirstOrDefault();
            if (hediff == null && create)
                hediff = pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed("XMT_CryptimorphicExposure")) as Hediff_CryptimorphicExposure;
            return hediff;
        }
        public static void Expose(Pawn receiver, Pawn donor)
        {
            float strength = DonorPotency(donor) * CryptimorphicInfluenceDef.Current.normalDepositInfluence;
            if (strength <= 0) return;
            var exposure = Exposure(receiver, true);
            exposure.persistentInfluence = Mathf.Max(exposure.persistentInfluence, strength);
        }
        // Optional adapters refresh cached retained volumes on their simulation/deposition path, never via a map scan.
        public static float Contamination(Pawn pawn) => (Exposure(pawn)?.Influence ?? 0f) * ExposureScale(pawn?.BodySize ?? 1f);
        public static string Examine(Pawn pawn)
        {
            var exposure = Exposure(pawn);
            if (exposure == null || exposure.discovered || exposure.Influence <= 0) return string.Empty;
            exposure.discovered = true;
            return "XMT_ExposureFinding".Translate(pawn.Named("PAWN"));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool UsesRetainedFluids(Pawn pawn) => false;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool UseChildTransformation(Pawn mother, Hediff pregnancy) => false;

        public static bool DifferentForm(Pawn pawn, TransformationHorror candidate) => candidate != null &&
            (candidate.pawnTransformationTarget != null
                ? candidate.pawnTransformationTarget.race != pawn.def
                : candidate.thingTransformationTarget != null && candidate.thingTransformationTarget != pawn.def &&
                  candidate.thingTransformationTarget.category != ThingCategory.Pawn);
        public static IEnumerable<TransformationHorror> EligibleForms(Pawn pawn, float essence)
        {
            if (pawn == null) yield break;
            IEnumerable<TransformationHorror> candidates = pawn.def.GetModExtension<AnimalMutateForms>()?.resultCandidates ??
                Enumerable.Empty<TransformationHorror>();
            candidates = candidates.Concat(XenoGeneDefOf.XMT_DefaultTransformationSet.transformations);
            foreach (var candidate in candidates)
                if (DifferentForm(pawn, candidate) && candidate.probability > 0 &&
                    (candidate.essenceMinimum <= 0 || essence >= candidate.essenceMinimum) &&
                    (candidate.essenceMaximum < 0 || essence <= candidate.essenceMaximum)) yield return candidate;
        }
        public static bool HasDestination(Pawn pawn, float extra = 0, bool starting = false)
        {
            if (pawn == null) return false;
            float essence = BioUtility.GetXenomorphInfluence(pawn) + extra;
            Hediff division = pawn.health?.hediffSet.hediffs.FirstOrDefault(h => h.def.defName == "XMT_BecomingMutant");
            if (division != null || starting)
            {
                // Ask whether the process can finish, including its own completed influence.
                // Otherwise a growing condition could reject itself before its severity reaches 1.
                InfluenceHealth influence = XenoGeneDefOf.XMT_InfluencesSet.influences.FirstOrDefault(i => i.hediff.defName == "XMT_BecomingMutant");
                if (influence != null) essence += influence.Contribution(1f) - (division == null ? 0f : influence.Contribution(division.Severity));
            }
            return EligibleForms(pawn, essence).Any();
        }
    }
}
