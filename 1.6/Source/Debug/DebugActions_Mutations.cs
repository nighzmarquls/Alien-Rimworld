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
                    new DebugActionNode("Apply mutation...", DebugActionType.Action, delegate { OpenMutationSetMenu(QueenMutationOperation.Add); }),
                    new DebugActionNode("Remove mutation...", DebugActionType.Action, delegate { OpenMutationSetMenu(QueenMutationOperation.Remove); }),
                    new DebugActionNode("Report mutation eligibility...", DebugActionType.Action, ReportMutationEligibility)
                };
            };

            yield return root;
        }

        private static void OpenMutationSetMenu(QueenMutationOperation operation)
        {
            List<FloatMenuOption> options = MutationSets()
                .Select(set =>
                {
                    XMT_MutationsHealthSet selectedSet = set;
                    return new FloatMenuOption(BioUtility.LabelForMutationSet(selectedSet), delegate
                    {
                        OpenMutationMenu(selectedSet, operation);
                    });
                })
                .ToList();

            if (options.NullOrEmpty())
            {
                Messages.Message("No mutation sets found.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void OpenMutationMenu(XMT_MutationsHealthSet set, QueenMutationOperation operation)
        {
            List<FloatMenuOption> options = BioUtility.AllMutationsForSet(set)
                .OrderBy(mutation => mutation.displayOrder)
                .ThenBy(BioUtility.LabelForMutation)
                .ThenBy(mutation => mutation.horror.defName)
                .Select(mutation =>
                {
                    MutationHealth selectedMutation = mutation;
                    return new FloatMenuOption(BioUtility.LabelForMutation(selectedMutation), delegate
                    {
                        BeginPawnTargeting("Select pawn.", delegate (Pawn pawn)
                        {
                            if (operation == QueenMutationOperation.Add)
                            {
                                AcceptanceReport report = BioUtility.CanApplyMutation(pawn, selectedMutation);
                                if (!report.Accepted)
                                {
                                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                                    return;
                                }

                                BioUtility.TryApplyMutation(pawn, selectedMutation, out Hediff _);
                            }
                            else
                            {
                                AcceptanceReport report = BioUtility.CanRemoveMutation(pawn, selectedMutation.horror);
                                if (!report.Accepted)
                                {
                                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                                    return;
                                }

                                BioUtility.TryRemoveMutation(pawn, selectedMutation.horror, out Hediff _);
                            }
                        });
                    });
                })
                .ToList();

            if (options.NullOrEmpty())
            {
                Messages.Message("No mutations found in " + set.defName + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void ReportMutationEligibility()
        {
            BeginPawnTargeting("Select pawn.", delegate (Pawn pawn)
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
            });
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
