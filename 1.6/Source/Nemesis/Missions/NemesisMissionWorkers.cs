using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public enum NemesisMissionTimingPhase
    {
        Launch,
        Continue,
        FollowUp
    }

    public abstract class NemesisMissionWorker
    {
        public virtual float Weight(NemesisMissionDef def, GameComponent_Nemesis component, Map map) => 1f;

        public virtual int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
            => Mathf.FloorToInt(Mathf.Lerp(def.populationRange.min, def.populationRange.max, def.Pressure(component, active)));

        public virtual bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => map != null && map.IsPlayerHome;

        public virtual bool TimingValid(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            NemesisMissionTimingPhase phase, out string reason)
        {
            bool valid = NemesisMissionUtility.MapDark(map);
            reason = valid ? null : "waiting for darkness";
            return valid;
        }

        public virtual IEnumerable<string> ConfigErrors(NemesisMissionDef def) { yield break; }

        public virtual string DescribeTarget(Map map) => "map=" + map?.uniqueID + " isPlayerHome=" + map?.IsPlayerHome;

        public virtual string DescribeSizing(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
            => "pressure=" + def.Pressure(component, active).ToString("0.###") + " pressureLimit="
                + Mathf.FloorToInt(Mathf.Lerp(def.populationRange.min, def.populationRange.max, def.Pressure(component, active)))
                + " minimum=" + def.populationRange.min + " finalCount=" + PartySize(def, component, map, active);

        public virtual List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            return NemesisMissionUtility.Routes(component.SpatialContacts.Where(contact => contact.mapId == map.uniqueID), def, map);
        }

        public virtual bool TryFindEntryCell(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            bool ignoreLight, out IntVec3 entry)
        {
            for (int attempt = 0; attempt < 24; attempt++)
                if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 candidate, map, CellFinder.EdgeRoadChance_Animal)
                    && (ignoreLight || XMTHiveUtility.IsLightSuitableAt(candidate, map)))
                {
                    entry = candidate;
                    return true;
                }
            entry = IntVec3.Invalid;
            return false;
        }

        public virtual Pawn GenerateMember(NemesisMissionDef def)
        {
            if (def?.workerSettings?.pawnKind == null)
            {
                return Current.Game.GetComponent<GameComponent_Xenomorph>().GetWorldOrGeneratedCryptimorphForMission();
            }

            PawnGenerationRequest request = new PawnGenerationRequest(def.workerSettings.pawnKind, null);
            request.FixedBiologicalAge = 0f;
            request.FixedChronologicalAge = 0f;
            return PawnGenerator.GeneratePawn(request);
        }
    }

    public sealed class NemesisMissionWorker_Scouting : NemesisMissionWorker
    {
        public override float Weight(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            List<NemesisSpatialContact> spatial = component.SpatialContacts.Where(contact => contact.mapId == map.uniqueID).ToList();
            if (spatial.Count == 0) return def.workerSettings.staleScoutWeight;
            float age = Mathf.Max(0, Find.TickManager.TicksGame - spatial.Max(contact => contact.observedTick)) / 60000f;
            float staleProgress = Mathf.Clamp01(age / def.workerSettings.staleIntelDays);
            return Mathf.Lerp(def.workerSettings.freshIntelWeight, def.workerSettings.staleScoutWeight,
                staleProgress * staleProgress * staleProgress);
        }

        public override List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            List<NemesisSpatialContact> before = component.SpatialContacts.ToList();
            component.RequestIntelligence("scouting mission deployed");
            List<NemesisSpatialContact> changed = NemesisMissionUtility.ChangedContacts(before, component.SpatialContacts, map.uniqueID);
            return NemesisMissionUtility.Routes(changed.Count > 0 ? changed : component.SpatialContacts.Where(c => c.mapId == map.uniqueID), def, map);
        }
    }

    public sealed class NemesisMissionWorker_HostCollection : NemesisMissionWorker
    {
        public override float Weight(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            float evidence = component.MapEvidenceValue(def.workerSettings.weightEvidence, map);
            return 1f + def.workerSettings.weightEvidenceBonus * Mathf.Clamp01(evidence);
        }

        public override bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => base.CanTarget(def, component, map)
            && HasCurrentPopulationIntel(def, component)
            && map.mapPawns.AllPawnsSpawned.Any(p => p.Faction == Faction.OfPlayer && NemesisMissionUtility.ValidAbductionTarget(p));

        private static bool HasCurrentPopulationIntel(NemesisMissionDef def, GameComponent_Nemesis component)
        {
            NemesisObservationRecord population = component.Observations.Where(o => o.observation.metric == "Population")
                .OrderByDescending(o => o.observedTick).FirstOrDefault();
            return population != null && population.EffectiveConfidenceAt(Find.TickManager.TicksGame) > 0f
                && Find.TickManager.TicksGame - population.observedTick <= def.workerSettings.staleIntelDays * 60000f;
        }

        public override IEnumerable<string> ConfigErrors(NemesisMissionDef def)
        {
            if (def.workerSettings.raidPointPawnKind == null || def.workerSettings.raidPointPawnKind.combatPower <= 0f)
                yield return def.defName + ": host collection requires a raid-point pawn kind with positive combat power.";
            if (def.workerSettings.weightEvidenceBonus > 0f && def.workerSettings.weightEvidence == null)
                yield return def.defName + ": host collection requires weight evidence when its evidence bonus is positive.";
        }

        public override string DescribeTarget(Map map)
        {
            if (map == null) return base.DescribeTarget(map);
            List<Pawn> owned = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer).ToList();
            return base.DescribeTarget(map) + " playerPawns=" + owned.Count + " eligiblePlayerHosts="
                + owned.Count(NemesisMissionUtility.ValidAbductionTarget) + " (requires at least one)";
        }

        public override string DescribeSizing(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            NemesisObservationRecord population = component.Observations.Where(o => o.observation.metric == "Population")
                .OrderByDescending(o => o.observedTick).FirstOrDefault();
            int pressureBase = base.PartySize(def, component, map, active);
            float threatPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            float combatPower = def.workerSettings.raidPointPawnKind?.combatPower ?? 0f;
            return base.DescribeSizing(def, component, map, active) + " mapHostEvidence="
                + component.MapEvidenceValue(def.workerSettings.weightEvidence, map).ToString("0.###")
                + " hostEvidenceWeight=" + Weight(def, component, map).ToString("0.###")
                + " storytellerThreatPoints=" + threatPoints.ToString("0.##")
                + " raidPointBudgetFactor=" + def.workerSettings.raidPointBudgetFactor
                + " scalingPawnKind=" + def.workerSettings.raidPointPawnKind?.defName + " combatPower=" + combatPower.ToString("0.##")
                + " pressureBase=" + pressureBase + " raidPointBonusBeforeIntel="
                + RaidPointBonus(pressureBase, threatPoints, combatPower, def.workerSettings.raidPointBudgetFactor)
                + " maximumWithRaidPoints=" + (2 * pressureBase)
                + " rememberedPopulation=" + (population?.value.ToString("0.##") ?? "unknown")
                + " populationConfidence=" + (population?.EffectiveConfidenceAt(Find.TickManager.TicksGame).ToString("0.###") ?? "unknown");
        }

        public override int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            NemesisObservationRecord population = component.Observations.Where(o => o.observation.metric == "Population")
                .OrderByDescending(o => o.observedTick).FirstOrDefault();
            return CollectionSize(def, base.PartySize(def, component, map, active), StorytellerUtility.DefaultThreatPointsNow(map),
                def.workerSettings.raidPointPawnKind.combatPower, population, Find.TickManager.TicksGame);
        }

        internal static int RaidPointBonus(int pressureBase, float threatPoints, float combatPower, float budgetFactor)
            => combatPower <= 0f ? 0 : Mathf.Min(pressureBase,
                Mathf.Max(0, Mathf.FloorToInt(threatPoints * budgetFactor / combatPower)));

        internal static int CollectionSize(NemesisMissionDef def, int pressureBase, float threatPoints, float combatPower,
            NemesisObservationRecord population, int tick)
        {
            int bonus = RaidPointBonus(pressureBase, threatPoints, combatPower, def.workerSettings.raidPointBudgetFactor);
            int bound = pressureBase + bonus;
            if (population == null) return bound;
            float confidence = population.EffectiveConfidenceAt(tick);
            float desired = Mathf.Max(pressureBase, population.value * def.workerSettings.populationFraction);
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(bound, desired, confidence)), pressureBase, bound);
        }

        public override List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            List<NemesisSpatialContact> contacts = component.SpatialContacts.Where(c => c.mapId == map.uniqueID).ToList();
            List<NemesisSpatialContact> beds = contacts.Where(c => c.tags.Contains("HostBed")).ToList();
            return NemesisMissionUtility.Routes(beds.Count > 0 ? beds : contacts, def, map);
        }
    }

    public abstract class NemesisMissionWorker_SwarmAssault : NemesisMissionWorker
    {
        public override int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            int xenoformingCeiling = base.PartySize(def, component, map, active);
            if (!def.workerSettings.useThreatPointsForPopulation)
            {
                return xenoformingCeiling;
            }

            float combatPower = def.workerSettings.pawnKind?.combatPower ?? 0f;
            return ThreatPointPopulation(def, xenoformingCeiling, StorytellerUtility.DefaultThreatPointsNow(map), combatPower);
        }

        internal static int ThreatPointPopulation(NemesisMissionDef def, int xenoformingCeiling, float threatPoints, float combatPower)
        {
            if (combatPower <= 0f)
            {
                return def.populationRange.min;
            }

            int threatPointCount = Mathf.FloorToInt(threatPoints * def.workerSettings.raidPointBudgetFactor / combatPower);
            return Mathf.Clamp(threatPointCount, def.populationRange.min, xenoformingCeiling);
        }

        public override bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => base.CanTarget(def, component, map) && EligibleTargets(null, map).Any();

        public override IEnumerable<string> ConfigErrors(NemesisMissionDef def)
        {
            if (def.workerSettings.pawnKind == null)
            {
                yield return def.defName + ": swarm assaults require a pawn kind.";
            }
            else if (def.workerSettings.useThreatPointsForPopulation && def.workerSettings.pawnKind.combatPower <= 0f)
            {
                yield return def.defName + ": threat-point population requires a pawn kind with positive combat power.";
            }
        }

        public override string DescribeTarget(Map map)
        {
            int eligible = map == null ? 0 : EligibleTargets(null, map).Count();
            return base.DescribeTarget(map) + " eligibleSwarmTargets=" + eligible + " (requires at least one)";
        }

        public override string DescribeSizing(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            string result = base.DescribeSizing(def, component, map, active);
            if (!def.workerSettings.useThreatPointsForPopulation)
            {
                return result;
            }

            float threatPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            float combatPower = def.workerSettings.pawnKind?.combatPower ?? 0f;
            int ceiling = Mathf.FloorToInt(Mathf.Lerp(def.populationRange.min, def.populationRange.max, def.Pressure(component, active)));
            return result + " storytellerThreatPoints=" + threatPoints.ToString("0.##")
                + " raidPointBudgetFactor=" + def.workerSettings.raidPointBudgetFactor
                + " scalingPawnKind=" + def.workerSettings.pawnKind?.defName
                + " combatPower=" + combatPower.ToString("0.##")
                + " xenoformingCeiling=" + ceiling;
        }

        public override List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            List<IntVec3> targets = EligibleTargets(null, map).Select(target => target.Position)
                .Distinct().InRandomOrder().Take(def.workerSettings.maximumRoutePoints).ToList();
            return targets.Count > 0 ? targets : base.PrepareRoute(def, component, map);
        }

        internal virtual IEnumerable<Thing> EligibleTargets(Pawn attacker, Map map)
            => map.mapPawns.AllPawnsSpawned.Where(target => IsEligibleTarget(attacker, target, map));

        internal abstract bool IsEligibleTarget(Pawn attacker, Thing target, Map map);

        public override bool TimingValid(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            NemesisMissionTimingPhase phase, out string reason)
        {
            if (phase == NemesisMissionTimingPhase.Continue)
            {
                reason = null;
                return true;
            }
            return base.TimingValid(def, component, map, phase, out reason);
        }
    }

    public sealed class NemesisMissionWorker_FacehuggerAssault : NemesisMissionWorker_SwarmAssault
    {
        internal override bool IsEligibleTarget(Pawn attacker, Thing target, Map map)
            => target is Pawn pawn && pawn != attacker && pawn.MapHeld == map && NemesisMissionUtility.ValidImplantTarget(pawn);
    }

    public sealed class NemesisMissionWorker_SubverterAssault : NemesisMissionWorker_SwarmAssault
    {
        public override int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            int normalPopulation = base.PartySize(def, component, map, active);
            return PopulationWithMechanoidBandwidth(normalPopulation, MechanoidBandwidthUsed(map),
                def.workerSettings.mechanoidBandwidthPopulationFactor);
        }

        public override string DescribeSizing(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
        {
            int normalPopulation = base.PartySize(def, component, map, active);
            float bandwidthUsed = MechanoidBandwidthUsed(map);
            return base.DescribeSizing(def, component, map, active)
                + " normalPopulation=" + normalPopulation
                + " playerMechanoidBandwidthUsed=" + bandwidthUsed.ToString("0.##")
                + " bandwidthPopulationFactor=" + def.workerSettings.mechanoidBandwidthPopulationFactor
                + " unboundedBandwidthBonus=" + Mathf.FloorToInt(bandwidthUsed * def.workerSettings.mechanoidBandwidthPopulationFactor);
        }

        internal static float MechanoidBandwidthUsed(Map map)
        {
            return InorganicSubversionUtility.PlayerControlledMechanoidBandwidth(map);
        }

        internal static int PopulationWithMechanoidBandwidth(int normalPopulation, float bandwidthUsed, float factor)
        {
            long bonus = Mathf.Max(0, Mathf.FloorToInt(bandwidthUsed * factor));
            long total = (long)Mathf.Max(0, normalPopulation) + bonus;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        internal override IEnumerable<Thing> EligibleTargets(Pawn attacker, Map map)
        {
            foreach (Thing target in base.EligibleTargets(attacker, map))
            {
                yield return target;
            }
            foreach (Building_TurretGun turret in map.listerThings.GetThingsOfType<Building_TurretGun>())
            {
                if (IsEligibleTarget(attacker, turret, map))
                {
                    yield return turret;
                }
            }
        }

        internal override bool IsEligibleTarget(Pawn attacker, Thing target, Map map)
        {
            if (target is Pawn pawn)
            {
                return InorganicSubversionUtility.IsValidSubverterMissionTarget(attacker, pawn, map);
            }
            return target is Building_TurretGun turret
                && XMT_IFFUtility.IsValidSubverterTurretTarget(attacker, turret, map);
        }
    }


    public sealed class NemesisMissionWorker_PowerSabotage : NemesisMissionWorker
    {
        public override bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => base.CanTarget(def, component, map) && XMTPowerSabotageUtility.TryFindTargetNetwork(map, out _);

        public override List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
        {
            List<NemesisSpatialContact> contacts = component.SpatialContacts.Where(c => c.mapId == map.uniqueID).ToList();
            List<IntVec3> turretCells = contacts.Where(c => c.tags.Contains("Turret")).Select(c => c.cell).ToList();
            List<NemesisSpatialContact> safer = contacts.OrderByDescending(c => turretCells.Count == 0
                ? 0 : turretCells.Min(cell => cell.DistanceToSquared(c.cell))).ToList();
            return NemesisMissionUtility.Routes(safer, def, map);
        }

        public override bool TryFindEntryCell(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            bool ignoreLight, out IntVec3 entry)
        {
            List<IntVec3> turretCells = component.SpatialContacts.Where(c => c.mapId == map.uniqueID && c.tags.Contains("Turret"))
                .Select(c => c.cell).Where(cell => cell.InBounds(map)).ToList();
            if (turretCells.Count == 0) return base.TryFindEntryCell(def, component, map, ignoreLight, out entry);
            List<IntVec3> candidates = new List<IntVec3>();
            for (int attempt = 0; attempt < 32; attempt++)
                if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 candidate, map, CellFinder.EdgeRoadChance_Animal)
                    && (ignoreLight || XMTHiveUtility.IsLightSuitableAt(candidate, map))) candidates.AddDistinct(candidate);
            if (candidates.Count == 0)
            {
                entry = IntVec3.Invalid;
                return false;
            }
            entry = candidates.OrderByDescending(candidate => turretCells.Min(turret => candidate.DistanceToSquared(turret))).First();
            return true;
        }
    }

    public sealed class NemesisMissionWorker_Assault : NemesisMissionWorker_SwarmAssault
    {
        internal override bool IsEligibleTarget(Pawn attacker, Thing target, Map map)
            => target is Pawn pawn && pawn != attacker && pawn.MapHeld == map && pawn.Faction == Faction.OfPlayer
                && !pawn.Dead;

        public override bool TimingValid(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            NemesisMissionTimingPhase phase, out string reason)
        {
            if (phase == NemesisMissionTimingPhase.Continue && XenoformingUtility.GetXenoforming() > 50f)
            {
                reason = null;
                return true;
            }
            bool valid = NemesisMissionUtility.MapDark(map);
            reason = valid ? null : "waiting for darkness";
            return valid;
        }
    }
}
