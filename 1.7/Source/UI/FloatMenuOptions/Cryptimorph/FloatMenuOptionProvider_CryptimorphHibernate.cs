using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphHibernate : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            IntVec3 cell = context.ClickedCell;
            if (!IsEmptyStandableCell(pawn, cell))
            {
                return null;
            }

            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_Hibernate".Translate(), delegate
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Hibernate, cell);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, cell);

            if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                option.Disabled = true;
                option.tooltip = "CannotReach".Translate(cell.ToString());
            }
            else if (!FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell))
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_HibernationSpotUnavailable".Translate();
            }

            return option;
        }
    }
}
