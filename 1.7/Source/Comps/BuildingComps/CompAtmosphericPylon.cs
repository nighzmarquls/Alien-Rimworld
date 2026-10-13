using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    public class CompAtmosphericPylon : ThingComp
    {
        CompAtmosphericPylonProperties Props => props as CompAtmosphericPylonProperties;
        public int updateWeatherEveryXTicks = 250;

        float heatPushCache = 0;

        int tickCountUp = 0;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Spawned)
            {
                tickCountUp++;
                if (tickCountUp > updateWeatherEveryXTicks)
                {
                    tickCountUp = 0;
                    heatPushCache = 0 - GetAtmosphericFactor();
                }

                if (tickCountUp % 60 == 0)
                {
                    float interiorTemperature = parent.Position.GetTemperature(parent.Map);
                    if (interiorTemperature - heatPushCache >= Props.IdealTemperature)
                    {
                        GenTemperature.PushHeat(parent.Position, parent.Map, heatPushCache);
                    }
                    else if(interiorTemperature > Props.IdealTemperature)
                    {
                        float temperaturePush = interiorTemperature - Props.IdealTemperature;
                        GenTemperature.PushHeat(parent.Position, parent.Map, temperaturePush);
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heatPushCache, "heatPushCache", 0);
            Scribe_Values.Look(ref tickCountUp, "tickCount", 0);

        }
        protected float GetAtmosphericFactor()
        {
            if (parent.Position.UsesOutdoorTemperature(parent.Map))
            {
                return 0;
            }

            if (parent.Position.GetTemperature(parent.Map) <= Props.IdealTemperature)
            {
                return 0;
            }

            float wind = Mathf.Max(parent.Map.windManager.WindSpeed,0.1f);
            float result = 0;

            Room space = parent.Position.GetRoomOrAdjacent(parent.Map);

            float interiorTemperature = space.Temperature;
            float exteriorTemperature = parent.Map.mapTemperature.OutdoorTemp;

            if (interiorTemperature > exteriorTemperature)
            {
                
                float differential = ((interiorTemperature - exteriorTemperature)+20);
                result = wind*differential;
            }
            else
            {
                
                result = wind*10;
            }
            return result;
        }

    }

    public class CompAtmosphericPylonProperties : CompProperties
    {
        public float IdealTemperature = 30;
        public CompAtmosphericPylonProperties()
        {
            this.compClass = typeof(CompAtmosphericPylon);
        }

    }
}
