using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal static class DebugActions_NemesisMission
    {
        private static GameComponent_Nemesis Component => Current.Game?.GetComponent<GameComponent_Nemesis>();
        private static GameComponent_NemesisWorldPawns Hosts => Current.Game?.GetComponent<GameComponent_NemesisWorldPawns>();
        private static Pawn Selected => Find.Selector.SingleSelectedThing as Pawn;
        private static DebugActionNode Action(string label, Action action, DebugActionType type = DebugActionType.Action)
            => new DebugActionNode(label, type, () =>
            {
                Log.Message("[XMT][Mission][Debug] Command: " + label + " map=" + Find.CurrentMap?.uniqueID
                    + " tick=" + Find.TickManager.TicksGame);
                action();
            });
        private static DebugActionNode Group(string label, params DebugActionNode[] nodes) => new DebugActionNode(label, DebugActionType.Action, null)
            { childGetter = () => nodes.ToList() };

        internal static DebugActionNode MakeNode() => Group("Mission",
            Group("Inspect", Action("Selection, sizing and pending launch", InspectSelection),
                Action("Mission lords and pawn state", InspectLords), Action("Retained and placed hosts", InspectHosts),
                Action("Preview remembered approach points", PreviewRoutes)),
            Group("Atomic", Action("Evaluate opportunity now", EvaluateOpportunity),
                Action("Recheck pending launch", () => Component?.RecheckPendingMission()),
                Action("Cancel pending launch", () => Component?.CancelPendingMission()),
                Action("Census and report generic spatial changes", CensusChanges),
                Action("Add scout-host evidence to current map", AddScoutHostEvidence),
                Action("Reveal selected pawn (real stealth)", () => Selected?.GetComp<CompStealth>()?.ForceVisible()),
                Action("Notify selected pawn revealed twice", RepeatReveal),
                Action("Selected cryptimorph: abduct clicked host", AssignAbduction, DebugActionType.ToolMap),
                Action("Extract selected pawn's carried host", ExtractCarried),
                Action("Selected on-map abduction: interrupt at completion boundary", InterruptCocoonBoundary),
                Action("Make retained hosts due and process", ProcessDue),
                Action("Place retained hosts near clicked cell", PlaceHosts, DebugActionType.ToolMap)),
            new DebugActionNode("Launch", DebugActionType.Action, null) { childGetter = LaunchNodes },
            Group("Scenarios (destructive fixtures)",
                Action("Run all mission scenarios in sequence", NemesisMissionScenarioHarness.RunAll),
                Action("Run mission data contracts", NemesisMissionScenarioHarness.RunContracts),
                Action("Run mission lord ownership", NemesisMissionScenarioHarness.RunLordPolicies),
                Action("Run extraction, retention and placement", NemesisMissionScenarioHarness.RunExtractionRetentionPlacement)));

        private static List<DebugActionNode> LaunchNodes()
        {
            List<DebugActionNode> nodes = new List<DebugActionNode>();
            foreach (NemesisMissionDef def in DefDatabase<NemesisMissionDef>.AllDefsListForReading)
                foreach (bool active in new[] { false, true })
                {
                    NemesisMissionDef selected = def;
                    bool mode = active;
                    nodes.Add(Action(selected.label + (mode ? " (awakened)" : " (dormant)") + " — force now", () =>
                    {
                        if (Find.CurrentMap == null) return;
                        Log.Message("[XMT][Mission] Launch requested: " + selected.defName + " mode=" + (mode ? "awakened" : "dormant")
                            + " map=" + Find.CurrentMap.uniqueID + " tick=" + Find.TickManager.TicksGame);
                        bool launched = Component.ForceMission(selected, Find.CurrentMap, mode, out string reason);
                        Log.Message("[XMT][Mission] " + (launched ? selected.defName + " deployed; runtime lighting rules remain active." : "Launch rejected: " + reason));
                    }));
                }
            return nodes;
        }

        private static void EvaluateOpportunity()
        {
            if (Component == null) { Log.Warning("[XMT][Mission][Debug] No Nemesis component is available."); return; }
            Component.EvaluateMissionOpportunity(true, logEvaluation: true);
            LordJob_NemesisMission live = Find.Maps.SelectMany(map => map.lordManager.lords)
                .Select(lord => lord.LordJob).OfType<LordJob_NemesisMission>().FirstOrDefault();
            Log.Message("[XMT][Mission][Debug] Evaluation result: pending=" + Component.PendingMission?.mission?.defName
                + " pendingMap=" + Component.PendingMission?.mapId + " reason=" + Component.PendingMissionReason
                + " live=" + live?.Mission?.defName + " liveMap=" + live?.lord?.Map?.uniqueID);
        }

        private static void InspectSelection()
        {
            if (Component == null || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            StringBuilder text = new StringBuilder("[XMT][Mission] Selection\n");
            text.AppendLine("Awakened=" + Component.Awakened + " committed stance=" + Component.CurrentStance?.defName
                + " wealth=" + map.wealthWatcher.WealthTotal + " opportunityChance="
                + Component.CurrentMissionOpportunityChance.ToString("0.###") + " nextOpportunity=" + Component.NextMissionTick);
            text.AppendLine("Pending=" + Component.PendingMission?.mission?.defName + " map=" + Component.PendingMission?.mapId
                + " reason=" + Component.PendingMissionReason);
            foreach (NemesisMissionDef def in DefDatabase<NemesisMissionDef>.AllDefsListForReading)
            {
                NemesisMissionWorker worker = def.Worker;
                text.AppendLine(def.defName + " pressure=" + def.Pressure(Component, Component.Awakened)
                    + " size=" + worker.PartySize(def, Component, map, Component.Awakened)
                    + " baseWeight=" + def.baseWeight + " stanceWeight=" + def.StanceWeight(Component, Component.Awakened)
                    + " workerWeight=" + worker.Weight(def, Component, map)
                    + " targetValid=" + worker.CanTarget(def, Component, map) + " overlap=" + GameComponent_Nemesis.MapHasMission(map)
                    + " finalWeight=" + Component.MissionWeight(def, map, Component.Awakened));
                text.AppendLine("  " + worker.DescribeTarget(map));
                text.AppendLine("  " + worker.DescribeSizing(def, Component, map, Component.Awakened));
            }
            Log.Message(text.ToString());
        }

        private static void InspectLords()
        {
            if (Find.CurrentMap == null) return;
            foreach (Lord lord in Find.CurrentMap.lordManager.lords.Where(l => l.LordJob is LordJob_Nemesis))
            {
                LordJob_Nemesis job = (LordJob_Nemesis)lord.LordJob;
                Log.Message("[XMT][Mission] " + job.GetType().Name);
                if (job is LordJob_NemesisMission mission)
                    Log.Message("  success=" + mission.Successful + " extracted=" + mission.Extracted + " covertEnded=" + mission.CovertEnded
                        + " withdrawing=" + mission.Withdrawing + " reason=" + mission.WithdrawalReason
                        + " routes=" + string.Join(", ", mission.Route));
                foreach (Pawn pawn in lord.ownedPawns)
                    Log.Message("  " + pawn + " discovered=" + job.WasDiscovered(pawn) + " downed=" + pawn.Downed
                        + " job=" + pawn.CurJobDef + " duty=" + pawn.mindState.duty?.def
                        + " position=" + pawn.Position + " target=" + pawn.CurJob?.targetA + " carrying=" + pawn.carryTracker?.CarriedThing);
            }
        }

        private static void InspectHosts()
        {
            if (Hosts == null) return;
            Log.Message("[XMT][Mission] Host records=" + Hosts.RetainedPawns.Count);
            foreach (NemesisRetainedPawn host in Hosts.RetainedPawns)
                Log.Message("  " + host.pawn + " id=" + host.pawn?.GetUniqueLoadID() + " due=" + host.processTick
                    + " processed=" + host.processingComplete
                    + " retained=" + Find.WorldPawns.ForcefullyKeptPawns.Contains(host.pawn));
        }

        private static void PreviewRoutes()
        {
            if (Component == null || Find.CurrentMap == null) return;
            foreach (NemesisMissionDef def in DefDatabase<NemesisMissionDef>.AllDefsListForReading)
            {
                IEnumerable<NemesisSpatialContact> contacts = Component.SpatialContacts.Where(c => c.mapId == Find.CurrentMap.uniqueID);
                if (def.Worker is NemesisMissionWorker_HostCollection) contacts = contacts.Where(c => c.tags.Contains("HostBed"));
                List<IntVec3> route = NemesisMissionUtility.Routes(contacts, def, Find.CurrentMap);
                Log.Message("[XMT][Mission] " + def.defName + " remembered route=" + string.Join(", ", route));
                foreach (IntVec3 cell in route) Find.CurrentMap.debugDrawer.FlashCell(cell, 0.5f, "mission");
            }
        }

        private static void CensusChanges()
        {
            if (Component == null || Find.CurrentMap == null) return;
            List<NemesisSpatialContact> before = Component.SpatialContacts.ToList();
            Component.RequestIntelligence("mission author spatial comparison");
            foreach (NemesisSpatialContact contact in NemesisMissionUtility.ChangedContacts(before, Component.SpatialContacts, Find.CurrentMap.uniqueID))
            {
                Log.Message("[XMT][Mission] Investigate " + contact.observation.defName + " " + contact.subjectId + " at " + contact.cell);
                Find.CurrentMap.debugDrawer.FlashCell(contact.cell, 0.7f, "changed");
            }
        }

        private static void AddScoutHostEvidence()
        {
            NemesisEvidenceDef evidence = DefDatabase<NemesisEvidenceDef>.GetNamedSilentFail("XMT_Nemesis_ScoutHostAcquired");
            bool accepted = Component?.RecordMapEvidence(evidence, 1f, Find.CurrentMap, evidence, "developer injection") == true;
            Log.Message("[XMT][Mission][Debug] Scout-host map evidence " + (accepted ? "recorded" : "was not recorded")
                + "; value=" + (Component?.MapEvidenceValue(evidence, Find.CurrentMap).ToString("0.###") ?? "unavailable"));
        }

        private static void RepeatReveal()
        {
            if (Selected?.GetLord()?.LordJob is LordJob_Nemesis lord) { lord.Notify_Revealed(Selected); lord.Notify_Revealed(Selected); }
        }

        private static void AssignAbduction()
        {
            Pawn carrier = Selected;
            Pawn target = UI.MouseCell().GetFirstPawn(Find.CurrentMap);
            if (carrier?.GetMorphComp() == null || target == null || carrier == target) return;
            Job job = NemesisMissionUtility.ExtractionJob(carrier, target);
            if (job != null) carrier.jobs.StartJob(job, JobCondition.InterruptForced, keepCarryingThingOverride: true);
        }

        private static void ExtractCarried()
        {
            Pawn carrier = Selected;
            if (!(carrier?.carryTracker?.CarriedThing is Pawn target)) return;
            Job job = NemesisMissionUtility.ExtractionJob(carrier, target);
            if (job != null) carrier.jobs.StartJob(job, JobCondition.InterruptForced, keepCarryingThingOverride: true);
        }

        private static void InterruptCocoonBoundary()
        {
            Pawn pawn = Selected;
            if (pawn?.CurJobDef != XenoWorkDefOf.XMT_AbductHost || !(pawn.jobs.curDriver is JobDriver_AbductHost driver)
                || !(pawn.carryTracker.CarriedThing is Pawn host) || pawn.Position != pawn.CurJob.targetB.Cell)
            {
                Log.Warning("[XMT][Mission] Select an on-map abductor carrying its host at the cocoon destination.");
                return;
            }
            bool cocoonedBefore = host.health.hediffSet.HasHediff(InternalDefOf.StarbeastCocoon);
            // Deliberately reproduce the old finish-action hazard without executing a successful toil tick.
            AccessTools.Field(typeof(JobDriver_AbductHost), "CocoonProgress").SetValue(driver, 1f);
            AccessTools.Field(typeof(JobDriver_AbductHost), "CocoonTicks").SetValue(driver, 100000f);
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            bool passed = host.health.hediffSet.HasHediff(InternalDefOf.StarbeastCocoon) == cocoonedBefore
                && !(pawn.Position.GetEdifice(pawn.Map) is CocoonBase) && pawn.carryTracker.CarriedThing == host;
            if (passed) Log.Message("[XMT][Mission scenario] PASS interruption did not cocoon or drop the host.");
            else Log.Error("[XMT][Mission scenario] FAIL interruption changed cocoon or carry state.");
        }

        private static void ProcessDue()
        {
            if (Hosts == null) return;
            foreach (NemesisRetainedPawn host in Hosts.RetainedPawns) host.processTick = Find.TickManager.TicksGame;
            Hosts.ProcessRetainedPawns();
            InspectHosts();
        }

        private static void PlaceHosts()
        {
            int count = Hosts.PlaceRetainedHosts(Find.CurrentMap, GenRadial.RadialCellsAround(UI.MouseCell(), 12f, true));
            Log.Message("[XMT][Mission] Placed " + count + " retained hosts.");
        }
    }
}
