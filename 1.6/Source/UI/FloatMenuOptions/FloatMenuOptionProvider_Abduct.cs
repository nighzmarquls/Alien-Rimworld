using RimWorld;
using VEF.Graphics;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_Abduct: FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;
        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {

            if (FeralJobUtility.IsThingAvailableForJobBy(context.FirstSelectedPawn, clickedThing) && clickedThing is Pawn clickedPawn)
            {
                if (!XMTUtility.IsXenomorph(clickedPawn))
                {
                    FloatMenuOption AdbuctOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_Abduct".Translate(), delegate
                    {

                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, clickedPawn, HiveUtility.GetValidCocoonCell(context.FirstSelectedPawn.Map));
                        job.count = 1;
                        context.FirstSelectedPawn.jobs.StartJob(job, JobCondition.InterruptForced);


                    }, priority: MenuOptionPriority.Default), context.FirstSelectedPawn, clickedPawn);

                   return AdbuctOption;
                }
            }

            return null;
        }
    }
}
