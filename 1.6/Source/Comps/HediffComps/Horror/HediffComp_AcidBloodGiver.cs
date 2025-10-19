

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static HarmonyLib.Code;

namespace Xenomorphtype
{
    internal class HediffComp_AcidBloodGiver : HediffComp
    {
        float Influence = 0f;
        float LastUpdatedInfluence = 0f;
        int internalAcidCheckInterval => Mathf.CeilToInt( Props.hourIntervalCheck * 2500);
        HediffCompProperties_AcidBloodGiver Props => props as HediffCompProperties_AcidBloodGiver;
        public override void CompExposeData()
        {
        }

        public float GetBloodFullness()
        {
            float fullness = 1f;
           
            Hediff bloodloss = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodloss != null)
            {
                fullness = 1 - bloodloss.Severity;
                fullness *= Pawn.BodySize;
            }
            
            return fullness;
        }

        protected void TakeInternalDamage(float Damage)
        {
            if (Pawn.Map != null)
            {
                FleckMaker.ThrowSmoke(Pawn.DrawPos, Pawn.Map, 5);
            }
            IEnumerable<BodyPartRecord> source = from x in Pawn.health.hediffSet.GetNotMissingParts()
                                                 where
                                                 x.depth == BodyPartDepth.Inside
                                                 select x;

            foreach (BodyPartRecord record in source)
            {
                if (record != null)
                {
                    DamageWorker.DamageResult result = Pawn.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Damage, 999f, -1, Pawn, record));
                    if (result.totalDamageDealt > 0 && !result.deflected && Props.appliedHediff != null)
                    {
                        BodyPartRecord targetPart = result.LastHitPart;
                        if (Pawn.health.hediffSet.HasBodyPart(targetPart) || XMTUtility.GetPartAttachedToPartOnPawn(Pawn, ref targetPart))
                        {
                            Hediff burn = HediffMaker.MakeHediff(Props.appliedHediff, Pawn, targetPart);
                            burn.Severity = result.totalDamageDealt * Props.damageToSeverity;

                            Pawn.health.AddHediff(burn, targetPart);
                        }
                    }
                }
            }
        }
        protected void DamageBiter(Pawn bitingPawn, float Damage)
        {
            if (bitingPawn != null)
            {
                if (Pawn.Map != null)
                {
                    FleckMaker.ThrowSmoke(bitingPawn.DrawPos, bitingPawn.Map, 5);
                }
                IEnumerable<BodyPartRecord> source = from x in bitingPawn.health.hediffSet.GetNotMissingParts()
                                                     where
                                                    x.IsInGroup(ExternalDefOf.Mouth) || x.depth == BodyPartDepth.Inside
                                                     select x;


                foreach (BodyPartRecord record in source)
                {
                    if (record != null)
                    {
                        DamageWorker.DamageResult result = bitingPawn.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Damage, 999f, -1, Pawn, record));
                        if (result.totalDamageDealt > 0 && !result.deflected && Props.appliedHediff != null)
                        {
                            BodyPartRecord targetPart = result.LastHitPart;
                            if (bitingPawn.health.hediffSet.HasBodyPart(targetPart) || XMTUtility.GetPartAttachedToPartOnPawn(bitingPawn, ref targetPart))
                            {
                                Hediff burn = HediffMaker.MakeHediff(Props.appliedHediff, bitingPawn, targetPart);
                                burn.Severity = result.totalDamageDealt * Props.damageToSeverity;

                                bitingPawn.health.AddHediff(burn, targetPart);
                            }
                        }
                    }
                }

            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            float AcidBloodValue = Influence / Pawn.AcidBloodValue() ;
            if (dinfo.Def == null)
            {
                return;
            }

            if (!Pawn.Spawned)
            {
                return;
            }

            if (totalDamageDealt <= 2)
            {
                return;
            }

            if (dinfo.Def == DamageDefOf.Stun)
            {
                return;
            }

            float Damage = (AcidBloodValue * Props.damagePerSeverity);
            float SplashRange = AcidBloodValue * Props.splashRangePerSeverity;
            if (dinfo.Def == DamageDefOf.Bite)
            {
                if (dinfo.Instigator != null)
                {
                    DamageBiter(dinfo.Instigator as Pawn, Damage);
                }

                AcidUtility.TrySplashAcid(Pawn, GetBloodFullness(), SplashRange, 3, true, Props.appliedHediff, Props.damageToSeverity, Damage);
            }

            if (totalDamageDealt < 4)
            {
                return;
            }

            bool AutoBleed = (Pawn.health.hediffSet.BleedRateTotal > 0);
            


            if (dinfo.Def == DamageDefOf.Cut
               || dinfo.Def == DamageDefOf.ExecutionCut
               || dinfo.Def == DamageDefOf.Scratch
               || dinfo.Def == DamageDefOf.Crush
               || dinfo.Def == DamageDefOf.Stab
               || dinfo.Def == DamageDefOf.Bullet || AutoBleed
               )
            {

                float bloodfullness = GetBloodFullness();

                if (dinfo.Instigator != null && dinfo.Instigator.Position.AdjacentTo8WayOrInside(Pawn.PositionHeld))
                {

                    Pawn attacker = dinfo.Instigator as Pawn;
                    if (attacker != null)
                    {
                        if (attacker.equipment != null)
                        {
                            if (!AcidUtility.IsAcidImmune(attacker?.equipment?.Primary))
                            {
                                attacker.equipment.Primary.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, Damage + totalDamageDealt, 9, -1, Pawn));
                            }
                        }
                    }
                }
                AcidUtility.TrySplashAcid(Pawn, bloodfullness + (totalDamageDealt / 10), SplashRange, 5, true, Props.appliedHediff, Props.damageToSeverity, Damage);
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (parent.Severity != Influence)
            {
                PopulateInfluence();
            }

            if (Pawn.IsHashIntervalTick(internalAcidCheckInterval))
            {
                
                float AcidBloodValue = Pawn.AcidBloodValue();
                float SilicateValue = Pawn.CarboSilicate();
                float SkeletonizationValue = Pawn.MesoSkeletonization();

                float AdjustedValue = AcidBloodValue - (SkeletonizationValue + (SilicateValue/2));

                if (AdjustedValue < Props.burnSeverityThreshold)
                {
                    float localValue = (GetBloodFullness() * (Influence / AcidBloodValue) * AdjustedValue);
                    Hediff toxic = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);

                    if (toxic == null)
                    {
                       
                        toxic = HediffMaker.MakeHediff(HediffDefOf.ToxicBuildup, Pawn);
                        toxic.Severity = HediffDefOf.ToxicBuildup.minSeverity + Props.toxinBuildup * localValue;
                        Log.Message(Pawn + " getting toxic build up of " +toxic.Severity);
                        Pawn.health.AddHediff(toxic);
                    }
                    else
                    {
                        toxic.Severity += Props.toxinBuildup * localValue;
                    }
                }
                else
                {
                    float localValue = (GetBloodFullness() * (Influence / AcidBloodValue) * (AdjustedValue-Props.burnSeverityThreshold));
                    TakeInternalDamage(localValue * Props.damagePerSeverity);
                }
            }
        }

        public void PopulateInfluence()
        {

            LastUpdatedInfluence = Influence;

            Influence = parent.Severity;

            float InfluenceDelta = Influence - LastUpdatedInfluence;

            Pawn.UpdateAcidBloodValue(Mathf.Max(0, Pawn.AcidBloodValue() + InfluenceDelta));

        }

        public override void CompPostPostRemoved()
        {
            Pawn.UpdateAcidBloodValue(Mathf.Max(0, Pawn.AcidBloodValue() - Influence));
        }
    }

    public class HediffCompProperties_AcidBloodGiver : HediffCompProperties
    {
        public float toxinBuildup = 1f;
        public float burnSeverityThreshold = 0.25f;
        public float hourIntervalCheck = 1;
        public float damagePerSeverity = 26;
        public float splashRangePerSeverity = 5;
        public float damageToSeverity = 1;
        public HediffDef appliedHediff;
        public HediffCompProperties_AcidBloodGiver()
        {
            compClass = typeof(HediffComp_AcidBloodGiver);
        }
    }
}


