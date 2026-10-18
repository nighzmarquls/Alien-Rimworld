using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PipeSystem;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public enum SteamRole
    {
        Geyser,
        Larder,
        Vent,
        Boiler
    }

    public class CompProperties_Steam : CompProperties
    {
        public SteamRole role;
        public PipeNetDef pipeNet;
        public int controlInterval = 250;
        public int resourceInterval = 2500;
        public float production = 20f;
        public float targetTemperature = 30f;
        public float minimumDemandPerInterval;
        public float heatPerSecond = 84f;
        public float heatPerSteam = 100f;
        public float boilerEnergyPerSteam = 400f;
        public EffecterDef consumptionEffect;
        public CompProperties_Steam()
        {
            compClass = typeof(CompSteam);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;
            if (pipeNet == null)
                yield return "Steam comp requires a pipe network.";
            if (resourceInterval < 1 || controlInterval < 1 || production < 0f || heatPerSteam <= 0f || boilerEnergyPerSteam <= 0f)
                yield return "Steam rates/interval/energy factors must be positive.";
            if (minimumDemandPerInterval < 0f)
                yield return "Minimum steam demand must not be negative.";
        }
    }

    public class CompSteam : ThingComp
    {
        private bool boilerEnabled;
        private float coverage;
        private int coverageUntil;
        private float lastDemand;
        private Building_SteamGeyser geyser;
        public CompProperties_Steam Props => (CompProperties_Steam)props;
        public bool BoilerEnabled => Props.role == SteamRole.Boiler && boilerEnabled && parent.Spawned;

        private CompResourceStorage storage;
        public CompResourceStorage Storage => storage ??= parent.AllComps.OfType<CompResourceStorage>().FirstOrDefault(c => c.Props.pipeNet == Props.pipeNet);
        public bool ProtectsLarderHeating => coverage >= 0.999f && Find.TickManager.TicksGame <= coverageUntil;

        private bool BindGeyser()
        {
            if (geyser == null || !geyser.Spawned || geyser.Map != parent.Map)
                geyser = parent.Map.thingGrid.ThingAt(parent.Position, ThingDefOf.SteamGeyser) as Building_SteamGeyser;
            if (geyser == null)
                return false;
            geyser.harvester = (Building)parent;
            return true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (Props.role == SteamRole.Geyser)
                BindGeyser();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (geyser?.harvester == parent)
                geyser.harvester = null;
            geyser = null;
            base.PostDeSpawn(map, mode);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref boilerEnabled, "steamBoilerEnabled");
            Scribe_Values.Look(ref coverage, "steamHeatCoverage");
            Scribe_Values.Look(ref coverageUntil, "steamHeatCoverageUntil");
        }

        public float AddSteam(float amount)
        {
            if (Storage == null || amount <= 0f)
                return 0f;
            float local = Math.Min(amount, Math.Max(0f, Storage.Props.storageCapacity - Storage.AmountStored));
            Storage.AddResource(local);
            float total = local;
            if (amount > local && Storage.PipeNet != null)
            {
                Storage.PipeNet.DistributeAmongStorage(amount - local, out float extra);
                total += extra;
            }

            return total;
        }

        public float DivertHeat(float energy)
        {
            if (!BoilerEnabled)
                return 0f;
            return AddSteam(Math.Max(0f, energy) / Props.boilerEnergyPerSteam) * Props.boilerEnergyPerSteam;
        }

        private float Draw(float requested)
        {
            if (Storage == null || requested <= 0f)
                return 0f;
            float local = Math.Min(requested, Storage.AmountStored);
            Storage.DrawResource(local);
            if (requested <= local || Storage.PipeNet == null)
                return local;
            Storage.PipeNet.DrawAmongStorage(requested - local, out float extra, Storage.PipeNet.storages);
            return local + extra;
        }

        public override void CompTick()
        {
            if (!parent.Spawned || !parent.IsHashIntervalTick(Props.controlInterval) || Props.role == SteamRole.Boiler)
                return;
            float fraction = Props.controlInterval / (float)Math.Max(1, Props.resourceInterval);
            if (Props.role == SteamRole.Geyser)
            {
                if (BindGeyser())
                    AddSteam(Props.production * fraction);
                return;
            }

            float temperature = parent.Position.GetTemperature(parent.Map);
            float target = Props.targetTemperature;
            if (Props.role == SteamRole.Larder && temperature >= target)
            {
                AddSteam(Props.production * fraction);
                lastDemand = 0f;
                coverage = 0f;
                return;
            }

            if (Props.role != SteamRole.Vent && parent.Position.UsesOutdoorTemperature(parent.Map))
            {
                lastDemand = 0f;
                coverage = 0f;
                return;
            }

            lastDemand = Math.Max(0f, target - temperature);
            if (lastDemand > 0f)
                lastDemand = Math.Max(lastDemand, Props.minimumDemandPerInterval);
            float needed = lastDemand * fraction;
            float paid = Draw(needed);
            coverage = needed > 0f ? paid / needed : 1f;
            coverageUntil = Find.TickManager.TicksGame + Props.controlInterval;
            if (Props.role == SteamRole.Vent && temperature < target && paid > 0f)
            {
                if (Props.consumptionEffect != null)
                {
                    Effecter effect = Props.consumptionEffect.Spawn();
                    effect.Trigger(parent, parent);
                    effect.Cleanup();
                }
                float energy = Math.Min(paid * Props.heatPerSteam, Props.heatPerSecond * (Props.controlInterval / 60f));
                float change = GenTemperature.ControlTemperatureTempChange(parent.Position, parent.Map, energy, target);
                Room room = parent.GetRoom();
                if (room != null && change > 0f)
                    room.Temperature += change;
            }
        }

        public override string CompInspectStringExtra()
        {
            float hourlyRate = 2500f / Math.Max(1, Props.resourceInterval);
            if (Props.role == SteamRole.Geyser)
                return "XMT_SteamProduction".Translate((Props.production * hourlyRate).ToString("F0"));
            if (Props.role == SteamRole.Boiler)
                return "XMT_SteamBoilerInspect".Translate(boilerEnabled ? "On".Translate().ToString() : "Off".Translate().ToString());
            return "XMT_SteamDemand".Translate((lastDemand * hourlyRate).ToString("F1"));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Props.role == SteamRole.Boiler && parent.Faction == Faction.OfPlayer)
                yield return new Command_Toggle
                {
                    defaultLabel = "XMT_SteamBoiler".Translate(),
                    defaultDesc = "XMT_SteamBoilerDesc".Translate(),
                    isActive = () => boilerEnabled,
                    toggleAction = () => boilerEnabled = !boilerEnabled
                };
        }
    }
}
