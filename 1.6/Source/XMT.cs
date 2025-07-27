
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public static class XMT
    {
        static XMT()
        {
            var harmony = new Harmony("XMT.XMTMod");

            LongEventHandler.QueueLongEvent(delegate
            {
                harmony.PatchAll();
                Log.Message("[Alien|Rimworld] finished harmony patches");
                UpdateXenomorphCorpses();
            }, "XMT_LoadPatching", doAsynchronously: true, null);
        }

        static void UpdateXenomorphCorpses()
        {
            if (InternalDefOf.XMT_Starbeast_AlienRace != null)
            {
                Log.Message("[Alien|Rimworld] patched corpse " + InternalDefOf.XMT_Starbeast_AlienRace.defName);
                InternalDefOf.XMT_Starbeast_AlienRace.race.corpseDef.thingClass = typeof(StarbeastCorpse);
            }

            if (InternalDefOf.XMT_Larva != null)
            {
                Log.Message("[Alien|Rimworld] patched corpse " + InternalDefOf.XMT_Royal_AlienRace.defName);
                InternalDefOf.XMT_Royal_AlienRace.race.corpseDef.thingClass = typeof(StarbeastCorpse);
            }

            if (InternalDefOf.XMT_Larva != null)
            {
                Log.Message("[Alien|Rimworld] patched corpse " + InternalDefOf.XMT_Larva.defName);
                InternalDefOf.XMT_Larva.race.corpseDef.thingClass = typeof(StarbeastCorpse);
            }
        }
    }

}
