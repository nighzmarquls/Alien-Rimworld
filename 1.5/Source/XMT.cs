
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

using System.Reflection;
using HarmonyLib;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public static class XMT
    {
        static XMT()
        {
            var harmony = new Harmony("XMT.XMTMod");
            harmony.PatchAll();
            UpdateXenomorphCorpses();
            
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
