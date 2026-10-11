using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphHunt : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override bool Drafted => true;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            if (clickedPawn == null || clickedPawn == pawn || XMTUtility.NotPrey(clickedPawn))
            {
                return null;
            }

            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_Hunt".Translate(), delegate
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, clickedPawn);
                    Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, clickedPawn);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, clickedPawn);

            if (pawn.needs?.food != null && pawn.needs.food.CurLevel >= pawn.needs.food.MaxLevel)
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_TooFull".Translate(pawn.LabelShort);
            }

            return option;
        }
    }
}
