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
    public class SteamBoilerCompatibilityDef : Def
    {
        public CompProperties_ResourceStorage storage;
        public CompProperties_Steam steam;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;
            if (storage?.pipeNet == null || steam?.pipeNet == null || storage.pipeNet != steam.pipeNet)
                yield return "Steam boiler storage and producer must use the same pipe network.";
            if (steam == null || steam.role != SteamRole.Boiler || steam.boilerEnergyPerSteam <= 0f)
                yield return "Steam boiler requires positive conversion energy and the Boiler role.";
        }
    }

}
