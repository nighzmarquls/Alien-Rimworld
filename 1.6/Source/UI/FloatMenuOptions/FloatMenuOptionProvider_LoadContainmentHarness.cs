using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_LoadContainmentHarness : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn carrier = context.FirstSelectedPawn;
            Pawn target = clickedThing as Pawn;
            if (carrier?.Faction != Faction.OfPlayer || target == null || target == carrier || target.Dead ||
                !target.RaceProps.Humanlike)
            {
                return null;
            }

            bool requiresArrest = !target.Downed && !target.IsPrisonerOfColony;
            bool canArrest = !requiresArrest || GenAI.CanBeArrestedBy(target, carrier);

            CompProperties_XMT_Containment props = XenoBuildingDefOf.XMT_ContainmentHarness?
                .GetCompProperties<CompProperties_XMT_Containment>();
            Building_ContainmentHarness harness = FindHarness(carrier, target);
            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_LoadIntoContainmentHarness".Translate(target.LabelShort), delegate
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LoadPawnIntoContainmentHarness, target, harness);
                    job.count = 1;
                    carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.Default), carrier, target);

            if (props != null && target.BodySize > props.maxBodySize)
            {
                option.Disabled = true;
                option.tooltip = "XMT_ContainmentHarnessTargetTooLarge".Translate(target.LabelShort);
            }
            else if (!canArrest)
            {
                option.Disabled = true;
                option.tooltip = "XMT_CannotArrestForHarness".Translate(target.Named("PAWN"));
            }
            else if (harness == null)
            {
                option.Disabled = true;
                option.tooltip = "XMT_NoEmptyContainmentHarness".Translate();
            }
            return option;
        }

        private static Building_ContainmentHarness FindHarness(Pawn carrier, Pawn target)
        {
            if (carrier?.Map == null)
            {
                return null;
            }

            Building_ContainmentHarness best = null;
            float bestDistance = float.MaxValue;
            foreach (Thing thing in carrier.Map.listerThings.ThingsOfDef(XenoBuildingDefOf.XMT_ContainmentHarness))
            {
                if (thing is not Building_ContainmentHarness harness || harness.ContainedThing != null ||
                    harness.ContainmentComp?.CanContain(target, requireDowned: false) != true || harness.IsForbidden(carrier) ||
                    !carrier.CanReserveAndReach(harness, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                float distance = harness.Position.DistanceToSquared(target.Position);
                if (distance < bestDistance)
                {
                    best = harness;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
