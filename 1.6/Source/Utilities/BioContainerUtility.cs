using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class BioContainerUtility
    {
        internal static Building_BioContainer Resolve(Thing thing)
        {
            return thing as Building_BioContainer
                ?? (thing as MinifiedThing)?.InnerThing as Building_BioContainer;
        }

        internal static bool TryFindSurgicalContainer(Pawn doctor, Thing patient, out Thing container,
            out bool noInstallSpace)
        {
            container = null;
            noInstallSpace = false;
            if (doctor?.Map == null || patient?.MapHeld != doctor.Map)
            {
                return false;
            }

            Map map = doctor.Map;
            if (doctor.inventory != null)
            {
                foreach (Thing thing in doctor.inventory.innerContainer)
                {
                    Building_BioContainer inner = Resolve(thing);
                    if (inner != null && !inner.Destroyed && inner.ContainedThing == null)
                    {
                        container = thing;
                        break;
                    }
                }
            }

            container ??= GenClosest.ClosestThing_Global_Reachable(doctor.Position, map,
                map.listerThings.ThingsOfDef(XenoBuildingDefOf.XMT_BioContainer)
                    .Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing)),
                PathEndMode.Touch, TraverseParms.For(doctor, Danger.Deadly), 9999f, delegate (Thing thing)
            {
                Building_BioContainer inner = Resolve(thing);
                return inner != null && !inner.Destroyed && inner.ContainedThing == null &&
                    !thing.IsForbidden(doctor) && doctor.CanReserve(thing);
            }, null, false);

            if (container == null)
            {
                return false;
            }

            if (TryFindSurgicalCells(doctor, patient, container, out IntVec3 _, out IntVec3 _))
            {
                return true;
            }

            container = null;
            noInstallSpace = true;
            return false;
        }

        internal static bool TryFindSurgicalCells(Pawn doctor, Thing patient, Thing container,
            out IntVec3 workCell, out IntVec3 installCell)
        {
            workCell = IntVec3.Invalid;
            installCell = IntVec3.Invalid;
            if (doctor?.Map == null || patient?.MapHeld != doctor.Map || Resolve(container) == null)
            {
                return false;
            }

            Map map = doctor.Map;
            Thing surgerySite = patient is Pawn patientPawn ? patientPawn.CurrentBed() ?? patient : patient;
            foreach (IntVec3 candidateWorkCell in GenAdj.CellsAdjacent8Way(patient)
                .OrderBy(cell => cell.DistanceToSquared(doctor.Position)))
            {
                if (!candidateWorkCell.InBounds(map) || !candidateWorkCell.Standable(map) ||
                    !FeralJobUtility.IsPlaceAvailableForJobBy(doctor, candidateWorkCell) ||
                    !doctor.CanReach(candidateWorkCell, PathEndMode.OnCell, Danger.Deadly))
                {
                    continue;
                }

                if (TryFindSurgicalInstallCell(doctor, surgerySite, container, candidateWorkCell,
                    out IntVec3 candidateInstallCell))
                {
                    workCell = candidateWorkCell;
                    installCell = candidateInstallCell;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryFindSurgicalInstallCell(Pawn doctor, Thing patient, Thing container,
            IntVec3 workCell, out IntVec3 installCell)
        {
            installCell = IntVec3.Invalid;
            if (doctor?.Map == null || patient?.MapHeld != doctor.Map || Resolve(container) is not Building_BioContainer inner)
            {
                return false;
            }

            Thing surgerySite = patient is Pawn patientPawn ? patientPawn.CurrentBed() ?? patient : patient;
            foreach (IntVec3 candidate in GenAdj.CellsAdjacent8Way(surgerySite)
                .Concat(GenAdj.CellsAdjacent8Way(workCell, Rot4.North, new IntVec2(1, 1))).Distinct()
                .OrderBy(cell => cell.DistanceToSquared(workCell)))
            {
                if (candidate.InBounds(doctor.Map) && candidate != workCell && GenAdj.AdjacentTo8Way(workCell, candidate) &&
                    FeralJobUtility.IsPlaceAvailableForJobBy(doctor, candidate) &&
                    XMTZoneUtility.CanInstallMovedThingAt(inner, candidate, doctor, container as Building_BioContainer))
                {
                    installCell = candidate;
                    return true;
                }
            }
            return false;
        }

        internal static Building_BioContainer InstallCarriedContainer(Pawn carrier, IntVec3 cell)
        {
            MinifiedThing minified = carrier?.carryTracker?.CarriedThing as MinifiedThing;
            Building_BioContainer inner = minified?.InnerThing as Building_BioContainer;
            if (inner == null || !XMTZoneUtility.CanInstallMovedThingAt(inner, cell, carrier))
            {
                return null;
            }

            if (inner.Faction == null && carrier.Faction != null)
            {
                inner.SetFaction(carrier.Faction);
            }

            XMTZoneUtility.MoveLooseItemsAside(inner, cell, carrier.Map);
            Building_BioContainer placed = GenSpawn.Spawn(inner, cell, carrier.Map,
                WipeMode.VanishOrMoveAside) as Building_BioContainer;
            if (placed == null)
            {
                return null;
            }

            minified.InnerThing = null;
            carrier.carryTracker.innerContainer.Remove(minified);
            minified.Destroy();
            return placed;
        }

        internal static Building_BioContainer FindLoadContainer(Pawn carrier, Pawn target)
        {
            if (carrier?.Map == null || target == null)
            {
                return null;
            }

            Building_BioContainer best = null;
            float bestDistance = float.MaxValue;
            foreach (Thing thing in carrier.Map.listerThings.AllThings)
            {
                if (thing is not Building_BioContainer container || !container.Spawned || container.ContainedThing != null ||
                    container.BioContainerComp?.CanContain(target) != true || container.IsForbidden(carrier) ||
                    !carrier.CanReserveAndReach(container, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                float distance = container.Position.DistanceToSquared(target.Position);
                if (distance < bestDistance)
                {
                    best = container;
                    bestDistance = distance;
                }
            }
            return best;
        }

        internal static Thing ContainOrSpawn(Pawn generatedPawn, Pawn host, Thing outerContainer)
        {
            Building_BioContainer container = Resolve(outerContainer);
            if (host != null && !host.Dead && container != null && !container.Destroyed && container.ContainedThing == null &&
                container.TryAcceptPawn(generatedPawn, medicalExtraction: true))
            {
                return generatedPawn;
            }
            return XMTUtility.TrySpawnPawnFromTarget(generatedPawn, host);
        }
    }
}
