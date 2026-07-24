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
                            if(KnowledgeUtility.IsObsessed(faction.leader))
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
                                    if (settlement.TryGetComponent(out XenoformingComp comp) && comp.DistressQuestSent)
                                    {
                                        XenoformingUtility.InvestigateSettlement(settlement, caravan);
                                    }
                                    else
                                    {
                                        XenoformingUtility.SettlementCounterAttack(settlement, caravan);
                                    }
                                }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
                            }
                            else
                            {
                                if (settlement.TryGetComponent(out XenoformingComp comp) && comp.DistressQuestSent)
                                {
                                    XenoformingUtility.InvestigateSettlement(settlement, caravan);
                                }
                                else
                                {
                                    XenoformingUtility.SettlementCounterAttack(settlement, caravan);
                                }
                            } 
                        }
                    };
                }
            }
        }

        [HarmonyPatch(typeof(MapParent), nameof(MapParent.Notify_MyMapRemoved))]
        public static class Patch_MapParent_Notify_MyMapRemoved
        {
            [HarmonyPostfix]
            public static void Postfix(MapParent __instance)
            {
                XenoformingUtility.RemoveDestroyedSettlementIfReady(__instance as Settlement);
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
                    if(thing is Ovomorph Ovomorph)
                    {
                        XenoformingUtility.HandleXenoformingImpact(Ovomorph);
                    }
                    if(thing is SelfOccupyingBuilding selfOccupyingBuilding)
                    {
                        XenoformingUtility.HandleXenoformingImpact(selfOccupyingBuilding);
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
                    Log.Message("[XMT][World] " + __instance + " is leaving the map");
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
                    Log.Message("[XMT][World] " + __instance + " is being kidnapped");
                }
                XenoformingUtility.HandleXenoformingImpact(__instance);
            }
        }

        [HarmonyPatch(typeof(FloodFillerFog), nameof(FloodFillerFog.FloodUnfog))]
        public static class Patch_FloodFillerFog_FloodUnfog
        {
            [HarmonyPrefix]
            public static bool Prefix(IntVec3 root, Map map, ref FloodUnfogResult __result)
            {
                if (XMTHiveDebug.DisableHiveRoomRefog || map == null || !root.InBounds(map))
                {
                    return true;
                }

                if (!ContainsCocoonedPlayerPawn(root, map))
                {
                    return true;
                }

                __result = default;
                return false;
            }

            private static bool ContainsCocoonedPlayerPawn(IntVec3 root, Map map)
            {
                List<Thing> things = root.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && pawn.Faction != null && pawn.Faction.IsPlayer && XMTUtility.IsCocooned(pawn))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.HasJobOnThing))]
        public static class Patch_WorkGiver_Tend_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_TendOther), nameof(WorkGiver_TendOther.HasJobOnThing))]
        public static class Patch_WorkGiver_TendOther_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_TendOther_Humanlike), nameof(WorkGiver_TendOther_Humanlike.HasJobOnThing))]
        public static class Patch_WorkGiver_TendOther_Humanlike_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_TendOther_Animal), nameof(WorkGiver_TendOther_Animal.HasJobOnThing))]
        public static class Patch_WorkGiver_TendOther_Animal_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_TendOtherUrgent), nameof(WorkGiver_TendOtherUrgent.HasJobOnThing))]
        public static class Patch_WorkGiver_TendOtherUrgent_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_TendSelf), nameof(WorkGiver_TendSelf.HasJobOnThing))]
        public static class Patch_WorkGiver_TendSelf_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_FeedPatient), nameof(WorkGiver_FeedPatient.HasJobOnThing))]
        public static class Patch_WorkGiver_FeedPatient_HasJobOnThing
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
            {
                return AllowPlayerCareTarget(pawn, t, ref __result);
            }
        }

        [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedTend), "IsValidTendTarget")]
        public static class Patch_FloatMenuOptionProvider_DraftedTend_IsValidTendTarget
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn doctor, Pawn patient, ref bool __result)
            {
                if (IsPlayerPawn(doctor) && IsFogHiddenCocoonedPawn(patient))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.CanJump))]
        public static class Patch_CameraJumper_CanJump
        {
            [HarmonyPostfix]
            public static void Postfix(GlobalTargetInfo target, ref bool __result)
            {
                if (__result && IsFogHiddenCocoonedTarget(target))
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.TryJump), new Type[] { typeof(GlobalTargetInfo), typeof(CameraJumper.MovementMode) })]
        public static class Patch_CameraJumper_TryJump_GlobalTargetInfo
        {
            [HarmonyPrefix]
            public static bool Prefix(GlobalTargetInfo target)
            {
                return !IsFogHiddenCocoonedTarget(target);
            }
        }

        [HarmonyPatch(typeof(CameraJumper), nameof(CameraJumper.TryJumpAndSelect), new Type[] { typeof(GlobalTargetInfo), typeof(CameraJumper.MovementMode) })]
        public static class Patch_CameraJumper_TryJumpAndSelect_GlobalTargetInfo
        {
            [HarmonyPrefix]
            public static bool Prefix(GlobalTargetInfo target)
            {
                return !IsFogHiddenCocoonedTarget(target);
            }
        }

        [HarmonyPatch(typeof(CameraJumper), "TryJumpInternal", new Type[] { typeof(Thing), typeof(CameraJumper.MovementMode) })]
        public static class Patch_CameraJumper_TryJumpInternal_Thing
        {
            [HarmonyPrefix]
            public static bool Prefix(Thing thing)
            {
                return !IsFogHiddenCocoonedPawn(thing as Pawn);
            }
        }

        private static bool AllowPlayerCareTarget(Pawn worker, Thing target, ref bool result)
        {
            if (IsPlayerPawn(worker) && IsFogHiddenCocoonedPawn(target as Pawn))
            {
                result = false;
                return false;
            }

            return true;
        }

        private static bool IsPlayerPawn(Pawn pawn)
        {
            return pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer;
        }

        private static bool IsFogHiddenCocoonedTarget(GlobalTargetInfo target)
        {
            return target.IsValid && IsFogHiddenCocoonedPawn(target.Pawn);
        }

        private static bool IsFogHiddenCocoonedPawn(Pawn pawn)
        {
            Map map = pawn?.MapHeld;
            if (map == null || !pawn.Spawned || !pawn.PositionHeld.InBounds(map))
            {
                return false;
            }

            return XMTUtility.IsCocooned(pawn) && pawn.PositionHeld.Fogged(map);
        }
    }
}
