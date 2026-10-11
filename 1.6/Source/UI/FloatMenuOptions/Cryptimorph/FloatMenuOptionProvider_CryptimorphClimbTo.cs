using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphClimbTo : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override bool IgnoreFogged => true;

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            IntVec3 cell = context.ClickedCell;
            if (!IsEmptyStandableCell(pawn, cell))
            {
                return null;
            }

            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_ClimbTo".Translate(), delegate
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_WallClimb, cell);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, cell);
        }
    }
}
