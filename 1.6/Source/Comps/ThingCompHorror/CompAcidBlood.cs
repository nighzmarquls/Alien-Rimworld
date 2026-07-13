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
            return AcidUtility.GetBloodFullness(parent);
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
            AcidUtility.TrySplashAcid(parent, severity, Props.splashRange, Props.cells, false, Props.appliedHediff, Props.damageToSeverity, Props.damage, Props.knowledgeProfile);
        }

        public void CreateAcidExplosion(float radius)
        {
            AcidUtility.TrySplashAcid(parent, 1, Props.splashRange, Props.cells, false, Props.appliedHediff, Props.damageToSeverity, Props.damage, Props.knowledgeProfile);
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
            AcidUtility.DamageBiter(parent, bitingPawn, Props.damage, Props.appliedHediff, Props.damageToSeverity, true);
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

                AcidUtility.TrySplashAcid(parent,GetBloodFullness(), Props.splashRange,Props.cells,true, Props.appliedHediff, Props.damageToSeverity, Props.damage, Props.knowledgeProfile);
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

            if (AcidUtility.IsAcidSplashDamage(dinfo, AutoBleed))
            {
                
                float bloodfullness = GetBloodFullness();

                AcidUtility.TryDamageAdjacentWeapon(parent, dinfo.Instigator, Props.damage + totalDamageDealt);
                AcidUtility.TrySplashAcidFromWound(parent, bloodfullness, totalDamageDealt, Props.splashRange, Props.cells, Props.appliedHediff, Props.damageToSeverity, Props.damage, Props.knowledgeProfile);
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
        public KnowledgeProfileDef knowledgeProfile;
        public CompAcidBloodProperties()
        {
            this.compClass = typeof(CompAcidBlood);
        }

    }
}
