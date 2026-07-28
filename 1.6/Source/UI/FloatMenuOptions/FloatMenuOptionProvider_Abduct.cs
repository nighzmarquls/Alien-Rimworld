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

                    XMTZoneUtility.TryGetAbductionCocoonCellQuiet(
                        context.FirstSelectedPawn,
                        out IntVec3 cell,
                        out AbductionDestinationWarning destinationWarnings);
                    GrappleCheckReport grappleReport = XMTUtility.GetGrappleCheckReport(context.FirstSelectedPawn, clickedPawn);
                    string label = "XMT_FMO_Abduct".Translate(grappleReport.SuccessChance.ToStringPercent());
                    FloatMenuOption AbductOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
                    {
                        Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AbductHost, clickedPawn, cell);
                        XMTZoneUtility.MarkPreferredHostDestination(job, context.FirstSelectedPawn.Map, cell);
                        FeralJobUtility.ReservePlaceForJob(context.FirstSelectedPawn, job, cell);
                        FeralJobUtility.ReserveThingForJob(context.FirstSelectedPawn, job, clickedPawn);
                        job.count = 1;

                        context.FirstSelectedPawn.jobs.StartJob(job, JobCondition.InterruptForced);

                    }, priority: MenuOptionPriority.Default), context.FirstSelectedPawn, clickedPawn);


                    if (!cell.IsValid)
                    {
                        AbductOption.Disabled = true;
                        AbductOption.tooltip = DestinationTooltip(
                            destinationWarnings,
                            "XMT_NoRoomToCocoon".Translate());
                    }
                    else if (grappleReport.BlockedReason.NullOrEmpty())
                    {
                        AbductOption.tooltip = DestinationTooltip(
                            destinationWarnings,
                            "XMT_FMO_AbductTooltip".Translate(
                                grappleReport.SuccessChance.ToStringPercent(),
                                grappleReport.ResistChance.ToStringPercent(),
                                grappleReport.ModifiedAttackerStrength.ToString("0.##"),
                                grappleReport.ModifiedDefenderStrength.ToString("0.##")));
                    }
                    else if (destinationWarnings != AbductionDestinationWarning.None)
                    {
                        AbductOption.tooltip = DestinationTooltip(
                            destinationWarnings,
                            AbductOption.tooltip?.ToString());
                    }

                    return AbductOption;
                }
            }

            return null;
        }

        private static string DestinationTooltip(
            AbductionDestinationWarning warnings,
            string existingTooltip)
        {
            string warningTooltip = null;
            if ((warnings & AbductionDestinationWarning.InvalidHostRoom) != 0)
            {
                warningTooltip = "XMT_HostZoneInvalidRoom".Translate();
            }
            if ((warnings & AbductionDestinationWarning.HostZoneFallback) != 0)
            {
                string fallback = "XMT_HostZonesUnavailable".Translate();
                warningTooltip = warningTooltip.NullOrEmpty()
                    ? fallback
                    : warningTooltip + "\n" + fallback;
            }

            if (warningTooltip.NullOrEmpty())
            {
                return existingTooltip;
            }
            if (existingTooltip.NullOrEmpty())
            {
                return warningTooltip;
            }

            return warningTooltip + "\n\n" + existingTooltip;
        }
    }
}
