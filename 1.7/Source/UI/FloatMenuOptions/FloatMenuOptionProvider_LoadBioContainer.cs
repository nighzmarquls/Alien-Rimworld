using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_LoadBioContainer : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn carrier = context.FirstSelectedPawn;
            Pawn target = clickedThing as Pawn;
            CompProperties_BioContainer props = XenoBuildingDefOf.XMT_BioContainer?.GetCompProperties<CompProperties_BioContainer>();
            if (carrier?.Faction != Faction.OfPlayer || target == null || target == carrier || target.Dead || !target.Spawned ||
                (!target.Downed && target.health.capacities.CapableOf(PawnCapacityDefOf.Consciousness)) ||
                props == null || target.BodySize > props.maxBodySize)
            {
                return null;
            }

            Building_BioContainer container = BioContainerUtility.FindLoadContainer(carrier, target);
            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_LoadIntoBioContainer".Translate(target.LabelShort), delegate
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LoadPawnIntoBioContainer, target, container);
                    job.count = 1;
                    carrier.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.Default), carrier, target);

            if (container == null)
            {
                option.Disabled = true;
                option.tooltip = "XMT_NoEmptyBioContainer".Translate();
            }
            return option;
        }
    }
}
