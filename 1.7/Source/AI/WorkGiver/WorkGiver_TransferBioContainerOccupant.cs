using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class WorkGiver_TransferBioContainerOccupant : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForUndefined();
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (thing.Faction == Faction.OfPlayer &&
                    (thing is Building_BioContainer ||
                    (thing is Building_ContainmentHarness harness &&
                    XMTUtility.IsXenomorph(harness.ContainedThing as Pawn)) ||
                    (XMTContainmentUtility.IsAnomalyHoldingPlatform(thing) &&
                    XMTUtility.IsXenomorph(XMTContainmentUtility.HeldPawn(thing)))))
                {
                    yield return thing;
                }
            }

            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
            {
                if (BioContainerUtility.Resolve(thing) != null)
                {
                    yield return thing;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing source, bool forced = false)
        {
            Pawn occupant = XMTContainedPawnTransferUtility.TransferOccupant(source);
            Thing destination = XMTContainedPawnTransferUtility.TransferTarget(source);
            return occupant != null && destination != null && destination.Spawned &&
                XMTContainedPawnTransferUtility.IsTransferDestination(source, destination, occupant) &&
                XMTContainedPawnTransferUtility.CanWorkerTransfer(pawn, source, destination);
        }

        public override Job JobOnThing(Pawn pawn, Thing source, bool forced = false)
        {
            return XMTContainedPawnTransferUtility.MakeTransferJob(source,
                XMTContainedPawnTransferUtility.TransferTarget(source),
                XMTContainedPawnTransferUtility.TransferOccupant(source));
        }
    }
}
