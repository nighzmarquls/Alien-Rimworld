using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_GeneContinuity
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> GeneContinuityTests()
        {
            DebugActionNode root = new DebugActionNode("Gene continuity tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    new DebugActionNode("Report selected gene carrier", DebugActionType.Action, ReportSelectedCarrier),
                    new DebugActionNode("Validate all gene remaps", DebugActionType.Action, ValidateRemaps),
                    new DebugActionNode("Copy lineage between things", DebugActionType.Action, CopyLineage),
                    AssignCanonicalGeneNode(),
                    new DebugActionNode("Validate horror holder coverage", DebugActionType.Action, ValidateHolderCoverage),
                    new DebugActionNode("Report invalid holders on map", DebugActionType.Action, ReportInvalidHolders),
                    new DebugActionNode("Mark selected holder as legacy", DebugActionType.Action, MarkSelectedHolderLegacy),
                    new DebugActionNode("Complete selected/held transformation", DebugActionType.Action, CompleteTransformation)
                };
            };
            yield return root;
        }

        private static void ReportSelectedCarrier()
        {
            BeginThingTargeting("Select a gene carrier.", thing => Log.Message(BuildCarrierReport(thing)));
        }

        private static string BuildCarrierReport(Thing thing)
        {
            StringBuilder report = new StringBuilder();
            CompHiveGeneHolder holder = thing.TryGetComp<CompHiveGeneHolder>();
            HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(thing);
            report.AppendLine("[Xenomorphtype] Gene carrier report: " + thing);
            report.AppendLine("Definition: " + thing.def?.defName);
            report.AppendLine("Native tracker: " + (thing is Pawn pawn && pawn.genes != null));
            report.AppendLine("Holder: " + (holder != null));
            if (holder != null)
            {
                report.AppendLine("Redirected: " + holder.UsesNativeGeneTracker);
                report.AppendLine("Storage version: " + holder.StorageVersion);
                report.AppendLine("Template: " + (holder.EffectiveTemplateName.NullOrEmpty() ? "none" : holder.EffectiveTemplateName));
            }
            report.AppendLine("Canonical genes: " + GeneNames(payload));
            if (thing is Pawn expressedPawn && expressedPawn.genes != null)
            {
                report.AppendLine("Expressed genes: " + string.Join(", ", expressedPawn.genes.GenesListForReading.Select(gene => gene.def.defName)));
                report.AppendLine("Canonical re-expression: " + string.Join(", ", BioUtility.GetGenesForExpression(expressedPawn.def, payload?.Genes).Select(gene => gene.defName)));
            }
            return report.ToString().TrimEndNewlines();
        }

        private static void ValidateRemaps()
        {
            List<string> errors = XMT_GeneRemapListDef.ValidateMappings().ToList();
            if (errors.Count == 0)
            {
                Messages.Message("Gene remap validation PASS.", MessageTypeDefOf.TaskCompletion, false);
                return;
            }

            Log.Error("[Xenomorphtype] Gene remap validation failed:\n" + string.Join("\n", errors));
            Messages.Message("Gene remap validation FAIL: " + errors.Count + " issue(s); see log.", MessageTypeDefOf.RejectInput, false);
        }

        private static void CopyLineage()
        {
            BeginThingTargeting("Select lineage source.", source =>
            {
                HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(source);
                BeginThingTargeting("Select lineage destination.", destination =>
                {
                    bool applied = BioUtility.TryApplyHorrorGenePayload(destination, payload, replaceNativeGenes: true);
                    Messages.Message(applied ? "Lineage copied: " + GeneNames(payload) : "Destination does not support lineage.",
                        applied ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
                });
            });
        }

        private static DebugActionNode AssignCanonicalGeneNode()
        {
            DebugActionNode node = new DebugActionNode("Assign canonical gene", DebugActionType.Action, null);
            node.childGetter = delegate
            {
                return DefDatabase<GeneDef>.AllDefsListForReading
                    .OrderBy(gene => gene.defName)
                    .Select(gene => new DebugActionNode(gene.LabelCap + " [" + gene.defName + "]", DebugActionType.Action, delegate
                    {
                        BeginThingTargeting("Select lineage destination.", destination =>
                        {
                            bool applied = BioUtility.TryApplyHorrorGenePayload(destination, new HorrorGenePayload(new[] { gene }), replaceNativeGenes: true);
                            Messages.Message(applied ? "Assigned " + gene.defName + "." : "Destination does not support lineage.",
                                applied ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
                        });
                    }))
                    .ToList();
            };
            return node;
        }

        private static void ValidateHolderCoverage()
        {
            List<ThingDef> expected = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.GetModExtension<XMT_HorrorPawnExtension>() != null ||
                    def.comps?.Any(comp => comp?.compClass != null && typeof(CompPerfectOrganism).IsAssignableFrom(comp.compClass)) == true ||
                    def.comps?.Any(comp => comp?.compClass != null && typeof(CompHatchingPod).IsAssignableFrom(comp.compClass)) == true)
                .ToList();

            string[] buildingDefs = { "XMT_MeatballLarder", "XMT_Petrolsump", "XMT_PetrolPool", "XMT_SlumberingBeast", "XMT_JellyWell" };
            foreach (string defName in buildingDefs)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    expected.AddDistinct(def);
                }
            }

            List<string> missing = expected
                .Where(def => def.comps?.Any(comp => comp?.compClass != null && typeof(CompHiveGeneHolder).IsAssignableFrom(comp.compClass)) != true)
                .Select(def => def.defName)
                .OrderBy(name => name)
                .ToList();

            if (missing.Count == 0)
            {
                Messages.Message("Horror holder coverage PASS: " + expected.Count + " defs.", MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Log.Error("[Xenomorphtype] Missing horror gene holders: " + string.Join(", ", missing));
                Messages.Message("Horror holder coverage FAIL; see log.", MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void ReportInvalidHolders()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            List<string> issues = new List<string>();
            Dictionary<GeneSet, Thing> geneSetOwners = new Dictionary<GeneSet, Thing>();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                CompHiveGeneHolder holder = thing.TryGetComp<CompHiveGeneHolder>();
                if (holder == null)
                {
                    continue;
                }

                if (holder.StorageVersion < 1)
                {
                    issues.Add(thing + ": legacy storage version " + holder.StorageVersion);
                }
                if (holder.UsesNativeGeneTracker && holder.genes != null && holder.genes.GenesListForReading.Count > 0)
                {
                    issues.Add(thing + ": redirected holder retains redundant genes");
                }
                if (!holder.UsesNativeGeneTracker && holder.genes != null)
                {
                    if (geneSetOwners.TryGetValue(holder.genes, out Thing otherOwner))
                    {
                        issues.Add(thing + ": shares GeneSet with " + otherOwner);
                    }
                    else
                    {
                        geneSetOwners.Add(holder.genes, thing);
                    }

                    foreach (GeneDef gene in holder.GenesListForReading)
                    {
                        if (XMT_GeneRemapListDef.GetCanonicalGeneFor(InternalDefOf.XMT_Starbeast_AlienRace, gene) != gene)
                        {
                            issues.Add(thing + ": race-specific gene in canonical storage: " + gene.defName);
                        }
                    }
                }
            }

            if (issues.Count == 0)
            {
                Messages.Message("Map gene-holder validation PASS.", MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Log.Warning("[Xenomorphtype] Invalid gene holders:\n" + string.Join("\n", issues));
                Messages.Message("Map gene-holder validation found " + issues.Count + " issue(s); see log.", MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void MarkSelectedHolderLegacy()
        {
            BeginThingTargeting("Select a non-redirected holder.", thing =>
            {
                CompHiveGeneHolder holder = thing.TryGetComp<CompHiveGeneHolder>();
                bool marked = holder?.DebugMarkLegacy(InternalDefOf.XMT_Starbeast_AlienRace) == true;
                Messages.Message(marked ? "Holder marked as legacy; save and reload to test migration." : "Target has no stored holder or redirects to native genes.",
                    marked ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            });
        }

        private static void CompleteTransformation()
        {
            BeginThingTargeting("Select a transforming pawn or a pawn carrying one.", thing =>
            {
                Pawn pawn = thing as Pawn;
                Pawn transformingPawn = XMTUtility.IsMorphing(pawn) ? pawn : pawn?.carryTracker?.CarriedThing as Pawn;
                if (!XMTUtility.IsMorphing(transformingPawn))
                {
                    Messages.Message("No active transformation found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                foreach (Hediff hediff in transformingPawn.health.hediffSet.hediffs)
                {
                    if (hediff.TryGetComp<HediffComp_BuildingMorphing>() != null || hediff.TryGetComp<HediffComp_Transformation>() != null)
                    {
                        hediff.Severity = Math.Max(1f, hediff.Severity);
                    }
                }
                Messages.Message("Transformation advanced to completion; its normal tick will perform replacement.", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void BeginThingTargeting(string prompt, Action<Thing> action)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters parameters = TargetingParameters.ForAttackAny();
            parameters.canTargetLocations = false;
            parameters.mapObjectTargetsMustBeAutoAttackable = false;
            parameters.validator = target => target.HasThing;
            Find.Targeter.BeginTargeting(parameters, target => action(target.Thing));
        }

        private static string GeneNames(HorrorGenePayload payload)
        {
            return payload == null ? "unsupported" : payload.Empty ? "none" : string.Join(", ", payload.Genes.Select(gene => gene.defName));
        }
    }
}
