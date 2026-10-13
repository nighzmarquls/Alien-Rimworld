using LudeonTK;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal static class DebugActions_OvomorphLineage
    {
        internal static DebugActionNode Menu()
        {
            return new DebugActionNode("Regular ovomorph lineage", DebugActionType.Action, null)
            {
                childGetter = () =>
                {
                    List<GeneDef> defaults = GeneLineageUtility.Defaults.OrderBy(gene => gene.defName).ToList();
                    List<DebugActionNode> nodes = new List<DebugActionNode>
                    {
                        Fixture("Empty (all defaults removed)", new List<GeneDef>()),
                        Fixture("All defaults", defaults),
                        new DebugActionNode("Run payload and hatching contracts (fixtures)", DebugActionType.Action, RunContracts)
                    };
                    for (int count = 1; count < defaults.Count; count++)
                    {
                        int subsetSize = count;
                        nodes.Add(new DebugActionNode(count + " default gene(s)", DebugActionType.Action, null)
                        {
                            childGetter = () => Subsets(defaults, subsetSize, 0, new List<GeneDef>()).Take(128)
                                .Select(genes => Fixture(string.Join(" + ", genes.Select(gene => gene.defName)), genes)).ToList()
                        });
                    }
                    return nodes;
                }
            };
        }

        private static IEnumerable<List<GeneDef>> Subsets(List<GeneDef> source, int count, int start, List<GeneDef> chosen)
        {
            if (chosen.Count == count) { yield return chosen.ToList(); yield break; }
            for (int i = start; i <= source.Count - (count - chosen.Count); i++)
            {
                chosen.Add(source[i]);
                foreach (List<GeneDef> subset in Subsets(source, count, i + 1, chosen)) yield return subset;
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        private static DebugActionNode Fixture(string label, List<GeneDef> genes)
        {
            return new DebugActionNode(label, DebugActionType.ToolMap, () =>
            {
                Map map = Find.CurrentMap;
                IntVec3 cell = UI.MouseCell();
                if (map == null || !cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map) != null) return;
                Ovomorph egg = (Ovomorph)GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovomorph, cell, map);
                egg.SetFaction(Faction.OfPlayer);
                GeneLineageUtility.SetSelection(egg, genes, label);
                egg.ForceProgress();
                Find.Selector.Select(egg);
                Log.Message("[XMT][Lineage] Regular egg: " + label + "; removed="
                    + string.Join(",", egg.GetComp<CompHatchingEgg>().removedDefaultGenes.Select(gene => gene.defName)));
            });
        }

        internal static void RunContracts()
        {
            if (!ModsConfig.BiotechActive || Find.CurrentMap == null) return;
            try
            {
                Map map = Find.CurrentMap;
                List<GeneDef> defaults = GeneLineageUtility.Defaults;
                foreach (List<GeneDef> subset in Subsets(defaults, 0, 0, new List<GeneDef>())
                    .Concat(defaults.Select(gene => new List<GeneDef> { gene }))
                    .Concat(Subsets(defaults, 2, 0, new List<GeneDef>()).Take(32)).Concat(new[] { defaults }))
                {
                    HorrorGenePayload payload = GeneLineageUtility.Selection(subset, "lineage contract");
                    NemesisMissionScenarioHarness.Check("removed defaults are exact complement", defaults.All(gene =>
                        subset.Contains(gene) != payload.RemovedDefaultGenes.Contains(gene)));
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
                    Ovomorph egg = (Ovomorph)ThingMaker.MakeThing(XenoBuildingDefOf.XMT_Ovomorph);
                    BioUtility.TryApplyHorrorGenePayload(egg, payload);
                    // Inventory-style initialization deliberately precedes the egg's SpawnSetup.
                    GenSpawn.Spawn(egg, cell, map, WipeMode.VanishOrMoveAside);
                    Pawn larva = egg.GetComp<CompHatchingEgg>().TrySpawnPawn(cell, 0f, map);
                    HorrorGenePayload inherited = BioUtility.CaptureHorrorGenePayload(larva);
                    NemesisMissionScenarioHarness.Check("hatching retains removal metadata",
                        new HashSet<GeneDef>(payload.RemovedDefaultGenes).SetEquals(inherited.RemovedDefaultGenes));
                    NemesisMissionScenarioHarness.Check("hatching cannot restore omitted defaults",
                        !inherited.Genes.Any(payload.RemovedDefaultGenes.Contains));
                    Pawn host = PawnGenerator.GeneratePawn(PawnKindDefOf.Drifter, Faction.OfPlayer);
                    GenSpawn.Spawn(host, CellFinder.RandomClosewalkCellNear(cell, map, 3), map);
                    GeneDef fire = defaults.FirstOrDefault(gene => gene.defName == "FireTerror");
                    if (fire != null) host.genes.AddGene(fire, false);
                    Hediff pregnancy = BioUtility.MakeEmbryoPregnancy(host);
                    HediffComp_EmbryoPregnancy embryo = pregnancy.TryGetComp<HediffComp_EmbryoPregnancy>();
                    embryo.genes = inherited.ToGeneSet();
                    embryo.removedDefaultGenes = inherited.RemovedDefaultGenes.ToList();
                    host.health.AddHediff(pregnancy);
                    HashSet<Pawn> beforeBirth = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
                    AccessTools.Method(typeof(HediffComp_EmbryoPregnancy), "SpawnChild").Invoke(embryo, new object[] { 0f, 1f });
                    Pawn child = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn => !beforeBirth.Contains(pawn) && XMTUtility.IsXenomorph(pawn));
                    NemesisMissionScenarioHarness.Check("birth produced a cryptimorph", child != null);
                    HorrorGenePayload born = BioUtility.CaptureHorrorGenePayload(child);
                    NemesisMissionScenarioHarness.Check("birth and host merging preserve removed defaults",
                        !born.Genes.Any(payload.RemovedDefaultGenes.Contains)
                        && new HashSet<GeneDef>(payload.RemovedDefaultGenes).SetEquals(born.RemovedDefaultGenes));
                    host.Destroy(DestroyMode.Vanish);
                    child.Destroy(DestroyMode.Vanish);
                    egg.Destroy(DestroyMode.Vanish);
                    larva.Destroy(DestroyMode.Vanish);
                }
                Log.Message("[XMT][Lineage] PASS payload, regular-egg hatching and embryo-birth contracts.");
            }
            catch (Exception exception) { Log.Error("[XMT][Lineage] FAIL " + exception); }
        }
    }
}
