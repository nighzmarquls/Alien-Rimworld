using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public sealed partial class GameComponent_Nemesis
    {
        private List<NemesisStrain> strains = new List<NemesisStrain>();
        private List<NemesisFactionSample> factionSamples = new List<NemesisFactionSample>();
        private List<NemesisGeneFitness> geneFitness = new List<NemesisGeneFitness>();
        private List<NemesisGeneticTrial> geneticTrials = new List<NemesisGeneticTrial>();
        private List<NemesisRecoveryRecord> recoveringMorphs = new List<NemesisRecoveryRecord>();
        private List<Pawn> geneticallyRetainedPawns = new List<Pawn>();
        private int geneticCycle;
        private int nextGeneticTrialId;
        private bool geneticMigrationComplete;
        private int geneticAcquisitionSuppression;
        private sealed class GeneticScenarioScope : IDisposable
        {
            private readonly GameComponent_Nemesis owner;
            public GeneticScenarioScope(GameComponent_Nemesis owner) { this.owner = owner; owner.geneticAcquisitionSuppression++; }
            public void Dispose() { owner.geneticAcquisitionSuppression--; }
        }
        internal IDisposable SuspendGeneticAcquisition() => new GeneticScenarioScope(this);
        public IReadOnlyList<NemesisStrain> Strains => strains;
        public IReadOnlyList<NemesisFactionSample> FactionSamples => factionSamples;
        public IReadOnlyList<NemesisGeneFitness> GeneFitness => geneFitness;
        public IReadOnlyList<NemesisGeneticTrial> GeneticTrials => geneticTrials;
        public IReadOnlyList<NemesisRecoveryRecord> RecoveringMorphs => recoveringMorphs;

        private void ExposeGenetics()
        {
            Scribe_Collections.Look(ref strains, "nemesisStrains", LookMode.Deep);
            Scribe_Collections.Look(ref factionSamples, "nemesisFactionSamples", LookMode.Deep);
            Scribe_Collections.Look(ref geneFitness, "nemesisGeneFitness", LookMode.Deep);
            Scribe_Collections.Look(ref geneticTrials, "nemesisGeneticTrials", LookMode.Deep);
            Scribe_Collections.Look(ref recoveringMorphs, "nemesisRecoveringMorphs", LookMode.Deep);
            Scribe_Collections.Look(ref geneticallyRetainedPawns, "nemesisGeneticallyRetainedPawns", LookMode.Reference);
            Scribe_Values.Look(ref geneticCycle, "geneticCycle");
            Scribe_Values.Look(ref nextGeneticTrialId, "nextGeneticTrialId");
            Scribe_Values.Look(ref geneticMigrationComplete, "geneticMigrationComplete");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                strains ??= new List<NemesisStrain>(); factionSamples ??= new List<NemesisFactionSample>();
                geneFitness ??= new List<NemesisGeneFitness>(); geneticTrials ??= new List<NemesisGeneticTrial>();
                recoveringMorphs ??= new List<NemesisRecoveryRecord>();
                geneticallyRetainedPawns ??= new List<Pawn>();
                geneticallyRetainedPawns.RemoveAll(pawn => pawn == null);
                strains.RemoveAll(value => value == null); geneFitness.RemoveAll(value => value?.gene == null);
                geneticTrials.RemoveAll(value => value?.mission == null);
                recoveringMorphs.RemoveAll(value => value?.pawn == null);
            }
        }

        internal List<RoyalEvolutionDef> ProjectedGeneticEvolutions => QueenProgressionUtility.PredictedFeralQueenEvolutions(
            Awakened ? CurrentStance?.evolutionSet ?? RoyalEvolutionDefOf.BaseQueenSet : RoyalEvolutionDefOf.BaseQueenSet,
            XenoformingUtility.GetXenoforming());

        private HashSet<RoyalEvolutionDef> ProjectedFeatures(bool hediffs)
        {
            List<RoyalEvolutionDef> purchased = ProjectedGeneticEvolutions;
            HashSet<RoyalEvolutionDef> result = new HashSet<RoyalEvolutionDef>();
            void Collect(RoyalEvolutionDef evolution)
            {
                if (evolution == null || !result.Add(evolution)) return;
                if (hediffs ? evolution.preserveHediff : evolution.preserveReplacedFeatures)
                    foreach (RoyalEvolutionDef prior in evolution.replaces ?? new List<RoyalEvolutionDef>())
                        if (purchased.Contains(prior)) Collect(prior);
            }
            foreach (RoyalEvolutionDef evolution in purchased)
                if (!purchased.Any(other => other != evolution && other.replaces?.Contains(evolution) == true)) Collect(evolution);
            return result;
        }

        internal bool GeneticFeature(RoyalEvolutionDef evolution) => ProjectedFeatures(false).Contains(evolution);
        internal bool CanEditMissionGenes => GeneticFeature(RoyalEvolutionDefOf.Evo_GeneControl);
        internal int ProjectedHereditaryCapacity()
        {
            ThingDef race = InternalDefOf.XMT_Royal_AlienRace;
            float value = race.GetStatValueAbstract(XenoStatDefOf.XMT_HereditaryCapacity);
            float factor = 1f;
            HashSet<string> seen = new HashSet<string>();
            foreach (RoyalEvolutionDef evolution in ProjectedFeatures(true))
            {
                HediffDef hediff = evolution.evolutionHediff;
                if (hediff?.stages.NullOrEmpty() != false) continue;
                HediffStage stage = hediff.stages[hediff.StageAtSeverity(hediff.initialSeverity)];
                int count = evolution.targetBodyPart == null ? 1 : race.race.body.AllParts.Count(part => part.def == evolution.targetBodyPart);
                if (!seen.Add(hediff.defName + ":" + evolution.targetBodyPart?.defName)) continue;
                for (int i = 0; i < count; i++)
                {
                    value += stage.statOffsets.GetStatOffsetFromList(XenoStatDefOf.XMT_HereditaryCapacity);
                    factor *= stage.statFactors.GetStatFactorFromList(XenoStatDefOf.XMT_HereditaryCapacity);
                }
            }
            return Mathf.Max(0, Mathf.RoundToInt(value * factor));
        }

        internal bool HasCapability(string name) => DefDatabase<NemesisCapabilityDef>.GetNamedSilentFail(name)?.Available(this) == true;

        internal NemesisStrain RememberStrain(IEnumerable<GeneDef> genes, string source, bool acquired = true,
            int availableTick = 0, NemesisStrain donor = null)
        {
            HorrorGenePayload payload = GeneLineageUtility.Selection(genes, source);
            string signature = NemesisStrain.Signature(payload.Genes);
            // Keep ancestry separate: equal genomes from different donors have different editing allowances.
            string id = (donor?.donorId ?? donor?.id ?? "source") + "|" + signature;
            NemesisStrain existing = strains.FirstOrDefault(strain => strain.id == id);
            if (existing != null)
            {
                existing.availableTick = Math.Min(existing.availableTick, availableTick);
                existing.acquired |= acquired;
                return existing;
            }
            NemesisStrain result = new NemesisStrain
            {
                id = id, source = source, genes = payload.Genes.ToList(), removedDefaults = payload.RemovedDefaultGenes.ToList(),
                donorId = donor?.donorId ?? donor?.id, donorComplexity = donor?.donorComplexity ?? GenomeComplexity(payload.Genes),
                acquired = acquired, availableTick = availableTick
            };
            strains.Add(result);
            return result;
        }

        private void EnsureDefaultStrain() => RememberStrain(GeneLineageUtility.Defaults, "default cryptimorph");
        private void AcquireProjectedGenes()
        {
            foreach (RoyalEvolutionDef evolution in ProjectedFeatures(false))
                if (!evolution.unlockedGenes.NullOrEmpty())
                    RememberStrain(GeneLineageUtility.Defaults.Concat(evolution.unlockedGenes), "advancement " + evolution.defName);
        }
        internal void AcquireAbduction(Pawn pawn, int availableTick)
        {
            if (!ModsConfig.BiotechActive || pawn == null) return;
            RememberStrain(GeneLineageUtility.Defaults.Concat(BioUtility.GetCanonicalPawnGenes(pawn)), "abducted " + pawn.ThingID, availableTick: availableTick);
        }

        internal void UpdateGeneticAcquisition(float xenoforming)
        {
            if (geneticAcquisitionSuppression > 0 || !ModsConfig.BiotechActive || xenoforming <= 0f) return;
            EnsureDefaultStrain();
            AcquireProjectedGenes();
            int target = Mathf.Clamp(Mathf.FloorToInt(xenoforming / 5f) * 50, 0, 1000);
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading.Where(value => value.def.humanlikeFaction))
            {
                HashSet<XenotypeDef> discovered = new HashSet<XenotypeDef>();
                NemesisFactionSample sample = factionSamples.FirstOrDefault(value => value.faction == faction);
                if (sample == null)
                {
                    sample = CreateFactionSample(faction); factionSamples.Add(sample);
                }
                while (sample.sampled < target && sample.remaining.Sum() > 0)
                {
                    int draw = Rand.Range(0, sample.remaining.Sum());
                    int index = 0;
                    while (draw >= sample.remaining[index]) draw -= sample.remaining[index++];
                    sample.remaining[index]--; sample.sampled++;
                    XenotypeDef xenotype = sample.xenotypes[index];
                    if (xenotype != null && discovered.Add(xenotype)) RememberStrain(GeneLineageUtility.Defaults.Concat(
                        BioUtility.NormalizeGenesForStorage(ThingDefOf.Human, xenotype.genes)), "faction " + xenotype.defName);
                }
            }
        }

        internal static NemesisFactionSample CreateFactionSample(Faction faction)
        {
            Dictionary<XenotypeDef, float> weights = new Dictionary<XenotypeDef, float>();
            XenotypeSet set = faction.def.xenotypeSet;
            if (set != null)
                for (int i = 0; i < set.Count; i++)
                    if (set[i].xenotype != null && set[i].xenotype != XenotypeDefOf.Baseliner && set[i].chance > 0f)
                        weights[set[i].xenotype] = weights.TryGetValue(set[i].xenotype, out float prior) ? prior + set[i].chance : set[i].chance;
            if (faction.def.BaselinerChance > 0f) weights[XenotypeDefOf.Baseliner] = faction.def.BaselinerChance;
            if (weights.Count == 0) weights[XenotypeDefOf.Baseliner] = 1f;
            NemesisFactionSample result = new NemesisFactionSample { faction = faction, xenotypes = weights.Keys.OrderBy(value => value.defName).ToList() };
            float total = weights.Values.Sum();
            // One representative for each positive category; apportion the remainder by largest remainder.
            int slots = Math.Max(0, 1000 - weights.Count);
            List<float> shares = result.xenotypes.Select(value => slots * weights[value] / total).ToList();
            result.remaining = shares.Select(value => 1 + Mathf.FloorToInt(value)).ToList();
            foreach (int index in Enumerable.Range(0, shares.Count).OrderByDescending(i => shares[i] - Mathf.Floor(shares[i]))
                .Take(Math.Max(0, 1000 - result.remaining.Sum()))) result.remaining[index]++;
            return result;
        }

        internal void SampleBiome(PlanetTile tile)
        {
            if (geneticAcquisitionSuppression > 0 || !ModsConfig.BiotechActive || !tile.Valid) return;
            BiomeDef biome = tile.Tile.PrimaryBiome;
            float temperature = GenTemperature.GetTemperatureFromSeasonAtTile(Find.TickManager.TicksAbs, tile);
            List<(PawnKindDef kind, float weight)> animals = new List<(PawnKindDef, float)>();
            foreach (PawnKindDef kind in biome.AllWildAnimals)
            {
                if (kind.race.GetModExtension<AnimalHostGenes>()?.genes.NullOrEmpty() != false) continue;
                if (temperature < kind.race.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin) - 10f
                    || temperature > kind.race.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax) + 10f) continue;
                float weight = biome.CommonalityOfAnimal(kind);
                if (ModsConfig.BiotechActive && tile.Tile.pollution > 0f)
                    weight = Mathf.Lerp(weight, biome.CommonalityOfPollutionAnimal(kind), Mathf.Clamp01(tile.Tile.pollution));
                if (tile.Tile.IsCoastal) weight += biome.CommonalityOfCoastalAnimal(kind);
                foreach (TileMutatorDef mutator in tile.Tile.Mutators)
                    if (mutator.Worker != null) weight *= mutator.Worker.AnimalCommonalityFactorFor(kind, tile);
                if (weight > 0f) animals.Add((kind, weight));
            }
            for (int draw = 0; draw < 2 && animals.Count > 0; draw++)
            {
                var choice = animals.RandomElementByWeight(value => value.weight); animals.Remove(choice);
                RememberStrain(GeneLineageUtility.Defaults.Concat(choice.kind.race.GetModExtension<AnimalHostGenes>().genes),
                    "animal " + choice.kind.race.defName + " in " + biome.defName);
            }
        }

        internal void ResetGeneticCycle()
        {
            if (geneticAcquisitionSuppression > 0) return;
            geneticCycle++; strains.Clear(); factionSamples.Clear(); geneFitness.Clear();
            foreach (Pawn pawn in geneticallyRetainedPawns)
                if (!IsRecovering(pawn)) Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
            geneticallyRetainedPawns.RemoveAll(pawn => !IsRecovering(pawn));
            // In-flight trials finish bookkeeping but cannot repopulate the new cycle.
            foreach (NemesisGeneticTrial trial in geneticTrials) trial.finalized = true;
        }

        private NemesisGeneFitness Fitness(GeneDef gene, NemesisMissionDef profile)
        {
            NemesisGeneFitness result = geneFitness.FirstOrDefault(value => value.gene == gene && value.profile == profile);
            if (result == null) { result = new NemesisGeneFitness { gene = gene, profile = profile }; geneFitness.Add(result); }
            return result;
        }

        internal float StrainFitness(NemesisStrain strain, NemesisMissionDef profile)
        {
            if (strain.genes.Count == 0) return 0.5f;
            return strain.genes.Average(gene =>
            {
                NemesisGeneFitness value = geneFitness.FirstOrDefault(record => record.gene == gene && record.profile == profile);
                return value == null || value.trials == 0 ? 0.5f : value.mean;
            });
        }

        internal static int GenomeComplexity(IEnumerable<GeneDef> genes) => BioUtility.GeneComplexityTotal(
            BioUtility.GetGenesForExpression(InternalDefOf.XMT_Starbeast_AlienRace, genes));
        internal static float GenomeCost(IEnumerable<GeneDef> genes)
        {
            float baseline = Math.Max(1, GenomeComplexity(GeneLineageUtility.Defaults));
            return Mathf.Max(0.25f, (Math.Max(0, GenomeComplexity(genes)) + 1f) / (baseline + 1f));
        }

        internal NemesisStrain SelectStrain(NemesisMissionDef mission, out NemesisGeneticTrial plan, float defaultBudget = float.PositiveInfinity)
        {
            EnsureDefaultStrain();
            AcquireProjectedGenes();
            int tick = Find.TickManager.TicksGame;
            NemesisMissionDef profile = mission.specialGeneConsideration ? mission : null;
            NemesisGeneticTrial prior = geneticTrials.LastOrDefault(value => value.mission == mission && value.finalized
                && !value.probe && value.cycle == geneticCycle && strains.Any(strain => strain.id == value.strainId));
            bool probe = prior != null && tick - prior.startedTick < 600000 && Rand.Chance(prior.successful ? 0.25f : 0.65f);
            List<NemesisStrain> pool = strains.Where(value => value.availableTick <= tick && GenomeCost(value.genes) <= defaultBudget).ToList();
            NemesisStrain donor = probe ? pool.FirstOrDefault(value => value.id == prior.strainId) : null;
            donor ??= pool.RandomElementByWeight(value => Mathf.Max(0.05f, StrainFitness(value, profile))
                * (mission.preferredGenes.Any(value.genes.Contains) ? 4f : 1f));
            plan = new NemesisGeneticTrial { id = ++nextGeneticTrialId, cycle = geneticCycle, mission = mission,
                startedTick = tick, baselineGeneric = prior?.genericScore ?? 0.5f,
                baseline = prior == null ? 0.5f : mission.specialGeneConsideration ? prior.specialistScore : prior.genericScore };
            NemesisStrain selected = donor;
            if (probe)
            {
                List<GeneDef> genes = donor.genes.ToList();
                List<GeneDef> available = (CanEditMissionGenes ? pool.SelectMany(value => value.genes) : donor.genes.Concat(GeneLineageUtility.Defaults))
                    .Concat(ProjectedFeatures(false).SelectMany(value => value.unlockedGenes ?? new List<GeneDef>())).Distinct()
                    .Where(gene => gene != null && !genes.Contains(gene) && Fitness(gene, profile).retryAfterTick <= tick).ToList();
                List<GeneDef> removable = genes.Where(gene => Fitness(gene, profile).retryAfterTick <= tick).ToList();
                if (available.Count > 0 && (removable.Count == 0 || Rand.Chance(0.6f)))
                {
                    plan.added = available.RandomElementByWeight(gene => Mathf.Max(0.05f, Fitness(gene, profile).trials == 0 ? 0.75f : Fitness(gene, profile).mean)
                        * (mission.preferredGenes.Contains(gene) ? 4f : 1f) / (1f + Fitness(gene, profile).failedProbes));
                    genes.Add(plan.added);
                    int allowance = ProjectedHereditaryCapacity() * (CanEditMissionGenes ? 2 : 1);
                    if (GenomeComplexity(genes) > donor.donorComplexity + allowance && removable.Count > 0)
                    {
                        plan.removed = removable.OrderBy(gene => Fitness(gene, profile).mean).First(); genes.Remove(plan.removed);
                    }
                    if (GenomeComplexity(genes) > donor.donorComplexity + allowance) { genes = donor.genes.ToList(); plan.added = null; plan.removed = null; }
                }
                else if (removable.Count > 0)
                {
                    plan.removed = removable.RandomElementByWeight(gene => 1f / (0.1f + Mathf.Max(0f, Fitness(gene, profile).mean)));
                    genes.Remove(plan.removed);
                }
                if (plan.added != null || plan.removed != null)
                {
                    if (genes.All(gene => gene.prerequisite == null || genes.Contains(gene.prerequisite))
                        && GenomeCost(genes) <= defaultBudget)
                    {
                        selected = RememberStrain(genes, "probe " + mission.defName, false, donor: donor); plan.probe = true;
                    }
                    else { plan.added = null; plan.removed = null; }
                }
            }
            plan.strainId = selected.id;
            return selected;
        }

        internal void ApplyQueenGenome(Pawn queen)
        {
            if (!Awakened || queen?.genes == null) return;
            EnsureDefaultStrain();
            Pawn donor = Find.WorldPawns.AllPawnsAlive.Where(value => EligibleGeneticMember(value) && !value.Downed && !value.Spawned
                && value.Faction != Faction.OfPlayer && !CaravanUtility.IsCaravanMember(value)).RandomElementWithFallback();
            NemesisStrain strain = donor != null && Rand.Chance(0.85f)
                ? RememberStrain(BioUtility.GetCanonicalPawnGenes(donor), "escaped founder " + donor.ThingID)
                : strains.Where(value => value.availableTick <= Find.TickManager.TicksGame).RandomElement();
            if (queen.GetComp<CompQueen>()?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression) == true)
                strain = strains.Where(value => value.availableTick <= Find.TickManager.TicksGame)
                    .OrderByDescending(value => StrainFitness(value, null)).First();
            BioUtility.TryApplyHorrorGenePayload(queen, strain.Payload, true);
        }

        internal void RefreshQueenExpression(Pawn queen = null)
        {
            queen ??= XMTUtility.GetQueen();
            if (queen == null || queen.Dead || queen.Faction == Faction.OfPlayer || !GeneticFeature(RoyalEvolutionDefOf.Evo_GeneSelfExpression)
                || queen.GetComp<CompQueen>()?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression) != true) return;
            NemesisStrain strain = strains.Where(value => value.availableTick <= Find.TickManager.TicksGame)
                .OrderByDescending(value => StrainFitness(value, null)).FirstOrDefault();
            if (strain == null || NemesisStrain.Signature(BioUtility.GetCanonicalPawnGenes(queen)) == NemesisStrain.Signature(strain.genes)) return;
            BioUtility.TryApplyHorrorGenePayload(queen, strain.Payload, true);
        }

        internal float GeneticOpportunityBonus()
        {
            List<GeneDef> available = strains.Where(value => value.availableTick <= Find.TickManager.TicksGame)
                .SelectMany(value => value.genes).Distinct().ToList();
            if (available.Count == 0) return 0f;
            return 0.15f * available.Count(gene => !geneFitness.Any(value => value.gene == gene && value.trials > 0)) / available.Count;
        }

        private void InitializeGeneticMigration()
        {
            if (geneticMigrationComplete || !ModsConfig.BiotechActive) return;
            geneticMigrationComplete = true;
            foreach (NemesisXenotypeRecord record in xenotypeKnowledge.Where(value => value.abductedCount > 0))
                RememberStrain(GeneLineageUtility.Defaults.Concat(record.genes), "legacy abduction", availableTick: record.availableTick);
        }
    }
}
