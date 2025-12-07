
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    internal class XMTHostilityPatch
    {
        
        /*[HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new Type[] { typeof(Thing), typeof(Faction) })]
        public static class Patch_GenHostility_HostileTo_Faction
        {
            [HarmonyPrefix]
            public static bool Prefix(Thing t, Faction fac, ref bool __result)
            {
                if(fac == null)
                {
                    return true;
                }

                if (XMTUtility.IsXenomorph(t))
                {
                    if (t.Faction == null)
                    {
                        __result = true;
                        return false;
                    }
                    
                }
        
                return true;
            }
        }

        [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.IsPermanentCombatant))]
        public static class Patch_PawnUtility_IsPermanentCombatant
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, ref bool __result)
            {
                if(XMTUtility.IsXenomorph(pawn))
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new Type[] { typeof(Thing), typeof(Thing) })]
        public static class Patch_GenHostility_HostileTo
        {
            [HarmonyPrefix]
            public static bool Prefix(Thing a, Thing b, ref bool __result)
            {

                if (XMTUtility.IsXenomorph(a))
                {
                    if (!XMTUtility.IsXenomorph(b))
                    {
                        if (XMTUtility.IsHostileAndAwareOf(b, a))
                        {
                            __result = true;
                            return false;
                        }
                    }
                }
                else
                {
                    if (XMTUtility.IsXenomorph(b))
                    {
                        if (XMTUtility.IsHostileAndAwareOf(a, b))
                        {
                            __result = true;
                            return false;
                        }
                    }
                }
                return true;
            }
        }
        */

        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.TryFindNewTarget))]
        public static class Patch_Building_TurretGun_TryFindNewTarget
        {
            [HarmonyPrefix]
            public static bool Prefix(ref LocalTargetInfo __result, Building_TurretGun __instance,
               Thing ___gun, CompMannable ___mannableComp)
            {
               
                if (HiveUtility.TotalHivePopulation(__instance.Map) > 0)
                {
                    if (___mannableComp != null)
                    {
                        return true;
                    }
         
                    CompEquippable GunCompEq = ___gun.TryGetComp<CompEquippable>();

                    if(GunCompEq == null)
                    {
                        return true;
                    }

                    Verb AttackVerb = GunCompEq.PrimaryVerb;

                    if (AttackVerb == null)
                    {
                        return true;
                    }

                    if (AttackVerb.ProjectileFliesOverhead())
                    {
                        return true;
                    }
                    Log.Message(" Checking for cryptimorphs for " + __instance);
                    float range = AttackVerb.EffectiveRange;

                    float maxDistSquared = range * range;
                    float minDistSquared = AttackVerb.verbProps.minRange * AttackVerb.verbProps.minRange;
                    bool foundTargets = false;

                    Func<IntVec3, bool> losValidator = null;
                    if ((AttackVerb.EquipmentSource == null || !AttackVerb.EquipmentSource.TryGetComp<CompUniqueWeapon>(out var comp) || !comp.IgnoreAccuracyMaluses))
                    {
                        losValidator = (IntVec3 vec3) => !vec3.AnyGas(__instance.Map, GasType.BlindSmoke);
                    }
                    List<Pawn> viableTargets = new List<Pawn>();

                    float bestDistanceSquared = float.MaxValue;
                    foreach (Pawn morph in HiveUtility.GetHiveMembersOnMap(__instance.Map))
                    {
                        float adjustedMaxDistance = (morph.IsPsychologicallyInvisible()) ? maxDistSquared * 0.5f : maxDistSquared;
                        float squaredDistance = (__instance.Position - morph.Position).LengthHorizontalSquared;
                        if (minDistSquared > 0f && squaredDistance < minDistSquared)
                        {
                            continue;
                        }

                        if (maxDistSquared < 9999f && squaredDistance > adjustedMaxDistance)
                        {
                            continue;
                        }

                        if (losValidator != null && (!losValidator(__instance.Position) || !losValidator(morph.Position)))
                        {
                            continue;
                        }

                        if (!__instance.CanSee(morph, losValidator))
                        {
                            continue;
                        }

                        if (bestDistanceSquared > squaredDistance)
                        {
                            Log.Message(morph + " is best distance " + __instance);
                            bestDistanceSquared = squaredDistance;
                            __result = morph;
                            foundTargets = true;
                        }
                    }

                    if (!foundTargets)
                    {
                        return true;
                    }

                    return false;
                }
                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(ref LocalTargetInfo __result, Building_TurretGun __instance)
            {
                //TODO: Make them acquire Xenomorphs within fractional ranges depending on lighting.
            }
        }

    }
}

