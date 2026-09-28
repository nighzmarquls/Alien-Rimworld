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
            Pawn occupant = XMTContainmentUtility.HeldPawn(source);
            if (source == null || !XMTUtility.IsXenomorph(occupant))
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

        private static void BeginTransferTargeting(Thing source, Pawn occupant)
        {
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetBuildings = true,
                canTargetPawns = false,
                canTargetItems = false,
                validator = target => target.Thing != source &&
                    XMTContainmentUtility.IsTransferDestination(target.Thing, occupant)
            };
            Find.Targeter.BeginTargeting(parameters,
                target => ChooseWorkerAndStart(source, target.Thing, occupant, release: false));
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

        private static void StartJob(Pawn worker, Thing source, Thing destination, Pawn occupant, bool release)
        {
            if (XMTContainmentUtility.HeldPawn(source) != occupant)
            {
                return;
            }

            Job job = release
                ? JobMaker.MakeJob(XenoWorkDefOf.XMT_ReleaseContainedPawn, source, occupant)
                : JobMaker.MakeJob(XenoWorkDefOf.XMT_TransferContainedPawn, source, destination, occupant);
            worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
