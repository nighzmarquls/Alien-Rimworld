using RimWorld;
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
        int _totalSpentEvoPoints = 0;

        public int TotalEvoPoints => _totalEvoPoints;

        public int TotalSpentEvoPoints => _totalSpentEvoPoints;
        public int AvailableEvoPoints => _totalEvoPoints - _totalSpentEvoPoints;

        static private Texture2D evolutionTexture => ContentFinder<Texture2D>.Get("UI/Rituals/XMT_Evolution");
        Pawn Parent => parent as Pawn;

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
            Log.Message(parent + " recieved: " + input + " progress: " + progress);
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
            if (ModsConfig.RoyaltyActive)
            {
                if (ModLister.CheckRoyalty("Psylinkable"))
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
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(Parent.Drafted)
            {
                yield break;
            }

            Command Command_Evolution = new Command_Evolution
            {
                defaultLabel = "Choose Traits",
                defaultDesc = "express different morphological traits.",
                action = delegate
                {
                    Dialogue_Evolution window = new Dialogue_Evolution("Choose Traits", Parent, this);
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
            Scribe_Values.Look(ref _advancementForPsyLink, "advancementForPsyLink", 0);
            Scribe_Values.Look(ref _totalSpentEvoPoints, "totalSpentEvoPoints", 0);
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            XMTUtility.DeclareQueen(Parent);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            XMTUtility.QueenDied(Parent);
        }

        public bool HasDependencies(RoyalEvolutionDef evolution, out RoyalEvolutionDef[] dependencies)
        {
            bool foundDependencies = false;
            List<RoyalEvolutionDef> list = new List<RoyalEvolutionDef> ();
            foreach(RoyalEvolutionDef evoDef in chosenEvolutions)
            {
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
        }

        private void AddEvolutionFeatures(RoyalEvolutionDef evolution)
        {
            if(evolution.replaces != null)
            {
                foreach (RoyalEvolutionDef replaced in evolution.replaces)
                {
                    if (chosenEvolutions.Contains(replaced))
                    {
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

        }

        internal void AddEvolution(RoyalEvolutionDef evolution)
        {
            if(chosenEvolutions.Contains(evolution))
            {
                return;
            }
            AddEvolutionFeatures(evolution);
            _totalSpentEvoPoints += evolution.evoPointCost;
            chosenEvolutions.Add(evolution);
        }

        internal void RemoveEvolution(RoyalEvolutionDef evolution)
        {
            if (!chosenEvolutions.Contains(evolution))
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
        public CompQueenProperties()
        {
            this.compClass = typeof(CompQueen);
        }

    }
}
