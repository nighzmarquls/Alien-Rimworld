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

namespace Xenomorphtype
{ 
    public class XMTMapPatches
    {


        [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetCaravanGizmos))]
        public static class Patch_Settlement_GetCaravanGizmos
        {
            [HarmonyPostfix]
            public static void Postfix(Caravan caravan, Settlement __instance,ref IEnumerable<Gizmo> __result)
            {
                if(!XenoformingUtility.XenoformingMeets(10))
                {
                    return;
                }
    
                if(__instance.TryGetComponent(out XenoformingComp comp))
                {
                    if (comp.SettlementAttacked)
                    {
                        List<Gizmo> adjustedGizmos = new List<Gizmo>();

                        adjustedGizmos.Add(XenoformingUtility.InvestigateCommand(caravan, __instance));

                        __result = adjustedGizmos;
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CaravanVisitUtility), nameof(CaravanVisitUtility.TradeCommand))]
        public static class Patch_CaravanVisitUtility_TradeCommand
        {
            [HarmonyPostfix]
            public static void Postfix(Caravan caravan, Faction faction, ref Command __result)
            {
                bool haveXenomorphSlave = false;
                bool haveXenomorphColonist = false;

                foreach(Pawn pawn in caravan.pawns)
                {
                    if(XMTUtility.IsXenomorph(pawn))
                    {
                        if (pawn.IsSlave)
                        {
                            haveXenomorphSlave = true;
                        }
                        else
                        {
                            haveXenomorphColonist = true;
                        }
                    }
                }

                bool haveXenomorph = haveXenomorphSlave || haveXenomorphColonist;

                bool violentResponse = false;

                if (faction != null)
                {
                    bool worshipers = false;

                    if (faction.leader != null)
                    {
                        CompPawnInfo info = faction.leader.Info();
                        if(info != null)
                        {
                            if(info.IsObsessed())
                            {
                                worshipers = true;
                            }
                        }
                    }
                    if (ModsConfig.IdeologyActive)
                    {
                        Ideo dominantIdeology = faction.ideos.PrimaryIdeo;
                        if (haveXenomorph)
                        {
                            if (dominantIdeology.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Abhorrent))
                            {
                                violentResponse = true;
                            }
                            else if (dominantIdeology.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Abhorrent))
                            {
                                violentResponse = true;
                            }
                            else if (dominantIdeology.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Study)
                                || dominantIdeology.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Hunt))
                            {
                                if(haveXenomorphColonist)
                                {
                                    violentResponse = true;
                                }
                            }
                            else if(dominantIdeology.HasPrecept(XenoPreceptDefOf.XMT_Biomorph_Worship))
                            {
                                worshipers = true;

                                if(haveXenomorphSlave && !haveXenomorphColonist)
                                {
                                    violentResponse = true;
                                }
                            }
                        }
                    }
                    if (XenoformingUtility.XenoformingMeets(10) && haveXenomorph)
                    {
                        if (!worshipers)
                        {
                            if (haveXenomorphColonist)
                            {
                                violentResponse = true;
                            }
                        }
                    }
                }
                if(violentResponse && __result is Command_Action command_Action)
                {
                    command_Action.action = delegate
                    {
                        Settlement settlement = CaravanVisitUtility.SettlementVisitedNow(caravan);
                        if (settlement != null)
                        {
                            if (!settlement.HasMap)
                            {
                                LongEventHandler.QueueLongEvent(delegate
                                {
                                    XenoformingUtility.SettlementCounterAttack(settlement, caravan);
                                }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
                            }
                            else
                            {
                                XenoformingUtility.SettlementCounterAttack(settlement, caravan);
                            } 
                        }
                    };
                }
            }
        }

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
