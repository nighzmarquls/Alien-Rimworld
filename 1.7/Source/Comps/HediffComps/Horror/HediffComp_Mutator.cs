using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class HediffComp_Mutator : HediffComp
    {
        public int mutateTick = -1;
        HediffCompProperties_Mutator Props => props as HediffCompProperties_Mutator;


        public override bool CompShouldRemove => parent.Severity <= parent.def.minSeverity;
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn == null || Pawn.health == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick > mutateTick)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("[XMT][Biohorror] " + parent + " is trying to mutate " + Pawn);
                }
               
                mutateTick = currentTick + Mathf.Max(1, Mathf.CeilToInt(Props.mutateHourInterval * 2500 / CryptimorphicInfluence.ExposureScale(Pawn.BodySize)));

                float chance = Props.useSeverityAsProbability ? parent.Severity : Props.probability;
                if (Rand.Chance(chance))
                {
                    // No mutation/target eligibility work until both the timer and cheap roll pass.
                    if (!CryptimorphicInfluence.CanMutate(Pawn)) return;
                    // Protofluid's risks are independent of whether the selected tissue is new or developing.
                    if (Props.toxicShockSeverity > 0f && Rand.Chance(Props.toxicShockChance))
                    {
                        float resistance = Mathf.Clamp01(Pawn.GetStatValue(StatDefOf.ToxicResistance));
                        if (resistance < 1f)
                            Pawn.health.GetOrAddHediff(HediffDefOf.ToxicBuildup).Severity += Props.toxicShockSeverity * (1f - resistance);
                        if (Pawn.Dead) return;
                    }
                    bool completedExisting = false;
                    if (Props.onlyOneMutation && Props.customMutationSet != null && Rand.Chance(Props.completionPreference))
                    {
                        var developing = Pawn.health.hediffSet.hediffs.Where(h => h.Severity < h.def.maxSeverity &&
                            Props.customMutationSet.mutations.Any(m => m.horror == h.def)).ToList();
                        if (developing.Count > 0)
                        {
                            Hediff mutation = developing.RandomElement();
                            mutation.Severity = Mathf.Min(mutation.def.maxSeverity, mutation.Severity + mutation.def.initialSeverity);
                            completedExisting = true;
                        }
                    }
                    if (!completedExisting) BioUtility.TryMutatingPawn(ref parent.pawn, Props.customMutationSet);
                    else if (Props.pregnancyProgressPerPulse > 0f)
                        HorrorPregnancyUtility.TryApply(Pawn, existingOnly: true);
                    // Corruption uses pre-acceleration gestation. Failed/newly exhausted tissue
                    // selection must not prevent an otherwise successful mutagenic pulse.
                    HorrorPregnancyUtility.AdvancePregnancies(Pawn, Props.pregnancyProgressPerPulse);
                }

                

            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref mutateTick, "mutateTick", -1);
        }
    }

    public class HediffCompProperties_Mutator : HediffCompProperties
    {
        public float completionPreference = 1f;
        public float pregnancyProgressPerPulse;
        public float toxicShockChance;
        public float toxicShockSeverity;
        public bool  onlyOneMutation = false;
        public float mutateHourInterval = 1;
        public float probability = 0.1f;
        public bool useSeverityAsProbability = false;
        public XMT_MutationsHealthSet customMutationSet = null;
        public HediffCompProperties_Mutator()
        {
            compClass = typeof(HediffComp_Mutator);
        }
    }
}
