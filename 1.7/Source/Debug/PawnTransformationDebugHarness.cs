using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Xenomorphtype
{
    internal static class PawnTransformationDebugHarness
    {
        private const string LogPrefix = "[XMT][Biohorror][TransformHarness] ";
        private const string SaveMarkerPrefix = "XMT_TransformHarness_Source_";
        private static readonly FieldInfo BattleConcernsField = AccessTools.Field(typeof(Battle), "concerns");
        private static int runNumber;

        private enum SourceLocation
        {
            Map,
            Carried,
            World,
            Caravan,
            Dead
        }

        private sealed class CaseResult
        {
            public string label;
            public bool passed;
            public readonly List<string> checks = new List<string>();
        }

        private sealed class ComprehensiveFixture
        {
            public Pawn associate;
            public LogEntry logEntry;
            public Tale tale;
            public Quest quest;
            public QuestPart_IsDead questPart;
            public Faction leaderFaction;
            public Pawn formerFactionLeader;
            public Precept_Role role;
            public Ideo ideology;
            public RoyalTitleDef royalTitle;
            public Faction royalFaction;
            public ThingWithComps selfBiocodedWeapon;
            public ThingWithComps foreignBiocodedWeapon;
            public float killRecord;
            public BodyPartDef missingPartDef;
            public string missingPartLabel;
            public int missingPartOccurrence = -1;
            public ThingDef removedImplantDef;
            public HashSet<Thing> existingRemovedImplants;
            public Thing removedImplant;
            public Pawn_MechanitorTracker mechanitorTracker;
            public Pawn controlledMech;
            public int mechanitorControlGroupCount;
        }

        internal static void RunCombinedSuite()
        {
            Map map = Find.CurrentMap;
            PawnKindDef hybridKind = HybridKind;
            if (map == null || hybridKind == null)
            {
                Messages.Message("Transformation identity suite requires an active map and XMT_HybridHorrorBlank.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            int currentRun = ++runNumber;
            List<CaseResult> results = new List<CaseResult>();
            HarnessLog(currentRun, "BEGIN combined suite destination=" + hybridKind.defName);

            RunSafely(results, currentRun, "map / full identity", () =>
                RunCase(map, hybridKind, SourceLocation.Map, true, currentRun));
            RunSafely(results, currentRun, "carried pawn", () =>
                RunCase(map, hybridKind, SourceLocation.Carried, false, currentRun));
            RunSafely(results, currentRun, "world pawn", () =>
                RunCase(map, hybridKind, SourceLocation.World, false, currentRun));
            RunSafely(results, currentRun, "caravan pawn", () =>
                RunCase(map, hybridKind, SourceLocation.Caravan, false, currentRun));
            RunSafely(results, currentRun, "already-dead pawn", () =>
                RunCase(map, hybridKind, SourceLocation.Dead, false, currentRun));
            RunSafely(results, currentRun, "gender constraints", () =>
                RunGenderConstraintChecks(hybridKind));

            int passed = results.Count(result => result.passed);
            foreach (CaseResult result in results)
            {
                HarnessLog(currentRun, (result.passed ? "PASS " : "FAIL ") + result.label
                    + " :: " + string.Join("; ", result.checks));
            }
            HarnessLog(currentRun, "END combined suite passed=" + passed + "/" + results.Count);

            bool suitePassed = passed == results.Count;
            Messages.Message(
                "Transformation identity suite " + (suitePassed ? "PASS" : "FAIL")
                + ": " + passed + "/" + results.Count
                + (XMTSettings.LogBiohorror ? ". See Biohorror log entries for details." : ". Enable Biohorror debug logging for details."),
                suitePassed ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
                false);
        }

        internal static void PrepareSaveReloadCheck()
        {
            Map map = Find.CurrentMap;
            PawnKindDef hybridKind = HybridKind;
            if (map == null || hybridKind == null)
            {
                Messages.Message("Save/reload preparation requires an active map and XMT_HybridHorrorBlank.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            int currentRun = ++runNumber;
            Pawn source = null;
            try
            {
                source = GenerateHuman();
                GenSpawn.Spawn(source, HarnessCell(map), map);
                int sourceId = source.thingIDNumber;
                source.records.AddTo(RecordDefOf.KillsHumanlikes, 4f);

                if (!XMTUtility.TransformPawnIntoPawn(source, hybridKind, out Pawn successor) || successor == null)
                {
                    throw new InvalidOperationException("Transformation returned false.");
                }

                successor.questTags ??= new List<string>();
                successor.questTags.Add(SaveMarkerPrefix + sourceId);
                HarnessLog(currentRun, "PREPARED save/reload source=" + sourceId
                    + " successor=" + successor.thingIDNumber
                    + " record=" + successor.records.GetValue(RecordDefOf.KillsHumanlikes));
                Find.Selector.Select(successor, playSound: false, forceDesignatorDeselect: false);
                Messages.Message("Save/reload identity check prepared. Save, reload, then run Verify/cleanup save-reload identity check.",
                    successor, MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception exception)
            {
                HarnessLog(currentRun, "FAIL save/reload preparation :: " + exception);
                if (source != null && !source.Discarded)
                {
                    CleanupPawn(source);
                }
                Messages.Message("Could not prepare the save/reload identity check. Enable Biohorror debug logging for details.",
                    MessageTypeDefOf.RejectInput, false);
            }
        }

        internal static void VerifyAndCleanupSaveReloadCheck()
        {
            int currentRun = ++runNumber;
            List<Pawn> markedPawns = AllKnownPawns()
                .Where(pawn => pawn?.questTags?.Any(tag => tag.StartsWith(SaveMarkerPrefix)) == true)
                .ToList();
            if (markedPawns.Count == 0)
            {
                Messages.Message("No prepared transformation save/reload pawn was found.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            int failures = 0;
            foreach (Pawn successor in markedPawns)
            {
                string marker = successor.questTags.First(tag => tag.StartsWith(SaveMarkerPrefix));
                bool parsed = int.TryParse(marker.Substring(SaveMarkerPrefix.Length), out int sourceId);
                bool sourceAbsent = parsed && !AllKnownPawns().Any(pawn => pawn != successor && pawn.thingIDNumber == sourceId);
                bool recordPreserved = successor.records?.GetValue(RecordDefOf.KillsHumanlikes) == 4f;
                bool passed = parsed && sourceAbsent && recordPreserved;
                if (!passed)
                {
                    failures++;
                }

                HarnessLog(currentRun, (passed ? "PASS" : "FAIL")
                    + " save/reload successor=" + successor.thingIDNumber
                    + " source=" + (parsed ? sourceId.ToString() : "invalid marker")
                    + " sourceAbsent=" + sourceAbsent
                    + " recordPreserved=" + recordPreserved);

                successor.questTags.Remove(marker);
                CleanupPawn(successor);
            }

            Messages.Message(
                failures == 0
                    ? "Save/reload transformation identity check PASS; generated successor cleaned up."
                    : "Save/reload transformation identity check FAIL for " + failures + " pawn(s).",
                failures == 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
                false);
        }

        private static CaseResult RunCase(
            Map map,
            PawnKindDef hybridKind,
            SourceLocation location,
            bool comprehensive,
            int currentRun)
        {
            CaseResult result = new CaseResult { label = location.ToString() };
            Gender expectedGender = (int)location % 2 == 0 ? Gender.Male : Gender.Female;
            Pawn source = GenerateHuman();
            source.gender = expectedGender;
            Pawn carrier = null;
            Caravan caravan = null;
            ComprehensiveFixture fixture = null;
            Pawn successor = null;
            int sourceId = source.thingIDNumber;
            bool sourceWasAlive = true;
            HashSet<Battle> battlesBefore = Find.BattleLog.Battles.ToHashSet();
            Dictionary<Battle, HashSet<LogEntry>> battleEntriesBefore = Find.BattleLog.Battles
                .ToDictionary(battle => battle, battle => battle.Entries.ToHashSet());

            try
            {
                switch (location)
                {
                    case SourceLocation.Map:
                        GenSpawn.Spawn(source, HarnessCell(map), map);
                        break;
                    case SourceLocation.Carried:
                        carrier = GenerateHuman();
                        GenSpawn.Spawn(carrier, HarnessCell(map), map);
                        GenSpawn.Spawn(source, HarnessCell(map), map);
                        source.DeSpawn(DestroyMode.Vanish);
                        if (!carrier.carryTracker.TryStartCarry(source))
                        {
                            throw new InvalidOperationException("Carrier refused the generated source pawn.");
                        }
                        break;
                    case SourceLocation.World:
                        Find.WorldPawns.PassToWorld(source, PawnDiscardDecideMode.KeepForever);
                        break;
                    case SourceLocation.Caravan:
                        caravan = CaravanMaker.MakeCaravan(new[] { source }, Faction.OfPlayer, map.Tile, true);
                        if (caravan == null || source.GetCaravan() != caravan)
                        {
                            throw new InvalidOperationException("Could not create the harness caravan.");
                        }
                        break;
                    case SourceLocation.Dead:
                        GenSpawn.Spawn(source, HarnessCell(map), map);
                        source.ideo = new Pawn_IdeoTracker(source);
                        source.SetFaction(null);
                        source.forceNoDeathNotification = true;
                        source.Kill(null, null);
                        sourceWasAlive = false;
                        if (!source.Dead)
                        {
                            throw new InvalidOperationException("Could not kill the already-dead test source.");
                        }
                        break;
                }

                if (comprehensive)
                {
                    fixture = SeedComprehensiveState(source, map, currentRun);
                }
                else
                {
                    source.records.AddTo(RecordDefOf.KillsHumanlikes, 2f);
                }

                float expectedRecord = source.records.GetValue(RecordDefOf.KillsHumanlikes);
                bool transformed = XMTUtility.TransformPawnIntoPawn(source, hybridKind, out successor);
                PawnTransformationReport report = PawnTransformationIdentityUtility.LastReport;

                Check(result, transformed && successor != null, "transformed");
                Check(result, report != null && report.sourceId == sourceId, "report matched source");
                Check(result, report?.sourceWasStrippedBeforeKill == true, "source stripped before kill");
                Check(result, !sourceWasAlive || report?.killCompleted == true, "kill path completed");
                Check(result, report?.sourceDiscarded == true, "source discarded");
                Check(result, source.Discarded && !Find.WorldPawns.Contains(source), "source absent from world pawns");
                Check(result, successor?.records?.GetValue(RecordDefOf.KillsHumanlikes) == expectedRecord, "records transferred");
                Check(result, successor?.Ideo == fixture?.ideology || !comprehensive, "ideology transferred");
                Check(result, successor?.gender == expectedGender, "gender preserved");

                if (location == SourceLocation.Carried)
                {
                    Check(result, carrier.carryTracker.CarriedThing == null, "carrier released predecessor");
                }
                if (location == SourceLocation.World)
                {
                    Check(result, Find.WorldPawns.Contains(successor), "successor retained as world pawn");
                    Check(result, Find.WorldPawns.ForcefullyKeptPawns.Contains(successor), "forced retention transferred");
                }
                if (location == SourceLocation.Caravan)
                {
                    Check(result, successor.GetCaravan() == caravan, "caravan membership transferred");
                }
                if (location == SourceLocation.Dead)
                {
                    Check(result, report?.sourceWasAlreadyDead == true, "dead source reported");
                }
                if (comprehensive)
                {
                    if (successor != null)
                    {
                        VerifyComprehensiveState(result, source, successor, fixture);
                    }
                    else
                    {
                        Check(result, false, "comprehensive successor state available");
                    }
                }

                result.passed = result.checks.All(check => check.StartsWith("PASS "));
                return result;
            }
            finally
            {
                CleanupFixture(fixture, successor);
                if (caravan != null && successor?.GetCaravan() == caravan)
                {
                    caravan.RemovePawn(successor);
                }
                RemoveNewBattleLogState(battlesBefore, battleEntriesBefore);
                CleanupPawn(successor);
                CleanupPawn(source);
                CleanupPawn(carrier);
                if (caravan != null && !caravan.Destroyed)
                {
                    Find.WorldObjects.Remove(caravan);
                }
            }
        }

        private static CaseResult RunGenderConstraintChecks(PawnKindDef hybridKind)
        {
            CaseResult result = new CaseResult { label = "gender constraints" };
            Pawn source = GenerateHuman();
            try
            {
                source.gender = Gender.Male;
                Check(result, XMTUtility.ResolveTransformationGender(source, hybridKind) == Gender.Male,
                    "male preserved for dual-gender hybrid");

                source.gender = Gender.Female;
                Check(result, XMTUtility.ResolveTransformationGender(source, hybridKind) == Gender.Female,
                    "female preserved for dual-gender hybrid");

                PawnKindDef femaleOnlyKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("XMT_StarbeastKind");
                source.gender = Gender.Male;
                Check(result, femaleOnlyKind == null
                    || XMTUtility.ResolveTransformationGender(source, femaleOnlyKind) == Gender.Female,
                    "invalid male falls back for female-only alien race");

                PawnKindDef genderlessKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("XMT_Exterminator");
                Check(result, genderlessKind == null
                    || XMTUtility.ResolveTransformationGender(source, genderlessKind) == Gender.None,
                    "genderless destination uses no gender");

                result.passed = result.checks.All(check => check.StartsWith("PASS "));
                return result;
            }
            finally
            {
                CleanupPawn(source);
            }
        }

        private static ComprehensiveFixture SeedComprehensiveState(Pawn source, Map map, int currentRun)
        {
            ComprehensiveFixture fixture = new ComprehensiveFixture
            {
                ideology = source.Ideo,
                associate = GenerateHuman()
            };
            GenSpawn.Spawn(fixture.associate, HarnessCell(map), map);

            source.records.AddTo(RecordDefOf.KillsHumanlikes, 7f);
            source.records.AddTo(RecordDefOf.DamageDealt, 125f);
            fixture.killRecord = source.records.GetValue(RecordDefOf.KillsHumanlikes);

            source.relations.AddDirectRelation(PawnRelationDefOf.Lover, fixture.associate);
            if (!fixture.associate.relations.DirectRelationExists(PawnRelationDefOf.Lover, source))
            {
                fixture.associate.relations.AddDirectRelation(PawnRelationDefOf.Lover, source);
            }
            ThoughtDef socialMemoryDef = DefDatabase<ThoughtDef>.AllDefsListForReading
                .FirstOrDefault(def => typeof(Thought_MemorySocial).IsAssignableFrom(def.thoughtClass));
            if (socialMemoryDef != null && fixture.associate.needs?.mood?.thoughts?.memories != null)
            {
                Thought_Memory memory = ThoughtMaker.MakeThought(socialMemoryDef) as Thought_Memory;
                if (memory != null)
                {
                    memory.otherPawn = source;
                    fixture.associate.needs.mood.thoughts.memories.Memories.Add(memory);
                }
            }

            fixture.logEntry = new PlayLogEntry_Interaction(
                InteractionDefOf.Chitchat,
                source,
                fixture.associate,
                new List<RulePackDef>());
            Find.PlayLog.Add(fixture.logEntry);

            List<Tale> talesBefore = Find.TaleManager.AllTalesListForReading.ToList();
            TaleRecorder.RecordTale(TaleDefOf.TamedAnimal, source, fixture.associate);
            fixture.tale = Find.TaleManager.AllTalesListForReading.FirstOrDefault(tale => !talesBefore.Contains(tale));

            fixture.quest = new Quest
            {
                id = -source.thingIDNumber,
                name = "XMT transformation harness",
                description = "Temporary pawn identity reference.",
                hidden = true,
                initiallyAccepted = true
            };
            fixture.questPart = new QuestPart_IsDead { pawn = source };
            fixture.quest.AddPart(fixture.questPart);
            Find.QuestManager.Add(fixture.quest);

            fixture.leaderFaction = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(faction => faction != null && faction != Faction.OfPlayer && !faction.def.hidden);
            if (fixture.leaderFaction != null)
            {
                fixture.formerFactionLeader = fixture.leaderFaction.leader;
                fixture.leaderFaction.leader = source;
            }

            if (ModsConfig.RoyaltyActive)
            {
                fixture.royalFaction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(faction => faction?.def?.HasRoyalTitles == true);
                fixture.royalTitle = fixture.royalFaction?.def?.RoyalTitlesAwardableInSeniorityOrderForReading?.FirstOrDefault();
                if (fixture.royalFaction != null && fixture.royalTitle != null && source.royalty != null)
                {
                    source.royalty.SetTitle(fixture.royalFaction, fixture.royalTitle, false, false, false);
                    source.royalty.SetFavor(fixture.royalFaction, fixture.royalTitle.favorCost + 3, false);
                }
            }

            fixture.role = source.Ideo?.RolesListForReading
                .FirstOrDefault(role => role.ChosenPawnSingle() == null && role.RequirementsMet(source));
            fixture.role?.Assign(source, false);

            ThingDef biocodableWeaponDef = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(def =>
                def.IsWeapon
                && def.comps?.Any(comp => typeof(CompBiocodable).IsAssignableFrom(comp.compClass)) == true);
            if (biocodableWeaponDef != null)
            {
                fixture.selfBiocodedWeapon = ThingMaker.MakeThing(biocodableWeaponDef) as ThingWithComps;
                if (fixture.selfBiocodedWeapon != null
                    && EquipmentUtility.CanEquip(fixture.selfBiocodedWeapon, source, out _, true))
                {
                    fixture.selfBiocodedWeapon.TryGetComp<CompBiocodable>()?.CodeFor(source);
                    source.equipment.AddEquipment(fixture.selfBiocodedWeapon);
                }
                else
                {
                    fixture.selfBiocodedWeapon?.Destroy(DestroyMode.Vanish);
                    fixture.selfBiocodedWeapon = null;
                }

                fixture.foreignBiocodedWeapon = ThingMaker.MakeThing(biocodableWeaponDef) as ThingWithComps;
                fixture.foreignBiocodedWeapon?.TryGetComp<CompBiocodable>()?.CodeFor(fixture.associate);
                if (fixture.foreignBiocodedWeapon != null)
                {
                    source.inventory.innerContainer.TryAdd(fixture.foreignBiocodedWeapon, false);
                }
            }

            ThingDef shirtDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_BasicShirt");
            if (shirtDef != null)
            {
                Apparel shirt = ThingMaker.MakeThing(shirtDef, GenStuff.DefaultStuffFor(shirtDef)) as Apparel;
                if (shirt != null && shirt.PawnCanWear(source, false))
                {
                    source.apparel.Wear(shirt, false, true);
                }
            }

            List<BodyPartRecord> clavicles = source.def.race.body.AllParts
                .Where(part => part.def.defName == "Clavicle")
                .ToList();
            if (clavicles.Count > 0)
            {
                BodyPartRecord missingPart = clavicles[clavicles.Count - 1];
                fixture.missingPartDef = missingPart.def;
                fixture.missingPartLabel = missingPart.Label;
                fixture.missingPartOccurrence = clavicles.Count - 1;
                source.health.AddHediff(HediffDefOf.MissingBodyPart, missingPart);
            }

            BodyPartRecord brain = source.health.hediffSet.GetBrain();
            if (ModsConfig.BiotechActive && brain != null)
            {
                HediffDef mechlinkImplant = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
                if (mechlinkImplant != null)
                {
                    source.health.AddHediff(mechlinkImplant, brain);
                    PawnComponentsUtility.AddAndRemoveDynamicComponents(source, false);
                    fixture.mechanitorTracker = source.mechanitor;

                    PawnKindDef mechKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Lifter")
                        ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Militor");
                    if (fixture.mechanitorTracker != null && mechKind != null)
                    {
                        fixture.controlledMech = PawnGenerator.GeneratePawn(mechKind, Faction.OfPlayer);
                        GenSpawn.Spawn(fixture.controlledMech, HarnessCell(map), map);
                        source.relations.AddDirectRelation(PawnRelationDefOf.Overseer, fixture.controlledMech);
                        fixture.mechanitorTracker.AssignPawnControlGroup(fixture.controlledMech, MechWorkModeDefOf.Work);
                        fixture.mechanitorControlGroupCount = fixture.mechanitorTracker.controlGroups.Count;
                    }
                }
            }

            HediffDef removableImplant = DefDatabase<HediffDef>.GetNamedSilentFail("CircadianAssistant");
            if (removableImplant?.spawnThingOnRemoved != null && brain != null)
            {
                fixture.removedImplantDef = removableImplant.spawnThingOnRemoved;
                fixture.existingRemovedImplants = map.listerThings
                    .ThingsOfDef(fixture.removedImplantDef)
                    .ToHashSet();
                source.health.AddHediff(removableImplant, brain);
            }

            HarnessLog(currentRun,
                "SEEDED source=" + source.thingIDNumber
                + " relation=" + (fixture.associate != null)
                + " log=" + (fixture.logEntry != null)
                + " tale=" + (fixture.tale != null)
                + " quest=" + (fixture.questPart != null)
                + " leader=" + (fixture.leaderFaction != null)
                + " role=" + (fixture.role != null)
                + " royalty=" + (fixture.royalTitle != null)
                + " biocodedWeapon=" + (fixture.selfBiocodedWeapon != null)
                + " missingPart=" + (fixture.missingPartDef != null)
                + " removableImplant=" + (fixture.removedImplantDef != null)
                + " mechanitor=" + (fixture.mechanitorTracker != null)
                + " controlledMech=" + (fixture.controlledMech != null));
            return fixture;
        }

        private static void VerifyComprehensiveState(
            CaseResult result,
            Pawn source,
            Pawn successor,
            ComprehensiveFixture fixture)
        {
            Check(result, successor.relations.DirectRelationExists(PawnRelationDefOf.Lover, fixture.associate),
                "social relation transferred");
            Check(result, fixture.associate.relations.DirectRelationExists(PawnRelationDefOf.Lover, successor),
                "social backlink transferred");
            Check(result, fixture.logEntry?.Concerns(successor) == true && fixture.logEntry.Concerns(source) == false,
                "play log rebound");
            Check(result, fixture.tale == null || fixture.tale.Concerns(successor) && !fixture.tale.Concerns(source),
                "tale rebound");
            Check(result, fixture.questPart?.pawn == successor, "quest reference rebound");
            Check(result, fixture.leaderFaction == null || fixture.leaderFaction.leader == successor,
                "faction leader rebound");
            Check(result, fixture.role == null || fixture.role.IsAssigned(successor) && !fixture.role.IsAssigned(source),
                "ideology role rebound");
            Check(result, fixture.royalTitle == null
                || successor.royalty.AllTitlesForReading.Any(title => title.def == fixture.royalTitle),
                "royalty transferred");
            Check(result, fixture.selfBiocodedWeapon == null
                || fixture.selfBiocodedWeapon.TryGetComp<CompBiocodable>()?.CodedPawn == successor,
                "carried self-biocode rebound");
            Check(result, fixture.foreignBiocodedWeapon == null
                || fixture.foreignBiocodedWeapon.TryGetComp<CompBiocodable>()?.CodedPawn == fixture.associate,
                "foreign biocode unchanged");
            PawnTransformationReport report = PawnTransformationIdentityUtility.LastReport;
            Check(result, report != null && report.apparelTransferred + report.apparelDropped > 0,
                "apparel transferred or dropped");
            Check(result, fixture.selfBiocodedWeapon == null
                || report != null && report.equipmentTransferred + report.equipmentDropped > 0,
                "equipment transferred or dropped");
            Check(result, successor.records.GetValue(RecordDefOf.KillsHumanlikes) == fixture.killRecord,
                "kill history preserved");

            if (fixture.missingPartDef != null)
            {
                List<BodyPartRecord> destinationParts = successor.def.race.body.AllParts
                    .Where(part => part.def == fixture.missingPartDef && part.Label == fixture.missingPartLabel)
                    .ToList();
                BodyPartRecord correspondingPart = fixture.missingPartOccurrence >= 0
                    && fixture.missingPartOccurrence < destinationParts.Count
                        ? destinationParts[fixture.missingPartOccurrence]
                        : null;
                Check(result, correspondingPart == null || successor.health.hediffSet.PartIsMissing(correspondingPart),
                    "missing anatomy transferred when compatible");
            }

            if (fixture.removedImplantDef != null)
            {
                fixture.removedImplant = successor.MapHeld?.listerThings
                    .ThingsOfDef(fixture.removedImplantDef)
                    .FirstOrDefault(thing => fixture.existingRemovedImplants?.Contains(thing) != true);
                bool implantRetained = successor.health.hediffSet
                    .GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamedSilentFail("CircadianAssistant")) != null;
                Check(result, implantRetained || fixture.removedImplant != null,
                    "removable implant transferred or dropped after spawn");
            }

            if (fixture.mechanitorTracker != null)
            {
                HediffDef mechlinkImplant = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
                Check(result, successor.mechanitor == fixture.mechanitorTracker,
                    "mechanitor tracker transferred");
                Check(result, mechlinkImplant != null
                    && successor.health.hediffSet.GetFirstHediffOfDef(mechlinkImplant) != null,
                    "mechlink transferred to corresponding brain");
                Check(result, report?.sourceMechlinksStripped > 0,
                    "predecessor mechlink stripped before death");
                Check(result, successor.mechanitor.controlGroups.Count == fixture.mechanitorControlGroupCount,
                    "mechanitor control groups preserved");

                if (fixture.controlledMech != null)
                {
                    Check(result, MechanitorUtility.GetOverseer(fixture.controlledMech) == successor,
                        "controlled mech overseer rebound");
                    Check(result, successor.mechanitor.GetControlGroup(fixture.controlledMech) != null,
                        "controlled mech group assignment preserved");
                }
            }
        }

        private static void CleanupFixture(ComprehensiveFixture fixture, Pawn successor)
        {
            if (fixture == null)
            {
                return;
            }

            if (fixture.leaderFaction != null && fixture.leaderFaction.leader == successor)
            {
                fixture.leaderFaction.leader = fixture.formerFactionLeader;
            }
            if (fixture.role?.IsAssigned(successor) == true)
            {
                fixture.role.Unassign(successor, false);
            }
            if (fixture.quest != null && Find.QuestManager.QuestsListForReading.Contains(fixture.quest))
            {
                Find.QuestManager.Remove(fixture.quest);
            }
            if (fixture.logEntry != null)
            {
                Find.PlayLog.AllEntries.Remove(fixture.logEntry);
            }
            if (fixture.tale != null && Find.TaleManager.AllTalesListForReading.Contains(fixture.tale))
            {
                Find.TaleManager.AllTalesListForReading.Remove(fixture.tale);
            }
            if (successor?.relations != null)
            {
                successor.relations.ClearAllRelations();
            }
            fixture.associate?.relations?.ClearAllRelations();
            CleanupThing(fixture.selfBiocodedWeapon);
            CleanupThing(fixture.foreignBiocodedWeapon);
            CleanupThing(fixture.removedImplant);
            fixture.controlledMech?.relations?.ClearAllRelations();
            CleanupPawn(fixture.controlledMech);
            CleanupPawn(fixture.associate);
        }

        private static void RunSafely(
            List<CaseResult> results,
            int currentRun,
            string label,
            Func<CaseResult> action)
        {
            try
            {
                CaseResult result = action();
                result.label = label;
                results.Add(result);
            }
            catch (Exception exception)
            {
                CaseResult result = new CaseResult { label = label, passed = false };
                result.checks.Add("FAIL exception: " + exception.Message);
                results.Add(result);
                HarnessLog(currentRun, "FAIL " + label + " :: " + exception);
            }
        }

        private static void Check(CaseResult result, bool condition, string label)
        {
            result.checks.Add((condition ? "PASS " : "FAIL ") + label);
        }

        private static Pawn GenerateHuman()
        {
            return PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        }

        private static IntVec3 HarnessCell(Map map)
        {
            return CellFinder.RandomClosewalkCellNear(map.Center, map, 12);
        }

        private static PawnKindDef HybridKind =>
            DefDatabase<PawnKindDef>.GetNamedSilentFail("XMT_HybridHorrorBlank");

        private static List<Pawn> AllKnownPawns()
        {
            return PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList();
        }

        private static void CleanupPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Discarded)
            {
                return;
            }

            RemoveBattleLogReferences(pawn);
            if (pawn.GetCaravan() is Caravan caravan)
            {
                caravan.RemovePawn(pawn);
            }
            if (pawn.Corpse != null && !pawn.Corpse.Destroyed)
            {
                pawn.Corpse.Destroy(DestroyMode.Vanish);
            }
            if (!pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
            if (Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
        }

        private static void RemoveBattleLogReferences(Pawn pawn)
        {
            foreach (Battle battle in Find.BattleLog.Battles)
            {
                battle.Entries.RemoveAll(entry => entry.Concerns(pawn));
                if (BattleConcernsField?.GetValue(battle) is HashSet<Pawn> concerns)
                {
                    concerns.Remove(pawn);
                }
            }
        }

        private static void RemoveNewBattleLogState(
            HashSet<Battle> battlesBefore,
            Dictionary<Battle, HashSet<LogEntry>> battleEntriesBefore)
        {
            foreach (Battle battle in Find.BattleLog.Battles.ToList())
            {
                if (!battlesBefore.Contains(battle))
                {
                    Find.BattleLog.Battles.Remove(battle);
                    continue;
                }

                if (battleEntriesBefore.TryGetValue(battle, out HashSet<LogEntry> oldEntries))
                {
                    battle.Entries.RemoveAll(entry => !oldEntries.Contains(entry));
                }
            }
        }

        private static void CleanupThing(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return;
            }

            thing.holdingOwner?.Remove(thing);
            if (!thing.Destroyed)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static void HarnessLog(int currentRun, string message)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(LogPrefix + "[run=" + currentRun + "] " + message);
            }
        }
    }
}
