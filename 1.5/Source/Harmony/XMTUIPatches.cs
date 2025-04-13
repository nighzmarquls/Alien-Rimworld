using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using VFECore;
using static UnityEngine.GraphicsBuffer;

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
        [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
        public static class Patch_FloatMenuMakerMap_ChoicesAtFor
        {
            private static void ItemOptions(Pawn pawn, Thing targetItem, ref List<FloatMenuOption> __result)
            {
                CompJellyMaker jellyMaker = pawn.GetComp<CompJellyMaker>();

                if (jellyMaker != null)
                {
                    if(jellyMaker.CanMakeIntoJelly(targetItem))
                    {
                        string JellyName = jellyMaker.GetJellyProduct()?.label;
                        FloatMenuOption JellyOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Make " + JellyName, delegate
                        {

                            Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastProduceJelly, targetItem);
                            if (pawn.jobs.curDriver is JobDriver_ProduceJelly)
                            {
                                //pawn.jobs//jobQueue.AddItem(new QueuedJob(job, JobTag.Misc));
                            }
                            else
                            {
                                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                            }

                        }, priority: MenuOptionPriority.Default), pawn, targetItem);

                        __result.Add(JellyOption);
                    }
                }
            }
            private static void PawnOptions(Pawn pawn, Pawn targetPawn, ref List<FloatMenuOption> __result)
            {
                if (!XMTUtility.IsXenomorph(targetPawn))
                {
                    FloatMenuOption AdbuctOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Abduct", delegate
                    {

                        Job job = JobMaker.MakeJob(XenoWorkDefOf.AbductHost, targetPawn, HiveUtility.GetNestPosition(pawn.Map));
                        job.count = 1;
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    __result.Add(AdbuctOption);

                    FloatMenuOption CocoonOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Cocoon", delegate
                    {

                        Job job = JobMaker.MakeJob(XenoWorkDefOf.CocoonTarget, targetPawn, HiveUtility.GetNestPosition(pawn.Map));
                        job.count = 1;
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    __result.Add(CocoonOption);
                }

                if (!XMTUtility.NotPrey(targetPawn))
                {
                    FloatMenuOption HuntOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hunt", delegate
                    {

                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, targetPawn);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    if (pawn.needs.food.CurLevel >= pawn.needs.food.MaxLevel)
                    {
                        HuntOption.Disabled = true;
                        HuntOption.tooltip = pawn + " is too full to hunt.";
                    }
                    __result.Add(HuntOption);
                }

                if (targetPawn.needs != null && targetPawn.needs.food != null)
                {
                    FloatMenuOption TrophallaxisOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Trophallaxis", delegate
                    {

                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.PerformTrophallaxis, targetPawn);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    if (pawn.needs.food.CurCategory == HungerCategory.Starving)
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = pawn + " is too hungry to perform Trophallaxis.";
                    }
                    else if(targetPawn.needs.food.CurLevel >= targetPawn.needs.food.MaxLevel)
                    {
                        TrophallaxisOption.Disabled = true;
                        TrophallaxisOption.tooltip = targetPawn + " is too full to recieve Trophallaxis.";
                    }
                    __result.Add(TrophallaxisOption);
                }

                if (targetPawn.Downed && HiveUtility.IsMorphingCandidate(targetPawn))
                {
                    FloatMenuOption OvamorphOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Ovamorph", delegate
                    {
                        pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.ApplyOvamorphing, targetPawn);
                        job.count = 1;
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, targetPawn);

                    __result.Add(OvamorphOption);

                    if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
                    {
                        FloatMenuOption LarderOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Larder", delegate
                        {

                            pawn.Map.reservationManager.ReleaseAllForTarget(targetPawn);
                            Job job = JobMaker.MakeJob(XenoWorkDefOf.ApplyLardering, targetPawn);
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
                    FloatMenuOption PruneLarderOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Prune Larder", delegate
                    {
                        pawn.Map.reservationManager.ReleaseAllForTarget(targetBuilding);
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.PruneLarder, targetBuilding);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, targetBuilding);

                    __result.Add(PruneLarderOption);

                }
            }

            private static void CellOptions(Pawn pawn, IntVec3 cell, ref List<FloatMenuOption> __result)
            {
                if (pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    TargetingParameters HibernateParameters = TargetingParameters.ForCell();

                    FloatMenuOption HibernateOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hibernate", delegate
                    {
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastHibernate, cell);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), pawn, cell);

                    __result.Add(HibernateOption);

                    TargetingParameters HideParameters = TargetingParameters.ForCell();

                    FloatMenuOption HideOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hide Inside", delegate
                    {

                        Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastHideInSpot, cell);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), pawn, cell);

                    __result.Add(HideOption);
                }

                if (!cell.Fogged(pawn.Map))
                {
                    FloatMenuOption ClimbOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Climb To", delegate
                    {
                        
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.StarbeastWallClimb, cell);
                        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                        

                    }, priority: MenuOptionPriority.Default), pawn, cell);

                    __result.Add(ClimbOption);
                }
            }
            [HarmonyPostfix]
            public static void Postfix(Vector3 clickPos, Pawn pawn, ref List<FloatMenuOption> __result)
            {
                if (pawn == null || !XMTUtility.IsXenomorph(pawn))
                {
                    return;
                }

                if (pawn.Drafted)
                {
                    return;
                }

                IntVec3 cell = IntVec3.FromVector3(clickPos);

                Pawn targetPawn = cell.GetFirstPawn(pawn.Map);

                if (targetPawn != null && targetPawn != pawn)
                {
                    PawnOptions(pawn, targetPawn, ref __result);
                }

                Building targetBuilding = cell.GetEdifice(pawn.Map);

                if(targetBuilding != null)
                {
                    BuildingOptions(pawn, targetBuilding, ref __result);
                }

                Thing targetItem = cell.GetFirstItem(pawn.Map);
                if(targetItem != null)
                {
                    ItemOptions(pawn, targetItem, ref __result);
                }

                if(cell.Standable(pawn.Map) && targetBuilding == null && targetPawn == null && targetItem == null)
                { 
                    CellOptions(pawn,cell, ref __result);
                }

            }
        }

    }
}
