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
                if (value == null || value.Count == 0)
                {
                    foreach (RoyalEvolutionDef e in chosenEvolutions)
                    {
                        RemoveEvolutionFeatures(e,true);
                    }
                    chosenEvolutions.Clear();
                    _totalSpentEvoPoints = 0;
                }

                if (chosenEvolutions == null)
                {
                    chosenEvolutions = value;
                }
                else
                {
                    foreach (RoyalEvolutionDef e in chosenEvolutions)
                    {
                        if (!value.Contains(e))
                        {
                            RemoveEvolutionFeatures(e, true);
                            _totalSpentEvoPoints -= e.evoPointCost;
                        }
                    }
                }

                foreach (RoyalEvolutionDef e in value)
                {
                    if (!chosenEvolutions.Contains(e))
                    {
                        AddEvolutionFeatures(e);
                        _totalSpentEvoPoints += e.evoPointCost;
                    }
                }

                chosenEvolutions = value;
            
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
            
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            XMTUtility.DeclareQueen(Parent);

            foreach (RoyalEvolutionDef evolution in ChosenEvolutions)
            {
                if (HasActiveEvolution(evolution))
                {
                    AddUnlockedAbilities(evolution);
                }
            }
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

            foreach (RoyalEvolutionDef chosenEvolution in ChosenEvolutions)
            {
                if (chosenEvolution == null || chosenEvolution == evolution || !HasActiveEvolution(chosenEvolution))
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

        private void AddUnlockedAbilities(RoyalEvolutionDef evolution)
        {
            if (evolution?.unlockedAbilities == null || Parent?.abilities == null)
            {
                return;
            }

            foreach (AbilityDef abilityDef in evolution.unlockedAbilities)
            {
                if (abilityDef != null && Parent.abilities.GetAbility(abilityDef) == null)
                {
                    Parent.abilities.GainAbility(abilityDef);
                }
            }
        }

        private void RemoveUnlockedAbilities(RoyalEvolutionDef evolution, RoyalEvolutionDef replacement = null)
        {
            if (evolution?.unlockedAbilities == null || Parent?.abilities == null)
            {
                return;
            }

            foreach (AbilityDef abilityDef in evolution.unlockedAbilities)
            {
                if (abilityDef == null || ShouldKeepUnlockedAbility(abilityDef, evolution, replacement))
                {
                    continue;
                }

                Ability ability = Parent.abilities.GetAbility(abilityDef);
                if (ability != null)
                {
                    Parent.abilities.RemoveAbility(abilityDef);
                }
            }
        }

        private bool ShouldKeepUnlockedAbility(AbilityDef abilityDef, RoyalEvolutionDef removingEvolution, RoyalEvolutionDef replacement)
        {
            if (replacement?.unlockedAbilities != null && replacement.unlockedAbilities.Contains(abilityDef))
            {
                return true;
            }

            foreach (RoyalEvolutionDef chosenEvolution in ChosenEvolutions)
            {
                if (chosenEvolution == null || chosenEvolution == removingEvolution || !HasActiveEvolution(chosenEvolution))
                {
                    continue;
                }

                if (chosenEvolution.unlockedAbilities != null && chosenEvolution.unlockedAbilities.Contains(abilityDef))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveEvolutionFeatures(RoyalEvolutionDef evolution, bool replacing = false)
        {
            if (evolution.replaces != null)
            {
                if (!replacing)
                {
                    foreach (RoyalEvolutionDef replaced in evolution.replaces)
                    {
                        if (chosenEvolutions.Contains(replaced))
                        {
                            if (replaced.evolutionHediff != null)
                            {
                                BodyPartDef bodyPart = replaced.targetBodyPart;

                                if (bodyPart != null)
                                {
                                    IEnumerable<BodyPartRecord> bodyparts = Parent.health.hediffSet.GetNotMissingParts();

                                    foreach (BodyPartRecord partRecord in bodyparts)
                                    {
                                        if (partRecord.def == bodyPart)
                                        {
                                            Hediff evoHediff = HediffMaker.MakeHediff(replaced.evolutionHediff, Parent, partRecord);
                                            Parent.health.AddHediff(evoHediff);
                                        }
                                    }
                                }
                                else
                                {
                                    Hediff evoHediff = HediffMaker.MakeHediff(replaced.evolutionHediff, Parent);
                                    Parent.health.AddHediff(evoHediff);
                                }
                            }
                        }
                    }
                }
            }

            if (evolution.evolutionHediff != null)
            {
                List<Hediff> hediffList = Parent.health.hediffSet.hediffs.ToList();
                foreach (Hediff hediff in hediffList)
                {
                    if(hediff.def == evolution.evolutionHediff)
                    {
                        Parent.health.RemoveHediff(hediff);
                    }
                }
            }

            RemoveUnlockedAbilities(evolution);
        }

        private void AddEvolutionFeatures(RoyalEvolutionDef evolution)
        {
            if(evolution.replaces != null)
            {
                foreach (RoyalEvolutionDef replaced in evolution.replaces)
                {
                    if (chosenEvolutions.Contains(replaced))
                    {
                        RemoveUnlockedAbilities(replaced, evolution);

                        if (replaced.evolutionHediff != null)
                        {
                            List<Hediff> hediffs = Parent.health.hediffSet.hediffs.ListFullCopy() ;
                            foreach (Hediff hediff in hediffs)
                            {
                                if(hediff == null)
                                {
                                    continue;
                                }

                                if (hediff.def == replaced.evolutionHediff)
                                {
                                    Parent.health.RemoveHediff(hediff);
                                }
                            }
                        }
                    }
                }
            }

            if(evolution.evolutionHediff != null)
            {
                
                BodyPartDef bodyPart = evolution.targetBodyPart;

                if (bodyPart != null)
                {
                    IEnumerable<BodyPartRecord> bodyparts = Parent.health.hediffSet.GetNotMissingParts();

                    foreach (BodyPartRecord partRecord in bodyparts)
                    {
                        if (partRecord.def == bodyPart)
                        {
                            Hediff evoHediff = HediffMaker.MakeHediff(evolution.evolutionHediff, Parent, partRecord);
                            Parent.health.AddHediff(evoHediff);
                        }
                    }
                }
                else
                {
                    Hediff evoHediff = HediffMaker.MakeHediff(evolution.evolutionHediff, Parent);
                    Parent.health.AddHediff(evoHediff);
                }
            }

            AddUnlockedAbilities(evolution);
        }

        internal void AddEvolution(RoyalEvolutionDef evolution)
        {
            if(ChosenEvolutions.Contains(evolution))
            {
                return;
            }
            AddEvolutionFeatures(evolution);
            _totalSpentEvoPoints += evolution.evoPointCost;
            chosenEvolutions.Add(evolution);
        }

        internal void RemoveEvolution(RoyalEvolutionDef evolution)
        {
            if (!ChosenEvolutions.Contains(evolution))
            {
                return;
            }
            RemoveEvolutionFeatures(evolution);
            _totalSpentEvoPoints -= evolution.evoPointCost;
            chosenEvolutions.Remove(evolution);
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
