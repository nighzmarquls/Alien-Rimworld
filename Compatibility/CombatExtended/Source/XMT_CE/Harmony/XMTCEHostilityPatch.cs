
using CombatExtended;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Xenomorphtype;
using static UnityEngine.GraphicsBuffer;

namespace XMT_CE
{
    internal class XMTCEHostilityPatch
    {
        [HarmonyPatch(typeof(XMTMentalStateUtility), nameof(XMTMentalStateUtility.FindXenoEnemyToKill))]
        public static class Patch_XMTMentalStateUtility_FindXenoEnemyToKill
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, ref Thing __result)
            {
                __result = null;
                if (!pawn.Spawned)
                {
                    return false;
                }

                bool KillerIsXenomorph = XMTUtility.IsXenomorph(pawn);

                List<Thing> tmpTargets = new List<Thing>();
                IReadOnlyList<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < allPawnsSpawned.Count; i++)
                {
                    Pawn candidate = allPawnsSpawned[i];
                    if (KillerIsXenomorph && (XMTUtility.IsXenomorphFriendly(candidate) || XMTUtility.IsMorphing(candidate) || XMTUtility.HasEmbryo(candidate) || XMTUtility.IsXenomorph(candidate)))
                    {
                        continue;
                    }

                    if (pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly) && (candidate.CurJob == null || !candidate.CurJob.exitMapOnArrival))
                    {
                        tmpTargets.Add(candidate);
                    }
                }

                List<Building_TurretGunCE> turrets = pawn.Map.listerThings.GetThingsOfType<Building_TurretGunCE>().ToList();

                for (int i = 0; i < turrets.Count; i++)
                {
                    Building_TurretGunCE candidate = turrets[i];

                    if (!candidate.Active || candidate.IsMannable)
                    {
                        continue;
                    }

                    if (pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                    {
                        tmpTargets.Add(candidate);
                    }
                }

                if (!tmpTargets.Any())
                {
                    return false;
                }

                Thing result = tmpTargets[0];
                int closest = int.MaxValue;
                float worstTarget = 0;

                foreach (Thing target in tmpTargets)
                {
                    float pheromone = -1;
                    if (target is Building_TurretGunCE turret)
                    {
                        if (turret.CurrentTarget == pawn)
                        {
                            __result = target;
                            return false;
                        }
                    }

                    if (target is Pawn targetPawn)
                    {
                        if (targetPawn.Downed)
                        {
                            continue;
                        }

                        CompPawnInfo info = targetPawn.Info();
                        if (info != null)
                        {


                            pheromone = info.XenomorphPheromoneValue();
                            if (target.HostileTo(pawn))
                            {
                                pheromone -= 0.5f;
                            }
                        }
                    }
                    int distance = target.Position.DistanceToSquared(pawn.Position);

                    if (pheromone < -5 && pheromone < worstTarget)
                    {
                        result = target;
                        worstTarget = pheromone;
                        closest = distance;
                    }
                    else if (worstTarget > -5 && distance < closest)
                    {
                        result = target;
                        worstTarget = pheromone;
                        closest = distance;
                    }
                }

                tmpTargets.Clear();
                __result = result;
                return false;
            }
        }

        [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryFindNewTarget))]
        public static class Patch_Building_TurretGunCE_TryFindNewTarget
        {
            [HarmonyPrefix]
            public static bool Prefix(ref LocalTargetInfo __result, Building_TurretGunCE __instance, CompMannable ___mannableComp)
            {
               
                if (XMTHiveUtility.TotalHivePopulation(__instance.Map) > 0)
                {
                    if (___mannableComp != null)
                    {
                        return true;
                    }
         
                    if(__instance.Gun == null)
                    {
                        return true;
                    }

                    CompEquippable GunCompEq = __instance.Gun.TryGetComp<CompEquippable>();

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
                    foreach (Pawn morph in XMTHiveUtility.GetHiveMembersOnMap(__instance.Map))
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

        }

    }
}

