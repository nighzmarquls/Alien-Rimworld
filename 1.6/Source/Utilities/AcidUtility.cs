
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
                if (TrySplashAcidCell(bleeder, bleeder.MapHeld, cell, severity, appliedHediff, damageToSeverity, damage))
                {
                    hitCells++;
                }
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

            Pawn target = cell.GetFirstPawn(map);

            if(target != null)
            {
                return TrySplashAcidThing(bleeder, severity, target, damage, appliedHediff, damageToSeverity);
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

            if (bleeder == null)
            {
                return false;
            }

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

                if (!IsAcidImmune(thing))
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
                                            if (AcidUtility.IsAcidImmune(w))
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

        public static bool IsAcidImmune(Thing thing)
        {
            if (thing == null)
            {
                return true;
            }

            Pawn asPawn = thing as Pawn;
            if (asPawn != null)
            {
                if (asPawn.IsAcidImmune())
                {
                    return true;
                }

                if (asPawn != null && asPawn.InBed())
                {
                    return IsAcidImmune(asPawn.CurrentBed());
                }

                return false;
            }

            if (thing.def == InternalDefOf.Hivemass ||
                thing.def == InternalDefOf.HiveWebbing ||
                thing.def == InternalDefOf.AtmospherePylon ||
                thing.def == InternalDefOf.XMT_CocoonBase ||
                thing.def == InternalDefOf.XMT_CocoonBaseAnimal
               )
            {
                return true;
            }

            CompAcidBlood compAcid = thing.TryGetComp<CompAcidBlood>();

            if (compAcid != null)
            {
                return true;
            }

            if (thing.Stuff != null)
            {
                if (thing.Stuff == InternalDefOf.Starbeast_Resin || thing.Stuff == InternalDefOf.Starbeast_Chitin || thing.Stuff == InternalDefOf.Starbeast_Fabric)
                {
                    return true;
                }
            }

            if (thing.def.designationCategory == InternalDefOf.XMT_Hive)
            {
                return true;
            }

            if (thing.def == ExternalDefOf.ShipHullTile ||
                thing.def == ExternalDefOf.ShipHullTileMech ||
                thing.def == ExternalDefOf.ShipHullTileArchotech ||
                thing.def == ExternalDefOf.ShipHullfoamTile)
            {
                TerrainDef terrain = thing.PositionHeld.GetTerrain(thing.MapHeld);
                if (terrain.affordances.Contains(InternalDefOf.Resin))
                {
                    return true;
                }
            }

            return false;
        }
        public static bool DamageFloors(IntVec3 location, Map map, float Damage = 1.0f)
        {
            if (map != null)
            {
                Building HitStructure = location.GetEdifice(map);
                if (HitStructure != null)
                {
                    if (!IsAcidImmune(HitStructure))
                    {
                        HitStructure.TakeDamage(new DamageInfo(DamageDefOf.AcidBurn, HitStructure.HitPoints * 0.1f, 1));
                        return true;
                    }
                }

                TerrainDef terrainAt = map.terrainGrid.TerrainAt(location);
                if (terrainAt != InternalDefOf.AcidBurned &&
                    terrainAt != InternalDefOf.HiveFloor &&
                    terrainAt != InternalDefOf.HeavyHiveFloor &&
                    terrainAt != InternalDefOf.SmoothHiveFloor &&
                    terrainAt != ExternalDefOf.EmptySpace &&
                    terrainAt != TerrainDefOf.Space)
                {
                    if (map.terrainGrid.CanRemoveTopLayerAt(location))
                    {
                        //map.terrainGrid.SetUnderTerrain(location, InternalDefOf.AcidBurned);
                        map.terrainGrid.RemoveTopLayer(location, false);
                    }
                    else
                    {
                        if (ExternalDefOf.ShipHullTile != null)
                        {
                            List<Thing> burnableThings = location.GetThingList(map);
                            foreach (Thing thing in burnableThings)
                            {
                                if (thing.def == ExternalDefOf.ShipHullTile)
                                {
                                    thing.Destroy();
                                    return true;
                                }
                            }
                        }
                        if (map.terrainGrid.TerrainAt(location) == InternalDefOf.MediumAcidBurned)
                        {
                            map.terrainGrid.SetTerrain(location, InternalDefOf.AcidBurned);
                        }
                        else if (map.terrainGrid.TerrainAt(location) == InternalDefOf.LightAcidBurned)
                        {
                            map.terrainGrid.SetTerrain(location, InternalDefOf.MediumAcidBurned);
                        }
                        else
                        {
                            map.terrainGrid.SetTerrain(location, InternalDefOf.LightAcidBurned);
                        }
                    }

                    return true;
                }
            }
            return false;
        }
    }


}
