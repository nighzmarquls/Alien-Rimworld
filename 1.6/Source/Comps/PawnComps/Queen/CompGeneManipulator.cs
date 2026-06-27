using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class CompGeneManipulator : ThingComp
    {
        static private Texture2D geneticTexture => ContentFinder<Texture2D>.Get("UI/Abilities/AlterGenes");
        static private Texture2D mutantTexture => ContentFinder<Texture2D>.Get("UI/GeneIcons/XMT_Gene_Unknown");

        static private Texture2D consumeTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ConsumeGenes");
        static private Texture2D selfTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ExpressGenes");
        static private Texture2D subjugateTexture => ContentFinder<Texture2D>.Get("UI/Abilities/DominateMutant");
        private const float DominionBaselineRange = 21f;
        private const float BaselineQueenPsychicSensitivity = 1.25f;
        Pawn Parent => parent as Pawn;
        CompGeneManipulatorProperties Props => props as CompGeneManipulatorProperties;
        private List<QueenMutationOrder> mutationOrders = new List<QueenMutationOrder>();

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if(!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                yield break;
            }

            if (Parent.Drafted)
            {
                Command_Action draftedSubjugation = CreateSubjugationAction();
                if (draftedSubjugation != null)
                {
                    yield return draftedSubjugation;
                }
                yield break;
            }

            TargetingParameters GeneTargetParameters = new TargetingParameters();

            GeneTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }
                return BioUtility.HasAlterableGenes(target.Thing);
            };

            Command_Action GeneControl_Action = new Command_Action();
            GeneControl_Action.defaultLabel = "XMT_AlterGenesLabel".Translate();
            GeneControl_Action.defaultDesc = "XMT_AlterGenesDescription".Translate();
            GeneControl_Action.icon = geneticTexture;
            GeneControl_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(GeneTargetParameters, delegate (LocalTargetInfo target)
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AlterGenes, target);
                    job.count = 1;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });
                
            };

            yield return GeneControl_Action;

            TargetingParameters MutantTargetParameters = new TargetingParameters();

            MutantTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }
                return target.Thing is Pawn pawn && pawn.health != null && !pawn.Dead && pawn.GetMorphComp() == null
                && BioUtility.HasMutations(pawn, false);
            };

            Command_Action MutantControl_Action = new Command_Action();
            MutantControl_Action.defaultLabel = "XMT_ManageMutationLabel".Translate();
            MutantControl_Action.defaultDesc = "XMT_ManageMutationDescription".Translate();
            MutantControl_Action.icon = mutantTexture;
            MutantControl_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(MutantTargetParameters, delegate (LocalTargetInfo target)
                {
                    OpenMutationActionMenu(target.Thing as Pawn);
                });
            };

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_MutantExpression))
            {
                yield return MutantControl_Action;
            }

            TargetingParameters CorpseParameters = new TargetingParameters();

            CorpseParameters.canTargetCorpses = true;
            CorpseParameters.canTargetItems = true;
            CorpseParameters.validator = delegate (TargetInfo target)
            {
               
                if(!target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Unspecified))
                {
                    return false;
                }

               
                if (BioUtility.HasConsumableGenes(target.Thing))
                {
                    return true;
                }
                

                return false;
            };

            Command_Action GeneConsume_Action = new Command_Action();
            GeneConsume_Action.defaultLabel = "XMT_ConsumeGenesLabel".Translate();
            GeneConsume_Action.defaultDesc = "XMT_ConsumeGenesDescription".Translate();
            GeneConsume_Action.icon = consumeTexture;
            GeneConsume_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(CorpseParameters, delegate (LocalTargetInfo target)
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target.Thing);
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_GeneDevour, target.Thing);
                    job.count = 1;
                    job.overeat = true;
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                });

            };

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneDigestion))
            {
                yield return GeneConsume_Action;
            }

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_SubjugatorCrest))
            {
                yield return CreateSubjugationAction();
            }

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression))
            {
                yield break;
            }

            yield return CreateSelfGeneExpressionAction();
        }

        public Command_Action CreateSelfGeneExpressionAction()
        {
            Command_Action GeneSelf_Action = new Command_Action();
            GeneSelf_Action.defaultLabel = "XMT_AlterSelfLabel".Translate();
            GeneSelf_Action.defaultDesc = "XMT_AlterSelfDescription".Translate();
            GeneSelf_Action.icon = selfTexture;
            GeneSelf_Action.action = delegate
            {
                AlterGenes(parent);
            };

            return GeneSelf_Action;
        }

        public List<QueenMutationOption> AvailableMutationOptions()
        {
            List<QueenMutationOption> options = new List<QueenMutationOption>();
            if (Props?.mutationOptions == null)
            {
                return options;
            }

            CompQueen queen = Parent?.GetComp<CompQueen>();
            foreach (QueenMutationOption option in Props.mutationOptions)
            {
                if (option?.mutationSet == null)
                {
                    continue;
                }

                if (option.requiredEvolution != null && (queen == null || !queen.ChosenEvolutions.Contains(option.requiredEvolution)))
                {
                    continue;
                }

                options.Add(option);
            }

            return options
                .GroupBy(option => option.mutationSet)
                .Select(group => group.OrderBy(option => option.displayOrder).First())
                .OrderBy(option => option.displayOrder)
                .ThenBy(option => option.mutationSet.displayOrder)
                .ThenBy(option => BioUtility.LabelForMutationSet(option.mutationSet))
                .ThenBy(option => option.mutationSet.defName)
                .ToList();
        }

        public bool HasAvailableMutationOptions()
        {
            return AvailableMutationOptions().Any();
        }

        public void OpenMutationActionMenu(Pawn target)
        {
            if (target == null)
            {
                Messages.Message("XMT_MutationInvalid_Target".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!BioUtility.HasMutations(target, false))
            {
                Messages.Message("XMT_MutationInvalid_NoEssence".Translate(BioUtility.GetXenomorphInfluence(target).ToString("0.##")), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("XMT_InduceMutationLabel".Translate(), delegate
                {
                    OpenMutationMenu(target, QueenMutationOperation.Add);
                }),
                new FloatMenuOption("XMT_SuppressMutationLabel".Translate(), delegate
                {
                    OpenMutationMenu(target, QueenMutationOperation.Remove);
                })
            }));
        }

        public void OpenMutationMenu(Pawn target, QueenMutationOperation operation)
        {
            if (target == null)
            {
                Messages.Message("XMT_MutationInvalid_Target".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!BioUtility.HasMutations(target, false))
            {
                Messages.Message("XMT_MutationInvalid_NoEssence".Translate(BioUtility.GetXenomorphInfluence(target).ToString("0.##")), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (operation == QueenMutationOperation.Remove)
            {
                List<FloatMenuOption> mutationOptions = SuppressMutationMenuOptions(target).ToList();
                if (mutationOptions.NullOrEmpty())
                {
                    Messages.Message("XMT_MutationInvalid_NoOptions".Translate(target.LabelShort), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(mutationOptions));
                return;
            }

            List<FloatMenuOption> setOptions = new List<FloatMenuOption>();
            foreach (QueenMutationOption option in AvailableMutationOptions())
            {
                XMT_MutationsHealthSet selectedSet = option.mutationSet;
                List<FloatMenuOption> mutationOptions = MutationMenuOptions(target, selectedSet, operation).ToList();
                if (mutationOptions.NullOrEmpty())
                {
                    continue;
                }

                setOptions.Add(new FloatMenuOption(BioUtility.LabelForMutationSet(selectedSet), delegate
                {
                    Find.WindowStack.Add(new FloatMenu(mutationOptions));
                }));
            }

            if (setOptions.NullOrEmpty())
            {
                Messages.Message("XMT_MutationInvalid_NoOptions".Translate(target.LabelShort), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(setOptions));
        }

        public void StartMutationOrder(Pawn target, QueenMutationOperation operation, XMT_MutationsHealthSet sourceSet, HediffDef mutationDef)
        {
            if (target == null || mutationDef == null)
            {
                return;
            }

            RegisterMutationOrder(target, operation, sourceSet, mutationDef);
            FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target);
            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MutateTarget, target);
            job.count = 1;
            Parent.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        public bool TryExecuteMutationOrder(Pawn target, out bool foundOrder)
        {
            foundOrder = false;
            QueenMutationOrder order = FindOrder(target);
            if (order == null)
            {
                return false;
            }

            foundOrder = true;
            mutationOrders.Remove(order);

            AcceptanceReport report;
            if (order.operation == QueenMutationOperation.Add)
            {
                MutationHealth mutation = BioUtility.MutationForDef(order.sourceSet, order.mutationDef);
                report = BioUtility.CanApplyMutation(target, mutation, requireExistingMutations: true);
                if (!report.Accepted)
                {
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                return BioUtility.TryApplyMutation(target, mutation, out Hediff _, requireExistingMutations: true);
            }

            report = BioUtility.CanRemoveMutation(target, order.mutationDef);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return BioUtility.TryRemoveMutation(target, order.mutationDef, out Hediff _);
        }

        public bool CanRemoveMutationFromOptions(Pawn target, HediffDef mutationDef)
        {
            if (target == null || mutationDef == null)
            {
                return false;
            }

            return AvailableMutationOptions()
                .Any(option => BioUtility.AllMutationsForSet(option.mutationSet)
                .Any(mutation => mutation.horror == mutationDef));
        }

        private Command_Action CreateSubjugationAction()
        {
            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_SubjugatorCrest))
            {
                return null;
            }

            TargetingParameters SubjugationTargetParameters = new TargetingParameters();
            SubjugationTargetParameters.canTargetPawns = true;
            SubjugationTargetParameters.validator = delegate (TargetInfo target)
            {
                return target.Thing is Pawn pawn && CanSubjugateTarget(pawn).Accepted;
            };

            Command_Action subjugationAction = new Command_Action();
            subjugationAction.defaultLabel = "XMT_SubjugateLabel".Translate();
            subjugationAction.defaultDesc = "XMT_SubjugateDescription".Translate(BiologicalSubjugationRange().ToString("0.#"));
            subjugationAction.icon = subjugateTexture;
            subjugationAction.action = delegate
            {
                Find.Targeter.BeginTargeting(
                    SubjugationTargetParameters,
                    delegate (LocalTargetInfo target)
                    {
                        TrySubjugateTarget(target.Thing as Pawn);
                    },
                    DrawBiologicalSubjugationTargetHighlight,
                    delegate (LocalTargetInfo target)
                    {
                        return target.Thing is Pawn pawn && CanSubjugateTarget(pawn).Accepted;
                    },
                    Parent,
                    null,
                    null,
                    true,
                    null,
                    DrawBiologicalSubjugationRange);
            };

            return subjugationAction;
        }

        private void DrawBiologicalSubjugationTargetHighlight(LocalTargetInfo target)
        {
            if (target.Thing is Pawn pawn && CanSubjugateTarget(pawn).Accepted)
            {
                GenDraw.DrawTargetHighlight(target);
            }
        }

        private void DrawBiologicalSubjugationRange(LocalTargetInfo target)
        {
            if (Parent == null || !Parent.Spawned)
            {
                return;
            }

            float range = BiologicalSubjugationRange();
            if (range <= 0f)
            {
                return;
            }

            GenDraw.DrawRadiusRing(Parent.Position, range);
        }

        private float BiologicalSubjugationRange()
        {
            float sensitivity = Parent != null ? Mathf.Max(0f, Parent.GetStatValue(StatDefOf.PsychicSensitivity)) : 0f;
            return DominionBaselineRange * (sensitivity / BaselineQueenPsychicSensitivity);
        }

        private AcceptanceReport CanSubjugateTarget(Pawn target)
        {
            if (Parent == null || Parent.Faction == null)
            {
                return "XMT_SubjugateInvalid_NoQueen".Translate();
            }

            if (target == null)
            {
                return "XMT_MutationInvalid_Target".Translate();
            }

            if (target == Parent)
            {
                return "XMT_SubjugateInvalid_Self".Translate();
            }

            if (target.Dead)
            {
                return "XMT_MutationInvalid_TargetDead".Translate(target.LabelShort);
            }

            if (target.health == null)
            {
                return "XMT_MutationInvalid_NoHealth".Translate(target.LabelShort);
            }

            if (target.MapHeld != null && Parent.MapHeld != null && target.MapHeld != Parent.MapHeld)
            {
                return "XMT_SubjugateInvalid_Map".Translate(target.LabelShort);
            }

            float range = BiologicalSubjugationRange();
            if (range <= 0f)
            {
                return "XMT_SubjugateInvalid_NoPsychicReach".Translate();
            }

            if (target.Spawned && Parent.Spawned && target.Position.DistanceToSquared(Parent.Position) > range * range)
            {
                return "XMT_SubjugateInvalid_Range".Translate(target.LabelShort, range.ToString("0.#"));
            }

            if (target.Faction == Parent.Faction && target.GuestStatus != GuestStatus.Prisoner && target.GuestStatus != GuestStatus.Slave)
            {
                return "XMT_SubjugateInvalid_AlreadyControlled".Translate(target.LabelShort);
            }

            if (!target.HasBrainMutation() && !target.IsHorror())
            {
                return "XMT_SubjugateInvalid_NoInfluence".Translate(target.LabelShort);
            }

            return true;
        }

        private bool TrySubjugateTarget(Pawn target)
        {
            AcceptanceReport report = CanSubjugateTarget(target);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            FeralJobUtility.ClearFeralJobReservationsForTarget(Parent.Map, target);
            ApplySubjugation(target);
            MoteMaker.ThrowText(target.DrawPos, target.MapHeld, "XMT_SubjugateMote".Translate(), 3.65f);
            Messages.Message("XMT_SubjugateSuccess".Translate(target.LabelShort, Parent.LabelShort), target, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private void ApplySubjugation(Pawn target)
        {
            if (ModsConfig.IdeologyActive && target.RaceProps.Humanlike && target.guest != null)
            {
                target.guest.SetGuestStatus(Parent.Faction, GuestStatus.Slave);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.EnslavedPrisoner, Parent.Named(HistoryEventArgsNames.Doer)));
            }
            else if (target.RaceProps.Humanlike)
            {
                RecruitUtility.Recruit(target, Parent.Faction, Parent);
            }
            else
            {
                target.SetFaction(Parent.Faction);
                target.guest?.SetGuestStatus(null);
            }

            CompMatureMorph morph = target.GetMorphComp();
            if (morph != null)
            {
                morph.tamingSocializing = Mathf.Max(morph.tamingSocializing, 1f);
            }

            if (ModsConfig.IdeologyActive && Parent.ideo?.Ideo != null && target.ideo != null)
            {
                target.ideo.SetIdeo(Parent.ideo.Ideo);
            }

            if (target.relations != null && !target.relations.DirectRelationExists(PawnRelationDefOf.Parent, Parent))
            {
                target.relations.AddDirectRelation(PawnRelationDefOf.Parent, Parent);
            }

            XMTUtility.GiveMemory(target, HorrorMoodDefOf.XMT_CommuneWithQueen);

            if (target.caller != null)
            {
                target.caller.DoCall();
            }
        }

        private IEnumerable<FloatMenuOption> MutationMenuOptions(Pawn target, XMT_MutationsHealthSet set, QueenMutationOperation operation)
        {
            IEnumerable<MutationHealth> mutations = BioUtility.AllMutationsForSet(set)
                .OrderBy(mutation => mutation.displayOrder)
                .ThenBy(BioUtility.LabelForMutation)
                .ThenBy(mutation => mutation.horror.defName);

            foreach (MutationHealth mutation in mutations)
            {
                if (operation == QueenMutationOperation.Remove && !target.health.hediffSet.hediffs.Any(hediff => hediff.def == mutation.horror))
                {
                    continue;
                }

                MutationHealth selectedMutation = mutation;
                string label = BioUtility.LabelForMutation(selectedMutation);
                FloatMenuOption option = new FloatMenuOption(label, delegate
                {
                    StartMutationOrder(target, operation, set, selectedMutation.horror);
                });

                AcceptanceReport report = operation == QueenMutationOperation.Add
                    ? BioUtility.CanApplyMutation(target, selectedMutation, requireExistingMutations: true)
                    : BioUtility.CanRemoveMutation(target, selectedMutation.horror);

                if (!report.Accepted)
                {
                    option.Disabled = true;
                    option.tooltip = report.Reason;
                }
                else if (!selectedMutation.horror.description.NullOrEmpty())
                {
                    option.tooltip = selectedMutation.horror.description;
                }

                yield return option;
            }
        }

        private IEnumerable<FloatMenuOption> SuppressMutationMenuOptions(Pawn target)
        {
            HashSet<HediffDef> seenMutationDefs = new HashSet<HediffDef>();
            foreach (QueenMutationOption option in AvailableMutationOptions())
            {
                XMT_MutationsHealthSet selectedSet = option.mutationSet;
                IEnumerable<MutationHealth> mutations = BioUtility.AllMutationsForSet(selectedSet)
                    .OrderBy(mutation => mutation.displayOrder)
                    .ThenBy(BioUtility.LabelForMutation)
                    .ThenBy(mutation => mutation.horror.defName);

                foreach (MutationHealth mutation in mutations)
                {
                    if (!target.health.hediffSet.hediffs.Any(hediff => hediff.def == mutation.horror))
                    {
                        continue;
                    }

                    if (!seenMutationDefs.Add(mutation.horror))
                    {
                        continue;
                    }

                    MutationHealth selectedMutation = mutation;
                    FloatMenuOption menuOption = new FloatMenuOption(BioUtility.LabelForMutation(selectedMutation), delegate
                    {
                        StartMutationOrder(target, QueenMutationOperation.Remove, selectedSet, selectedMutation.horror);
                    });

                    AcceptanceReport report = BioUtility.CanRemoveMutation(target, selectedMutation.horror);
                    if (!report.Accepted)
                    {
                        menuOption.Disabled = true;
                        menuOption.tooltip = report.Reason;
                    }
                    else if (!selectedMutation.horror.description.NullOrEmpty())
                    {
                        menuOption.tooltip = selectedMutation.horror.description;
                    }

                    yield return menuOption;
                }
            }
        }

        private void RegisterMutationOrder(Pawn target, QueenMutationOperation operation, XMT_MutationsHealthSet sourceSet, HediffDef mutationDef)
        {
            ClearStaleMutationOrders();
            mutationOrders.RemoveAll(order => order?.target == target);
            mutationOrders.Add(new QueenMutationOrder
            {
                target = target,
                operation = operation,
                sourceSet = sourceSet,
                mutationDef = mutationDef
            });
        }

        private QueenMutationOrder FindOrder(Pawn target)
        {
            ClearStaleMutationOrders();
            return mutationOrders.FirstOrDefault(order => order?.target == target);
        }

        private void ClearStaleMutationOrders()
        {
            mutationOrders.RemoveAll(order => order == null || order.target == null || order.target.Dead || order.mutationDef == null);
        }

        public void AlterGenes(Thing Target)
        {
            Find.WindowStack.Add(new Dialogue_GeneExpression(Target));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref mutationOrders, "mutationOrders", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && mutationOrders == null)
            {
                mutationOrders = new List<QueenMutationOrder>();
            }
        }
    }

    public class CompGeneManipulatorProperties : CompProperties
    {
        public List<QueenMutationOption> mutationOptions;

        public CompGeneManipulatorProperties()
        {
            this.compClass = typeof(CompGeneManipulator);
        }

    }

    public class QueenMutationOption
    {
        public RoyalEvolutionDef requiredEvolution;
        public XMT_MutationsHealthSet mutationSet;
        public int displayOrder = 0;
    }

    public enum QueenMutationOperation
    {
        Add,
        Remove
    }

    public class QueenMutationOrder : IExposable
    {
        public Pawn target;
        public QueenMutationOperation operation;
        public HediffDef mutationDef;
        public XMT_MutationsHealthSet sourceSet;

        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref operation, "operation", QueenMutationOperation.Add);
            Scribe_Defs.Look(ref mutationDef, "mutationDef");
            Scribe_Defs.Look(ref sourceSet, "sourceSet");
        }
    }
}
