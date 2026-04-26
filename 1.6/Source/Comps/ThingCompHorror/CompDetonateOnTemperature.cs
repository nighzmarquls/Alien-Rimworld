
using RimWorld;

namespace Xenomorphtype
{
    internal class CompDetonateOnTemperature : CompTemperatureRuinable
    {
        CompExplosive explosiveComp;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            explosiveComp = parent.GetComp<CompExplosive>();
        }
        public override void CompTick()
        {
            if (!Ruined)
            {
                float ambientTemperature = parent.AmbientTemperature;
                if (ambientTemperature > Props.maxSafeTemperature)
                {
                    ruinedPercent += (ambientTemperature - Props.maxSafeTemperature) * Props.progressPerDegreePerTick;
                }
                else if (ambientTemperature < Props.minSafeTemperature)
                {
                    ruinedPercent -= (ambientTemperature - Props.minSafeTemperature) * Props.progressPerDegreePerTick;
                }

                if (ruinedPercent >= 1f)
                {
                    ruinedPercent = 1f;
                    parent.BroadcastCompSignal("RuinedByTemperature");
                    if (explosiveComp != null)
                    {
                        explosiveComp.StartWick();
                    }
                }
                else if (ruinedPercent < 0f)
                {
                    ruinedPercent = 0f;
                }
            }
        }
    }
}
