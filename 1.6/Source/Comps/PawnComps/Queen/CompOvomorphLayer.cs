using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class CompOvomorphLayer : ThingComp
    {

        static private Texture2D OvomorphTexture => ContentFinder<Texture2D>.Get("UI/Abilities/Ovomorph");
        static private Texture2D LineageTexture => ContentFinder<Texture2D>.Get("UI/Abilities/Lineage");
        Pawn Parent => parent as Pawn;
        CompOvomorphLayerProperties Props => props as CompOvomorphLayerProperties;

        ThingDef selectedEggDef = null;
        GeneSet broodTemplateGenes;
        string broodTemplateName;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref selectedEggDef, "selectedEggDef");
            Scribe_Deep.Look(ref broodTemplateGenes, "broodTemplateGenes");
            Scribe_Values.Look(ref broodTemplateName, "broodTemplateName", "");
        }

        public void SetNextOvomorphAsGene()
        {
            if (Props.geneOvomorphDef != null)
            {
                selectedEggDef = Props.geneOvomorphDef;
            }
        }
        public Rot4 GetLayingFacing(IntVec3 eggspot)
        {
            IntVec3 dif = eggspot - parent.Position;
            if (dif.x < 0)
            {
                return Rot4.East;
            }
            else if (dif.x > 0)
            {
                return Rot4.West;
            }
            else if (dif.z < 0)
            {
                return Rot4.North;
            }
            else
            {
                return Rot4.South;
            }
        }
        public float FoodCost
        {
            get
            {
                return OvomorphLayUtility.GetAdjustedFoodCost(Parent, Props.foodCost);
            }
        }

        public string OvomorphDescription(QueenEggLayOption option)
        {
            ThingDef eggDef = option?.thingDef ?? Props.OvomorphDef;
            string description = "XMT_LayOvomorphDescription".Translate(eggDef?.label ?? "thing");
           
            if (Parent.needs.food == null || Parent.needs.food.CurLevel > FoodCostFor(option))
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(eggDef?.label ?? "thing");

            return description;
        }

        public string GeneOvomorphDescription()
        {
            string description = "XMT_LayOvomorphDescription".Translate(Props.geneOvomorphDef.label); ;

            if (Parent.needs.food == null || Parent.needs.food.CurLevel > FoodCost/2)
            {
                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(Props.geneOvomorphDef.label);

            return description;
        }

        public bool CannotLayOvomorph(float cost)
        {
            return Parent.needs.food != null && Parent.needs.food.CurLevel <= cost;
        }

        public float FoodCostFor(QueenEggLayOption option)
        {
            return FoodCost * (option?.foodCostFactor ?? 1f);
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

            if (Parent.Downed)
            {
                yield break;
            }

            yield return ShapeBroodLineageAction();

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_OvoThrone))
            {
                yield break;
            }

            yield return CreateLaySelectedEggAction(parent, startJob: true);
        }

        public IEnumerable<Gizmo> GetThroneManagementGizmos(Thing positionSource)
        {
            if (Parent == null || Parent.Faction != Faction.OfPlayer || Parent.Dead || Parent.Downed)
            {
                yield break;
            }

            yield return ShapeBroodLineageAction();
            yield return CreateLaySelectedEggAction(positionSource, startJob: false);
        }

        public Command_Action CreateLaySelectedEggAction(Thing positionSource, bool startJob)
        {
            QueenEggLayOption selectedOption = ResolveSelectedEggOption();
            Command_ActionWithOptions action = new Command_ActionWithOptions(delegate
            {
                return EggOptionFloatMenuOptions();
            });

            if (positionSource is EggSack)
            {
                action.defaultLabel = selectedOption?.thingDef?.label;
                action.defaultDesc = OvomorphDescription(selectedOption) + "\n\n" + "XMT_SelectEggOptionDesc".Translate();
                action.icon = selectedOption?.Icon ?? OvomorphTexture;
                action.action = delegate
                {

                };
                return action;
            }

            action.defaultLabel = "XMT_LayOvomorphLabel".Translate(selectedOption?.thingDef?.label ?? "thing");
            action.defaultDesc = OvomorphDescription(selectedOption) + "\n\n" + "XMT_SelectEggOptionDesc".Translate();
            action.icon = selectedOption?.Icon ?? OvomorphTexture;
            action.action = delegate
            {
                AcceptanceReport report = CanUseEggOption(selectedOption);
                if (!report.Accepted)
                {
                    return;
                }
                else if (CannotLayOvomorph(FoodCostFor(selectedOption)))
                {
                    return;
                }
               
                StartLaySelectedEggTargeting(positionSource, startJob);
            };

            AcceptanceReport report = CanUseEggOption(selectedOption);
            if (!report.Accepted)
            {
                action.defaultDesc += "\n" + report.Reason;
            }
            else if (CannotLayOvomorph(FoodCostFor(selectedOption)))
            {


            }

            return action;
        }

        private void StartLaySelectedEggTargeting(Thing positionSource, bool startJob)
        {
            QueenEggLayOption selectedOption = ResolveSelectedEggOption();
            AcceptanceReport report = CanUseEggOption(selectedOption);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return;
            }

            TargetingParameters LayOvomorphParameters = TargetingParameters.ForCell();

            LayOvomorphParameters.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                Thing source = positionSource ?? parent;
                return source.Spawned && target.Map.reachability.CanReach(source.PositionHeld, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Find.Targeter.BeginTargeting(LayOvomorphParameters, delegate (LocalTargetInfo target)
            {
                SelectEggOption(selectedOption);
                FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.MapHeld, target.Thing);

                if (startJob)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_LayOvomorph, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                else
                {
                    LayOvomorph(target.Cell, positionSource);
                }
            });
        }

        private Command_Action ShapeBroodLineageAction()
        {
            Command_Action action = new Command_Action();
            action.defaultLabel = "XMT_ShapeBroodLineageLabel".Translate();
            action.defaultDesc = "XMT_ShapeBroodLineageDesc".Translate();
            action.icon = LineageTexture;
            action.action = delegate
            {
                OpenBroodLineageDialog();
            };
            return action;
        }

        private void OpenBroodLineageDialog()
        {
            int hereditaryCapacity = BioUtility.GetHereditaryCapacity(Parent, 12);
            List<GeneDef> selectedGenes = GetStoredBroodTemplateGenes();

            if (!selectedGenes.Any())
            {
                selectedGenes = BioUtility.FilterGenesWithinComplexity(BioUtility.GetCryptimorphInheritableGenes(Parent), hereditaryCapacity);
            }

            List<GeneDef> availableGenes = GetAvailableBroodLineageGenes();

            Find.WindowStack.Add(new Dialogue_GeneExpression(
                selectedGenes,
                availableGenes,
                "XMT_ShapeBroodLineageHeader".Translate(),
                "XMT_AcceptBroodLineage".Translate(),
                hereditaryCapacity,
                delegate (List<GeneDef> genes, string templateName)
                {
                    broodTemplateGenes = new GeneSet();
                    BioUtility.ExtractGenesToGeneset(ref broodTemplateGenes, genes);
                    broodTemplateName = templateName;
                }));
        }

        private List<GeneDef> GetStoredBroodTemplateGenes()
        {
            if (broodTemplateGenes == null)
            {
                return new List<GeneDef>();
            }

            return broodTemplateGenes.GenesListForReading.ListFullCopy();
        }

        private List<GeneDef> GetAvailableBroodLineageGenes()
        {
            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                return BioUtility.GetAllHiveGenes(parent.MapHeld);
            }

            return BioUtility.GetCryptimorphInheritableGenes(Parent);
        }

        private IEnumerable<FloatMenuOption> EggOptionFloatMenuOptions()
        {
            foreach (QueenEggLayOption option in AvailableEggOptions(includeUnavailable: false))
            {
                QueenEggLayOption selectedOption = option;
                string label = selectedOption.Label;
                FloatMenuOption menuOption = new FloatMenuOption(label, delegate
                {
                    SelectEggOption(selectedOption);
                }, selectedOption.Icon, Color.white);

                if (ResolveSelectedEggOption() == selectedOption)
                {
                    menuOption.Label = "XMT_SelectedEggOption".Translate(label);
                }

                yield return menuOption;
            }
        }

        private List<QueenEggLayOption> AvailableEggOptions(bool includeUnavailable)
        {
            List<QueenEggLayOption> options = Props.AllEggOptions()
                .Where(option => includeUnavailable || CanUseEggOption(option).Accepted)
                .OrderBy(option => option.displayOrder)
                .ThenBy(option => option.Label)
                .ToList();

            return options;
        }

        private QueenEggLayOption ResolveSelectedEggOption()
        {
            List<QueenEggLayOption> availableOptions = AvailableEggOptions(includeUnavailable: false);
            QueenEggLayOption selectedOption = availableOptions.FirstOrDefault(option => option.thingDef == selectedEggDef);
            if (selectedOption != null)
            {
                return selectedOption;
            }

            selectedOption = availableOptions.FirstOrDefault(option => option.defaultSelection) ?? availableOptions.FirstOrDefault();
            selectedEggDef = selectedOption?.thingDef;
            return selectedOption;
        }

        private QueenEggLayOption DefaultEggOption()
        {
            return AvailableEggOptions(includeUnavailable: false).FirstOrDefault(option => option.defaultSelection) ?? AvailableEggOptions(includeUnavailable: false).FirstOrDefault();
        }

        private void SelectEggOption(QueenEggLayOption option)
        {
            if (option?.thingDef == null)
            {
                return;
            }

            selectedEggDef = option.thingDef;
        }

        private void ResetSelectedEggOptionIfNeeded(QueenEggLayOption option)
        {
            if (option == null || option.persistSelection)
            {
                return;
            }

            selectedEggDef = DefaultEggOption()?.thingDef;
        }

        private AcceptanceReport CanUseEggOption(QueenEggLayOption option)
        {
            if (Parent == null || Parent.Destroyed || Parent.Dead)
            {
                return "XMT_MessageMustDesignateAlive".Translate();
            }

            if (option == null || option.thingDef == null)
            {
                return "XMT_InvalidEggOption".Translate();
            }

            CompQueen queen = Parent.GetComp<CompQueen>();
            if (option.requiredEvolution != null && (queen == null || !queen.ChosenEvolutions.Contains(option.requiredEvolution)))
            {
                return "XMT_EggOptionRequiresEvolution".Translate(option.requiredEvolution.LabelCap);
            }

            if (option.requiredGene != null && !BioUtility.GetGeneForExpressionList(Parent).Contains(option.requiredGene))
            {
                return "XMT_EggOptionRequiresGene".Translate(option.requiredGene.LabelCap);
            }

            if (option.RequiresGeneHolder && !ThingDefHasHiveGeneHolder(option.thingDef))
            {
                return "XMT_EggOptionRequiresGeneHolder".Translate(option.thingDef.LabelCap);
            }

            return true;
        }

        private bool ThingDefHasHiveGeneHolder(ThingDef thingDef)
        {
            if (thingDef?.comps == null)
            {
                return false;
            }

            return thingDef.comps.Any(comp => comp?.compClass != null && typeof(CompHiveGeneHolder).IsAssignableFrom(comp.compClass));
        }

        
        public Thing LayOvomorph( IntVec3 loc)
        {
            return LayOvomorph(loc, parent);
        }

        public Thing LayOvomorph(IntVec3 loc, Thing positionSource)
        {
            QueenEggLayOption option = ResolveSelectedEggOption();
            AcceptanceReport report = CanUseEggOption(option);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return null;
            }

            float foodCost = FoodCostFor(option);
            Thing laidThing = OvomorphLayUtility.TryLayOvomorphWithCost(Parent, loc, positionSource, option.thingDef, foodCost);

            if (laidThing != null)
            {
                ApplyEggOptionGenes(laidThing, option);
                if (option.openGeneDialog && laidThing.TryGetComp<CompHiveGeneHolder>() != null)
                {
                    Find.WindowStack.Add(new Dialogue_GeneExpression(laidThing));
                }

                ResetSelectedEggOptionIfNeeded(option);
            }

            return laidThing;
        }

        public void TryApplyBroodLineage(Ovomorph ovomorph, ThingDef ovomorphDef)
        {
            if (ovomorph == null || ResolveSelectedEggOption()?.thingDef != Props.OvomorphDef)
            {
                return;
            }

            int hereditaryCapacity = BioUtility.GetHereditaryCapacity(Parent, 12);
            List<GeneDef> geneSource = GetStoredBroodTemplateGenes();

            if (!geneSource.Any())
            {
                geneSource = BioUtility.GetCryptimorphInheritableGenes(Parent);
            }

            List<GeneDef> filteredGenes = BioUtility.FilterGenesWithinComplexity(geneSource, hereditaryCapacity);
            ovomorph.SetParentsWithGenes(Parent, Parent, filteredGenes);
        }

        private void ApplyEggOptionGenes(Thing laidThing, QueenEggLayOption option)
        {
            if (laidThing == null || option?.genes == null || option.genes.Count == 0)
            {
                return;
            }

            CompHiveGeneHolder geneHolder = laidThing.TryGetComp<CompHiveGeneHolder>();
            if (geneHolder == null)
            {
                return;
            }

            if (option.overrideGenes || geneHolder.genes == null)
            {
                geneHolder.genes = new GeneSet();
            }

            BioUtility.ExtractGenesToGeneset(ref geneHolder.genes, option.genes);
        }

        internal class Command_ActionWithOptions : Command_Action
        {
            private readonly System.Func<IEnumerable<FloatMenuOption>> optionsGetter;

            public Command_ActionWithOptions(System.Func<IEnumerable<FloatMenuOption>> optionsGetter)
            {
                this.optionsGetter = optionsGetter;
            }

            public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions => optionsGetter?.Invoke() ?? Enumerable.Empty<FloatMenuOption>();
        }
    }



    public class CompOvomorphLayerProperties : CompProperties
    {
        public float foodCost;
        public ThingDef OvomorphDef;
        public ThingDef geneOvomorphDef;
        public List<QueenEggLayOption> eggOptions;
        public CompOvomorphLayerProperties()
        {
            this.compClass = typeof(CompOvomorphLayer);
        }

        public List<QueenEggLayOption> AllEggOptions()
        {
            if (!eggOptions.NullOrEmpty())
            {
                return eggOptions;
            }

            List<QueenEggLayOption> fallbackOptions = new List<QueenEggLayOption>();
            if (OvomorphDef != null)
            {
                fallbackOptions.Add(new QueenEggLayOption
                {
                    thingDef = OvomorphDef,
                    iconPath = "UI/Abilities/Ovomorph",
                    defaultSelection = true,
                    persistSelection = true,
                    displayOrder = 0
                });
            }

            if (geneOvomorphDef != null)
            {
                fallbackOptions.Add(new QueenEggLayOption
                {
                    thingDef = geneOvomorphDef,
                    requiredEvolution = RoyalEvolutionDefOf.Evo_GeneStorage,
                    iconPath = "UI/Abilities/GeneOvomorph",
                    foodCostFactor = 0.5f,
                    openGeneDialog = true,
                    displayOrder = 10
                });
            }

            return fallbackOptions;
        }
    }

    public class QueenEggLayOption
    {
        public ThingDef thingDef;
        public RoyalEvolutionDef requiredEvolution;
        public GeneDef requiredGene;
        public string iconPath;
        public int displayOrder = 0;
        public float foodCostFactor = 1f;
        public bool persistSelection = false;
        public bool defaultSelection = false;
        public bool openGeneDialog = false;
        public List<GeneDef> genes;
        public bool overrideGenes = false;

        [Unsaved(false)]
        private Texture2D icon;

        public string Label => thingDef?.LabelCap ?? "thing";
        public bool RequiresGeneHolder => openGeneDialog || !genes.NullOrEmpty();
        public Texture2D Icon
        {
            get
            {
                if (icon == null)
                {
                    if (!iconPath.NullOrEmpty())
                    {
                        icon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false);
                    }

                    if (icon == null && thingDef != null)
                    {
                        icon = thingDef.uiIcon;
                    }
                }

                return icon ?? BaseContent.BadTex;
            }
        }
    }
}
