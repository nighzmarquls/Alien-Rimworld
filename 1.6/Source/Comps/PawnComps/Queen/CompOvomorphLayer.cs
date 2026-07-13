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
        static private Texture2D MassLayTexture => ContentFinder<Texture2D>.Get("UI/Abilities/OvomorphCluster");
        static private Texture2D LineageTexture => ContentFinder<Texture2D>.Get("UI/Abilities/Lineage");
        private const int MaxStoredEggs = 255;
        private const int AutoLayRetryInterval = 250;
        private const float SpillRadius = 10f;
        private const int MassLayLimit = 22;
        Pawn Parent => parent as Pawn;
        CompOvomorphLayerProperties Props => props as CompOvomorphLayerProperties;

        ThingDef selectedEggDef = null;
        GeneSet broodTemplateGenes;
        string broodTemplateName;
        int storedEggs;
        float eggProductionProgress;
        bool autoLay;
        int lastKnownCapacity = -1;
        int nextAutoLayTick;

        public int StoredEggs => storedEggs;
        public float EggProductionProgress => eggProductionProgress;
        public bool AutoLay => autoLay;
        public int EggCapacity => HyperFertilityActive ? CapacityForBodySize(Parent.BodySize) : 0;
        public float TicksPerStoredEgg => Mathf.Max(1f, 90000f / Mathf.Max(0.01f, Parent.BodySize * Parent.BodySize));
        public bool HyperFertilityActive => Parent?.GetComp<CompQueen>()?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_IntegratedEggSac) == true;

        public int PreferredLayingDistance
        {
            get
            {
                if (!HyperFertilityActive)
                {
                    return 1;
                }
                float bodySize = Mathf.Clamp(Parent.BodySize, 3f, 10f);
                return Mathf.RoundToInt(Mathf.Lerp(2f, 4f, Mathf.InverseLerp(3f, 10f, bodySize)));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref selectedEggDef, "selectedEggDef");
            Scribe_Deep.Look(ref broodTemplateGenes, "broodTemplateGenes");
            Scribe_Values.Look(ref broodTemplateName, "broodTemplateName", "");
            Scribe_Values.Look(ref storedEggs, "storedOvomorphs", 0);
            Scribe_Values.Look(ref eggProductionProgress, "storedOvomorphProgress", 0f);
            Scribe_Values.Look(ref autoLay, "autoLayOvomorphs", false);
            Scribe_Values.Look(ref lastKnownCapacity, "lastOvomorphCapacity", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                storedEggs = Mathf.Clamp(storedEggs, 0, MaxStoredEggs);
                eggProductionProgress = Mathf.Clamp01(eggProductionProgress);
            }
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if (Parent == null || Parent.Dead || Parent.Destroyed)
            {
                return;
            }

            int capacity = EggCapacity;
            if (capacity != lastKnownCapacity)
            {
                lastKnownCapacity = capacity;
                if (!HyperFertilityActive)
                {
                    autoLay = false;
                }
                ResolveOverflow(capacity);
            }

            if (HyperFertilityActive && autoLay && storedEggs >= capacity && Find.TickManager.TicksGame >= nextAutoLayTick)
            {
                TryAutoLay();
            }

            if (!HyperFertilityActive || storedEggs >= capacity || Parent.needs?.food?.Starving == true)
            {
                return;
            }

            eggProductionProgress = Mathf.Min(1f, eggProductionProgress + delta / TicksPerStoredEgg);
            if (eggProductionProgress < 1f)
            {
                return;
            }

            float cost = FoodCost;
            if (Parent.needs?.food != null && Parent.needs.food.CurLevel <= cost)
            {
                return;
            }

            if (Parent.needs?.food != null)
            {
                Parent.needs.food.CurLevel -= cost;
            }
            storedEggs = Mathf.Min(capacity, storedEggs + 1);
            eggProductionProgress = 0f;

            if (autoLay && storedEggs >= capacity)
            {
                TryAutoLay();
            }
        }

        private static int CapacityForBodySize(float bodySize)
        {
            float normalized = Mathf.Max(0f, bodySize) / 3f;
            return Mathf.Clamp(Mathf.FloorToInt(6f * normalized * normalized), 1, MaxStoredEggs);
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

        public string OvomorphDescription(OvomorphLayOption option)
        {
            ThingDef eggDef = option?.thingDef ?? Props.OvomorphDef;
            string description = "XMT_LayOvomorphDescription".Translate(eggDef?.label ?? "thing");
           
            if (Parent.needs.food == null || Parent.needs.food.CurLevel > FoodCostFor(option))
            {
                AcceptanceReport resourceReport = ResourceCostReport(option);
                if (!resourceReport.Accepted)
                {
                    description += "\n" + resourceReport.Reason;
                }

                return description;
            }

            description += "\n" + "XMT_InsufficientNutrition".Translate(eggDef?.label ?? "thing");
            AcceptanceReport missingResourceReport = ResourceCostReport(option);
            if (!missingResourceReport.Accepted)
            {
                description += "\n" + missingResourceReport.Reason;
            }

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

        public float FoodCostFor(OvomorphLayOption option)
        {
            return FoodCost * (option?.foodCostFactor ?? 1f);
        }

        public float ResourceCostFor(OvomorphLayOption option)
        {
            if (option?.resourceDef == null || option.resourceCostFactor <= 0f)
            {
                return 0f;
            }

            return Mathf.Ceil(FoodCost * option.resourceCostFactor);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Downed)
            {
                yield break;
            }

            if (HyperFertilityActive)
            {
                yield return new Gizmo_OvomorphStock(this);
                yield return CreateAutoLayToggle();
                yield return CreateMassLayAction();
            }

            if (Parent.Drafted)
            {
                yield break;
            }

            yield return ShapeBroodLineageAction();

            CompQueen queen = Parent.GetComp<CompQueen>();
            if (queen?.HasActiveEvolution(RoyalEvolutionDefOf.Evo_OvoThrone) == true)
            {
                yield break;
            }

            yield return CreateLaySelectedEggAction(parent, startJob: true);
        }

        private Command_Toggle CreateAutoLayToggle()
        {
            return new Command_Toggle
            {
                defaultLabel = "XMT_AutoLayOvomorphs".Translate(),
                defaultDesc = "XMT_AutoLayOvomorphsDesc".Translate(),
                icon = OvomorphTexture,
                isActive = () => autoLay,
                toggleAction = delegate
                {
                    autoLay = !autoLay;
                    if (autoLay && storedEggs >= EggCapacity)
                    {
                        TryAutoLay();
                    }
                }
            };
        }

        private Command_Action CreateMassLayAction()
        {
            Command_Action action = new Command_Action
            {
                defaultLabel = "XMT_MassLayOvomorphs".Translate(),
                defaultDesc = "XMT_MassLayOvomorphsDesc".Translate(MassLayLimit),
                icon = MassLayTexture,
                action = MassLayStoredEggs
            };
            if (storedEggs <= 0)
            {
                action.Disable("XMT_NoStoredOvomorphs".Translate());
            }
            return action;
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
            OvomorphLayOption selectedOption = ResolveSelectedEggOption();
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
            OvomorphLayOption selectedOption = ResolveSelectedEggOption();
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
            foreach (OvomorphLayOption option in AvailableEggOptions(includeUnavailable: false))
            {
                OvomorphLayOption selectedOption = option;
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

        private List<OvomorphLayOption> AvailableEggOptions(bool includeUnavailable)
        {
            List<OvomorphLayOption> options = Props.AllEggOptions()
                .Where(option => includeUnavailable || CanUseEggOption(option).Accepted)
                .OrderBy(option => option.displayOrder)
                .ThenBy(option => option.Label)
                .ToList();

            return options;
        }

        private OvomorphLayOption ResolveSelectedEggOption()
        {
            List<OvomorphLayOption> availableOptions = AvailableEggOptions(includeUnavailable: false);
            OvomorphLayOption selectedOption = availableOptions.FirstOrDefault(option => option.thingDef == selectedEggDef);
            if (selectedOption != null)
            {
                return selectedOption;
            }

            selectedOption = availableOptions.FirstOrDefault(option => option.defaultSelection) ?? availableOptions.FirstOrDefault();
            selectedEggDef = selectedOption?.thingDef;
            return selectedOption;
        }

        private OvomorphLayOption DefaultEggOption()
        {
            return AvailableEggOptions(includeUnavailable: false).FirstOrDefault(option => option.defaultSelection) ?? AvailableEggOptions(includeUnavailable: false).FirstOrDefault();
        }

        private void SelectEggOption(OvomorphLayOption option)
        {
            if (option?.thingDef == null)
            {
                return;
            }

            selectedEggDef = option.thingDef;
        }

        private void ResetSelectedEggOptionIfNeeded(OvomorphLayOption option)
        {
            if (option == null || option.persistSelection)
            {
                return;
            }

            selectedEggDef = DefaultEggOption()?.thingDef;
        }

        private AcceptanceReport CanUseEggOption(OvomorphLayOption option)
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

            AcceptanceReport resourceReport = ResourceCostReport(option);
            if (!resourceReport.Accepted)
            {
                return resourceReport;
            }

            return true;
        }

        private AcceptanceReport ResourceCostReport(OvomorphLayOption option)
        {
            if (option?.resourceDef == null || option.resourceCostFactor <= 0f)
            {
                return AcceptanceReport.WasAccepted;
            }

            CompQueenAssimilation assimilation = Parent.GetComp<CompQueenAssimilation>();
            if (assimilation == null || !assimilation.ResourceUnlocked(option.resourceDef))
            {
                return "XMT_MechGestationLocked".Translate();
            }

            float resourceCost = ResourceCostFor(option);
            if (assimilation.GetResourceAmount(option.resourceDef) < resourceCost)
            {
                return "XMT_MechGestationNeedsMaterial".Translate(Mathf.CeilToInt(resourceCost), option.resourceDef.LabelCap);
            }

            return AcceptanceReport.WasAccepted;
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
            OvomorphLayOption option = ResolveSelectedEggOption();
            AcceptanceReport report = CanUseEggOption(option);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return null;
            }

            bool useStoredEgg = HyperFertilityActive && storedEggs > 0;
            float foodCost = FoodCostFor(option);
            Thing laidThing = OvomorphLayUtility.TryLayOvomorphWithCost(Parent, loc, positionSource, option.thingDef, foodCost, resourceDef: option.resourceDef, resourceCost: ResourceCostFor(option), initialProgress: useStoredEgg ? 1f : option.progressOverride, knowledgeProfile: Props.layKnowledgeProfile, chargeFood: !useStoredEgg);

            if (laidThing != null)
            {
                if (useStoredEgg)
                {
                    storedEggs--;
                }
                ApplyEggOptionGenes(laidThing, option);
                if (option.openGeneDialog && laidThing.TryGetComp<CompHiveGeneHolder>() != null)
                {
                    Find.WindowStack.Add(new Dialogue_GeneExpression(laidThing));
                }

                ResetSelectedEggOptionIfNeeded(option);
            }

            return laidThing;
        }

        private void TryAutoLay()
        {
            nextAutoLayTick = Find.TickManager.TicksGame + AutoLayRetryInterval;
            OvomorphLayOption option = ResolveSelectedEggOption();
            if (option == null || !CanUseEggOption(option).Accepted)
            {
                option = DefaultEggOption();
                SelectEggOption(option);
            }
            if (option == null || !ResourceCostReport(option).Accepted)
            {
                return;
            }

            IntVec3 layingCenter = Parent.PositionHeld + Parent.Rotation.Opposite.AsIntVec3 * PreferredLayingDistance;
            List<IntVec3> cells = FindLayCells(option.thingDef, 1, SpillRadius, layingCenter);
            if (cells.Count > 0)
            {
                LayStoredEgg(option, cells[0], playSound: true, recordEvent: true, witness: true);
            }
        }

        private void MassLayStoredEggs()
        {
            OvomorphLayOption option = ResolveSelectedEggOption();
            if (option == null)
            {
                return;
            }

            int wanted = Mathf.Min(MassLayLimit, storedEggs);
            IntVec3 layingCenter = Parent.PositionHeld + Parent.Rotation.Opposite.AsIntVec3 * PreferredLayingDistance;
            List<IntVec3> cells = FindLayCells(option.thingDef, wanted, SpillRadius, layingCenter);
            int laid = 0;
            string stopReason = null;
            for (int i = 0; i < cells.Count && laid < wanted; i++)
            {
                AcceptanceReport resourceReport = ResourceCostReport(option);
                if (!resourceReport.Accepted)
                {
                    stopReason = resourceReport.Reason;
                    break;
                }

                bool playSound = i < 4;
                Thing result = LayStoredEgg(option, cells[i], playSound, recordEvent: laid == 0, witness: laid == 0);
                if (result != null)
                {
                    laid++;
                }
            }

            if (Parent.Faction == Faction.OfPlayer)
            {
                if (!stopReason.NullOrEmpty())
                {
                    Messages.Message("XMT_MassLayStopped".Translate(laid, stopReason), Parent, MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    Messages.Message("XMT_MassLayComplete".Translate(laid), Parent, MessageTypeDefOf.TaskCompletion, false);
                }
            }
        }

        private Thing LayStoredEgg(OvomorphLayOption option, IntVec3 cell, bool playSound, bool recordEvent, bool witness, bool allowDeadLayer = false)
        {
            if (storedEggs <= 0 || option?.thingDef == null)
            {
                return null;
            }

            Thing laidThing = OvomorphLayUtility.TryLayOvomorphWithCost(Parent, cell, Parent, option.thingDef, 0f,
                initialProgress: 1f,
                resourceDef: option.resourceDef,
                resourceCost: ResourceCostFor(option),
                knowledgeProfile: Props.layKnowledgeProfile,
                chargeFood: false,
                playSound: playSound,
                makeFilth: true,
                recordEvent: recordEvent,
                witness: witness,
                requireReachability: false,
                allowDeadLayer: allowDeadLayer);
            if (laidThing == null)
            {
                return null;
            }

            storedEggs--;
            ApplyEggOptionGenes(laidThing, option);
            return laidThing;
        }

        private List<IntVec3> FindLayCells(ThingDef ovomorphDef, int wanted, float radius, IntVec3? searchCenter = null)
        {
            List<IntVec3> result = new List<IntVec3>();
            Map map = Parent.MapHeld;
            if (map == null || ovomorphDef == null || wanted <= 0)
            {
                return result;
            }

            IntVec3 center = searchCenter ?? Parent.PositionHeld;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (result.Count >= wanted)
                {
                    break;
                }
                if (cell == Parent.PositionHeld || !cell.InBounds(map))
                {
                    continue;
                }
                if (OvomorphLayUtility.CanLayAt(Parent, cell, Parent, ovomorphDef, out string _, requireReachability: false, allowDeadLayer: Parent.Dead))
                {
                    result.Add(cell);
                }
            }
            return result;
        }

        private OvomorphLayOption SpillOption()
        {
            OvomorphLayOption selected = ResolveSelectedEggOption();
            if (selected != null && selected.resourceDef == null && CanUseEggOption(selected).Accepted)
            {
                return selected;
            }
            return Props.AllEggOptions().FirstOrDefault(option => option.thingDef == Props.OvomorphDef)
                ?? new OvomorphLayOption { thingDef = Props.OvomorphDef };
        }

        private void ResolveOverflow(int capacity)
        {
            int overflow = Mathf.Max(0, storedEggs - capacity);
            if (overflow <= 0 || !Parent.Spawned || Parent.MapHeld == null)
            {
                return;
            }

            OvomorphLayOption option = SpillOption();
            List<IntVec3> cells = FindLayCells(option.thingDef, overflow, SpillRadius);
            int laid = SpillIntoCells(option, cells, overflow);
            int discarded = overflow - laid;
            if (discarded > 0)
            {
                storedEggs = Mathf.Max(capacity, storedEggs - discarded);
                if (Parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message("XMT_OvomorphOverflowLost".Translate(discarded), Parent, MessageTypeDefOf.NegativeEvent, false);
                }
            }
        }

        private int SpillIntoCells(OvomorphLayOption option, List<IntVec3> cells, int limit, bool allowDeadLayer = false)
        {
            int laid = 0;
            for (int i = 0; i < cells.Count && laid < limit && storedEggs > 0; i++)
            {
                bool playSound = i < 4;
                Thing result = LayStoredEgg(option, cells[i], playSound, recordEvent: laid == 0, witness: laid == 0, allowDeadLayer: allowDeadLayer);
                if (result != null)
                {
                    laid++;
                }
            }
            return laid;
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (storedEggs <= 0)
            {
                return;
            }

            if (prevMap != null && Parent.PositionHeld.IsValid)
            {
                OvomorphLayOption option = SpillOption();
                List<IntVec3> cells = FindLayCells(option.thingDef, storedEggs, SpillRadius);
                SpillIntoCells(option, cells, storedEggs, allowDeadLayer: true);
                storedEggs = 0;
            }
            else
            {
                Current.Game?.GetComponent<GameComponent_Xenomorph>()?.ReleaseStoredOvomorphsOnWorld(storedEggs);
                storedEggs = 0;
            }
            eggProductionProgress = 0f;
            autoLay = false;
        }

        public string StockTooltip()
        {
            string state = "XMT_OvomorphStockProducing".Translate();
            if (Parent.needs?.food?.Starving == true)
            {
                state = "XMT_OvomorphStockStarving".Translate();
            }
            else if (storedEggs >= EggCapacity)
            {
                state = "XMT_OvomorphStockFull".Translate();
            }
            else if (Parent.needs?.food != null && eggProductionProgress >= 1f && Parent.needs.food.CurLevel <= FoodCost)
            {
                state = "XMT_OvomorphStockNeedsFood".Translate();
            }
            int ticksRemaining = Mathf.CeilToInt((1f - eggProductionProgress) * TicksPerStoredEgg);
            return "XMT_OvomorphStockTooltip".Translate(storedEggs, EggCapacity, Parent.BodySize.ToString("F2"), ticksRemaining.ToStringTicksToPeriod(), FoodCost.ToString("F2"), ResolveSelectedEggOption()?.Label ?? "thing", state, autoLay ? "On".Translate() : "Off".Translate());
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

        private void ApplyEggOptionGenes(Thing laidThing, OvomorphLayOption option)
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
        public List<OvomorphLayOption> options;
        public KnowledgeProfileDef layKnowledgeProfile;
        public CompOvomorphLayerProperties()
        {
            this.compClass = typeof(CompOvomorphLayer);
        }

        public List<OvomorphLayOption> AllEggOptions()
        {
            if (!options.NullOrEmpty())
            {
                return options;
            }

            List<OvomorphLayOption> fallbackOptions = new List<OvomorphLayOption>();
            if (OvomorphDef != null)
            {
                fallbackOptions.Add(new OvomorphLayOption
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
                fallbackOptions.Add(new OvomorphLayOption
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

    public class OvomorphLayOption
    {
        public string label = string.Empty;
        public ThingDef thingDef;
        public RoyalEvolutionDef requiredEvolution;
        public GeneDef requiredGene;
        public string iconPath;
        public int displayOrder = 0;
        public float resourceCostFactor = 0f;
        public float progressOverride = 0f;
        public QueenIngestibleResourceDef resourceDef = null;
        public float foodCostFactor = 1f;
        public bool persistSelection = false;
        public bool defaultSelection = false;
        public bool openGeneDialog = false;
        public List<GeneDef> genes;
        public bool overrideGenes = false;
        
        [Unsaved(false)]
        private Texture2D icon;

        public string Label => label != string.Empty ? label : thingDef?.LabelCap ?? "thing";
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
