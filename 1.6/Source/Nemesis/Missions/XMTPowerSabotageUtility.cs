using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTPowerSabotageUtility
    {
        internal static bool TryFindTargetNetwork(Map map, out PowerNet bestNetwork)
        {
            bestNetwork = null;
            int bestScore = 0;
            if (map?.powerNetManager == null) return false;
            foreach (PowerNet network in map.powerNetManager.AllNetsListForReading)
            {
                List<Thing> providers = Providers(network).ToList();
                List<CompPowerTrader> priorityConsumers = network.powerComps.Where(IsPriorityConsumer).ToList();
                if (providers.Count == 0 || priorityConsumers.Count == 0) continue;
                int score = network.batteryComps.Count * 2 + network.powerComps.Count;
                foreach (CompPowerTrader consumer in priorityConsumers)
                {
                    if (consumer.parent is Building_Door poweredDoor &&
                        XMTDoorUtility.HasPoweredResistance(poweredDoor))
                    {
                        score += XMTDoorUtility.PoweredDoorSabotageScore;
                    }
                    CompGlower glower = consumer.parent.GetComp<CompGlower>();
                    if (glower != null)
                    {
                        float lightScore = glower.Props.glowRadius + glower.Props.overlightRadius * 2f - 1f;
                        if (glower.Props.darklightToggle) lightScore *= 0.5f;
                        score += UnityEngine.Mathf.CeilToInt(lightScore);
                    }
                    if (consumer.parent is Building_TurretGun) score += 20;
                }
                if (score <= bestScore) continue;
                bestScore = score;
                bestNetwork = network;
            }
            return bestNetwork != null;
        }

        internal static void Snapshot(Map map, out List<Thing> providers, out List<Thing> consumers, out List<Thing> targets)
        {
            providers = new List<Thing>();
            consumers = new List<Thing>();
            List<Thing> snapshotTargets = new List<Thing>();
            targets = snapshotTargets;
            if (!TryFindTargetNetwork(map, out PowerNet network)) return;
            providers = Providers(network).Distinct().ToList();
            consumers = network.powerComps.Where(IsPriorityConsumer).Select(comp => (Thing)comp.parent).Distinct().ToList();
            // Active fueled generators commonly illuminate their own footprint. Darkness is a conduit safety rule,
            // not a provider eligibility rule; otherwise the most valuable live generators can never be targeted.
            List<Thing> providerTargets = providers.Where(provider => provider != null && provider.Spawned).ToList();
            List<Building_TurretGun> defendingTurrets = map.listerThings.GetThingsOfType<Building_TurretGun>()
                .Where(turret => turret.Spawned && !turret.Destroyed && turret.Faction == Faction.OfPlayer
                    && !XMT_IFFUtility.IsCryptimorphSubvertedTurret(turret) && turret.AttackVerb != null
                    && !turret.AttackVerb.ProjectileFliesOverhead()).ToList();
            List<Thing> conduitTargets = network.transmitters
                .Where(comp => comp is not CompPowerBattery && comp is not CompPowerTrader)
                .Select(comp => (Thing)comp.parent)
                .Where(thing => thing != null && IsDark(thing))
                .Distinct().ToList();
            // This is the mission's single, explicit LOS pass. Turret coverage is not re-evaluated while rotating targets.
            conduitTargets.RemoveAll(conduit => HasTurretLineOfFireAtSnapshot(conduit, defendingTurrets, map));
            providerTargets.Shuffle();
            conduitTargets.Shuffle();
            snapshotTargets.AddRange(providerTargets);
            snapshotTargets.AddRange(conduitTargets.Where(conduit => !snapshotTargets.Contains(conduit)));
        }

        internal static bool Disrupted(List<Thing> providers)
        {
            HashSet<PowerNet> networks = new HashSet<PowerNet>();
            foreach (Thing provider in providers ?? Enumerable.Empty<Thing>())
            {
                if (!ProviderFunctional(provider)) continue;
                foreach (CompPower power in PowerComps(provider))
                    if (power.PowerNet != null) networks.Add(power.PowerNet);
            }
            return networks.Count == 0 || !networks.SelectMany(network => network.powerComps).Any(IsConsumer);
        }

        internal static bool MeetsSuccessThreshold(List<Thing> providers, List<Thing> consumers,
            float requiredFraction, out int disabledConsumers, out int totalConsumers)
        {
            List<Thing> trackedConsumers = (consumers ?? new List<Thing>()).Where(consumer => consumer != null).Distinct().ToList();
            totalConsumers = trackedConsumers.Count;
            if (totalConsumers == 0)
            {
                bool fullyDisrupted = Disrupted(providers);
                disabledConsumers = fullyDisrupted ? 1 : 0;
                totalConsumers = 1;
                return fullyDisrupted;
            }

            disabledConsumers = trackedConsumers.Count(ConsumerDisabled);
            int required = UnityEngine.Mathf.CeilToInt(totalConsumers * UnityEngine.Mathf.Clamp(requiredFraction, 0.5f, 0.75f));
            return disabledConsumers >= required;
        }

        internal static bool PermanentlyInvalid(Thing target, List<Thing> providers)
        {
            if (target == null || target.Destroyed || !target.Spawned) return true;
            if (providers?.Contains(target) == true) return !ProviderFunctional(target);
            HashSet<PowerNet> providerNetworks = new HashSet<PowerNet>((providers ?? new List<Thing>())
                .Where(ProviderFunctional).SelectMany(PowerComps)
                .Select(comp => comp.PowerNet).Where(network => network != null));
            return !PowerComps(target).Any(comp => comp.PowerNet != null && providerNetworks.Contains(comp.PowerNet));
        }

        internal static bool TemporarilyEligible(Pawn pawn, Thing target, bool requireDarkness)
            => pawn != null && target != null && (!requireDarkness || IsDark(target))
            && FeralJobUtility.IsThingAvailableForJobBy(pawn, target);

        internal static bool TemporarilyAvailable(Pawn pawn, Thing target, bool requireDarkness)
            => TemporarilyEligible(pawn, target, requireDarkness)
            && pawn.CanReach(target, Verse.AI.PathEndMode.Touch, Danger.Deadly);

        private static IEnumerable<Thing> Providers(PowerNet network)
        {
            foreach (CompPowerBattery battery in network.batteryComps) yield return battery.parent;
            foreach (CompPowerTrader trader in network.powerComps)
                if (trader.Props.PowerConsumption < 0f) yield return trader.parent;
        }

        private static bool IsConsumer(CompPowerTrader trader) => trader?.Props?.PowerConsumption > 0f;

        private static bool IsPriorityConsumer(CompPowerTrader trader)
            => IsConsumer(trader) && (trader.parent is Building_TurretGun ||
                trader.parent.GetComp<CompGlower>() != null ||
                trader.parent is Building_Door door && XMTDoorUtility.HasPoweredResistance(door));

        private static bool ConsumerDisabled(Thing consumer)
        {
            if (consumer == null || consumer.Destroyed || !consumer.Spawned) return true;
            if (consumer.TryGetComp<CompBreakdownable>()?.BrokenDown == true) return true;
            CompPowerTrader power = consumer.TryGetComp<CompPowerTrader>();
            return power == null || !power.PowerOn;
        }

        private static bool HasTurretLineOfFireAtSnapshot(Thing target, List<Building_TurretGun> turrets, Map map)
        {
            foreach (Building_TurretGun turret in turrets)
            {
                Verb verb = turret.AttackVerb;
                float distanceSquared = turret.Position.DistanceToSquared(target.Position);
                float maximumRange = verb.EffectiveRange;
                float minimumRange = verb.verbProps.minRange;
                if ((maximumRange >= 9999f || distanceSquared <= maximumRange * maximumRange)
                    && distanceSquared >= minimumRange * minimumRange
                    && GenSight.LineOfSight(turret.Position, target.Position, map)) return true;
            }
            return false;
        }

        private static IEnumerable<CompPower> PowerComps(Thing thing)
            => (thing as ThingWithComps)?.AllComps?.OfType<CompPower>() ?? Enumerable.Empty<CompPower>();

        private static bool ProviderFunctional(Thing provider)
        {
            if (provider == null || provider.Destroyed || !provider.Spawned) return false;
            if (provider.TryGetComp<CompBreakdownable>()?.BrokenDown == true) return false;
            CompPowerTrader trader = provider.TryGetComp<CompPowerTrader>();
            return provider.TryGetComp<CompPowerBattery>() != null
                || trader != null && trader.Props.PowerConsumption < 0f;
        }

        private static bool IsDark(Thing thing) => thing?.Map != null
            && XMTMischiefUtility.IsDarkEnoughForMischief(thing.Position, thing.Map);
    }
}
