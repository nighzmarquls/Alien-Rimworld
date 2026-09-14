using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public abstract class NemesisMissionWorker
    {
        public virtual float Weight(NemesisMissionDef def, GameComponent_Nemesis component, Map map) => 1f;

        public virtual int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
            => Mathf.FloorToInt(Mathf.Lerp(def.populationRange.min, def.populationRange.max, def.Pressure(component, active)));

        public virtual bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => map != null && map.IsPlayerHome;

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
            && map.mapPawns.AllPawnsSpawned.Any(p => p.Faction == Faction.OfPlayer && NemesisMissionUtility.ValidHost(p));

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
                + owned.Count(NemesisMissionUtility.ValidHost) + " (requires at least one)";
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
}
