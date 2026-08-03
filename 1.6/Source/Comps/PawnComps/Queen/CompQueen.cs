using RimWorld;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;


namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class CompQueen : ThingComp
    {
        float progress = 0;
        float lastBenefit = 0;
        int _totalEvoPoints = 0;
        int _advancementForPsyLink = 1;
        int _advancementForBiotic = 1;
        int _totalSpentEvoPoints = 0;
        private const float QueenAidPainThreshold = 0.25f;
        private const int HarmedQueenThoughtMaxCount = 3;

        public int TotalEvoPoints => _totalEvoPoints;
        public float SubjugationBaseRange => Props.subjugationBaseRange;

        public int TotalSpentEvoPoints => _totalSpentEvoPoints;
        public int AvailableEvoPoints => _totalEvoPoints - _totalSpentEvoPoints;

        static private Texture2D evolutionTexture => ContentFinder<Texture2D>.Get("UI/Rituals/XMT_Evolution");
        Pawn Parent => parent as Pawn;
        CompQueenProperties Props => props as CompQueenProperties;

        public List<RoyalEvolutionDef> ChosenEvolutions
        {
            get
            {
                if(chosenEvolutions == null)
                {
                    chosenEvolutions = new List<RoyalEvolutionDef>();
                }

                return chosenEvolutions;
            }

            set
            {
                chosenEvolutions = value?.Where(evolution => evolution != null).Distinct().ToList()
                    ?? new List<RoyalEvolutionDef>();
                _totalSpentEvoPoints = chosenEvolutions.Sum(evolution => evolution.evoPointCost);
                ReconcileEvolutionFeatures();
            }
        }
        

        private List<RoyalEvolutionDef> chosenEvolutions;
  
        public void RecieveProgress(float input)
        {
            
            progress += input;
            
            int totalNewBenefits = Mathf.FloorToInt(progress - lastBenefit);

            if(totalNewBenefits > 0)
            {
                for(int i = 0; i < totalNewBenefits; i++)
                {
                    GainProgressBenefit();
                }
            }
            lastBenefit = progress;


        }

        private void GainProgressBenefit()
        {
            _totalEvoPoints++;

            if (ModsConfig.IsActive("RimEffectRenegade.AsariReapers"))
            {
                if (Parent.genes != null)
                {
                    if (Parent.genes.HasActiveGene(ExternalDefOf.XMT_NaturalBiotic))
                    {
                        Hediff firstHediffOfDef = Parent.health.hediffSet.GetFirstHediffOfDef(ExternalDefOf.RE_BioticNatural);
                        if (_totalEvoPoints > _advancementForBiotic)
                        {
                            if (firstHediffOfDef == null)
                            {
                                Parent.health.AddHediff(ExternalDefOf.RE_BioticNatural, Parent.health.hediffSet.GetBodyPartRecord(InternalDefOf.StarbeastBrain));
                            }
                            else
                            {
                                ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                            }

                            if (_advancementForBiotic == 1)
                            {
                                _advancementForBiotic += 2;
                            }
                            else if (_advancementForBiotic == 3)
                            {
                                _advancementForBiotic += 5;
                            }
                            else
                            {
                                _advancementForBiotic += 8;
                            }
                        }
                    }
                }
            }

            if (ModsConfig.RoyaltyActive)
            {
                if (_totalEvoPoints == _advancementForPsyLink)
                {
                    Parent.ChangePsylinkLevel(1);
                    Find.History.Notify_PsylinkAvailable();
                    if (_advancementForPsyLink == 1)
                    {
                        _advancementForPsyLink += 2;
                    }
                    else if(_advancementForPsyLink == 3)
                    {
                        _advancementForPsyLink += 5;
                    }
                    else
                    {
                        _advancementForPsyLink += 8;
                    }
                }
            }

            
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {

            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Drafted)
            {
                yield break;
            }

            foreach(Hediff hediff in Parent.health.hediffSet.hediffs)
            {
                if(hediff.def == XenoGeneDefOf.XMT_GeneIntegration)
                {
                    yield break;
                }

                if(hediff.def == RoyalEvolutionDefOf.XMT_TornEggSack)
                {
                    yield break;
                }
            }

            Command Command_Evolution = new Command_Evolution
            {
                defaultLabel = "XMT_EvolutionLabel".Translate(),
                defaultDesc = "XMT_EvolutionDescription".Translate(),
                action = delegate
                {
                    Dialogue_Evolution window = new Dialogue_Evolution("XMT_EvolutionLabel".Translate(), Parent, this);
                    Find.WindowStack.Add(window);
                },
                icon = evolutionTexture
            };
            yield return Command_Evolution;

            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "DEV: Gain Advancement Point";
                command_Action.action = delegate
                {
                    RecieveProgress(1);
                };
                yield return command_Action;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref progress, "progress", 0);
            Scribe_Values.Look(ref lastBenefit, "lastBenefit", progress);
            Scribe_Collections.Look(ref chosenEvolutions, "chosenEvolutions");
            Scribe_Values.Look(ref _totalEvoPoints, "totalEvoPoints", 0);
            Scribe_Values.Look(ref _advancementForPsyLink, "advancementForPsyLink", 1);
            Scribe_Values.Look(ref _advancementForBiotic, "advancementForBiotic", 1);
            Scribe_Values.Look(ref _totalSpentEvoPoints, "totalSpentEvoPoints", 0); 

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                chosenEvolutions = chosenEvolutions?.Where(evolution => evolution != null).Distinct().ToList()
                    ?? new List<RoyalEvolutionDef>();
                _totalSpentEvoPoints = chosenEvolutions.Sum(evolution => evolution.evoPointCost);
                ReconcileEvolutionFeatures();
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            XMTUtility.DeclareQueen(Parent);

            ReconcileEvolutionFeatures();
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
           
            XMTUtility.QueenDied(Parent);
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            Pawn aggressor = dinfo.Instigator as Pawn;

            if (aggressor != null)
            {
                if (aggressor.Dead)
                {
                    return;
                }

                if (aggressor == Parent)
                {
                    return;
                }

                if (totalDamageDealt > 0 && (XMTUtility.IsXenomorph(aggressor) || aggressor.HasBrainMutation()))
                {
                    XMTUtility.GiveMemory(aggressor, HorrorMoodDefOf.StarbeastHarmedQueenMood, HarmedQueenThoughtMaxCount);
                    XMTUtility.GiveInteractionMemory(aggressor, HorrorMoodDefOf.StarbeastHarmedMyQueen, Parent);
                }

                if (XMTUtility.IsXenomorph(aggressor))
                {
                    return;
                }

                CompPawnInfo info = aggressor.Info();

                if (info != null)
                {
                    info.ApplyThreatPheromone(Parent,1,10);
                }

                if(XenoformingUtility.XenoformingMeets(10))
                {
                    if (Parent.Downed || Parent.health.hediffSet.PainTotal >= QueenAidPainThreshold || Parent.health.hediffSet.BleedRateTotal > 0.1f)
                    {
                        if (XenoformingUtility.QueenCalledForAid(Parent, aggressor))
                        {
                            if (ModsConfig.RoyaltyActive)
                            {
                                FleckMaker.Static(Parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 10f);
                            }
                        }
                    }
                }
            }
        }
        public bool HasDependencies(RoyalEvolutionDef evolution, out RoyalEvolutionDef[] dependencies)
        {
            bool foundDependencies = false;
            List<RoyalEvolutionDef> list = new List<RoyalEvolutionDef> ();
            foreach(RoyalEvolutionDef evoDef in ChosenEvolutions)
            {
                if (!HasActiveEvolution(evoDef))
                {
                    continue;
                }

                if(evoDef.prerequisites == null || evoDef.prerequisites.Count == 0)
                {
                    continue;
                }

                if(evoDef.prerequisites.Contains(evolution))
                {
                    foundDependencies = true;
                    list.Add(evoDef);
                }
            }
            dependencies = list.ToArray();
            return foundDependencies;
        }

        public bool HasActiveEvolution(RoyalEvolutionDef evolution)
        {
            return evolution != null && ChosenEvolutions.Contains(evolution) && !IsEvolutionReplaced(evolution);
        }

        public bool HasEvolutionFeature(RoyalEvolutionDef evolution)
        {
            return TryGetEvolutionFeatureProvider(evolution, out _);
        }

        public bool HasFunctionalEvolutionFeature(RoyalEvolutionDef evolution)
        {
            if (!TryGetEvolutionFeatureProvider(evolution, out RoyalEvolutionDef provider))
            {
                return false;
            }

            return EvolutionBodyPartIntact(provider);
        }

        public bool TryGetEvolutionFeatureProvider(RoyalEvolutionDef evolution, out RoyalEvolutionDef provider)
        {
            provider = null;
            if (evolution == null)
            {
                return false;
            }

            foreach (RoyalEvolutionDef activeEvolution in ActiveEvolutions())
            {
                if (EvolutionProvides(activeEvolution, evolution, preserveHediffs: false, new HashSet<RoyalEvolutionDef>()))
                {
                    provider = activeEvolution;
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<RoyalEvolutionDef> EvolutionFeatures()
        {
            HashSet<RoyalEvolutionDef> features = new HashSet<RoyalEvolutionDef>();
            foreach (RoyalEvolutionDef activeEvolution in ActiveEvolutions())
            {
                CollectProvidedEvolutions(activeEvolution, preserveHediffs: false, features);
            }

            return features;
        }

        private IEnumerable<RoyalEvolutionDef> ActiveEvolutions()
        {
            return ChosenEvolutions.Where(HasActiveEvolution);
        }

        private bool EvolutionProvides(RoyalEvolutionDef current, RoyalEvolutionDef requested, bool preserveHediffs, HashSet<RoyalEvolutionDef> visited)
        {
            if (current == null || !visited.Add(current))
            {
                return false;
            }

            if (current == requested)
            {
                return true;
            }

            bool preserves = preserveHediffs ? current.preserveHediff : current.preserveReplacedFeatures;
            if (!preserves || current.replaces.NullOrEmpty())
            {
                return false;
            }

            foreach (RoyalEvolutionDef replaced in current.replaces)
            {
                if (replaced != null && ChosenEvolutions.Contains(replaced)
                    && EvolutionProvides(replaced, requested, preserveHediffs, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectProvidedEvolutions(RoyalEvolutionDef current, bool preserveHediffs, HashSet<RoyalEvolutionDef> results)
        {
            if (current == null || !results.Add(current))
            {
                return;
            }

            bool preserves = preserveHediffs ? current.preserveHediff : current.preserveReplacedFeatures;
            if (!preserves || current.replaces.NullOrEmpty())
            {
                return;
            }

            foreach (RoyalEvolutionDef replaced in current.replaces)
            {
                if (replaced != null && ChosenEvolutions.Contains(replaced))
                {
                    CollectProvidedEvolutions(replaced, preserveHediffs, results);
                }
            }
        }

        private bool EvolutionBodyPartIntact(RoyalEvolutionDef evolution)
        {
            if (evolution?.targetBodyPart == null)
            {
                return true;
            }

            return Parent?.health?.hediffSet?.GetNotMissingParts().Any(part => part.def == evolution.targetBodyPart) == true;
        }

        public bool IsEvolutionReplaced(RoyalEvolutionDef evolution)
        {
            if (evolution == null)
            {
                return false;
            }

            foreach (RoyalEvolutionDef chosenEvolution in ChosenEvolutions)
            {
                if (chosenEvolution == null || chosenEvolution == evolution || chosenEvolution.replaces == null)
                {
                    continue;
                }

                if (chosenEvolution.replaces.Contains(evolution))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetIncompatibleEvolution(RoyalEvolutionDef evolution, out RoyalEvolutionDef blocker)
        {
            blocker = null;
            if (evolution == null)
            {
                return false;
            }

            foreach (RoyalEvolutionDef chosenEvolution in EvolutionFeatures())
            {
                if (chosenEvolution == null || chosenEvolution == evolution)
                {
                    continue;
                }

                if (EvolutionIncompatibleWith(evolution, chosenEvolution))
                {
                    blocker = chosenEvolution;
                    return true;
                }
            }

            return false;
        }

        private bool EvolutionIncompatibleWith(RoyalEvolutionDef evolution, RoyalEvolutionDef other)
        {
            return (evolution.incompatible != null && evolution.incompatible.Contains(other))
                || (other.incompatible != null && other.incompatible.Contains(evolution));
        }

        private void ReconcileEvolutionFeatures()
        {
            if (Parent == null)
            {
                return;
            }

            ReconcileEvolutionHediffs();
            ReconcileEvolutionAbilities();
        }

        private void ReconcileEvolutionAbilities()
        {
            if (Parent.abilities == null)
            {
                return;
            }

            HashSet<AbilityDef> evolutionAbilities = DefDatabase<RoyalEvolutionDef>.AllDefsListForReading
                .Where(evolution => !evolution.unlockedAbilities.NullOrEmpty())
                .SelectMany(evolution => evolution.unlockedAbilities)
                .Where(ability => ability != null)
                .ToHashSet();
            HashSet<AbilityDef> desiredAbilities = EvolutionFeatures()
                .Where(evolution => !evolution.unlockedAbilities.NullOrEmpty())
                .SelectMany(evolution => evolution.unlockedAbilities)
                .Where(ability => ability != null)
                .ToHashSet();

            foreach (AbilityDef abilityDef in evolutionAbilities)
            {
                Ability ability = Parent.abilities.GetAbility(abilityDef);
                if (desiredAbilities.Contains(abilityDef))
                {
                    if (ability == null)
                    {
                        Parent.abilities.GainAbility(abilityDef);
                    }
                }
                else if (ability != null)
                {
                    Parent.abilities.RemoveAbility(abilityDef);
                }
            }
        }

        private void ReconcileEvolutionHediffs()
        {
            if (Parent.health?.hediffSet == null)
            {
                return;
            }

            HashSet<HediffDef> evolutionHediffDefs = DefDatabase<RoyalEvolutionDef>.AllDefsListForReading
                .Select(evolution => evolution.evolutionHediff)
                .Where(hediff => hediff != null)
                .ToHashSet();
            HashSet<RoyalEvolutionDef> desiredEvolutions = new HashSet<RoyalEvolutionDef>();
            foreach (RoyalEvolutionDef activeEvolution in ActiveEvolutions())
            {
                CollectProvidedEvolutions(activeEvolution, preserveHediffs: true, desiredEvolutions);
            }

            HashSet<(HediffDef hediff, BodyPartRecord part)> desiredHediffs = new HashSet<(HediffDef, BodyPartRecord)>();
            foreach (RoyalEvolutionDef evolution in desiredEvolutions.Where(evolution => evolution.evolutionHediff != null))
            {
                if (evolution.targetBodyPart == null)
                {
                    desiredHediffs.Add((evolution.evolutionHediff, null));
                    continue;
                }

                foreach (BodyPartRecord part in Parent.health.hediffSet.GetNotMissingParts().Where(part => part.def == evolution.targetBodyPart))
                {
                    desiredHediffs.Add((evolution.evolutionHediff, part));
                }
            }

            foreach (Hediff hediff in Parent.health.hediffSet.hediffs.ListFullCopy())
            {
                if (evolutionHediffDefs.Contains(hediff.def) && !desiredHediffs.Contains((hediff.def, hediff.Part)))
                {
                    Parent.health.RemoveHediff(hediff);
                }
            }

            foreach ((HediffDef hediffDef, BodyPartRecord part) in desiredHediffs)
            {
                if (!Parent.health.hediffSet.hediffs.Any(hediff => hediff.def == hediffDef && hediff.Part == part))
                {
                    Parent.health.AddHediff(HediffMaker.MakeHediff(hediffDef, Parent, part));
                }
            }
        }

        internal void AddEvolution(RoyalEvolutionDef evolution)
        {
            if(ChosenEvolutions.Contains(evolution))
            {
                return;
            }
            chosenEvolutions.Add(evolution);
            _totalSpentEvoPoints += evolution.evoPointCost;
            ReconcileEvolutionFeatures();
        }

        internal void RemoveEvolution(RoyalEvolutionDef evolution)
        {
            if (!ChosenEvolutions.Contains(evolution))
            {
                return;
            }
            chosenEvolutions.Remove(evolution);
            _totalSpentEvoPoints -= evolution.evoPointCost;
            ReconcileEvolutionFeatures();
        }
    }

    public class CompQueenProperties : CompProperties
    {
        public float subjugationBaseRange = 5f;

        public CompQueenProperties()
        {
            this.compClass = typeof(CompQueen);
        }

    }
}
