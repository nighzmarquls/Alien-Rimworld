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

            foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
            {
                if (building is Building_BioContainer)
                {
                    yield return building;
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
            Building_BioContainer container = BioContainerUtility.Resolve(source);
            Pawn occupant = container?.ContainedThing as Pawn;
            Thing destination = container?.BioContainerComp?.TransferTarget;
            return occupant != null && destination != null && destination.Spawned &&
                XMTContainedPawnTransferUtility.IsTransferDestination(source, destination, occupant) &&
                XMTContainedPawnTransferUtility.CanWorkerTransfer(pawn, source, destination);
        }

        public override Job JobOnThing(Pawn pawn, Thing source, bool forced = false)
        {
            Building_BioContainer container = BioContainerUtility.Resolve(source);
            return XMTContainedPawnTransferUtility.MakeTransferJob(source,
                container?.BioContainerComp?.TransferTarget,
                container?.ContainedThing as Pawn);
        }
    }
}
