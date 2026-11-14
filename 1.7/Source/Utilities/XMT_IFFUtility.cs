using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Xenomorphtype
{
    public static class XMT_IFFUtility
    {
        private static readonly MethodInfo ResetForcedTargetMethod = AccessTools.Method(typeof(Building_TurretGun), "ResetForcedTarget");
        private static readonly MethodInfo ResetCurrentTargetMethod = AccessTools.Method(typeof(Building_TurretGun), "ResetCurrentTarget");

        public static List<Pawn> GetAutomaticTurretCandidates(Map map)
        {
            List<Pawn> candidates = XMTHiveUtility.GetHiveMembersOnMap(map);
            if (map == null)
            {
                return candidates;
            }

            // Parasites have no CompMatureMorph and therefore do not register as HiveMates.
            // Use their native def indexes without adding them to nest work/population bookkeeping.
            AddParasites(InternalDefOf.XMT_Larva);
            AddParasites(XenoPawnKindDefOf.XMT_Subverter.race);
            return candidates;

            void AddParasites(ThingDef def)
            {
                List<Thing> parasites = map.listerThings.ThingsOfDef(def);
                for (int i = 0; i < parasites.Count; i++)
                {
                    if (parasites[i] is Pawn pawn)
                    {
                        candidates.Add(pawn);
                    }
                }
            }
        }

        public static bool IsCryptimorphSubvertedTurret(Thing turret)
        {
            return turret?.MapHeld?.GetComponent<HiveMapComponent>()?.IsCryptimorphSubvertedTurret(turret) == true;
        }

        public static bool IsValidSubverterTurretTarget(Pawn subverter, Building_TurretGun turret, Map map)
        {
            if (turret == null || turret.Destroyed || !turret.Spawned || map == null || turret.MapHeld != map
                || turret.TryGetComp<CompMannable>() != null || turret.def.building?.IsMortar == true
                || IsCryptimorphSubvertedTurret(turret))
            {
                return false;
            }

            if (subverter?.Faction == Faction.OfPlayer)
            {
                return turret.Faction != Faction.OfPlayer;
            }

            return turret.Faction == Faction.OfPlayer;
        }

        public static bool TrySubvertTurret(Pawn subverter, Building_TurretGun turret)
        {
            Map map = subverter?.MapHeld;
            if (!IsValidSubverterTurretTarget(subverter, turret, map)
                || !turret.OccupiedRect().ExpandedBy(1).Contains(subverter.PositionHeld))
            {
                return false;
            }

            if (subverter.Faction == Faction.OfPlayer)
            {
                turret.SetFaction(Faction.OfPlayer);
            }
            else
            {
                map.GetComponent<HiveMapComponent>()?.MarkCryptimorphSubvertedTurret(turret);
            }

            CompFlickable flickable = turret.TryGetComp<CompFlickable>();
            if (flickable != null)
            {
                flickable.SwitchIsOn = true;
            }

            ResetForcedTargetMethod?.Invoke(turret, null);
            ResetCurrentTargetMethod?.Invoke(turret, null);

            return subverter.Faction == Faction.OfPlayer
                ? turret.Faction == Faction.OfPlayer
                : IsCryptimorphSubvertedTurret(turret);
        }

        public static bool TryHandleCryptimorphSubvertedTargeting(Thing turret, Verb attackVerb,
            Func<Pawn, bool> visibilityValidator, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (!IsCryptimorphSubvertedTurret(turret))
            {
                return false;
            }

            if (turret.MapHeld == null || attackVerb == null || attackVerb.ProjectileFliesOverhead())
            {
                return true;
            }

            float maxDistanceSquared = attackVerb.EffectiveRange * attackVerb.EffectiveRange;
            float minDistanceSquared = attackVerb.verbProps.minRange * attackVerb.verbProps.minRange;
            Pawn best = null;
            float bestDistanceSquared = float.MaxValue;
            foreach (Pawn candidate in turret.MapHeld.mapPawns.AllPawnsSpawned)
            {
                if (!IsCryptimorphSubvertedTarget(candidate)
                    || !IsAutomaticTurretAggressionAppropriate(turret, candidate))
                {
                    continue;
                }

                float distanceSquared = (turret.PositionHeld - candidate.PositionHeld).LengthHorizontalSquared;
                float adjustedMaximum = candidate.IsPsychologicallyInvisible() ? maxDistanceSquared * 0.5f : maxDistanceSquared;
                if (distanceSquared < minDistanceSquared || (maxDistanceSquared < 9999f && distanceSquared > adjustedMaximum)
                    || (visibilityValidator != null && !visibilityValidator(candidate)))
                {
                    continue;
                }

                if (distanceSquared < bestDistanceSquared)
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                }
            }

            if (best != null)
            {
                target = best;
            }
            return true;
        }

        private static bool IsCryptimorphSubvertedTarget(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed
                && !XMTUtility.IsXenomorph(pawn) && !XMTUtility.IsXenomorphFriendly(pawn)
                && !XMTUtility.IsMorphing(pawn) && !XMTUtility.HasEmbryo(pawn);
        }

        public static bool IsAutomaticTurretAggressionAppropriate(Thing turret, Pawn target)
        {
            if (turret == null || target == null)
            {
                return true;
            }

            if (turret.TryGetComp<CompMannable>() == null && target.IsPsychologicallyInvisible()
                && target.BodySize <= FreshCryptimorphBodySize())
            {
                return false;
            }

            if (XMTUtility.ArraySuppressesAutomatedThreat(turret, target))
            {
                return false;
            }

            if (target.def != InternalDefOf.XMT_Starbeast_AlienRace)
            {
                return true;
            }

            Pawn_ApparelTracker apparelTracker = target.apparel;
            if (apparelTracker == null)
            {
                return true;
            }

            bool wearingIFFCollar = false;
            List<Apparel> wornApparel = apparelTracker.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                if (wornApparel[i].def == InternalDefOf.XMT_IFFCollar)
                {
                    wearingIFFCollar = true;
                    break;
                }
            }

            if (!wearingIFFCollar || turret.Faction == null || target.Faction == null)
            {
                return true;
            }

            return turret.Faction != target.Faction;
        }

        private static float FreshCryptimorphBodySize()
        {
            // Match Pawn.BodySize for a healthy cryptimorph at biological age three.
            // Read the resolved race/stage defs so balance patches remain authoritative.
            RaceProperties race = InternalDefOf.XMT_Starbeast_AlienRace.race;
            for (int i = race.lifeStageAges.Count - 1; i >= 0; i--)
            {
                LifeStageAge stage = race.lifeStageAges[i];
                if (stage.minAge <= 3f)
                {
                    return race.baseBodySize * stage.def.bodySizeFactor;
                }
            }
            return 0f;
        }
    }
}
