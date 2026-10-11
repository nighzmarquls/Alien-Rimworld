using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphOvomorph : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn?.Downed != true || !XMTHiveUtility.IsMorphingCandidate(clickedPawn))
            {
                return null;
            }

            Pawn pawn = context.FirstSelectedPawn;
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_Ovomorph".Translate(), delegate
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, clickedPawn);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyOvomorphing, clickedPawn);
                    job.count = 1;
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, clickedPawn);
        }
    }
}
