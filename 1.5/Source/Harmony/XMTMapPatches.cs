using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace Xenomorphtype
{ 
    public class XMTMapPatches
    {
        [HarmonyPatch(typeof(SitePartWorker), nameof(SitePartWorker.Notify_SiteMapAboutToBeRemoved))]
        public static class Patch_SitePartWorker_SiteMapAboutToBeRemoved
        {
            [HarmonyPrefix]
            public static void Prefix(SitePart sitePart)
            {
                if(sitePart == null)
                {
                    return;
                }

                if(sitePart.site == null)
                {
                    return;
                }
                
                if(!sitePart.site.HasMap)
                {
                    return;
                }

                foreach(Thing thing in sitePart.site.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                {
                    XenoformingUtility.HandleXenoformingImpact(thing);
                }

                foreach(Pawn pawn in sitePart.site.Map.mapPawns.AllPawnsSpawned)
                {
                    if(pawn.Faction == Faction.OfPlayer)
                    {
                        continue;
                    }

                    XenoformingUtility.HandleXenoformingImpact(pawn);
                }

                foreach(Thing thing in sitePart.site.Map.spawnedThings)
                {
                    if(thing is Ovamorph ovamorph)
                    {
                        XenoformingUtility.HandleXenoformingImpact(ovamorph);
                    }
                    if(thing is HibernationCocoon hibernationCocoon)
                    {
                        XenoformingUtility.HandleXenoformingImpact(hibernationCocoon);
                    }
                }
              
                return;
            }
        }
       
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
        public static class Patch_Pawn_ExitMap
        {
            [HarmonyPostfix]
            public static void Postfix(bool allowedToJoinOrCreateCaravan, Rot4 exitDir, Pawn __instance)
            {
                if(XMTSettings.LogWorld)
                {
                    Log.Message(__instance + " is leaving the map");
                }

                XenoformingUtility.HandleXenoformingImpact(__instance);
               
                return;
            }
        }

        [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreKidnapped))]
        public static class Patch_Pawn_PreKidnapped
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn kidnapper, Pawn __instance)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(__instance + " is being kidnapped");
                }
                XenoformingUtility.HandleXenoformingImpact(__instance);
            }
        }
    }
}
