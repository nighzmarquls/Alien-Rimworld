using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_Mutations
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> MutationTestNodes()
        {
            DebugActionNode root = new DebugActionNode("Mutation tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    MutationOperationNode("Apply mutation...", QueenMutationOperation.Add),
                    MutationOperationNode("Remove mutation...", QueenMutationOperation.Remove),
                    new DebugActionNode("Report mutation eligibility...", DebugActionType.Action, ReportMutationEligibility)
                };
            };

            yield return root;
        }

        private static DebugActionNode MutationOperationNode(string label, QueenMutationOperation operation)
        {
            DebugActionNode node = new DebugActionNode(label, DebugActionType.Action, null);
            node.childGetter = delegate
            {
                List<DebugActionNode> children = MutationSets()
                    .Select(set => MutationSetNode(set, operation))
                    .ToList();

                if (children.NullOrEmpty())
                {
                    children.Add(MessageNode("No mutation sets found.", "No mutation sets found."));
                }

                return children;
            };

            return node;
        }

        private static DebugActionNode MutationSetNode(XMT_MutationsHealthSet set, QueenMutationOperation operation)
        {
            DebugActionNode node = new DebugActionNode(BioUtility.LabelForMutationSet(set), DebugActionType.Action, null);
            node.childGetter = delegate
            {
                List<DebugActionNode> children = BioUtility.AllMutationsForSet(set)
                    .OrderBy(mutation => mutation.displayOrder)
                    .ThenBy(BioUtility.LabelForMutation)
                    .ThenBy(mutation => mutation.horror.defName)
                    .Select(mutation => MutationNode(mutation, operation))
                    .ToList();

                if (children.NullOrEmpty())
                {
                    string message = "No mutations found in " + set.defName + ".";
                    children.Add(MessageNode(message, message));
                }

                return children;
            };

            return node;
        }

        private static DebugActionNode MutationNode(MutationHealth mutation, QueenMutationOperation operation)
        {
            return new DebugActionNode(BioUtility.LabelForMutation(mutation), DebugActionType.Action, delegate
            {
                BeginPawnTargeting("Select pawn.", pawn => ExecuteMutationOperation(pawn, mutation, operation));
            });
        }

        private static DebugActionNode MessageNode(string label, string message)
        {
            return new DebugActionNode(label, DebugActionType.Action, delegate
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
            });
        }

        private static void ExecuteMutationOperation(Pawn pawn, MutationHealth mutation, QueenMutationOperation operation)
        {
            if (operation == QueenMutationOperation.Add)
            {
                TryApplyMutation(pawn, mutation);
            }
            else
            {
                TryRemoveMutation(pawn, mutation);
            }
        }

        private static void TryApplyMutation(Pawn pawn, MutationHealth mutation)
        {
            AcceptanceReport report = BioUtility.CanApplyMutation(pawn, mutation);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            BioUtility.TryApplyMutation(pawn, mutation, out Hediff _);
        }

        private static void TryRemoveMutation(Pawn pawn, MutationHealth mutation)
        {
            AcceptanceReport report = BioUtility.CanRemoveMutation(pawn, mutation.horror);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            BioUtility.TryRemoveMutation(pawn, mutation.horror, out Hediff _);
        }

        private static void ReportMutationEligibility()
        {
            BeginPawnTargeting("Select pawn.", WriteMutationEligibilityReport);
        }

        private static void WriteMutationEligibilityReport(Pawn pawn)
        {
            List<string> lines = new List<string>();
            foreach (XMT_MutationsHealthSet set in MutationSets())
            {
                foreach (MutationHealth mutation in BioUtility.AllMutationsForSet(set).OrderBy(mutation => mutation.displayOrder).ThenBy(BioUtility.LabelForMutation))
                {
                    AcceptanceReport apply = BioUtility.CanApplyMutation(pawn, mutation);
                    AcceptanceReport remove = BioUtility.CanRemoveMutation(pawn, mutation.horror);
                    lines.Add(BioUtility.LabelForMutationSet(set) + " / " + BioUtility.LabelForMutation(mutation)
                        + " | apply: " + (apply.Accepted ? "valid" : apply.Reason)
                        + " | remove: " + (remove.Accepted ? "valid" : remove.Reason));
                }
            }

            string output = string.Join("\n", lines);
            if (output.NullOrEmpty())
            {
                output = "No mutation eligibility entries found.";
            }

            Log.Message("Mutation eligibility for " + pawn + ":\n" + output);
            Messages.Message("Wrote mutation eligibility to log.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static IEnumerable<XMT_MutationsHealthSet> MutationSets()
        {
            return DefDatabase<XMT_MutationsHealthSet>.AllDefsListForReading
                .Where(set => set?.mutations != null)
                .OrderBy(set => set.displayOrder)
                .ThenBy(BioUtility.LabelForMutationSet)
                .ThenBy(set => set.defName);
        }

        private static void BeginPawnTargeting(string prompt, System.Action<Pawn> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetLocations = false,
                validator = target => target.Thing is Pawn
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (target.Thing is Pawn pawn)
                {
                    onSelected(pawn);
                }
            });
        }
    }
}
