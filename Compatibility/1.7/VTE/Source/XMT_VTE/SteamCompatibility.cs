using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PipeSystem;
using RimWorld;
using Verse;
using Xenomorphtype;

namespace XMT_VTE
{

    [StaticConstructorOnStartup]
    public static class SteamCompatibility
    {
        // VTE's 1.6 heater PushHeat contract supplies energy for a 250-tick control interval.
        private const int HeaterControlInterval = 250;
        static SteamCompatibility()
        {
            if (!VTEApiShim.Ready)
            {
                Log.Warning("[XMT][VTE] Unsupported heater API; steam diversion was not installed.");
                return;
            }

            SteamBoilerCompatibilityDef settings = DefDatabase<SteamBoilerCompatibilityDef>.AllDefsListForReading.SingleOrDefault();
            if (settings?.storage?.pipeNet == null || settings.steam?.pipeNet == null)
            {
                Log.Warning("[Alien|Rimworld] VTE steam boiler settings are missing; steam diversion was not installed.");
                return;
            }

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.thingClass != null
                && VTEApiShim.HeaterType.IsAssignableFrom(d.thingClass)
                && d.comps?.OfType<CompProperties_TempControl>().Any(c => c.energyPerSecond > 0f) == true))
            {
                if (def.comps.OfType<CompProperties_Steam>().Any(c => c.role == SteamRole.Boiler))
                {
                    continue;
                }
                def.comps.Add(settings.storage);
                def.comps.Add(settings.steam);
            }

            new Harmony("XMT.VTE.Steam").Patch(VTEApiShim.PushHeat,
                prefix: new HarmonyMethod(typeof(SteamCompatibility), nameof(DivertHeat)));
        }

        public static bool DivertHeat(Building __instance, ref bool __result)
        {
            CompSteam steam = __instance.GetComp<CompSteam>();
            if (steam?.BoilerEnabled != true)
                return true;
            CompTempControl control = __instance.GetComp<CompTempControl>();
            CompPowerTrader power = __instance.GetComp<CompPowerTrader>();
            if (control == null || power?.PowerOn != true)
            {
                __result = false;
                return false;
            }

            // Called once for each native output cell. Returning true preserves VTE's full-power accounting.
            float energy = Math.Max(0f, control.Props.energyPerSecond) * (HeaterControlInterval / 60f);
            __result = steam.DivertHeat(energy) > 0f;
            return false;
        }
    }

}
