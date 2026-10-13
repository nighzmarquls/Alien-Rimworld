using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphLarder : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn?.Downed != true || !XMTHiveUtility.IsMorphingCandidate(clickedPawn) ||
                !XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_LarderSerum))
            {
                return null;
            }

            Pawn pawn = context.FirstSelectedPawn;
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_Larder".Translate(), delegate
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, clickedPawn);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ApplyLardering, clickedPawn);
                    job.count = 1;
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, clickedPawn);
        }
    }
}
