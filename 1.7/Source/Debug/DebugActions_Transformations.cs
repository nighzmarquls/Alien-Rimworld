using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_Transformations
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> TransformationTests()
        {
            DebugActionNode root = new DebugActionNode("Transformation tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    new DebugActionNode("Run pawn identity handoff suite", DebugActionType.Action, PawnTransformationDebugHarness.RunCombinedSuite),
                    new DebugActionNode("Prepare pawn identity save/reload check", DebugActionType.Action, PawnTransformationDebugHarness.PrepareSaveReloadCheck),
                    new DebugActionNode("Verify/cleanup pawn identity save/reload check", DebugActionType.Action, PawnTransformationDebugHarness.VerifyAndCleanupSaveReloadCheck),
                    MakePawnDestinationRoute("Pawn to pawn", TransformPawnToPawn),
                    MakeThingDestinationRoute("Pawn to thing", TransformPawnToThing),
                    MakePawnDestinationRoute("Thing to pawn", TransformThingToPawn),
                    MakeThingDestinationRoute("Thing to thing", TransformThingToThing)
                };
            };

            yield return root;
        }

        private static DebugActionNode MakePawnDestinationRoute(string label, Action<PawnKindDef> action)
        {
            DebugActionNode route = new DebugActionNode(label, DebugActionType.Action, null);
            route.childGetter = delegate
            {
                return DefDatabase<PawnKindDef>.AllDefsListForReading
                    .Where(def => def?.race?.race != null)
                    .GroupBy(def => def.modContentPack?.Name ?? "Core")
                    .OrderBy(group => group.Key)
                    .Select(group => MakePawnKindGroup(group.Key, group, action))
                    .ToList();
            };
            return route;
        }

        private static DebugActionNode MakePawnKindGroup(string label, IEnumerable<PawnKindDef> defs, Action<PawnKindDef> action)
        {
            DebugActionNode group = new DebugActionNode(label, DebugActionType.Action, null);
            group.childGetter = delegate
            {
                return defs
                    .OrderBy(def => def.LabelCap.ToString())
                    .ThenBy(def => def.defName)
                    .Select(def =>
                    {
                        PawnKindDef selectedDef = def;
                        return new DebugActionNode(selectedDef.LabelCap + " [" + selectedDef.defName + "]", DebugActionType.Action, delegate
                        {
                            action(selectedDef);
                        });
                    })
                    .ToList();
            };
            return group;
        }

        private static DebugActionNode MakeThingDestinationRoute(string label, Action<ThingDef> action)
        {
            DebugActionNode route = new DebugActionNode(label, DebugActionType.Action, null);
            route.childGetter = delegate
            {
                return DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(IsSupportedThingDestination)
                    .GroupBy(def => def.modContentPack?.Name ?? "Core")
                    .OrderBy(group => group.Key)
                    .Select(group => MakeThingDefGroup(group.Key, group, action))
                    .ToList();
            };
            return route;
        }

        private static DebugActionNode MakeThingDefGroup(string label, IEnumerable<ThingDef> defs, Action<ThingDef> action)
        {
            DebugActionNode group = new DebugActionNode(label, DebugActionType.Action, null);
            group.childGetter = delegate
            {
                return defs
                    .OrderBy(def => def.LabelCap.ToString())
                    .ThenBy(def => def.defName)
                    .Select(def =>
                    {
                        ThingDef selectedDef = def;
                        return new DebugActionNode(selectedDef.LabelCap + " [" + selectedDef.defName + "]", DebugActionType.Action, delegate
                        {
                            action(selectedDef);
                        });
                    })
                    .ToList();
            };
            return group;
        }

        private static bool IsSupportedThingDestination(ThingDef def)
        {
            if (def?.thingClass == null || def.category == ThingCategory.Pawn || def.MadeFromStuff)
            {
                return false;
            }

            return def.category == ThingCategory.Building ||
                   def.category == ThingCategory.Item ||
                   def.category == ThingCategory.Plant;
        }

        private static void TransformPawnToPawn(PawnKindDef destination)
        {
            BeginSourceTargeting(true, "Select pawn to transform into " + destination.defName + ".", delegate (Thing source)
            {
                HorrorGenePayload before = BioUtility.CaptureHorrorGenePayload(source);
                bool success = XMTUtility.TransformPawnIntoPawn((Pawn)source, destination, out Pawn result);
                ReportResult("Pawn to pawn", source, destination.defName, success, result, before);
            });
        }

        private static void TransformPawnToThing(ThingDef destination)
        {
            BeginSourceTargeting(true, "Select pawn to transform into " + destination.defName + ".", delegate (Thing source)
            {
                HorrorGenePayload before = BioUtility.CaptureHorrorGenePayload(source);
                bool success = XMTUtility.TransformPawnIntoThing((Pawn)source, destination, out Thing result);
                ReportResult("Pawn to thing", source, destination.defName, success, result, before);
            });
        }

        private static void TransformThingToPawn(PawnKindDef destination)
        {
            BeginSourceTargeting(false, "Select thing to transform into " + destination.defName + ".", delegate (Thing source)
            {
                HorrorGenePayload before = BioUtility.CaptureHorrorGenePayload(source);
                bool success = XMTUtility.TransformThingIntoPawn(source, destination, out Pawn result);
                ReportResult("Thing to pawn", source, destination.defName, success, result, before);
            });
        }

        private static void TransformThingToThing(ThingDef destination)
        {
            BeginSourceTargeting(false, "Select thing to transform into " + destination.defName + ".", delegate (Thing source)
            {
                HorrorGenePayload before = BioUtility.CaptureHorrorGenePayload(source);
                bool success = XMTUtility.TransformThingIntoThing(source, destination, out Thing result);
                ReportResult("Thing to thing", source, destination.defName, success, result, before);
            });
        }

        private static void BeginSourceTargeting(bool pawnSource, string prompt, Action<Thing> action)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetPawns = pawnSource,
                canTargetBuildings = !pawnSource,
                canTargetItems = !pawnSource,
                canTargetPlants = !pawnSource,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target => target.HasThing && target.Thing.Spawned && (pawnSource ? target.Thing is Pawn : !(target.Thing is Pawn))
            };

            Find.Targeter.BeginTargeting(parameters, delegate (LocalTargetInfo target)
            {
                Thing source = target.Thing;
                if (source == null)
                {
                    Messages.Message("No transformation source selected.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                try
                {
                    action(source);
                }
                catch (Exception exception)
                {
                    Log.Error("Debug transformation failed: " + exception);
                    Messages.Message("Transformation threw an exception; see the log.", MessageTypeDefOf.RejectInput, false);
                }
            });
        }

        private static void ReportResult(string route, Thing source, string destination, bool success, Thing result, HorrorGenePayload before)
        {
            if (!success || result == null)
            {
                Messages.Message(route + " failed: " + source + " -> " + destination + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.Selector.Select(result, playSound: false, forceDesignatorDeselect: false);
            bool supportsLineage = result.TryGetComp<CompHiveGeneHolder>() != null || result is Pawn pawn && pawn.genes != null;
            HorrorGenePayload after = BioUtility.CaptureHorrorGenePayload(result);
            string continuity;
            if (!supportsLineage)
            {
                continuity = "EXPECTED DROP";
            }
            else if (PayloadMatches(before, after))
            {
                continuity = "PASS";
            }
            else if (OnlyDestinationRestrictedGenesAreMissing(before, after, result.def, out List<GeneDef> filteredGenes))
            {
                continuity = "PASS (filtered " + filteredGenes.Count + " incompatible)";
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("[XMT][Biohorror][PawnTransformation] " + route
                        + " intentionally filtered destination-incompatible genes: "
                        + string.Join(", ", filteredGenes.Select(gene => gene.defName)));
                }
            }
            else
            {
                continuity = "FAIL";
                Log.Warning("[Xenomorphtype] " + route + " gene continuity failed. Before: " + GeneNames(before) + "; after: " + GeneNames(after));
            }

            Messages.Message(route + " " + continuity + ": " + source + " -> " + result + ".", result, MessageTypeDefOf.TaskCompletion, false);
        }

        private static bool PayloadMatches(HorrorGenePayload before, HorrorGenePayload after)
        {
            if (before == null)
            {
                return after == null || after.Empty;
            }

            if (after == null || !before.TemplateName.NullOrEmpty() && before.TemplateName != after.TemplateName)
            {
                return false;
            }

            return before.Genes.All(gene => after.Genes.Contains(gene));
        }

        private static bool OnlyDestinationRestrictedGenesAreMissing(
            HorrorGenePayload before,
            HorrorGenePayload after,
            ThingDef destinationRace,
            out List<GeneDef> filteredGenes)
        {
            filteredGenes = new List<GeneDef>();
            if (before == null
                || after == null
                || !before.TemplateName.NullOrEmpty() && before.TemplateName != after.TemplateName)
            {
                return false;
            }

            foreach (GeneDef canonicalGene in before.Genes)
            {
                if (after.Genes.Contains(canonicalGene))
                {
                    continue;
                }

                GeneDef expressedGene = XMT_GeneRemapListDef.GetRemappedGeneFor(destinationRace, canonicalGene);
                if (expressedGene == null
                    || expressedGene.geneClass == typeof(UnknownGene)
                    || !AlienRace.RaceRestrictionSettings.CanHaveGene(expressedGene, destinationRace, false))
                {
                    filteredGenes.Add(canonicalGene);
                    continue;
                }

                return false;
            }

            return filteredGenes.Count > 0;
        }

        private static string GeneNames(HorrorGenePayload payload)
        {
            return payload == null ? "unsupported" : string.Join(", ", payload.Genes.Select(gene => gene.defName));
        }
    }
}
