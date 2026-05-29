using RimWorld;
using VEF.Graphics;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;


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
            if(!XMTUtility.IsXenomorph(context.FirstSelectedPawn) && context.FirstSelectedPawn.ageTracker.Adult)
            {
                return null;
            }

            if (FeralJobUtility.IsThingAvailableForJobBy(context.FirstSelectedPawn, clickedThing) && clickedThing is Pawn clickedPawn)
            {
                if (!XMTUtility.IsXenomorph(clickedPawn))
                {

                    IntVec3 cell = XMTHiveUtility.GetValidCocoonCell(context.FirstSelectedPawn.Map, context.FirstSelectedPawn);
                    GrappleCheckReport grappleReport = XMTUtility.GetGrappleCheckReport(context.FirstSelectedPawn, clickedPawn);
                    string label = "XMT_FMO_Abduct".Translate(grappleReport.SuccessChance.ToStringPercent());
                    FloatMenuOption AbductOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
                    {
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, clickedPawn, cell);
                        FeralJobUtility.ReservePlaceForJob(context.FirstSelectedPawn, job, cell);
                        FeralJobUtility.ReserveThingForJob(context.FirstSelectedPawn, job, clickedPawn);
                        job.count = 1;

                        context.FirstSelectedPawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), context.FirstSelectedPawn, clickedPawn);


                    if (XMTHiveUtility.ShouldBuildNest(clickedThing.Map))
                    {
                        AbductOption.Disabled = true;
                        AbductOption.tooltip = "XMT_FMO_NoNestEnclosed".Translate();
                    }
                    else if (!cell.IsValid)
                    {
                        AbductOption.Disabled = true;
                        AbductOption.tooltip = "XMT_NoRoomToCocoon".Translate();
                    }
                    else if (grappleReport.BlockedReason.NullOrEmpty())
                    {
                        AbductOption.tooltip = "XMT_FMO_AbductTooltip".Translate(
                            grappleReport.SuccessChance.ToStringPercent(),
                            grappleReport.ResistChance.ToStringPercent(),
                            grappleReport.ModifiedAttackerStrength.ToString("0.##"),
                            grappleReport.ModifiedDefenderStrength.ToString("0.##"));
                    }

                    return AbductOption;
                }
            }

            return null;
        }
    }
}
