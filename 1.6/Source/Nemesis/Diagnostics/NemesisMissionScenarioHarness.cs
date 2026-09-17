using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal static class NemesisMissionScenarioHarness
    {
        internal static void RunAll() { RunContracts(); RunLordPolicies(); RunExtractionRetentionPlacement(); }

        internal static void RunContracts()
        {
            Log.Message("[XMT][Mission scenario] BEGIN mission data contracts");
            try
            {
                GameComponent_Nemesis component = Current.Game.GetComponent<GameComponent_Nemesis>();
                Map map = Find.CurrentMap;
                foreach (NemesisMissionDef def in DefDatabase<NemesisMissionDef>.AllDefsListForReading)
                {
                    Check(def.defName + " configuration", !def.ConfigErrors().Any());
                    NemesisMissionWorker worker = def.Worker;
                    int count = worker.PartySize(def, component, map, component.Awakened);
                    int pressureBase = Mathf.FloorToInt(Mathf.Lerp(def.populationRange.min, def.populationRange.max,
                        def.Pressure(component, component.Awakened)));
                    Check(def.defName + " pressure bound", count <= pressureBase * (worker is NemesisMissionWorker_HostCollection ? 2 : 1));
                    if (worker is NemesisMissionWorker_HostCollection)
                    {
                        Check("collection preserves pressure base", count >= pressureBase);
                        float combatPower = def.workerSettings.raidPointPawnKind.combatPower;
                        Check("zero raid points preserves base", NemesisMissionWorker_HostCollection.CollectionSize(def, 4, 0f, combatPower, null, 0) == 4);
                        Check("storyteller points cap at double base", NemesisMissionWorker_HostCollection.CollectionSize(def, 4,
                            100 * combatPower, combatPower, null, 0) == 8);
                    }
                }
                NemesisObservationDef observation = DefDatabase<NemesisObservationDef>.AllDefsListForReading.First(o => o.metric == "Walls");
                NemesisSpatialContact old = new NemesisSpatialContact { observation = observation, subjectId = "fixture",
                    mapId = map.uniqueID, cell = map.Center, defName = "Wall", tags = new List<string> { "Wall" } };
                NemesisSpatialContact fresh = new NemesisSpatialContact { observation = observation, subjectId = "fixture",
                    mapId = map.uniqueID, cell = map.Center, defName = "Wall", tags = new List<string> { "Wall" }, label = "different label" };
                Check("labels do not invent spatial changes", NemesisMissionUtility.ChangedContacts(new[] { old }, new[] { fresh }, map.uniqueID).Count == 0);
                fresh.cell += IntVec3.East;
                Check("moved contact investigates both locations", NemesisMissionUtility.ChangedContacts(new[] { old }, new[] { fresh }, map.uniqueID).Count == 2);
                RunSwarmProgressionContracts(component, map);
                RunFollowUpContracts();
                Log.Message("[XMT][Mission scenario] PASS all mission data contracts.");
            }
            catch (Exception exception) { Log.Error("[XMT][Mission scenario] FAIL mission data contracts: " + exception); }
        }

        private static void RunFollowUpContracts()
        {
            NemesisMissionDef scouting = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_Scouting");
            NemesisMissionDef sabotage = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_PowerSabotage");
            NemesisMissionDef assault = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_Assault");
            Check("scouting is a prelude candidate", scouting.followUpMissions.Count > 0);
            Check("power sabotage is awakened-only", !sabotage.allowDormant && sabotage.allowAwakened);
            Check("assault has a hard twenty-five percent floor", Mathf.Approximately(assault.minimumXenoforming, 25f));
            Check("assault is terminal", assault.followUpMissions.Count == 0);
            Check("follow-ups are nearly certain by seventy-five percent",
                GameComponent_Nemesis.Settings.followUpChanceByXenoforming.Evaluate(75f) >= 0.95f);
            Check("exactly two-thirds swarm resolution fails",
                !LordJob_NemesisSwarmAssault.MeetsAttachmentThreshold(2, 3));
            Check("more than two-thirds swarm resolution succeeds",
                LordJob_NemesisSwarmAssault.MeetsAttachmentThreshold(3, 4));
        }

        private static void RunSwarmProgressionContracts(GameComponent_Nemesis component, Map map)
        {
            float originalXenoforming = XenoformingUtility.GetXenoforming();
            try
            {
                NemesisMissionDef facehugger = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_FacehuggerAssault");
                XenoformingUtility.SetXenoforming(15f);
                int earlyCeiling = Mathf.FloorToInt(Mathf.Lerp(facehugger.populationRange.min, facehugger.populationRange.max,
                    facehugger.Pressure(component, true)));
                Check("facehugger assault xenoforming ceiling is 23 at fifteen percent", earlyCeiling == 23);
                Check("facehugger assault stays at its floor without threat points",
                    NemesisMissionWorker_SwarmAssault.ThreatPointPopulation(facehugger, earlyCeiling, 0f,
                        facehugger.workerSettings.pawnKind.combatPower) == 8);
                XenoformingUtility.SetXenoforming(50f);
                Check("facehugger assault can reach 60 at fifty percent",
                    NemesisMissionWorker_SwarmAssault.ThreatPointPopulation(facehugger, 60, 10000f,
                        facehugger.workerSettings.pawnKind.combatPower) == 60);
                XenoformingUtility.SetXenoforming(100f);
                Check("facehugger assault remains capped at 60",
                    NemesisMissionWorker_SwarmAssault.ThreatPointPopulation(facehugger, 60, 10000f,
                        facehugger.workerSettings.pawnKind.combatPower) == 60);
                Check("ovothrone lineage is not reached at fourteen percent",
                    !QueenProgressionUtility.WouldReachEvolutionLineage(RoyalEvolutionDefOf.BaseQueenSet,
                        RoyalEvolutionDefOf.Evo_OvoThrone, 14f));
                Check("ovothrone lineage is reached at fifteen percent",
                    QueenProgressionUtility.WouldReachEvolutionLineage(RoyalEvolutionDefOf.BaseQueenSet,
                        RoyalEvolutionDefOf.Evo_OvoThrone, 15f));
                Check("integrated egg sac keeps ovothrone lineage reached",
                    QueenProgressionUtility.WouldReachEvolutionLineage(RoyalEvolutionDefOf.BaseQueenSet,
                        RoyalEvolutionDefOf.Evo_OvoThrone, 25f));

                NemesisStanceDef mechanitor = DefDatabase<NemesisStanceDef>.GetNamedSilentFail("XMT_NemesisStance_Mechanitor");
                RoyalEvolutionDef subversion = DefDatabase<RoyalEvolutionDef>.GetNamedSilentFail("Evo_MechanoidSubversion");
                if (mechanitor?.evolutionSet != null && subversion != null)
                {
                    NemesisMissionDef subverter = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_SubverterAssault");
                    Check("mechanoid subversion lineage is not reached at twenty-five percent",
                        !QueenProgressionUtility.WouldReachEvolutionLineage(mechanitor.evolutionSet, subversion, 25f));
                    Check("mechanoid subversion lineage is reached at twenty-six percent",
                        QueenProgressionUtility.WouldReachEvolutionLineage(mechanitor.evolutionSet, subversion, 26f));
                    Check("mechanoid subversion diagnostic threshold is twenty-six percent",
                        QueenProgressionUtility.MinimumXenoformingToReachEvolutionLineage(mechanitor.evolutionSet, subversion) == 26f);
                    XenoformingUtility.SetXenoforming(25f);
                    Check("subverter assault normal progression starts at 6 at twenty-five percent",
                        Mathf.FloorToInt(Mathf.Lerp(subverter.populationRange.min, subverter.populationRange.max,
                            subverter.Pressure(component, true))) == 6);
                    XenoformingUtility.SetXenoforming(75f);
                    Check("subverter assault normal progression reaches 60 at seventy-five percent",
                        Mathf.FloorToInt(Mathf.Lerp(subverter.populationRange.min, subverter.populationRange.max,
                            subverter.Pressure(component, true))) == 60);
                    Check("used mechanoid bandwidth can push subverter population beyond sixty",
                        NemesisMissionWorker_SubverterAssault.PopulationWithMechanoidBandwidth(60, 75f, 1f) == 135);
                }
            }
            finally
            {
                XenoformingUtility.SetXenoforming(originalXenoforming);
            }
        }

        internal static void RunLordPolicies()
        {
            Log.Message("[XMT][Mission scenario] BEGIN mission lord ownership");
            Lord lord = null;
            try
            {
                Map map = Find.CurrentMap;
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
                Pawn morph = Current.Game.GetComponent<GameComponent_Xenomorph>().GetWorldOrGeneratedCryptimorphForMission();
                GenSpawn.Spawn(morph, cell, map);
                Pawn host = PawnGenerator.GeneratePawn(PawnKindDefOf.Drifter, Faction.OfPlayer);
                GenSpawn.Spawn(host, CellFinder.RandomClosewalkCellNear(cell, map, 4), map);
                host.health.AddHediff(HediffDefOf.Anesthetic);
                NemesisMissionDef def = DefDatabase<NemesisMissionDef>.GetNamed("XMT_NemesisMission_HostCollection");
                LordJob_NemesisHostCollection job = new LordJob_NemesisHostCollection();
                job.Initialize(def, false, new List<IntVec3> { cell });
                lord = LordMaker.MakeNewLord(null, job, map, new[] { morph });
                job.Begin();
                Job decision = job.GetMissionJob(morph);
                Check("mission lord owns its host decision", decision?.def == NemesisMissionUtility.AbductJob && decision.targetA.Pawn == host);
                Check("collection reveal does not directly attack", !job.AllowRevealAttack(morph, host));
                job.Notify_Revealed(morph);
                Check("lord records revelation", job.WasDiscovered(morph));
                job.Withdraw("scenario anti-blockage check");
                Check("withdrawing mission does not block later missions", !job.BlocksNewMissions);
                Log.Message("[XMT][Mission scenario] PASS mission lord ownership. Fixtures remain.");
            }
            catch (Exception exception) { Log.Error("[XMT][Mission scenario] FAIL mission lord ownership: " + exception); }
            finally { if (lord != null) lord.Map.lordManager.RemoveLord(lord); }
        }

        internal static void RunExtractionRetentionPlacement()
        {
            Log.Message("[XMT][Mission scenario] BEGIN extraction, retention and placement");
            try
            {
                Map map = Find.CurrentMap;
                GameComponent_NemesisWorldPawns manager = Current.Game.GetComponent<GameComponent_NemesisWorldPawns>();
                if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 edge, map, 0f))
                    throw new InvalidOperationException("no pawn entry cell for extraction fixture");
                Pawn carrier = Current.Game.GetComponent<GameComponent_Xenomorph>().GetWorldOrGeneratedCryptimorphForMission();
                GenSpawn.Spawn(carrier, edge, map);
                Pawn host = PawnGenerator.GeneratePawn(PawnKindDefOf.Drifter, Faction.OfPlayer);
                GenSpawn.Spawn(host, CellFinder.RandomClosewalkCellNear(edge, map, 3), map);
                host.health.AddHediff(HediffDefOf.Anesthetic);
                Check("fixture host picked up", carrier.carryTracker.TryStartCarry(host, 1, reserve: false) == 1);
                Check("edge extraction retained exact pawn", manager.RetainExtractedHost(carrier, host)
                    && manager.RetainedPawns.Any(record => record.pawn == host) && Find.WorldPawns.Contains(host));
                NemesisRetainedPawn record = manager.RetainedPawns.First(value => value.pawn == host);
                record.processTick = Find.TickManager.TicksGame;
                float before = Current.Game.GetComponent<GameComponent_Xenomorph>().Xenoforming;
                manager.ProcessRetainedPawns();
                float once = Current.Game.GetComponent<GameComponent_Xenomorph>().Xenoforming;
                manager.ProcessRetainedPawns();
                Check("delayed processing is bounded and idempotent", once >= before && once <= Mathf.Max(before, 10f)
                    && Mathf.Approximately(once, Current.Game.GetComponent<GameComponent_Xenomorph>().Xenoforming));
                List<IntVec3> cells = GenRadial.RadialCellsAround(map.Center, 20f, true)
                    .Where(cell => cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null && cell.GetFirstPawn(map) == null).ToList();
                Check("retained pawn placed once", manager.PlaceRetainedHosts(map, cells) == 1
                    && host.Spawned && host.CurrentBed() is CocoonBase && !manager.RetainedPawns.Any(value => value.pawn == host));
                Check("placed pawn cannot be reused", manager.PlaceRetainedHosts(map, cells) == 0);
                Log.Message("[XMT][Mission scenario] PASS extraction, retention and placement. Fixtures remain.");
            }
            catch (Exception exception) { Log.Error("[XMT][Mission scenario] FAIL extraction, retention and placement: " + exception); }
        }

        internal static void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException(name);
            Log.Message("[XMT][Mission scenario] PASS " + name);
        }
    }
}
