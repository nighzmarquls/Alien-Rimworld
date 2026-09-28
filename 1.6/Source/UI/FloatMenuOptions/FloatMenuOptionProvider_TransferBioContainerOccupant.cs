using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_TransferBioContainerOccupant : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn worker = context.FirstSelectedPawn;
            Building_BioContainer container = BioContainerUtility.Resolve(clickedThing);
            Pawn occupant = container?.ContainedThing as Pawn;
            if (worker?.Faction != Faction.OfPlayer || occupant == null || clickedThing.Map != worker.Map)
            {
                return null;
            }

            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_TransferContainedPawn".Translate(occupant.Named("PAWN")),
                () => XMTContainedPawnTransferUtility.BeginTransferTargeting(clickedThing, occupant, worker),
                MenuOptionPriority.Default), worker, clickedThing);

            if (!XMTContainedPawnTransferUtility.CanWorkerTransfer(worker, clickedThing, null))
            {
                option.Disabled = true;
                option.tooltip = "XMT_NoContainmentWorker".Translate();
            }
            return option;
        }
    }
}
