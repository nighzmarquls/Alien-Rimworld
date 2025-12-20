using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTUIPatches
    {

        [HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
        public static class Patch_PawnUIOverlay_DrawPawnGUIOverlay
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn ___pawn)
            {
                if(___pawn != null)
                {
                    CompStealth stealth = ___pawn.GetComp<CompStealth>();
                    if (stealth != null)
                    {
                        if (___pawn.Faction == null || !___pawn.Faction.IsPlayer)
                        {
                            
                            return false;
                            
                        }
                    }
                }

                //DO REGULAR OVERLAY
                return true;
            }

        }
        
        [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
        public static class Patch_FloatMenuMakerMap_GetOptions
        {
            private static void PawnOptions(Pawn pawn, Pawn targetPawn, ref List<FloatMenuOption> __result)
            {
                if (!XMTUtility.NotPrey(targetPawn))
                {
                    FloatMenuOption HuntOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Hunt".Translate(), delegate
                    {

                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, targetPawn);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    if (pawn.needs.food.CurLevel >= pawn.needs.food.MaxLevel)
                    {
                        HuntOption.Disabled = true;
                        HuntOption.tooltip = "XMT_FMO_TooFull".Translate(pawn.LabelShort);
                    }
                    __result.Add(HuntOption);
                }

                if (targetPawn.needs != null && targetPawn.needs.food != null)
                {
                    FloatMenuOption TrophallaxisOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Feed".Translate(), delegate
                    {

                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, targetPawn);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    if (pawn.Starving())
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = "XMT_FMO_TooHungry".Translate(pawn.LabelShort);
                    }
                    else if(targetPawn.needs.food.CurLevel >= targetPawn.needs.food.MaxLevel)
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = "XMT_FMO_TooStuffed".Translate(targetPawn.LabelShort);
                    }
                    __result.Add(TrophallaxisOption);
                }

                if (targetPawn.Downed && XMTHiveUtility.IsMorphingCandidate(targetPawn))
                {
                    FloatMenuOption OvomorphOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Ovomorph".Translate(), delegate
                    {
                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyOvomorphing, targetPawn);
                        job.count = 1;
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    __result.Add(OvomorphOption);

                    if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
                    {
                        
                        FloatMenuOption LarderOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Larder".Translate(), delegate
                        {

                            pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyLardering, targetPawn);
                            job.count = 1;
                            pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                        }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                        __result.Add(LarderOption);
                    }
                }
            }

            private static void BuildingOptions(Pawn pawn, Building targetBuilding, ref List<FloatMenuOption> __result)
            {
                Petrolsump Larder = targetBuilding as Petrolsump;
                if (Larder != null && Larder.bodySize > 1)
                {
                    FloatMenuOption PruneLarderOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_PruneLarder".Translate(), delegate
                    {
                        pawn.Map.reservationManager.ReleaseAllForTarget(targetBuilding);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PruneLarder, targetBuilding);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, targetBuilding);

                    __result.Add(PruneLarderOption);

                }

                if (targetBuilding is EggSack eggSack && eggSack.Occupant != null)
                {
                    FloatMenuOption TrophallaxisOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Feed".Translate(), delegate
                    {

                        pawn.Map.reservationManager.ReleaseAllForTarget(eggSack);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, eggSack.Occupant, eggSack);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, eggSack);

                    if (pawn.Starving())
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = "XMT_FMO_TooHungry".Translate(pawn.LabelShort);
                    }
                    else if (eggSack.Occupant.needs.food.CurLevel >= eggSack.Occupant.needs.food.MaxLevel)
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = "XMT_FMO_TooStuffed".Translate(eggSack.Occupant.LabelShort);
                    }
                    __result.Add(TrophallaxisOption);
                }
            }

            private static void CellOptions(Pawn pawn, IntVec3 cell, ref List<FloatMenuOption> __result)
            {
                if (pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    TargetingParameters HibernateParameters = TargetingParameters.ForCell();

                    FloatMenuOption HibernateOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Hibernate".Translate(), delegate
                    {
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Hibernate, cell);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, cell);

                    __result.Add(HibernateOption);
                }

                if (!cell.Fogged(pawn.Map))
                {
                    FloatMenuOption ClimbOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_ClimbTo".Translate(), delegate
                    {
                        
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_WallClimb, cell);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                        

                    }, priority: MenuOptionPriority.Default), pawn, cell);

                    __result.Add(ClimbOption);
                }
            }
            [HarmonyPostfix]
            public static void Postfix(Vector3 clickPos, List<Pawn> selectedPawns, ref List<FloatMenuOption> __result)
            {
                if(selectedPawns == null || selectedPawns.Count == 0)
                {
                    return;
                }

                bool alreadyFound = false;
                foreach (Pawn pawn in selectedPawns)
                {
                    if (alreadyFound)
                    {
                        return;
                    }

                    if (pawn == null || !XMTUtility.IsXenomorph(pawn))
                    {
                        continue;
                    }

                    if (pawn.Drafted)
                    {
                        continue;
                    }

                    if(pawn.Downed)
                    {
                        continue;
                    }

                    if(pawn.MapHeld == null)
                    {
                        continue;
                    }

                    alreadyFound = true;

                    IntVec3 cell = IntVec3.FromVector3(clickPos);

                    Pawn targetPawn = cell.GetFirstPawn(pawn.Map);

                    if (targetPawn != null && targetPawn != pawn)
                    {
                        PawnOptions(pawn, targetPawn, ref __result);
                    }

                    Building targetBuilding = cell.GetEdifice(pawn.Map);

                    if (targetBuilding != null)
                    {
                        BuildingOptions(pawn, targetBuilding, ref __result);
                    }

                    if (cell.Standable(pawn.Map) && targetBuilding == null && targetPawn == null)
                    {
                        CellOptions(pawn, cell, ref __result);
                    }
                }
            }
        }
        
    }
}
