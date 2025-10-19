using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;


namespace Xenomorphtype
{ 
    public class CompAcidBlood : ThingComp
    {
        bool IsParentPawn = false;

        Pawn Parent = null;
        CompAcidBloodProperties Props => props as CompAcidBloodProperties;

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            Parent = parent as Pawn;
            if (Parent != null)
            {
                IsParentPawn = true;
                
            }
        }

        public float GetBloodFullness()
        {
            float fullness = 1f;
            if(IsParentPawn)
            {
                Hediff bloodloss = Parent.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodloss != null)
                {
                    fullness = 1 - bloodloss.Severity;
                    fullness *= Parent.BodySize;
                }
            }
            return fullness*(parent.HitPoints/parent.MaxHitPoints);
        }
        public bool TrySplashAcidThing(float severity, Thing thing)
        {
            return AcidUtility.TrySplashAcidThing(parent, severity,thing, Props.damage, Props.appliedHediff, Props.damageToSeverity);
        }

        public void TrySplashAcidCell(IntVec3 cell)
        {
            AcidUtility.TrySplashAcidCell(parent, parent.MapHeld, cell, GetBloodFullness(), Props.appliedHediff, Props.damageToSeverity, Props.damage);
        }

        public void TrySplashAcid(float severity)
        {
            AcidUtility.TrySplashAcid(parent, severity, Props.splashRange, Props.cells, false, Props.appliedHediff, Props.damageToSeverity, Props.damage);
        }

        public void CreateAcidExplosion(float radius)
        {
            AcidUtility.TrySplashAcid(parent, 1, Props.splashRange, Props.cells, false, Props.appliedHediff, Props.damageToSeverity, Props.damage);
        }
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.AcidBurn || dinfo.Def == ExternalDefOf.AG_AcidSpit)
            {
                absorbed = true;
                return;
            }
            base.PostPreApplyDamage(ref dinfo, out absorbed);
        }

        public void DamageBiter(Pawn bitingPawn)
        {
            if (bitingPawn != null)
            {
                FleckMaker.ThrowSmoke(bitingPawn.DrawPos, bitingPawn.Map, 5);
                IEnumerable<BodyPartRecord> source = from x in bitingPawn.health.hediffSet.GetNotMissingParts()
                                                     where
                                                    x.IsInGroup(ExternalDefOf.Mouth) || x.depth == BodyPartDepth.Inside
                                                     select x;

                float Damage = Props.damage;
                foreach (BodyPartRecord record in source)
                {
                    if (record != null)
                    {
                        DamageWorker.DamageResult result = bitingPawn.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Damage, 999f, -1, parent, record));
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
                        Damage -= (Props.damage / source.Count());
                    }
                }
               
            }
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            if (dinfo.Def == null)
            {
                return;
            }

            if (!parent.Spawned)
            {
                return;
            }

            if (totalDamageDealt <= 2)
            {
                return;
            }

            if(dinfo.Def == DamageDefOf.Stun)
            {
                return;
            }

            if(dinfo.Def == DamageDefOf.Bite)
            {
                if (dinfo.Instigator != null)
                {
                    DamageBiter(dinfo.Instigator as Pawn);
                }

                AcidUtility.TrySplashAcid(parent,GetBloodFullness(), Props.splashRange,Props.cells,true, Props.appliedHediff, Props.damageToSeverity, Props.damage);
            }

            if(totalDamageDealt < 4)
            {
                return;
            }

            bool AutoBleed = false;
            if(IsParentPawn)
            {
                AutoBleed = (Parent.health.hediffSet.BleedRateTotal > 0);
            }
            else
            {
                AutoBleed = parent.MaxHitPoints > parent.HitPoints;
            }

            if (dinfo.Def == DamageDefOf.Cut
               || dinfo.Def == DamageDefOf.ExecutionCut
               || dinfo.Def == DamageDefOf.Scratch
               || dinfo.Def == DamageDefOf.Crush
               || dinfo.Def == DamageDefOf.Stab
               || dinfo.Def == DamageDefOf.Bullet || AutoBleed
               )
            {
                
                float bloodfullness = GetBloodFullness();

                if (dinfo.Instigator != null && dinfo.Instigator.Position.AdjacentTo8WayOrInside(parent.PositionHeld))
                {
   
                    Pawn attacker = dinfo.Instigator as Pawn;
                    if (attacker != null)
                    {

                        if (attacker.equipment != null)
                        {
                            if (!AcidUtility.IsAcidImmune(attacker?.equipment?.Primary))
                            {
                                attacker.equipment.Primary.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, Props.damage + totalDamageDealt, 9, -1, parent));
                            }
                        }
                    }
                }
                AcidUtility.TrySplashAcid(parent,bloodfullness + (totalDamageDealt / 10),Props.splashRange,Props.cells,true, Props.appliedHediff, Props.damageToSeverity, Props.damage);
            }
        }

        internal float GetDamage()
        {
            return Props.damage;
        }
    }


    public class CompAcidBloodProperties : CompProperties
    {
        public float splashRange = 1.5f;
        public int  cells = 5;
        public float damage = 26f;
        public float damageToSeverity = 1f;
        public float bloodLossSeverity = 0.05f;
        public HediffDef appliedHediff;
        public ThingDef  acidFilth = null;
        public CompAcidBloodProperties()
        {
            this.compClass = typeof(CompAcidBlood);
        }

    }
}
