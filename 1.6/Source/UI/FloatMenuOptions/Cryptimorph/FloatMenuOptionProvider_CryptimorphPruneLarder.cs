using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphPruneLarder : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing is not Petrolsump larder || larder.bodySize <= 1)
            {
                return null;
            }

            Pawn pawn = context.FirstSelectedPawn;
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_PruneLarder".Translate(), delegate
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, larder);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_PruneLarder, larder);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, larder);
        }
    }
}
