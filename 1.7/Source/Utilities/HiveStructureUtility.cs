using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class HiveStructureUtility
    {
        public static bool Unlocked(HiveStructureUnlockDef def)
        {
            CompQueen queen = HiveQueenUtility.Resolve()?.GetComp<CompQueen>();
            return queen != null && DefDatabase<RoyalEvolutionDef>.AllDefsListForReading.Any(e => e.structureUnlocks?.Contains(def) == true && queen.HasEvolutionFeature(e));
        }

        public static IEnumerable<HiveStructureUnlockDef> Available => DefDatabase<HiveStructureUnlockDef>.AllDefsListForReading.Where(Unlocked).OrderByDescending(d => d.priority).ThenBy(d => d.defName);

        public static void Initialize()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                bool inputs = def.comps?.Any(p => p is CompJellyMakerProperties || p is CompProperties_AcidPit) == true;
                if (inputs)
                {
                    System.Type tabType = def.comps.Any(p => p is CompProperties_AcidPit) ? typeof(ITab_AcidPitInputs) : typeof(ITab_HiveInputs);
                    def.inspectorTabs ??= new List<System.Type>();
                    def.inspectorTabsResolved ??= new List<InspectTabBase>();
                    if (!def.inspectorTabs.Contains(tabType))
                    {
                        def.inspectorTabs.Add(tabType);
                        def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
                    }
                }

                if (DefDatabase<HiveStructureUnlockDef>.AllDefsListForReading.Any(u => u.mode == HiveCreationMode.Merge && u.sourceBuildings?.Allows(def) == true))
                {
                    def.comps ??= new List<CompProperties>();
                    if (!def.comps.Any(p => p.compClass == typeof(CompStructureCreationCommands)))
                        def.comps.Add(new CompProperties(typeof(CompStructureCreationCommands)));
                }
            }

            InstallArchitectCommands(DefDatabase<DesignationCategoryDef>.GetNamed("XMT_Hive"));
        }

        public static void InstallArchitectCommands(DesignationCategoryDef category)
        {
            if (category?.defName != "XMT_Hive")
                return;
            List<Designator> commands = category.AllResolvedDesignators;
            List<HiveStructureUnlockDef> unlocks = DefDatabase<HiveStructureUnlockDef>.AllDefsListForReading.Where(d => d.architect).ToList();
            commands.RemoveAll(d => d is Designator_HiveHost || d is Designator_HiveUnlockCommand || d is Designator_HiveStructure || (d is Designator_Build build && unlocks.Any(u => u.building == build.PlacingDef)));
            foreach (HiveStructureUnlockDef unlock in unlocks)
            {
                if (unlock.mode == HiveCreationMode.ApplyHediff)
                    commands.Add(new Designator_HiveHost(unlock));
                else if (unlock.mode == HiveCreationMode.Merge)
                    commands.Add(new Designator_HiveUnlockCommand(unlock));
                else
                    commands.Add(new Designator_HiveStructure(unlock));
            }
        }

        public static Job MakeJob(HiveStructureUnlockDef def, Thing target) => JobMaker.MakeJob(def.job, target);
        public static bool TryGetAutomaticJob(Pawn actor, WorkTypeDef workType, out Job job)
        {
            job = null;
            if (actor?.Map == null)
                return false;
            foreach (HiveStructureUnlockDef def in Available.Where(d => d.automatic && (workType == null || d.workType == workType)))
            {
                if (!def.Worker.ActorAllowed(def, actor) || (actor.Faction == null && !def.allowFeral))
                    continue;
                IEnumerable<Thing> candidates = actor.Faction != null ? actor.Map.designationManager.SpawnedDesignationsOfDef(def.designation).Select(d => d.target.Thing) : actor.Map.mapPawns.AllPawnsSpawned.Cast<Thing>();
                Thing target = candidates.Where(t => t != null && def.Worker.TargetAllowed(def, t) && FeralJobUtility.IsThingAvailableForJobBy(actor, t) && actor.CanReserveAndReach(t, PathEndMode.Touch, Danger.Deadly)).OrderByDescending(t => t is Pawn p ? p.BodySize * def.bodySizeScore - (XMTUtility.IsAcceptableHost(p) ? def.acceptableHostPenalty : 0f) : 0f).FirstOrDefault();
                if (target == null)
                    continue;
                job = MakeJob(def, target);
                return true;
            }

            return false;
        }

        public static void StartOrder(HiveStructureUnlockDef def, Pawn actor, Thing target)
        {
            if (!Unlocked(def) || !def.Worker.ActorAllowed(def, actor) || !def.Worker.TargetAllowed(def, target) || actor.Map != target.Map || actor.Faction != Faction.OfPlayer)
                return;
            System.Action start = () =>
            {
                if (!target.Spawned || !def.Worker.ActorAllowed(def, actor) || !def.Worker.TargetAllowed(def, target))
                    return;
                Job job = MakeJob(def, target);
                job.playerForced = true;
                actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };
            if (!def.contributorConfirmationKey.NullOrEmpty())
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(def.contributorConfirmationKey.Translate(actor.LabelShortCap, def.building?.label ?? def.label), start));
            else
                start();
        }

        public static void SelectContributor(HiveStructureUnlockDef def, Thing source)
        {
            if (!def.Worker.TargetAllowed(def, source))
                return;
            var parameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = t => t.Thing is Pawn p && p.Faction == Faction.OfPlayer && p.Map == source.Map && def.Worker.ActorAllowed(def, p)
            };
            Find.Targeter.BeginTargeting(parameters, t => StartOrder(def, t.Pawn, source));
        }

        public static void BeginMerge(HiveStructureUnlockDef def, Thing source)
        {
            if (!def.confirmationKey.NullOrEmpty())
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(def.confirmationKey.Translate(), () => SelectContributor(def, source)));
            else
                SelectContributor(def, source);
        }

        public static void SelectDesignator(HiveStructureUnlockDef def)
        {
            if (def.mode == HiveCreationMode.ApplyHediff)
                Find.DesignatorManager.Select(new Designator_HiveHost(def));
            else if (def.mode == HiveCreationMode.Merge)
            {
                System.Action selectSource = () => Find.Targeter.BeginTargeting(new TargetingParameters { canTargetBuildings = true, canTargetPawns = false, canTargetItems = false, validator = t => def.Worker.TargetAllowed(def, t.Thing) }, target => HiveStructureUtility.SelectContributor(def, target.Thing));
                if (!def.confirmationKey.NullOrEmpty())
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(def.confirmationKey.Translate(), selectSource));
                else
                    selectSource();
            }
            else
                Find.DesignatorManager.Select(new Designator_HiveStructure(def));
        }
    }
}
