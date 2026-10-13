using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class XMTContainedPawnTransferUtility
    {
        private static Texture2D TransferIcon => ContentFinder<Texture2D>.Get(
            ModsConfig.AnomalyActive ? "UI/Commands/TransferEntity" : "UI/Commands/ForPrisoners");

        public static Command_Action MakeTransferCommand(Thing source)
        {
            Pawn occupant = TransferOccupant(source);
            if (!CanTransferFrom(source, occupant))
            {
                return null;
            }

            return new Command_Action
            {
                defaultLabel = "XMT_TransferContainedPawn".Translate(occupant.Named("PAWN")),
                defaultDesc = "XMT_TransferContainedPawnDesc".Translate(occupant.Named("PAWN")),
                icon = TransferIcon,
                action = () => BeginTransferTargeting(source, occupant)
            };
        }

        public static Command_Action MakeReleaseCommand(Thing source)
        {
            Pawn occupant = XMTContainmentUtility.HeldPawn(source);
            if (source == null || occupant == null)
            {
                return null;
            }

            Command_Action command = new Command_Action
            {
                defaultLabel = "XMT_ReleaseFromHarness".Translate(),
                defaultDesc = "XMT_ReleaseFromHarnessDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(ModsConfig.AnomalyActive
                    ? "UI/Commands/TransferEntity"
                    : "UI/Designators/ReleasePioneer"),
                action = () => ChooseWorkerAndStart(source, null, occupant, release: true)
            };

            if (!EligibleWorkers(source, null).Any())
            {
                command.Disable("XMT_NoContainmentWorker".Translate());
            }
            return command;
        }

        public static void QueueRelease(Pawn worker, Thing source, Pawn occupant)
        {
            if (worker?.jobs == null || source == null || occupant == null)
            {
                return;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ReleaseContainedPawn, source, occupant);
            worker.jobs.jobQueue.EnqueueFirst(job);
        }

        internal static void BeginTransferTargeting(Thing source, Pawn occupant, Pawn forcedWorker = null)
        {
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetBuildings = true,
                canTargetPawns = false,
                canTargetItems = false,
                validator = target => target.Thing != source &&
                    IsTransferDestination(source, target.Thing, occupant)
            };
            Find.Targeter.BeginTargeting(parameters, target =>
            {
                Thing destination = target.Thing;
                if (forcedWorker != null)
                {
                    if (CanWorkerTransfer(forcedWorker, source, destination))
                    {
                        StartJob(forcedWorker, source, destination, occupant, release: false);
                    }
                    return;
                }

                SetTransferTarget(source, destination, occupant);
            });
        }

        private static void ChooseWorkerAndStart(Thing source, Thing destination, Pawn occupant, bool release)
        {
            List<Pawn> workers = EligibleWorkers(source, destination).ToList();
            if (workers.Count == 0)
            {
                Messages.Message("XMT_NoContainmentWorker".Translate(), source,
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (workers.Count == 1)
            {
                StartJob(workers[0], source, destination, occupant, release);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(workers.Select(worker => new FloatMenuOption(
                worker.LabelShortCap,
                () => StartJob(worker, source, destination, occupant, release))).ToList()));
        }

        private static IEnumerable<Pawn> EligibleWorkers(Thing source, Thing destination)
        {
            if (source?.Map == null)
            {
                yield break;
            }

            foreach (Pawn worker in source.Map.mapPawns.FreeColonistsSpawned)
            {
                if (worker.Dead || worker.Downed ||
                    !worker.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    !worker.CanReserveAndReach(source, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                if (destination != null &&
                    !worker.CanReserveAndReach(destination, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }
                yield return worker;
            }
        }

        internal static bool CanWorkerTransfer(Pawn worker, Thing source, Thing destination)
        {
            return worker != null && !worker.Dead && !worker.Downed && worker.Map == source?.Map &&
                worker.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                worker.CanReserveAndReach(source, PathEndMode.Touch, Danger.Deadly) &&
                (destination == null || worker.CanReserveAndReach(destination, PathEndMode.Touch, Danger.Deadly));
        }

        internal static Job MakeTransferJob(Thing source, Thing destination, Pawn occupant)
        {
            return TransferOccupant(source) == occupant &&
                IsTransferDestination(source, destination, occupant)
                ? JobMaker.MakeJob(XenoWorkDefOf.XMT_TransferContainedPawn, source, destination, occupant)
                    .WithCount(1)
                : null;
        }

        private static void StartJob(Pawn worker, Thing source, Thing destination, Pawn occupant, bool release)
        {
            if (TransferOccupant(source) != occupant)
            {
                return;
            }

            Job job = release
                ? JobMaker.MakeJob(XenoWorkDefOf.XMT_ReleaseContainedPawn, source, occupant)
                : MakeTransferJob(source, destination, occupant);
            if (job == null)
            {
                return;
            }
            worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        internal static Pawn TransferOccupant(Thing source)
        {
            return BioContainerUtility.Resolve(source)?.ContainedThing as Pawn ??
                XMTContainmentUtility.HeldPawn(source);
        }

        internal static bool CanTransferFrom(Thing source, Pawn occupant)
        {
            return source != null && occupant != null &&
                (BioContainerUtility.Resolve(source) != null || XMTUtility.IsXenomorph(occupant));
        }

        internal static Thing TransferTarget(Thing source)
        {
            Building_BioContainer container = BioContainerUtility.Resolve(source);
            if (container != null)
            {
                return container.BioContainerComp?.TransferTarget;
            }

            if (source is Building_ContainmentHarness harness)
            {
                return harness.ContainmentComp?.TransferTarget;
            }

            Pawn occupant = XMTContainmentUtility.HeldPawn(source);
            return occupant?.GetComp<CompPawnInfo>()?.ContainmentTransferTarget;
        }

        internal static bool SetTransferTarget(Thing source, Thing target, Pawn occupant = null)
        {
            Building_BioContainer container = BioContainerUtility.Resolve(source);
            if (container != null)
            {
                container.BioContainerComp?.SetTransferTarget(target);
                return container.BioContainerComp != null;
            }

            if (source is Building_ContainmentHarness harness)
            {
                harness.ContainmentComp?.SetTransferTarget(target);
                return harness.ContainmentComp != null;
            }

            occupant ??= XMTContainmentUtility.HeldPawn(source);
            CompPawnInfo pawnInfo = occupant?.GetComp<CompPawnInfo>();
            if (!XMTContainmentUtility.IsAnomalyHoldingPlatform(source) ||
                pawnInfo == null)
            {
                return false;
            }

            pawnInfo.SetContainmentTransferTarget(target);
            return true;
        }

        internal static bool IsTransferDestination(Thing source, Thing destination, Pawn occupant)
        {
            return XMTContainmentUtility.IsTransferDestination(destination, occupant,
                AllowsArrestOnTransfer(source));
        }

        internal static bool AllowsArrestOnTransfer(Thing source)
        {
            return BioContainerUtility.Resolve(source) != null ||
                XMTContainmentUtility.IsAnomalyHoldingPlatform(source);
        }

        internal static bool TryTakeFromBioContainer(Thing source, Pawn worker, Pawn occupant)
        {
            Building_BioContainer container = BioContainerUtility.Resolve(source);
            if (container?.ContainedThing != occupant || worker?.carryTracker?.CarriedThing != null ||
                occupant?.holdingOwner == null)
            {
                return false;
            }

            bool transferred = occupant.holdingOwner.TryTransferToContainer(occupant,
                worker.carryTracker.innerContainer, false);
            if (transferred)
            {
                container.BioContainerComp?.Notify_Emptied();
                occupant.Drawer?.renderer?.SetAllGraphicsDirty();
            }
            return transferred;
        }
    }
}
