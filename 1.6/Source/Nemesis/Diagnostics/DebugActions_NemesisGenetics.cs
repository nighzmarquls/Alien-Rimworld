using HarmonyLib;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal static class DebugActions_NemesisGenetics
    {
        internal static DebugActionNode Menu() => new DebugActionNode("Genetics", DebugActionType.Action, null)
        {
            childGetter = () => new List<DebugActionNode>
            {
                new DebugActionNode("Inspect templates, gene fitness and trials", DebugActionType.Action, Inspect),
                new DebugActionNode("Run genetic contracts (isolated state)", DebugActionType.Action, RunContracts),
                new DebugActionNode("Run regular-egg lineage contracts", DebugActionType.Action, DebugActions_OvomorphLineage.RunContracts),
                new DebugActionNode("Complete due recovery now", DebugActionType.Action, () =>
                {
                    GameComponent_Nemesis component = Current.Game.GetComponent<GameComponent_Nemesis>();
                    foreach (NemesisRecoveryRecord record in component.RecoveringMorphs) record.readyTick = Find.TickManager.TicksGame;
                    component.DebugTickGenetics();
                })
            }
        };
        private static void Inspect()
        {
            GameComponent_Nemesis component = Current.Game.GetComponent<GameComponent_Nemesis>();
            Log.Message("[XMT][Genetics] capacity=" + component.ProjectedHereditaryCapacity() + " control=" + component.CanEditMissionGenes);
            foreach (NemesisStrain strain in component.Strains)
                Log.Message("[XMT][Genetics] strain=" + strain.source + " genes=" + NemesisStrain.Signature(strain.genes)
                    + " removed=" + NemesisStrain.Signature(strain.removedDefaults) + " donorComplexity=" + strain.donorComplexity
                    + " cost=" + GameComponent_Nemesis.GenomeCost(strain.genes)
                    + " available=" + strain.availableTick);
            foreach (NemesisGeneFitness fitness in component.GeneFitness)
                Log.Message("[XMT][Genetics] gene=" + fitness.gene.defName + " profile=" + (fitness.profile?.defName ?? "generic")
                    + " mean=" + fitness.mean + " trials=" + fitness.trials + " failedProbes=" + fitness.failedProbes + " retry=" + fitness.retryAfterTick);
            foreach (NemesisGeneticTrial trial in component.GeneticTrials)
                Log.Message("[XMT][Genetics] trial=" + trial.id + " mission=" + trial.mission.defName + " probe=" + trial.probe
                    + " added=" + trial.added?.defName + " removed=" + trial.removed?.defName + " baseline=" + trial.baseline
                    + " generic=" + trial.genericScore + " specialist=" + trial.specialistScore + " finalized=" + trial.finalized
                    + " escaped=" + trial.members.Count(member => member.escaped) + "/" + trial.members.Count);
        }
        private static void RunContracts()
        {
            if (!ModsConfig.BiotechActive || Find.CurrentMap == null) return;
            Rand.PushState(73191);
            try
            {
                GameComponent_Nemesis isolated = new GameComponent_Nemesis(Current.Game);
                isolated.UpdateGeneticAcquisition(5f);
                Check("five percent withdraws fifty per faction", isolated.FactionSamples.All(value => value.sampled == 50 && value.remaining.Sum() == 950));
                string before = string.Join(";", isolated.FactionSamples.Select(value => string.Join(",", value.remaining)));
                isolated.UpdateGeneticAcquisition(5f); isolated.UpdateGeneticAcquisition(1f);
                Check("repeated and reduced percentages cannot resample", before == string.Join(";", isolated.FactionSamples.Select(value => string.Join(",", value.remaining))));
                isolated.UpdateGeneticAcquisition(50f);
                Check("half population sampled", isolated.FactionSamples.All(value => value.sampled == 500 && value.remaining.Sum() == 500));
                isolated.UpdateGeneticAcquisition(100f);
                Check("entire population sampled", isolated.FactionSamples.All(value => value.sampled == 1000 && value.remaining.Sum() == 0));
                foreach (NemesisFactionSample sample in isolated.FactionSamples)
                    Check("every faction xenotype acquired", sample.xenotypes.All(xenotype => isolated.Strains.Any(strain =>
                        BioUtility.NormalizeGenesForStorage(ThingDefOf.Human, xenotype.genes).All(strain.genes.Contains))));
                float normal = GameComponent_Nemesis.GenomeCost(GeneLineageUtility.Defaults);
                Check("default genome costs one", Math.Abs(normal - 1f) < 0.001f);
                Check("empty genome cheaper and finite", GameComponent_Nemesis.GenomeCost(new List<GeneDef>()) > 0f
                    && GameComponent_Nemesis.GenomeCost(new List<GeneDef>()) < normal);
                NemesisGeneticTrial trial = new NemesisGeneticTrial { members = new List<NemesisGeneticMember> { new NemesisGeneticMember() } };
                float failed = NemesisGeneEvaluationWorker.GenericFitness(trial);
                trial.members[0].escaped = true;
                Check("escape dominates generic fitness", NemesisGeneEvaluationWorker.GenericFitness(trial) > failed + 0.5f);
                trial.members[0].escaped = false; trial.members[0].kills = 2;
                Check("kills alone cannot substitute for escape", NemesisGeneEvaluationWorker.GenericFitness(trial) < 0.5f);
                NemesisGeneFitness fitness = new NemesisGeneFitness(); fitness.Observe(0.2f); fitness.Observe(0.8f);
                Check("continuous fitness retains partial results", fitness.trials == 2 && Math.Abs(fitness.mean - 0.5f) < 0.001f);
                foreach (string stanceName in new[] { "XMT_NemesisStance_Mechanitor", "XMT_NemesisStance_Juggernaut", "XMT_NemesisStance_FleshShaper" })
                {
                    NemesisStanceDef stance = DefDatabase<NemesisStanceDef>.GetNamedSilentFail(stanceName);
                    if (stance == null) continue;
                    int templateCount = isolated.Strains.Count;
                    AccessTools.Field(typeof(GameComponent_Nemesis), "currentStance").SetValue(isolated, stance);
                    AccessTools.Field(typeof(GameComponent_Nemesis), "awakened").SetValue(isolated, true);
                    Check("stance preserves all templates", isolated.Strains.Count == templateCount);
                    PawnGenerationRequest request = new PawnGenerationRequest(XenoPawnKindDefOf.XMT_RoyaltyKind, null);
                    request.ForceGenerateNewPawn = true; request.CanGeneratePawnRelations = false;
                    request.ForceNoGear = true; request.ForceNoBackstory = true; request.ForceNoIdeo = true;
                    request.ForceBaselinerChance = 100;
                    Pawn queen = PawnGenerator.GeneratePawn(request);
                    try
                    {
                        foreach (RoyalEvolutionDef evolution in QueenProgressionUtility.PredictedFeralQueenEvolutions(stance.evolutionSet, XenoformingUtility.GetXenoforming()))
                            queen.GetComp<CompQueen>().AddEvolution(evolution);
                        Check("projected capacity matches actual " + stanceName,
                            isolated.ProjectedHereditaryCapacity() == BioUtility.GetHereditaryCapacity(queen));
                    }
                    finally { queen.Destroy(DestroyMode.Vanish); }
                }
                isolated.ResetGeneticCycle();
                Check("cycle reset clears acquisition and evaluations", isolated.Strains.Count == 0 && isolated.FactionSamples.Count == 0 && isolated.GeneFitness.Count == 0);
                Log.Message("[XMT][Genetics] PASS genetic contracts. Live Nemesis state was not changed.");
            }
            catch (Exception exception) { Log.Error("[XMT][Genetics] FAIL " + exception); }
            finally { Rand.PopState(); }
        }
        private static void Check(string label, bool passed) => NemesisMissionScenarioHarness.Check(label, passed);
    }
}
