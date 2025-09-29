
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace Xenomorphtype
{
    internal class AcidUtility
    {
        protected static ThingDef GetBloodDef(Pawn bleeder)
        {
            return  InternalDefOf.Starbeast_Filth_AcidBlood;
        }
        protected static void TryBloodLoss(Pawn bleeder, float bloodLossSeverity)
        {
            if (bleeder is Pawn)
            {
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, bleeder);
                hediff.Severity = bloodLossSeverity;
                bleeder.health.AddHediff(hediff);

            }
        }
        public static bool TrySplashAcid(Thing bleeder, float severity = 1, float splashRange = -1f, int maxCells = 3, bool cellLimit = true, HediffDef appliedHediff = null, float damageToSeverity = 1, float damage = 26)
        {
            if (bleeder is Pawn pawnBleeder)
            {
                if (pawnBleeder.Dead)
                {
                    return false;
                }
                TryBloodLoss(pawnBleeder, severity*0.001f);
            }

            float modifiedSplashRange = splashRange * severity;
            int modifiedCells = cellLimit ? Mathf.CeilToInt(maxCells * severity) : int.MaxValue;

            XMTUtility.WitnessAcid(bleeder.PositionHeld, bleeder.MapHeld, 0.1f);

            List<IntVec3> Cells = GenRadial.RadialCellsAround(bleeder.Position, modifiedSplashRange, true).ToList();
            int hitCells = 0;
            Cells.Shuffle();

            foreach (IntVec3 cell in Cells)
            {
                TrySplashAcidCell(bleeder, bleeder.MapHeld, cell, severity, appliedHediff, damageToSeverity, damage);
                hitCells++;
                if (hitCells >= modifiedCells)
                {
                    break;
                }
            }

            return true;
        }
        public static bool TrySplashAcidCell(Thing bleeder, Map map, IntVec3 cell, float severity = 1, HediffDef appliedHediff = null, float damageToSeverity = 1, float damage = 26)
        {
            ThingDef BloodFilth = InternalDefOf.Starbeast_Filth_AcidBlood;
            if (bleeder is Pawn pawnBleeder)
            {
                if (pawnBleeder.Dead)
                {
                    return false;
                }

                BloodFilth = pawnBleeder.RaceProps.BloodDef;
            }

            Thing thing = cell.GetEdifice(bleeder.MapHeld);

            if (thing == null)
            {
                IEnumerable<Thing> splashedThings = from x in cell.GetThingList(map)
                                                    where !(x is Mote) && !(x is Filth) && !(x is Corpse)
                                                    select x;

                if (splashedThings.Any())
                {
                    thing = splashedThings.First();
                }
            }

            if (thing != null)
            {
                return TrySplashAcidThing(bleeder, severity, thing, damage, appliedHediff, damageToSeverity);
            }

            if (bleeder.def != InternalDefOf.Starbeast_Filth_AcidBlood)
            { 
                FilthMaker.TryMakeFilth(cell, map, BloodFilth);
                return true;
            }

            return false;
        }
        public static bool TrySplashAcidThing(Thing bleeder, float severity, Thing thing, float damage = 1, HediffDef appliedHediff = null, float damageToSeverity = 1)
        {

            if (bleeder is Pawn pawnBleeder)
            {
                if (pawnBleeder.Dead)
                {
                    return false;
                }
            }

            float modifiedDamage = damage * severity;
            float pristineDamage = modifiedDamage;
            if (thing != bleeder && thing != null)
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

                            if (acidContact.Any())
                            {
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
                                                w.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, inanimateDamage ? modifiedDamage * 10 : modifiedDamage, 9, -1, bleeder, hitPart));
                                            }
                                        }
                                    }
                                }
                                if (unblocked && (thingAsPawn.health.hediffSet.HasBodyPart(hitPart) || XMTUtility.GetPartAttachedToPartOnPawn(thingAsPawn, ref hitPart)))
                                {
                                    DamageWorker.DamageResult result = thingAsPawn.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, inanimateDamage ? modifiedDamage * 10 : modifiedDamage, 9, -1, bleeder, hitPart));

                                    if (result.totalDamageDealt > 0 && !result.deflected)
                                    {
                                        HediffDef acidBurn = InternalDefOf.AcidCorrosion;

                                        if (appliedHediff != null)
                                        {
                                            acidBurn = appliedHediff;
                                        }

                                        BodyPartRecord targetPart = result.LastHitPart;
                                        if (thingAsPawn.health.hediffSet.HasBodyPart(targetPart) || XMTUtility.GetPartAttachedToPartOnPawn(thingAsPawn, ref targetPart))
                                        {
                                            Hediff burn = HediffMaker.MakeHediff(acidBurn, thingAsPawn, targetPart);
                                            burn.Severity = result.totalDamageDealt * damageToSeverity;

                                            thingAsPawn.health.AddHediff(burn, targetPart);
                                        }

                                    }
                                }
                            }
                        }
                        else
                        {
                            thing.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, modifiedDamage * 10, 1, -1, bleeder));
                        }
                        FleckMaker.ThrowSmoke(thing.DrawPos, thing.Map, 5);
                    }

                    return true;
                }
            }
            return false;
        }
    }
}
