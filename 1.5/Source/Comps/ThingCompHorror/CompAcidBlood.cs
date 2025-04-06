using AlienRace;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{ 
    public class CompAcidBlood : ThingComp
    {
        bool IsParentPawn = false;

        Pawn Parent = null;
        CompAcidBloodProperties Props => props as CompAcidBloodProperties;

        public override void CompTick()
        {
            base.CompTick();

            if (IsParentPawn)
            {
                if (!Parent.health.CanBleed)
                {
                    return;
                }

                if (Parent.health.hediffSet.HasHediff(HediffDefOf.BloodfeederMark))
                {
                    Hediff bloodmark = Parent.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodfeederMark);
                    Parent.health.RemoveHediff(bloodmark);
                    IEnumerable<Pawn> bystanders = GenRadial.RadialDistinctThingsAround(Parent.PositionHeld, Parent.MapHeld, 1.5f, true).OfType<Pawn>();

                    foreach (Pawn bystander in bystanders)
                    {
                        if (bystander == Parent || XMTUtility.IsXenomorph(bystander))
                        {
                            continue;
                        }

                        if (bystander.IsBloodfeeder())
                        {
                            bystander.ClearAllReservations();
                            bystander.jobs.StopAll();
                            DamageBiter(bystander);

                        }
                    }
                    TrySplashAcid(GetBloodFullness());
                }
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
        }
        public override void CompTickRare()
        {
            base.CompTickRare();

            if (IsParentPawn)
            {
                if(!Parent.health.CanBleed)
                {
                    return;
                }

                Pawn other = null;
                Bill_Medical bill = null;
                float progress = -1;

                if(XMTUtility.IsPawnInSurgery(Parent,out other,out progress, out bill))
                {
                    if (bill != null)
                    {
                        Parent.health.AddHediff(HediffDefOf.SurgicalCut, bill.Part,new DamageInfo(DamageDefOf.SurgicalCut,1,instigator: other));
                    }
                    if (other != null)
                    {
                        other.ClearAllReservations();
                        other.jobs.StopAll();
                    }
                    TrySplashAcid(GetBloodFullness());
                }

                if(Parent.health.hediffSet.BleedRateTotal > 0)
                {
                    TrySplashAcid(GetBloodFullness());
                }
            }
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
            else
            {
                fullness = parent.HitPoints / parent.MaxHitPoints;
            }
            return fullness;
        }

        public void CreateAcidExplosion(float radius)
        {
            TrySplashAcid(1, radius, false);
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

        protected void DamageBiter(Pawn bitingPawn)
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
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

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

            if(dinfo.Def == DamageDefOf.Bite)
            {
                if (dinfo.Instigator != null)
                {
                    DamageBiter(dinfo.Instigator as Pawn);
                }

                TrySplashAcid(GetBloodFullness());
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
                AutoBleed = parent.MaxHitPoints < parent.HitPoints;
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
                            if (!XMTUtility.IsAcidImmune(attacker?.equipment?.Primary))
                            {
                                attacker.equipment.Primary.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, Props.damage * bloodfullness, 9, -1, parent));
                            }
                        }
                    }
                }
                TrySplashAcid(bloodfullness);
            }
        }

        public void TrySplashAcidCell(IntVec3 cell)
        {
            if (IsParentPawn)
            {
                if (Parent == null || Parent.Dead)
                {
                    return;
                }
            }

            TryBloodLoss();

            Thing thing = cell.GetEdifice(parent.MapHeld);
           
            if (thing == null)
            {
                IEnumerable<Thing> splashedThings = from x in GenRadial.RadialDistinctThingsAround(cell, parent.MapHeld, 1.5f, true)
                                                    where !(x is Mote) && !(x is Filth) && !(x is Corpse)
                                                    select x;

                if (splashedThings.Any())
                {
                    thing = splashedThings.First();
                }
            }

            if(thing != null)
            {
                TrySplashAcidThing(GetBloodFullness(), thing);
            }
            else
            {
                FilthMaker.TryMakeFilth(cell, parent.MapHeld, Props.acidFilth);
            }

        }
        public bool TrySplashAcidThing(float severity, Thing thing)
        {
            if (IsParentPawn)
            {
                if (Parent == null || Parent.Dead)
                {
                    return false;
                }
            }
            float modifiedDamage = Props.damage * severity;
            float pristineDamage = modifiedDamage;
            if (thing != parent && thing != null)
            {
                if (!XMTUtility.IsAcidImmune(thing))
                {
                    IntVec3 SplashCell = thing.PositionHeld;

                    bool acidHit = true;
                    bool inanimateDamage = false;
                    if (!XMTUtility.IsTargetImmobile(thing))
                    {
                        if (Rand.Chance(XMTUtility.GetDodgeChance(thing, false)))
                        {
                            MoteMaker.ThrowText(thing.DrawPos, thing.Map, "TextMote_Dodge".Translate(), 1.9f);
                            acidHit = false;
                        }

                    }
                    else
                    {
                        inanimateDamage = true;
                    }

                    if (acidHit && thing != null)
                    {
                        Pawn thingAsPawn = thing as Pawn;

                        if (thingAsPawn != null)
                        {
                            IEnumerable<BodyPartRecord> acidContact = thingAsPawn.health.hediffSet.GetNotMissingParts().Where(x => x.depth != BodyPartDepth.Inside && x.def.hitPoints >= modifiedDamage);

                            BodyPartRecord hitPart = acidContact.RandomElement();
                            bool unblocked = true;
                            if (thingAsPawn.apparel != null)
                            {

                                List<Apparel> wornThings = thingAsPawn.apparel.WornApparel;
                                foreach (Apparel w in wornThings)
                                {
                                    if (w.def.apparel.CoversBodyPart(hitPart))
                                    {
                                        if (XMTUtility.IsAcidImmune(w))
                                        {
                                            if (w.Stuff == InternalDefOf.Starbeast_Fabric)
                                            {
                                                unblocked = false;
                                            }
                                            else
                                            {
                                                modifiedDamage = pristineDamage / 2;
                                            }
                                        }
                                        else
                                        {
                                            w.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, inanimateDamage ? modifiedDamage * 10 : modifiedDamage, 9, -1, parent, hitPart));
                                        }
                                    }
                                }
                            }
                            if (unblocked && (thingAsPawn.health.hediffSet.HasBodyPart(hitPart) || XMTUtility.GetPartAttachedToPartOnPawn(thingAsPawn, ref hitPart)))
                            {
                                DamageWorker.DamageResult result = thingAsPawn.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, inanimateDamage ? modifiedDamage * 10 : modifiedDamage, 9, -1, parent, hitPart));
                                if (result.totalDamageDealt > 0 && !result.deflected && Props.appliedHediff != null)
                                {


                                    BodyPartRecord targetPart = result.LastHitPart;
                                    if (thingAsPawn.health.hediffSet.HasBodyPart(targetPart) || XMTUtility.GetPartAttachedToPartOnPawn(thingAsPawn, ref targetPart))
                                    {
                                        Hediff burn = HediffMaker.MakeHediff(Props.appliedHediff, thingAsPawn, targetPart);
                                        burn.Severity = result.totalDamageDealt * Props.damageToSeverity;

                                        thingAsPawn.health.AddHediff(burn, targetPart);
                                    }

                                }
                            }
                        }
                        else
                        {
                            thing.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, modifiedDamage * 10, 1, -1, parent));
                        }
                        FleckMaker.ThrowSmoke(thing.DrawPos, thing.Map, 5);
                    }
                    FilthMaker.TryMakeFilth(SplashCell, Parent.MapHeld, Props.acidFilth);
                    return true;
                }
            }
            return false;
        }

        protected void TryBloodLoss()
        {
            if (IsParentPawn)
            {
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, Parent);
                hediff.Severity = Props.bloodLossSeverity;
                Parent.health.AddHediff(hediff);

            }
        }
        public void TrySplashAcid(float severity = 1, float radiusOverride = -1f, bool cellLimit = true)
        {
            
            if (IsParentPawn)
            {
                if (Parent == null || Parent.Dead)
                {
                    return;
                }
            }
            float modifiedSplashRange = (radiusOverride > 0)? radiusOverride : Props.splashRange * severity;
            int modifiedCells = cellLimit? Mathf.CeilToInt(Props.cells*severity) : int.MaxValue;

            XMTUtility.WitnessAcid(parent.PositionHeld, parent.MapHeld, 0.1f);

            IEnumerable<Thing> splashedThings = from x in GenRadial.RadialDistinctThingsAround(parent.PositionHeld, parent.MapHeld, modifiedSplashRange, true)
                                                where !(x is Mote) && !(x is Filth) && !(x is Corpse)
                                                select x;
            if (!splashedThings.Any())
            {
                IEnumerable<IntVec3> Cells = GenRadial.RadialCellsAround(parent.Position, modifiedSplashRange, true);
                int hitCells = 0;
                foreach (IntVec3 cell in Cells)
                { 
                    TrySplashAcidCell(cell);
                    hitCells++;
                    if(hitCells >= modifiedCells)
                    {
                        break;
                    }
                }
            }
            else
            {
                int hitCells = 0;
                int passthrough = 2;
                while (hitCells < modifiedCells && passthrough > 0)
                {
                    bool nohit = true;
                    foreach (Thing thing in splashedThings)
                    {
                        if(TrySplashAcidThing(severity, thing))
                        {
                            nohit = false;
                            hitCells++;
                        }
                    }
                    if (nohit)
                    {
                        passthrough--;
                    }
                }
            }
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
        public ThingDef  acidFilth = InternalDefOf.Starbeast_Filth_AcidBlood;
        public CompAcidBloodProperties()
        {
            this.compClass = typeof(CompAcidBlood);
        }

    }
}
