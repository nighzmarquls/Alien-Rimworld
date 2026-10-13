using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;


namespace Xenomorphtype
{
    public class CompBatteryBoosting : CompPowerBattery
    {
        List<CompPowerBattery> AdjacentBatteries = new List<CompPowerBattery>();
        protected float maxChargeBoost = 0;
        int nextCheckTick = -1;
        public new CompBatteryBoostingProperties Props => (CompBatteryBoostingProperties)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
        }

        protected CompBatteryBoostingProperties MakeLocalCopyOfProperties()
        {
            CompBatteryBoostingProperties instance = new CompBatteryBoostingProperties();

            instance.updateInterval = Props.updateInterval;
            instance.showPowerNeededIfOff = Props.showPowerNeededIfOff;
            instance.efficiency = Props.efficiency;
            instance.shortCircuitInRain = Props.shortCircuitInRain;
            instance.boostPercentage = Props.boostPercentage;
            instance.compClass = Props.compClass;
            instance.idlePowerDraw = Props.idlePowerDraw;
            instance.powerUpgrades = Props.powerUpgrades;
            instance.alwaysDisplayAsUsingPower = Props.alwaysDisplayAsUsingPower;
            instance.soundAmbientPowered = Props.soundAmbientPowered;
            instance.soundAmbientProducingPower = Props.soundAmbientProducingPower;
            instance.soundPowerOff = Props.soundPowerOff;
            instance.soundPowerOn = Props.soundPowerOn;
            instance.storedEnergyMax = Props.storedEnergyMax;
            instance.transmitsPower = Props.transmitsPower;

            return instance;
        }
        public override void PostPostMake()
        {
            base.PostPostMake();
            props = MakeLocalCopyOfProperties();
        }
        public void UpdateAdjacentPower()
        {
            foreach (IntVec3 direction in GenAdj.AdjacentCells)
            {

                IntVec3 c = parent.Position + direction;

                if (!c.InBounds(parent.Map))
                {
                    continue;
                }

                List<Thing> adjacentThings = c.GetThingList(parent.Map);

                foreach (Thing thing in adjacentThings)
                {
                    CompPowerBattery battery = thing.TryGetComp<CompPowerBattery>();
                    if (battery == null)
                    {
                        continue;
                    }

                    if (battery is CompBatteryBoosting)
                    {
                        continue;
                    }
                    if (AdjacentBatteries.Contains(battery))
                    {
                        continue;
                    }
                    AdjacentBatteries.Add(battery);

                }

                UpdateMaxPower();
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }

        protected void UpdateMaxPower()
        {
            maxChargeBoost = 0;
            List<CompPowerBattery> removeList = new List<CompPowerBattery>();
            foreach (CompPowerBattery battery in AdjacentBatteries)
            {
                if (!battery.parent.Spawned)
                {
                    removeList.Add(battery);
                    continue;
                }
                maxChargeBoost += battery.Props.storedEnergyMax * Props.boostPercentage;
            }
            
            foreach (CompPowerBattery battery in removeList)
            {
                AdjacentBatteries.Remove(battery);
            }

            if(Props.storedEnergyMax == maxChargeBoost)
            {
                return;
            }

            Props.storedEnergyMax = maxChargeBoost;

            if (StoredEnergy <= maxChargeBoost)
            {
                return;
            }

            float adjustPower = StoredEnergy - maxChargeBoost;

            if (adjustPower > 0)
            {
                DrawPower(adjustPower);
            }

        }
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);

            if (parent.IsHashIntervalTick(Props.updateInterval))
            {
                UpdateAdjacentPower();
            }
        }

   
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            maxChargeBoost = 0;
            AdjacentBatteries.Clear();
            UpdateMaxPower();
            base.PostDeSpawn(map, mode);
        }
    }
    public class CompBatteryBoostingProperties : CompProperties_Battery
    {
        public int updateInterval = 600;
        public float boostPercentage = 0.25f;
        public CompBatteryBoostingProperties()
        {
            this.compClass = typeof(CompBatteryBoosting);
        }
    }
}
