using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    public class CompQueenAssimilation : ThingComp
    {
        private static readonly FieldInfo passingShipsField = AccessTools.Field(typeof(PassingShipManager), "passingShips");
        private static readonly FieldInfo floatMenuInitialPositionShiftField = AccessTools.Field(typeof(FloatMenu), "InitialPositionShift");

        private List<QueenAssimilationDef> completedAssimilations;
        private List<QueenIngestibleResourceDef> resourceDefs;
        private List<float> resourceAmounts;

        private Pawn Parent => parent as Pawn;
        private CompQueen QueenComp => Parent?.GetComp<CompQueen>();
        private static Texture2D AssimilateTexture => ContentFinder<Texture2D>.Get("UI/Abilities/ConsumeStuff");

        private static Texture2D GestateTexture => ContentFinder<Texture2D>.Get("UI/Abilities/GestateMechanoid");
        private static Texture2D CommsTexture => ContentFinder<Texture2D>.Get("UI/Commands/CallAid");

        public List<QueenAssimilationDef> CompletedAssimilations
        {
            get
            {
                if (completedAssimilations == null)
                {
                    completedAssimilations = new List<QueenAssimilationDef>();
                }

                return completedAssimilations;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref completedAssimilations, "completedAssimilations", LookMode.Def);
            Scribe_Collections.Look(ref resourceDefs, "queenResourceDefs", LookMode.Def);
            Scribe_Collections.Look(ref resourceAmounts, "queenResourceAmounts", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureMechanitorBaselineResearch();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad)
            {
                EnsureMechanitorBaselineResearch();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!CanShowQueenGizmos())
            {
                yield break;
            }

            if (AnyRelevantAssimilation())
            {
                yield return new Command_Action
                {
                    defaultLabel = "XMT_Assimilate".Translate(),
                    defaultDesc = "XMT_AssimilateDesc".Translate(),
                    icon = AssimilateTexture,
                    action = StartAssimilationTargeting
                };
            }

            if (AnyVisibleResource())
            {
                yield return new Gizmo_QueenResources(this);
            }

            if (CanGestateMechanoids())
            {
                yield return new Command_Action
                {
                    defaultLabel = "XMT_GestateMechanoid".Translate(),
                    defaultDesc = "XMT_GestateMechanoidDesc".Translate(),
                    icon = GestateTexture,
                    action = OpenMechGestationMenu
                };
            }

            if (HasEvolution(RoyalEvolutionDefOf.Evo_SignalAmplifyingAntenna))
            {
                yield return new Command_Action
                {
                    defaultLabel = "XMT_HijackComms".Translate(),
                    defaultDesc = "XMT_HijackCommsDesc".Translate(),
                    icon = CommsTexture,
                    action = OpenCommsMenu
                };
            }
        }

        public bool HasAssimilated(QueenAssimilationDef def)
        {
            return def != null && CompletedAssimilations.Contains(def);
        }

        public bool HasEvolution(RoyalEvolutionDef evolution)
        {
            return evolution != null && QueenComp != null && QueenComp.ChosenEvolutions.Contains(evolution);
        }

        private bool CanShowQueenGizmos()
        {
            return Parent != null
                && Parent.Faction == Faction.OfPlayer
                && !Parent.Drafted
                && !Parent.Downed
                && !Parent.Dead;
        }

        private bool AnyRelevantAssimilation()
        {
            foreach (QueenAssimilationDef def in DefDatabase<QueenAssimilationDef>.AllDefsListForReading)
            {
                if (def.requiredEvolution == null || HasEvolution(def.requiredEvolution) || MissingOnlyAssimilationPrerequisites(def))
                {
                    return true;
                }
            }

            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (ResourceUnlocked(resource))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MissingOnlyAssimilationPrerequisites(QueenAssimilationDef def)
        {
            if (def == null || def.requiredEvolution == null || !HasEvolution(def.requiredEvolution))
            {
                return false;
            }

            return def.prerequisiteAssimilations != null && def.prerequisiteAssimilations.Any(prerequisite => !HasAssimilated(prerequisite));
        }

        private void StartAssimilationTargeting()
        {
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetItems = true,
                canTargetBuildings = false,
                canTargetPawns = false,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target => target.HasThing && CanAssimilate(target.Thing, out QueenAssimilationDef _).Accepted
            };

            Find.Targeter.BeginTargeting(parameters, delegate (LocalTargetInfo target)
            {
                TryStartAssimilationJob(target.Thing);
            }, null, delegate (LocalTargetInfo target)
            {
                AcceptanceReport report = target.HasThing ? CanAssimilate(target.Thing, out QueenAssimilationDef _) : false;
                if (!report.Accepted && !report.Reason.NullOrEmpty())
                {
                    Widgets.MouseAttachedLabel(report.Reason);
                }

                return true;
            });
        }

        public void TryStartAssimilationJob(Thing item)
        {
            AcceptanceReport report = CanAssimilate(item, out QueenAssimilationDef def);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, item, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!Parent.CanReserveAndReach(item, PathEndMode.Touch, Danger.Deadly))
            {
                Messages.Message("XMT_AssimilationCannotReach".Translate(item.LabelShort), item, MessageTypeDefOf.RejectInput, false);
                return;
            }

            item.SetForbidden(false, false);
            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AssimilateQueenItem, item);
            job.count = def?.consumeCount ?? item.stackCount;
            job.playerForced = true;
            Parent.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        public bool TryCreateAutoResourceAssimilationJob(out Job job)
        {
            job = null;
            Thing target = BestAutoResourceAssimilationTarget();
            if (target == null)
            {
                return false;
            }

            job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AssimilateQueenItem, target);
            job.count = target.stackCount;
            FeralJobUtility.ReserveThingForJob(Parent, job, target);
            return true;
        }

        private Thing BestAutoResourceAssimilationTarget()
        {
            if (Parent?.MapHeld == null || !AnyUnlockedResourceSpace())
            {
                return null;
            }

            Thing best = null;
            float bestScore = 0f;
            foreach (Thing thing in Parent.MapHeld.listerThings.AllThings)
            {
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.IsForbidden(Parent))
                {
                    continue;
                }

                if (thing.def.category != ThingCategory.Item && !(thing is Corpse))
                {
                    continue;
                }

                if (thing == Parent || !FeralJobUtility.IsThingAvailableForJobBy(Parent, thing) || !Parent.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                List<QueenResourceGain> gains = QueenIngestibleResourceUtility.GetResourceGains(thing, Parent, this);
                if (gains.Count == 0)
                {
                    continue;
                }

                float amount = gains.Sum(gain => gain.amount);
                float score = amount * 100f - thing.Position.DistanceToSquared(Parent.Position);
                if (best == null || score > bestScore)
                {
                    best = thing;
                    bestScore = score;
                }
            }

            return best;
        }

        private bool AnyUnlockedResourceSpace()
        {
            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (ResourceUnlocked(resource) && GetResourceSpace(resource) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        public AcceptanceReport CanAssimilate(Thing item, out QueenAssimilationDef def)
        {
            def = null;
            if (item == null || item.Destroyed)
            {
                return false;
            }

            foreach (QueenAssimilationDef candidate in DefDatabase<QueenAssimilationDef>.AllDefsListForReading.OrderBy(candidate => candidate.label))
            {
                if (candidate.thingDef == item.def)
                {
                    AcceptanceReport report = CanAssimilate(candidate, item);
                    if (report.Accepted)
                    {
                        def = candidate;
                        return AcceptanceReport.WasAccepted;
                    }
                }
            }

            List<QueenResourceGain> resourceGains = QueenIngestibleResourceUtility.GetResourceGains(item, Parent, this);
            if (resourceGains.Count > 0)
            {
                return AcceptanceReport.WasAccepted;
            }

            return "XMT_AssimilationNotAssimilatable".Translate();
        }

        public AcceptanceReport CanAssimilate(QueenAssimilationDef def, Thing item)
        {
            if (def == null)
            {
                return false;
            }

            if (def.oncePerQueen && HasAssimilated(def))
            {
                return "XMT_AssimilationAlreadyAssimilated".Translate();
            }

            if (def.requiredEvolution != null && !HasEvolution(def.requiredEvolution))
            {
                return "XMT_AssimilationRequires".Translate(def.requiredEvolution.LabelCap);
            }

            if (def.prerequisiteAssimilations != null)
            {
                foreach (QueenAssimilationDef prerequisite in def.prerequisiteAssimilations)
                {
                    if (!HasAssimilated(prerequisite))
                    {
                        return "XMT_AssimilationRequires".Translate(prerequisite.LabelCap);
                    }
                }
            }

            if (def.thingDef != null && item == null)
            {
                return "XMT_AssimilationRequiresCount".Translate(def.consumeCount, def.thingDef.LabelCap);
            }

            if (def.thingDef != null && item.def != def.thingDef)
            {
                return "XMT_AssimilationRequires".Translate(def.thingDef.LabelCap);
            }

            if (item != null && item.stackCount < def.consumeCount)
            {
                return "XMT_AssimilationRequiresCount".Translate(def.consumeCount, def.thingDef.LabelCap);
            }

            return AcceptanceReport.WasAccepted;
        }

        public AcceptanceReport CanAssimilate(Thing item, QueenAssimilationDef def)
        {
            return CanAssimilate(def, item);
        }

        public void Assimilate(Thing item, bool showMessage = true)
        {
            AcceptanceReport report = CanAssimilate(item, out QueenAssimilationDef def);
            if (!report.Accepted)
            {
                if (showMessage)
                {
                    Messages.Message(report.Reason, Parent, MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            if (def == null)
            {
                AssimilateResources(item, showMessage);
                return;
            }

            if (def.thingDef != null)
            {
                ConsumeThing(item, def.consumeCount);
            }

            ApplyAssimilationResults(def);

            if (!CompletedAssimilations.Contains(def))
            {
                CompletedAssimilations.Add(def);
            }

            EnsureMechanitorBaselineResearch();

            string message = def.completedMessageKey.NullOrEmpty()
                ? "XMT_AssimilationCompletedGeneric".Translate(Parent.Named("PAWN"), def.LabelCap).Resolve()
                : def.completedMessageKey.Translate(Parent.Named("PAWN")).Resolve();
            if (showMessage)
            {
                Messages.Message(message, Parent, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void ApplyAssimilationResults(QueenAssimilationDef def)
        {
            if (def.implantHediff != null)
            {
                BodyPartRecord part = GetBodyPart(def.implantBodyPart);
                if (!Parent.health.hediffSet.HasHediff(def.implantHediff, part))
                {
                    Hediff hediff = HediffMaker.MakeHediff(def.implantHediff, Parent, part);
                    Parent.health.AddHediff(hediff, part);
                    Parent.mechanitor?.Notify_HediffStateChange(hediff);
                }
            }

            if (def.hediffsToAdd != null)
            {
                foreach (HediffDef hediffDef in def.hediffsToAdd)
                {
                    if (hediffDef != null && !Parent.health.hediffSet.HasHediff(hediffDef))
                    {
                        Parent.health.AddHediff(hediffDef);
                    }
                }
            }

            if (def.researchToFinish != null && !def.researchToFinish.IsFinished)
            {
                Find.ResearchManager.FinishProject(def.researchToFinish, doCompletionDialog: false, researcher: Parent, doCompletionLetter: true);
            }

            if (def.questToTrigger != null)
            {
                QuestUtility.GenerateQuestAndMakeAvailable(def.questToTrigger, SlateForQuest(def));
                if (!def.questLetterLabelKey.NullOrEmpty() && !def.questLetterTextKey.NullOrEmpty())
                {
                    Find.LetterStack.ReceiveLetter(def.questLetterLabelKey.Translate(Parent.Named("PAWN")).Resolve(), def.questLetterTextKey.Translate(Parent.Named("PAWN")).Resolve(), LetterDefOf.PositiveEvent, Parent);
                }
            }

            Parent.mechanitor?.Notify_BandwidthChanged();
        }

        private void EnsureMechanitorBaselineResearch()
        {
            if (!ModsConfig.BiotechActive || Parent == null || !HasEvolution(DefDatabase<RoyalEvolutionDef>.GetNamedSilentFail("Evo_MechanoidGestation")))
            {
                return;
            }

            ResearchProjectDef basicMechtech = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("BasicMechtech");
            if (basicMechtech == null || basicMechtech.IsFinished)
            {
                return;
            }

            QueenAssimilationDef mechlinkAssimilation = DefDatabase<QueenAssimilationDef>.GetNamedSilentFail("XMT_Assimilate_Mechlink");
            HediffDef mechlinkImplant = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
            bool hasMechlink = HasAssimilated(mechlinkAssimilation)
                || (mechlinkImplant != null && Parent.health?.hediffSet?.HasHediff(mechlinkImplant) == true);
            if (hasMechlink)
            {
                Find.ResearchManager.FinishProject(basicMechtech, doCompletionDialog: false, researcher: Parent, doCompletionLetter: false);
            }
        }

        public bool ResourceUnlocked(QueenIngestibleResourceDef resource)
        {
            if (resource == null)
            {
                return false;
            }

            if (resource.requiredEvolution != null && !HasEvolution(resource.requiredEvolution))
            {
                return false;
            }

            if (resource.prerequisiteAssimilations != null)
            {
                foreach (QueenAssimilationDef prerequisite in resource.prerequisiteAssimilations)
                {
                    if (!HasAssimilated(prerequisite))
                    {
                        return false;
                    }
                }
            }

            return GetResourceCapacity(resource) > 0f;
        }

        public float GetResourceAmount(QueenIngestibleResourceDef resource)
        {
            int index = ResourceIndex(resource);
            return index < 0 ? 0f : resourceAmounts[index];
        }

        public float GetResourceCapacity(QueenIngestibleResourceDef resource)
        {
            return QueenIngestibleResourceUtility.CapacityFor(Parent, resource);
        }

        public float GetResourceSpace(QueenIngestibleResourceDef resource)
        {
            return Mathf.Max(0f, GetResourceCapacity(resource) - GetResourceAmount(resource));
        }

        public void AddResource(QueenIngestibleResourceDef resource, float amount)
        {
            if (resource == null || amount <= 0f)
            {
                return;
            }

            EnsureResourceLists();
            int index = ResourceIndex(resource);
            if (index < 0)
            {
                resourceDefs.Add(resource);
                resourceAmounts.Add(0f);
                index = resourceAmounts.Count - 1;
            }

            resourceAmounts[index] = Mathf.Min(GetResourceCapacity(resource), resourceAmounts[index] + amount);
        }

        public bool TrySpendResource(QueenIngestibleResourceDef resource, float amount)
        {
            if (resource == null || amount < 0f || GetResourceAmount(resource) < amount)
            {
                return false;
            }

            int index = ResourceIndex(resource);
            resourceAmounts[index] = Mathf.Max(0f, resourceAmounts[index] - amount);
            return true;
        }

        private int ResourceIndex(QueenIngestibleResourceDef resource)
        {
            EnsureResourceLists();
            return resourceDefs.IndexOf(resource);
        }

        private void EnsureResourceLists()
        {
            if (resourceDefs == null)
            {
                resourceDefs = new List<QueenIngestibleResourceDef>();
            }

            if (resourceAmounts == null)
            {
                resourceAmounts = new List<float>();
            }

            while (resourceAmounts.Count < resourceDefs.Count)
            {
                resourceAmounts.Add(0f);
            }
        }

        private void AssimilateResources(Thing item, bool showMessage)
        {
            List<QueenResourceGain> gains = QueenIngestibleResourceUtility.GetResourceGains(item, Parent, this);
            if (gains.Count == 0)
            {
                return;
            }

            int count = item.stackCount;
            string itemLabel = item.LabelCap;
            ConsumeThing(item, count);
            foreach (QueenResourceGain gain in gains)
            {
                AddResource(gain.resource, gain.amount);
            }

            if (showMessage)
            {
                string gainLabel = QueenIngestibleResourceUtility.GainsLabel(gains);
                string message = "XMT_AssimilatedResourcesMessage".Translate(Parent.Named("PAWN"), itemLabel, gainLabel).Resolve();
                Messages.Message(message, Parent, MessageTypeDefOf.PositiveEvent, false);
                Find.PlayLog.Add(new PlayLogEntry_QueenResourceAssimilation(Parent, message));
            }
        }

        private bool AnyVisibleResource()
        {
            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (ResourceUnlocked(resource) || GetResourceAmount(resource) > 0f || resource.showWhenLocked)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanGestateMechanoids()
        {
            QueenIngestibleResourceDef resource = QueenMechGestationUtility.MechMaterialResource;
            return resource != null && ResourceUnlocked(resource);
        }

        private void OpenMechGestationMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (RecipeDef recipe in QueenMechGestationUtility.EligibleRecipes(Parent).OrderBy(recipe => recipe.LabelCap.ToString()))
            {
                ThingDef product = QueenMechGestationUtility.ProductFor(recipe);
                string optionLabel = "XMT_GestateMechanoidOption".Translate(product.LabelCap, Mathf.CeilToInt(QueenMechGestationUtility.MaterialCost(Parent, recipe)), Mathf.CeilToInt(QueenMechGestationUtility.GestationTicks(Parent, recipe) / 2500f));
                if (QueenMechGestationUtility.CanGestate(Parent, recipe, out string reason))
                {
                    options.Add(new FloatMenuOption(optionLabel, delegate
                    {
                        StartMechGestationTargeting(recipe);
                    }));
                }
                else
                {
                    options.Add(new FloatMenuOption(optionLabel + " (" + reason + ")", null));
                }
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("XMT_NoMechGestationOptions".Translate(), null));
            }

            AddFloatMenu(options);
        }

        private void StartMechGestationTargeting(RecipeDef recipe)
        {
            ThingDef product = QueenMechGestationUtility.ProductFor(recipe);
            TargetingParameters parameters = TargetingParameters.ForCell();
            parameters.validator = target => target.IsValid && CanLayMechAt(target.Cell);

            Find.Targeter.BeginTargeting(parameters, delegate (LocalTargetInfo target)
            {
                if (!CanLayMechAt(target.Cell))
                {
                    Messages.Message("CannotGenericWorkCustom".Translate("NoPath".Translate()), Parent, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_QueenGestateMech, target.Cell);
                job.thingDefToCarry = product;
                Parent.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, null, delegate (LocalTargetInfo target)
            {
                if (target.IsValid && !CanLayMechAt(target.Cell))
                {
                    Widgets.MouseAttachedLabel("CannotGenericWorkCustom".Translate("NoPath".Translate()));
                }

                return true;
            });
        }

        private bool CanLayMechAt(IntVec3 cell)
        {
            Map map = Parent?.MapHeld;
            if (map == null || !cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstPawn(map) != null || cell.GetEdifice(map) != null)
            {
                return false;
            }

            foreach (IntVec3 adjacentCell in GenRadial.RadialCellsAround(cell, 1f, false))
            {
                if (adjacentCell.InBounds(map)
                    && adjacentCell.Standable(map)
                    && (adjacentCell == Parent.Position || adjacentCell.GetFirstPawn(map) == null)
                    && map.reachability.CanReach(Parent.Position, adjacentCell, PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                {
                    return true;
                }
            }

            return false;
        }

        private BodyPartRecord GetBodyPart(BodyPartDef bodyPartDef)
        {
            if (bodyPartDef == null)
            {
                return null;
            }

            return Parent.health.hediffSet.GetNotMissingParts().FirstOrDefault(part => part.def == bodyPartDef);
        }

        private Slate SlateForQuest(QueenAssimilationDef def)
        {
            Slate slate = new Slate();
            slate.Set("asker", Parent);
            slate.Set("map", Parent.MapHeld);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(Parent.MapHeld));
            return slate;
        }

        private void OpenQueenBossgroupMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            AddQueenBossgroupOption(options, "Diabolus", null);
            AddQueenBossgroupOption(options, "Warqueen", "XMT_Assimilate_SignalChip");
            AddQueenBossgroupOption(options, "Apocriton", "XMT_Assimilate_PowerfocusChip");

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("XMT_NoMechThreatsAvailable".Translate(), null));
            }

            AddUpwardFloatMenu(options);
        }

        private void AddQueenBossgroupOption(List<FloatMenuOption> options, string bossgroupDefName, string requiredAssimilationDefName)
        {
            BossgroupDef bossgroupDef = DefDatabase<BossgroupDef>.GetNamedSilentFail(bossgroupDefName);
            if (bossgroupDef == null)
            {
                return;
            }

            QueenAssimilationDef requiredAssimilation = requiredAssimilationDefName.NullOrEmpty()
                ? null
                : DefDatabase<QueenAssimilationDef>.GetNamedSilentFail(requiredAssimilationDefName);
            if (requiredAssimilation != null && !HasAssimilated(requiredAssimilation))
            {
                FloatMenuOption disabledOption = new FloatMenuOption("XMT_CallMechThreatRequires".Translate(GetBossgroupLabel(bossgroupDef), requiredAssimilation.LabelCap), null);
                disabledOption.tooltip = GetBossgroupTooltip(bossgroupDef);
                options.Add(disabledOption);
                return;
            }

            FloatMenuOption option = new FloatMenuOption("XMT_CallMechThreatOption".Translate(GetBossgroupLabel(bossgroupDef)), delegate
            {
                CallQueenBossgroup(bossgroupDef);
            });
            option.tooltip = GetBossgroupTooltip(bossgroupDef);
            options.Add(option);
        }

        private string GetBossgroupTooltip(BossgroupDef bossgroupDef)
        {
            if (bossgroupDef == null)
            {
                return null;
            }

            string text = bossgroupDef.LeaderDescription;
            if (bossgroupDef.rewardDef != null)
            {
                text += "\n\n" + "XMT_CallMechThreatReward".Translate(bossgroupDef.rewardDef.LabelCap);
            }

            GameComponent_Bossgroup bossgroupComponent = Current.Game.GetComponent<GameComponent_Bossgroup>();
            int wave = (bossgroupComponent?.NumTimesCalledBossgroup(bossgroupDef) ?? 0) + 1;
            text += "\n\n" + "XMT_CallMechThreatWave".Translate(wave);
            return text;
        }

        private string GetBossgroupLabel(BossgroupDef bossgroupDef)
        {
            return bossgroupDef?.boss?.kindDef?.LabelCap ?? bossgroupDef?.defName ?? "";
        }

        private void CallQueenBossgroup(BossgroupDef bossgroupDef)
        {
            if (bossgroupDef == null || Parent.MapHeld == null)
            {
                return;
            }

            GameComponent_Bossgroup bossgroupComponent = Current.Game.GetComponent<GameComponent_Bossgroup>();
            int wave = bossgroupComponent?.NumTimesCalledBossgroup(bossgroupDef) ?? 0;
            Slate slate = new Slate();
            slate.Set("map", Parent.MapHeld);
            slate.Set("bossgroup", bossgroupDef);
            slate.Set("wave", wave);
            slate.Set("reward", bossgroupDef.rewardDef);
            QuestUtility.GenerateQuestAndMakeAvailable(bossgroupDef.quest, slate);
            bossgroupComponent?.Notify_BossgroupCalled(bossgroupDef);
            Messages.Message("XMT_CalledMechThreat".Translate(Parent.Named("PAWN"), GetBossgroupLabel(bossgroupDef)), Parent, MessageTypeDefOf.ThreatBig, false);
        }

        private void AddUpwardFloatMenu(List<FloatMenuOption> options)
        {
            FloatMenu menu = new FloatMenu(options);
            try
            {
                floatMenuInitialPositionShiftField?.SetValue(null, new Vector2(0f, -menu.InitialSize.y));
                Find.WindowStack.Add(menu);
            }
            finally
            {
                floatMenuInitialPositionShiftField?.SetValue(null, Vector2.zero);
            }
        }

        private void AddFloatMenu(List<FloatMenuOption> options)
        {
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ConsumeThing(Thing item, int count)
        {
            if (item == null || item.Destroyed)
            {
                return;
            }

            if (item.stackCount <= count)
            {
                item.Destroy();
                return;
            }

            Thing split = item.SplitOff(count);
            split.Destroy();
        }

        private void OpenCommsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            FloatMenuOption bossgroupOption = new FloatMenuOption("XMT_CallMechThreat".Translate(), OpenQueenBossgroupMenu);
            bossgroupOption.tooltip = "XMT_CallMechThreatDesc".Translate();
            options.Add(bossgroupOption);

            int commsTargetCount = 0;
            foreach (ICommunicable target in GetCommsTargets())
            {
                commsTargetCount++;
                string label = target.GetCallLabel();
                options.Add(new FloatMenuOption(label, delegate
                {
                    target.TryOpenComms(Parent);
                }));
            }

            if (commsTargetCount == 0)
            {
                options.Add(new FloatMenuOption("XMT_NoCommsTargetsAvailable".Translate(), null));
            }

            AddFloatMenu(options);
        }

        private IEnumerable<ICommunicable> GetCommsTargets()
        {
            if (Parent.MapHeld != null && passingShipsField?.GetValue(Parent.MapHeld.passingShipManager) is List<PassingShip> passingShips)
            {
                foreach (PassingShip passingShip in passingShips)
                {
                    if (!passingShip.Departed)
                    {
                        yield return passingShip;
                    }
                }
            }

            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (!faction.IsPlayer && !faction.def.hidden && faction.def.humanlikeFaction)
                {
                    yield return faction;
                }
            }
        }

    }

    public class CompQueenAssimilationProperties : CompProperties
    {
        public CompQueenAssimilationProperties()
        {
            compClass = typeof(CompQueenAssimilation);
        }
    }
}
