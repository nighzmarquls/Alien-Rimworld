using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Xenomorphtype
{
    internal sealed class PawnTransformationReport
    {
        public int sourceId;
        public int successorId;
        public bool sourceWasAlreadyDead;
        public bool sourceWasStrippedBeforeKill;
        public bool killCompleted;
        public bool sourceDiscarded;
        public int referencesReplaced;
        public int apparelTransferred;
        public int apparelDropped;
        public int equipmentTransferred;
        public int equipmentDropped;
        public int biocodesRebound;
        public int sourceMechlinksStripped;
        public readonly List<string> warnings = new List<string>();
    }

    internal static class PawnTransformationIdentityUtility
    {
        private const string LogPrefix = "[XMT][Biohorror][PawnTransformation] ";

        private static readonly FieldInfo RelationsPawnField = AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn");
        private static readonly FieldInfo IdeoPawnField = AccessTools.Field(typeof(Pawn_IdeoTracker), "pawn");
        private static readonly FieldInfo MechanitorPawnField = AccessTools.Field(typeof(Pawn_MechanitorTracker), "pawn");
        private static readonly FieldInfo ConnectionsPawnField = AccessTools.Field(typeof(Pawn_ConnectionsTracker), "pawn");
        private static readonly FieldInfo PregnancyApproachesField = AccessTools.Field(typeof(Pawn_RelationsTracker), "pregnancyApproaches");
        private static readonly FieldInfo BiocodedPawnField = AccessTools.Field(typeof(CompBiocodable), "codedPawn");
        private static readonly FieldInfo BiocodedPawnLabelField = AccessTools.Field(typeof(CompBiocodable), "codedPawnLabel");
        private static readonly FieldInfo QuestAccepterPawnField = AccessTools.Field(typeof(Quest), "accepterPawn");
        private static readonly FieldInfo ThoughtCachedOtherPawnField = AccessTools.Field(typeof(Thought_Memory), "cachedLabelCapForOtherPawn");
        private static readonly FieldInfo BattleConcernsField = AccessTools.Field(typeof(Battle), "concerns");
        private static readonly MethodInfo LogEntryResetCacheMethod = AccessTools.Method(typeof(LogEntry), "ResetCache");
        private static readonly MethodInfo RelationsChangedMethod = AccessTools.Method(typeof(Pawn_RelationsTracker), "GainedOrLostDirectRelation");

        internal static PawnTransformationReport LastReport { get; private set; }

        private enum GearLocation
        {
            Apparel,
            Equipment,
            Inventory,
            Carried
        }

        private sealed class GearRecord
        {
            public Thing thing;
            public GearLocation location;
            public bool locked;
        }

        private sealed class TransferContext
        {
            public readonly Pawn source;
            public readonly Pawn successor;
            public readonly Faction sourceFaction;
            public readonly bool sourceWasForceKept;
            public readonly PawnTransformationReport report;

            public Pawn_RelationsTracker sourceRelations;
            public Pawn_RecordsTracker sourceRecords;
            public Pawn_RoyaltyTracker sourceRoyalty;
            public Pawn_IdeoTracker sourceIdeo;
            public Pawn_MechanitorTracker sourceMechanitor;
            public Pawn_ConnectionsTracker sourceConnections;
            public Thing sourceBondedWeapon;
            public readonly List<Thought_Memory> sourceMemories = new List<Thought_Memory>();
            public readonly List<Precept_Role> sourceRoles = new List<Precept_Role>();
            public readonly List<Faction> leaderFactions = new List<Faction>();
            public readonly List<GearRecord> gear = new List<GearRecord>();
            public readonly List<Hediff> sourceMechlinks = new List<Hediff>();

            public Building_Bed ownedBed;
            public Building_Grave assignedGrave;
            public Building_Throne assignedThrone;
            public Building assignedMeditationSpot;
            public Building_Bed assignedDeathrestCasket;

            public bool relationsMoved;
            public bool recordsMoved;
            public bool royaltyMoved;
            public bool ideoMoved;
            public bool mechanitorMoved;
            public bool connectionsMoved;
            public bool rolesMoved;
            public bool globalReferencesMoved;
            public bool leadersMoved;
            public bool gearMoved;
            public bool ownershipMoved;

            public TransferContext(Pawn source, Pawn successor)
            {
                this.source = source;
                this.successor = successor;
                sourceFaction = source.Faction;
                sourceWasForceKept = Find.WorldPawns != null && Find.WorldPawns.ForcefullyKeptPawns.Contains(source);
                report = new PawnTransformationReport
                {
                    sourceId = source.thingIDNumber,
                    successorId = successor.thingIDNumber,
                    sourceWasAlreadyDead = source.Dead
                };
            }
        }

        internal static bool TryTransferAndRetire(Pawn source, Pawn successor, out string failure)
        {
            failure = null;
            LastReport = null;
            if (source == null || successor == null || source == successor)
            {
                failure = "Transformation identity handoff requires two distinct pawns.";
                return false;
            }

            TransferContext context = new TransferContext(source, successor);
            LastReport = context.report;

            try
            {
                CaptureState(context);
                PrepareSuccessorTrackers(context);
                TransferRelations(context);
                TransferRecords(context);
                TransferIdeology(context);
                TransferRoyalty(context);
                TransferConnections(context);
                ReplaceGlobalReferences(context, source, successor);
                TransferFactionLeadership(context);
                TransferOwnership(context);
                TransferGear(context);
                NormalizeWorldPawnRetention(context);
                TransferMechanitor(context);
                StripSourceFaction(context);
                ClearRecreatedSourceMechanitor(context);
                StripTransferredMechlinksFromSource(context);

                context.report.sourceWasStrippedBeforeKill = ValidateSourceIsStripped(context, out failure);
                if (!context.report.sourceWasStrippedBeforeKill)
                {
                    throw new InvalidOperationException(failure);
                }
            }
            catch (Exception exception)
            {
                failure = "Identity handoff failed before retirement: " + exception;
                Log.Error(LogPrefix + failure);
                RollBack(context);
                DiscardFailedSuccessor(successor);
                return false;
            }

            try
            {
                RetireSource(context);
                return true;
            }
            catch (Exception exception)
            {
                failure = "Identity handoff succeeded, but predecessor retirement failed: " + exception;
                Log.Error(LogPrefix + failure);
                return false;
            }
        }

        private static void CaptureState(TransferContext context)
        {
            Pawn source = context.source;
            context.sourceRelations = source.relations;
            context.sourceRecords = source.records;
            context.sourceRoyalty = source.royalty;
            context.sourceIdeo = source.ideo;
            context.sourceMechanitor = source.mechanitor;
            context.sourceConnections = source.connections;
            context.sourceBondedWeapon = source.equipment?.bondedWeapon;

            if (source.needs?.mood?.thoughts?.memories?.Memories != null)
            {
                context.sourceMemories.AddRange(source.needs.mood.thoughts.memories.Memories);
            }

            Ideo ideology = source.Ideo;
            if (ideology != null)
            {
                context.sourceRoles.AddRange(ideology.RolesListForReading.Where(role => role != null && role.IsAssigned(source)));
            }

            if (Find.FactionManager != null)
            {
                context.leaderFactions.AddRange(Find.FactionManager.AllFactionsListForReading.Where(faction => faction?.leader == source));
            }

            if (source.ownership != null)
            {
                context.ownedBed = source.ownership.OwnedBed;
                context.assignedGrave = source.ownership.AssignedGrave;
                context.assignedThrone = source.ownership.AssignedThrone;
                context.assignedMeditationSpot = source.ownership.AssignedMeditationSpot;
                context.assignedDeathrestCasket = source.ownership.AssignedDeathrestCasket;
            }

            if (source.apparel != null)
            {
                foreach (Apparel apparel in source.apparel.WornApparel.ToList())
                {
                    context.gear.Add(new GearRecord
                    {
                        thing = apparel,
                        location = GearLocation.Apparel,
                        locked = source.apparel.IsLocked(apparel)
                    });
                }
            }

            if (source.equipment != null)
            {
                foreach (ThingWithComps equipment in source.equipment.AllEquipmentListForReading.ToList())
                {
                    context.gear.Add(new GearRecord
                    {
                        thing = equipment,
                        location = GearLocation.Equipment
                    });
                }
            }

            if (source.inventory?.innerContainer != null)
            {
                foreach (Thing item in source.inventory.innerContainer.ToList())
                {
                    context.gear.Add(new GearRecord
                    {
                        thing = item,
                        location = GearLocation.Inventory
                    });
                }
            }

            Thing carriedThing = source.carryTracker?.CarriedThing;
            if (carriedThing != null)
            {
                context.gear.Add(new GearRecord
                {
                    thing = carriedThing,
                    location = GearLocation.Carried
                });
            }
        }

        private static void PrepareSuccessorTrackers(TransferContext context)
        {
            context.successor.relations?.ClearAllRelations();
            if (context.successor.needs?.mood?.thoughts?.memories?.Memories != null)
            {
                context.successor.needs.mood.thoughts.memories.Memories.Clear();
            }
        }

        private static void TransferRelations(TransferContext context)
        {
            if (context.sourceRelations == null)
            {
                return;
            }

            context.successor.relations = context.sourceRelations;
            RelationsPawnField.SetValue(context.successor.relations, context.successor);
            context.source.relations = new Pawn_RelationsTracker(context.source);
            context.relationsMoved = true;

            context.report.referencesReplaced += ReplaceRelationBacklinks(context.source, context.successor);
            NotifyRelationsChanged(context.source.relations);
            NotifyRelationsChanged(context.successor.relations);

            if (context.successor.needs?.mood?.thoughts?.memories?.Memories != null)
            {
                context.successor.needs.mood.thoughts.memories.Memories.AddRange(context.sourceMemories);
                context.source.needs?.mood?.thoughts?.memories?.Memories.Clear();
            }
        }

        private static void TransferRecords(TransferContext context)
        {
            if (context.sourceRecords == null)
            {
                return;
            }

            context.successor.records = context.sourceRecords;
            context.successor.records.pawn = context.successor;
            context.source.records = new Pawn_RecordsTracker(context.source);
            context.recordsMoved = true;
        }

        private static void TransferIdeology(TransferContext context)
        {
            if (context.sourceIdeo != null)
            {
                context.successor.ideo = context.sourceIdeo;
                IdeoPawnField.SetValue(context.successor.ideo, context.successor);
                context.source.ideo = new Pawn_IdeoTracker(context.source);
                context.ideoMoved = true;
            }

            foreach (Precept_Role role in context.sourceRoles)
            {
                role.Assign(context.successor, false);
                if (role.IsAssigned(context.source))
                {
                    role.Unassign(context.source, false);
                }
            }
            context.rolesMoved = context.sourceRoles.Count > 0;
        }

        private static void TransferRoyalty(TransferContext context)
        {
            if (context.sourceRoyalty == null)
            {
                return;
            }

            context.successor.royalty = context.sourceRoyalty;
            context.successor.royalty.pawn = context.successor;
            foreach (RoyalTitle title in context.successor.royalty.AllTitlesForReading)
            {
                if (title != null)
                {
                    title.pawn = context.successor;
                }
            }
            foreach (Ability ability in context.successor.royalty.AllAbilitiesForReading)
            {
                if (ability != null)
                {
                    ability.pawn = context.successor;
                }
            }

            context.source.royalty = new Pawn_RoyaltyTracker(context.source);
            context.royaltyMoved = true;
        }

        private static void TransferMechanitor(TransferContext context)
        {
            if (context.sourceMechanitor == null)
            {
                return;
            }

            context.successor.mechanitor = context.sourceMechanitor;
            MechanitorPawnField.SetValue(context.successor.mechanitor, context.successor);

            // Health and equipment notifications call
            // AddAndRemoveDynamicComponents and can recreate this tracker while the
            // predecessor still has a mechlink. This transfer deliberately runs after
            // those notifications but before faction stripping; with the source tracker
            // now null, SetFaction cannot propagate the null faction to controlled mechs.
            context.source.mechanitor = null;
            context.mechanitorMoved = true;
        }

        private static void ClearRecreatedSourceMechanitor(TransferContext context)
        {
            if (!context.mechanitorMoved || context.source.mechanitor == null)
            {
                return;
            }

            // SetFaction invokes dynamic-component maintenance while changing faction.
            // A pawn that still physically has a mechlink can briefly satisfy the old
            // mechanitor conditions and receive a new empty tracker. The identity-bearing
            // tracker is already on the successor, so remove this predecessor-only shell.
            context.source.mechanitor = null;
            if (XMTSettings.LogBiohorror)
            {
                Log.Message(LogPrefix + "cleared dynamically recreated predecessor mechanitor tracker.");
            }
        }

        private static void StripTransferredMechlinksFromSource(TransferContext context)
        {
            List<Hediff> sourceMechlinks = context.source.health?.hediffSet?.hediffs
                .Where(hediff => hediff is Hediff_Mechlink)
                .ToList();
            if (sourceMechlinks.NullOrEmpty())
            {
                return;
            }

            foreach (Hediff sourceMechlink in sourceMechlinks)
            {
                bool successorHasMechlink = context.successor.health?.hediffSet?.hediffs
                    .Any(hediff => hediff.def == sourceMechlink.def) == true;
                if (!successorHasMechlink)
                {
                    throw new InvalidOperationException(
                        "The successor did not receive source mechlink " + sourceMechlink.def.defName + ".");
                }

                context.source.health.RemoveHediff(sourceMechlink);
                context.sourceMechlinks.Add(sourceMechlink);
                context.report.sourceMechlinksStripped++;
            }

            // Removing the implant normally updates dynamic components. Keep this
            // explicit because other health patches may recreate an empty tracker.
            context.source.mechanitor = null;
        }

        private static void TransferConnections(TransferContext context)
        {
            if (context.sourceConnections == null)
            {
                return;
            }

            context.successor.connections = context.sourceConnections;
            ConnectionsPawnField.SetValue(context.successor.connections, context.successor);
            context.source.connections = new Pawn_ConnectionsTracker(context.source);
            context.connectionsMoved = true;

            foreach (Thing connectedThing in context.successor.connections.ConnectedThings.ToList())
            {
                if (connectedThing is ThingWithComps thingWithComps)
                {
                    foreach (ThingComp comp in thingWithComps.AllComps)
                    {
                        context.report.referencesReplaced += ReplaceDirectPawnFields(comp, context.source, context.successor);
                    }
                }
            }
        }

        private static void ReplaceGlobalReferences(TransferContext context, Pawn oldPawn, Pawn newPawn)
        {
            foreach (Pawn pawn in AllKnownPawns(oldPawn, newPawn))
            {
                List<Thought_Memory> memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null)
                {
                    continue;
                }

                foreach (Thought_Memory memory in memories)
                {
                    if (memory.otherPawn == oldPawn)
                    {
                        memory.otherPawn = newPawn;
                        ThoughtCachedOtherPawnField?.SetValue(memory, null);
                        context.report.referencesReplaced++;
                    }
                }
            }

            if (Find.PlayLog != null)
            {
                foreach (LogEntry entry in Find.PlayLog.AllEntries.ToList())
                {
                    context.report.referencesReplaced += ReplaceLogEntryReferences(entry, oldPawn, newPawn);
                }
            }

            if (Find.BattleLog != null)
            {
                foreach (Battle battle in Find.BattleLog.Battles.ToList())
                {
                    HashSet<Pawn> concerns = BattleConcernsField?.GetValue(battle) as HashSet<Pawn>;
                    if (concerns != null && concerns.Remove(oldPawn))
                    {
                        concerns.Add(newPawn);
                        context.report.referencesReplaced++;
                    }

                    foreach (LogEntry entry in battle.Entries.ToList())
                    {
                        context.report.referencesReplaced += ReplaceLogEntryReferences(entry, oldPawn, newPawn);
                    }
                }
            }

            if (Find.TaleManager != null)
            {
                foreach (Tale tale in Find.TaleManager.AllTalesListForReading.ToList())
                {
                    context.report.referencesReplaced += ReplaceTaleReferences(tale, oldPawn, newPawn);
                }
            }

            if (Find.QuestManager != null)
            {
                foreach (Quest quest in Find.QuestManager.QuestsListForReading.ToList())
                {
                    if (QuestAccepterPawnField?.GetValue(quest) == oldPawn)
                    {
                        QuestAccepterPawnField.SetValue(quest, newPawn);
                        context.report.referencesReplaced++;
                    }

                    context.report.referencesReplaced += ReplaceDirectPawnFields(quest, oldPawn, newPawn);
                    foreach (QuestPart part in quest.PartsListForReading)
                    {
                        part.ReplacePawnReferences(oldPawn, newPawn);
                        context.report.referencesReplaced += ReplaceDirectPawnFields(part, oldPawn, newPawn);
                    }
                }
            }

            context.globalReferencesMoved = true;
        }

        private static void TransferFactionLeadership(TransferContext context)
        {
            foreach (Faction faction in context.leaderFactions)
            {
                faction.leader = context.successor;
                context.report.referencesReplaced++;
            }
            context.leadersMoved = context.leaderFactions.Count > 0;
        }

        private static void TransferOwnership(TransferContext context)
        {
            if (context.source.ownership == null || context.successor.ownership == null)
            {
                return;
            }

            bool attempted = false;
            if (context.ownedBed != null)
            {
                attempted = true;
                if (!context.successor.ownership.ClaimBedIfNonMedical(context.ownedBed))
                {
                    Warn(context, "Could not transfer owned bed " + context.ownedBed + ".");
                }
            }
            if (context.assignedGrave != null)
            {
                attempted = true;
                if (!context.successor.ownership.ClaimGrave(context.assignedGrave))
                {
                    Warn(context, "Could not transfer assigned grave " + context.assignedGrave + ".");
                }
            }
            if (context.assignedThrone != null)
            {
                attempted = true;
                if (!context.successor.ownership.ClaimThrone(context.assignedThrone))
                {
                    Warn(context, "Could not transfer assigned throne " + context.assignedThrone + ".");
                }
            }
            if (context.assignedMeditationSpot != null)
            {
                attempted = true;
                if (!context.successor.ownership.ClaimMeditationSpot(context.assignedMeditationSpot))
                {
                    Warn(context, "Could not transfer meditation spot " + context.assignedMeditationSpot + ".");
                }
            }
            if (context.assignedDeathrestCasket != null)
            {
                attempted = true;
                if (!context.successor.ownership.ClaimDeathrestCasket(context.assignedDeathrestCasket))
                {
                    Warn(context, "Could not transfer deathrest casket " + context.assignedDeathrestCasket + ".");
                }
            }

            context.source.ownership.UnclaimAll();
            context.ownershipMoved = attempted;
        }

        private static void TransferGear(TransferContext context)
        {
            foreach (GearRecord record in context.gear)
            {
                if (record.thing != null && !record.thing.Destroyed)
                {
                    context.report.biocodesRebound += RebindBiocoding(record.thing, context.source, context.successor);
                }
            }

            foreach (GearRecord record in context.gear
                .Where(record => record.location == GearLocation.Apparel)
                .OrderByDescending(record => record.locked))
            {
                Apparel apparel = record.thing as Apparel;
                if (apparel == null || apparel.Destroyed)
                {
                    continue;
                }

                context.source.apparel.Remove(apparel);
                if (CanWear(context.successor, apparel))
                {
                    context.successor.apparel.Wear(apparel, false, record.locked);
                    context.report.apparelTransferred++;
                }
                else
                {
                    PlaceLooseThing(context, apparel);
                    context.report.apparelDropped++;
                }
            }

            foreach (GearRecord record in context.gear.Where(record => record.location == GearLocation.Equipment))
            {
                ThingWithComps equipment = record.thing as ThingWithComps;
                if (equipment == null || equipment.Destroyed)
                {
                    continue;
                }

                context.source.equipment.Remove(equipment);
                if (EquipmentUtility.CanEquip(equipment, context.successor, out _, true))
                {
                    context.successor.equipment.AddEquipment(equipment);
                    if (context.sourceBondedWeapon == equipment)
                    {
                        context.source.equipment.bondedWeapon = null;
                        context.successor.equipment.bondedWeapon = equipment;
                    }
                    context.report.equipmentTransferred++;
                }
                else
                {
                    PlaceLooseThing(context, equipment);
                    context.report.equipmentDropped++;
                }
            }

            foreach (GearRecord record in context.gear.Where(record => record.location == GearLocation.Inventory))
            {
                Thing item = record.thing;
                if (item == null || item.Destroyed)
                {
                    continue;
                }

                context.source.inventory.innerContainer.Remove(item);
                PlaceLooseThing(context, item);
            }

            foreach (GearRecord record in context.gear.Where(record => record.location == GearLocation.Carried))
            {
                Thing item = record.thing;
                if (item == null || item.Destroyed)
                {
                    continue;
                }

                context.source.carryTracker.innerContainer.Remove(item);
                PlaceLooseThing(context, item);
            }

            context.gearMoved = true;
        }

        private static bool CanWear(Pawn pawn, Apparel apparel)
        {
            if (pawn?.apparel == null || apparel?.def == null)
            {
                return false;
            }

            CompBiocodable biocodable = apparel.TryGetComp<CompBiocodable>();
            if (biocodable?.Biocoded == true && biocodable.CodedPawn != pawn)
            {
                return false;
            }

            return apparel.PawnCanWear(pawn, false)
                && ApparelUtility.HasPartsToWear(pawn, apparel.def)
                && pawn.apparel.CanWearWithoutDroppingAnything(apparel.def);
        }

        private static int RebindBiocoding(Thing thing, Pawn oldPawn, Pawn newPawn)
        {
            if (!(thing is ThingWithComps thingWithComps))
            {
                return 0;
            }

            CompBiocodable biocodable = thingWithComps.TryGetComp<CompBiocodable>();
            if (biocodable?.Biocoded != true || biocodable.CodedPawn != oldPawn)
            {
                return 0;
            }

            BiocodedPawnField.SetValue(biocodable, newPawn);
            BiocodedPawnLabelField.SetValue(biocodable, newPawn.Name?.ToStringFull ?? newPawn.LabelShort);
            return 1;
        }

        private static void PlaceLooseThing(TransferContext context, Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return;
            }

            Map map = context.successor.MapHeld;
            if (map != null)
            {
                if (!GenPlace.TryPlaceThing(thing, context.successor.PositionHeld, map, ThingPlaceMode.Near))
                {
                    throw new InvalidOperationException("Could not place transformed pawn item " + thing + ".");
                }
                return;
            }

            if (context.successor.inventory?.innerContainer != null && context.successor.inventory.innerContainer.TryAdd(thing, false))
            {
                return;
            }

            Caravan caravan = context.successor.GetCaravan();
            if (caravan != null)
            {
                caravan.AddPawnOrItem(thing, true);
                return;
            }

            throw new InvalidOperationException("No valid destination exists for transformed pawn item " + thing + ".");
        }

        private static void NormalizeWorldPawnRetention(TransferContext context)
        {
            if (Find.WorldPawns == null || !Find.WorldPawns.Contains(context.successor))
            {
                return;
            }

            if (context.sourceWasForceKept)
            {
                Find.WorldPawns.ForcefullyKeptPawns.Add(context.successor);
            }
            else
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(context.successor);
            }
        }

        private static void StripSourceFaction(TransferContext context)
        {
            if (context.successor.Faction != context.sourceFaction)
            {
                context.successor.SetFaction(context.sourceFaction);
            }

            if (context.source.Faction != null)
            {
                context.source.SetFaction(null);
            }
        }

        private static bool ValidateSourceIsStripped(TransferContext context, out string failure)
        {
            Pawn source = context.source;
            List<string> remnants = new List<string>();
            if (source.Faction != null)
            {
                remnants.Add("faction");
            }
            if (source.Ideo != null)
            {
                remnants.Add("ideology");
            }
            if (source.relations?.DirectRelations.Count > 0)
            {
                remnants.Add("relations");
            }
            if (source.royalty?.AllTitlesForReading.Count > 0)
            {
                remnants.Add("royalty");
            }
            if (source.mechanitor != null)
            {
                remnants.Add("mechanitor");
            }
            if (source.health?.hediffSet?.hediffs.Any(hediff => hediff is Hediff_Mechlink) == true)
            {
                remnants.Add("mechlink");
            }
            if (source.connections?.ConnectedThings.Count > 0)
            {
                remnants.Add("connections");
            }
            if (source.apparel?.WornApparelCount > 0)
            {
                remnants.Add("apparel");
            }
            if (source.equipment?.AllEquipmentListForReading.Count > 0)
            {
                remnants.Add("equipment");
            }
            if (source.inventory?.innerContainer.Count > 0)
            {
                remnants.Add("inventory");
            }
            if (source.carryTracker?.CarriedThing != null)
            {
                remnants.Add("carried item");
            }
            if (context.leaderFactions.Any(faction => faction?.leader == source))
            {
                remnants.Add("faction leadership");
            }
            if (context.sourceRoles.Any(role => role?.IsAssigned(source) == true))
            {
                remnants.Add("ideology role");
            }

            failure = remnants.Count == 0
                ? null
                : "The predecessor still owns transformation state: " + string.Join(", ", remnants) + ".";
            return remnants.Count == 0;
        }

        private static void RetireSource(TransferContext context)
        {
            Pawn source = context.source;
            Corpse corpse = source.Corpse;

            if (!source.Dead)
            {
                source.Kill(null, null);
                context.report.killCompleted = source.Dead;
                corpse = source.Corpse ?? corpse;
            }

            // A modded death hook may have written a new log, tale, or quest reference.
            // Run the global pass again after the real Kill call before the source is discarded.
            ReplaceGlobalReferences(context, source, context.successor);

            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (Find.WorldPawns != null && Find.WorldPawns.Contains(source))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(source);
            }
            else if (!source.Discarded)
            {
                source.Discard(false);
            }

            context.report.sourceDiscarded = source.Discarded
                && (Find.WorldPawns == null || !Find.WorldPawns.Contains(source));

            if (!context.report.sourceDiscarded)
            {
                throw new InvalidOperationException("The predecessor remained in world/save state after retirement.");
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message(LogPrefix
                    + "retired source=" + context.report.sourceId
                    + " successor=" + context.report.successorId
                    + " refs=" + context.report.referencesReplaced
                    + " apparelMoved=" + context.report.apparelTransferred
                    + " apparelDropped=" + context.report.apparelDropped
                    + " equipmentMoved=" + context.report.equipmentTransferred
                    + " equipmentDropped=" + context.report.equipmentDropped
                    + " biocodes=" + context.report.biocodesRebound
                    + " sourceMechlinksStripped=" + context.report.sourceMechlinksStripped);
            }
        }

        private static void RollBack(TransferContext context)
        {
            try
            {
                foreach (Hediff mechlink in context.sourceMechlinks)
                {
                    if (mechlink != null
                        && context.source.health?.hediffSet?.hediffs.Contains(mechlink) == false)
                    {
                        context.source.health.AddHediff(mechlink, mechlink.Part);
                    }
                }

                if (context.globalReferencesMoved)
                {
                    ReplaceGlobalReferences(context, context.successor, context.source);
                }

                if (context.leadersMoved)
                {
                    foreach (Faction faction in context.leaderFactions)
                    {
                        if (faction?.leader == context.successor)
                        {
                            faction.leader = context.source;
                        }
                    }
                }

                RestoreGear(context);

                if (context.connectionsMoved)
                {
                    context.source.connections = context.sourceConnections;
                    ConnectionsPawnField.SetValue(context.source.connections, context.source);
                    context.successor.connections = new Pawn_ConnectionsTracker(context.successor);
                }
                if (context.mechanitorMoved)
                {
                    context.source.mechanitor = context.sourceMechanitor;
                    MechanitorPawnField.SetValue(context.source.mechanitor, context.source);
                    context.successor.mechanitor = null;
                }
                if (context.royaltyMoved)
                {
                    context.source.royalty = context.sourceRoyalty;
                    context.source.royalty.pawn = context.source;
                    foreach (RoyalTitle title in context.source.royalty.AllTitlesForReading)
                    {
                        title.pawn = context.source;
                    }
                    foreach (Ability ability in context.source.royalty.AllAbilitiesForReading)
                    {
                        ability.pawn = context.source;
                    }
                    context.successor.royalty = new Pawn_RoyaltyTracker(context.successor);
                }
                if (context.ideoMoved)
                {
                    context.source.ideo = context.sourceIdeo;
                    IdeoPawnField.SetValue(context.source.ideo, context.source);
                    context.successor.ideo = new Pawn_IdeoTracker(context.successor);
                }
                if (context.source.Faction == null && context.sourceFaction != null)
                {
                    context.source.SetFaction(context.sourceFaction);
                }
                if (context.rolesMoved)
                {
                    foreach (Precept_Role role in context.sourceRoles)
                    {
                        role.Assign(context.source, false);
                        if (role.IsAssigned(context.successor))
                        {
                            role.Unassign(context.successor, false);
                        }
                    }
                }
                if (context.recordsMoved)
                {
                    context.source.records = context.sourceRecords;
                    context.source.records.pawn = context.source;
                    context.successor.records = new Pawn_RecordsTracker(context.successor);
                }
                if (context.relationsMoved)
                {
                    context.source.relations = context.sourceRelations;
                    RelationsPawnField.SetValue(context.source.relations, context.source);
                    context.successor.relations = new Pawn_RelationsTracker(context.successor);
                    ReplaceRelationBacklinks(context.successor, context.source);
                    NotifyRelationsChanged(context.source.relations);
                    NotifyRelationsChanged(context.successor.relations);

                    if (context.source.needs?.mood?.thoughts?.memories?.Memories != null)
                    {
                        context.source.needs.mood.thoughts.memories.Memories.Clear();
                        context.source.needs.mood.thoughts.memories.Memories.AddRange(context.sourceMemories);
                    }
                    context.successor.needs?.mood?.thoughts?.memories?.Memories.Clear();
                }

                RestoreOwnership(context);
            }
            catch (Exception rollbackException)
            {
                Log.Error(LogPrefix + "rollback failed: " + rollbackException);
            }
        }

        private static void RestoreGear(TransferContext context)
        {
            if (!context.gearMoved)
            {
                return;
            }

            foreach (GearRecord record in context.gear)
            {
                Thing thing = record.thing;
                if (thing == null || thing.Destroyed)
                {
                    continue;
                }

                DetachThing(thing);
                RebindBiocoding(thing, context.successor, context.source);
                switch (record.location)
                {
                    case GearLocation.Apparel:
                        context.source.apparel.Wear((Apparel)thing, false, record.locked);
                        break;
                    case GearLocation.Equipment:
                        context.source.equipment.AddEquipment((ThingWithComps)thing);
                        break;
                    case GearLocation.Inventory:
                        context.source.inventory.innerContainer.TryAdd(thing, false);
                        break;
                    case GearLocation.Carried:
                        context.source.carryTracker.innerContainer.TryAdd(thing, false);
                        break;
                }
            }

            context.source.equipment.bondedWeapon = context.sourceBondedWeapon;
        }

        private static void RestoreOwnership(TransferContext context)
        {
            if (!context.ownershipMoved || context.source.ownership == null)
            {
                return;
            }

            context.successor.ownership?.UnclaimAll();
            if (context.ownedBed != null)
            {
                context.source.ownership.ClaimBedIfNonMedical(context.ownedBed);
            }
            if (context.assignedGrave != null)
            {
                context.source.ownership.ClaimGrave(context.assignedGrave);
            }
            if (context.assignedThrone != null)
            {
                context.source.ownership.ClaimThrone(context.assignedThrone);
            }
            if (context.assignedMeditationSpot != null)
            {
                context.source.ownership.ClaimMeditationSpot(context.assignedMeditationSpot);
            }
            if (context.assignedDeathrestCasket != null)
            {
                context.source.ownership.ClaimDeathrestCasket(context.assignedDeathrestCasket);
            }
        }

        private static void DiscardFailedSuccessor(Pawn successor)
        {
            try
            {
                if (successor?.Corpse != null && !successor.Corpse.Destroyed)
                {
                    successor.Corpse.Destroy(DestroyMode.Vanish);
                }
                if (successor == null || successor.Discarded)
                {
                    return;
                }
                Caravan caravan = successor.GetCaravan();
                caravan?.RemovePawn(successor);
                if (Find.WorldPawns != null && Find.WorldPawns.Contains(successor))
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(successor);
                }
                else
                {
                    if (!successor.Destroyed)
                    {
                        successor.Destroy(DestroyMode.Vanish);
                    }
                    if (Find.WorldPawns != null && Find.WorldPawns.Contains(successor))
                    {
                        Find.WorldPawns.RemoveAndDiscardPawnViaGC(successor);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "could not discard failed successor: " + exception);
            }
        }

        private static int ReplaceLogEntryReferences(LogEntry entry, Pawn oldPawn, Pawn newPawn)
        {
            if (entry == null || !entry.Concerns(oldPawn))
            {
                return 0;
            }

            int replacements = ReplaceDirectPawnFields(entry, oldPawn, newPawn);
            LogEntryResetCacheMethod?.Invoke(entry, null);
            return replacements;
        }

        private static int ReplaceTaleReferences(Tale tale, Pawn oldPawn, Pawn newPawn)
        {
            int replacements = 0;
            foreach (FieldInfo field in InstanceFields(tale.GetType()))
            {
                object value = field.GetValue(tale);
                if (value is TaleData_Pawn pawnData && pawnData.pawn == oldPawn)
                {
                    pawnData.pawn = newPawn;
                    if (pawnData.royalTitles != null)
                    {
                        foreach (RoyalTitle title in pawnData.royalTitles)
                        {
                            if (title?.pawn == oldPawn)
                            {
                                title.pawn = newPawn;
                            }
                        }
                    }
                    replacements++;
                }
            }
            return replacements;
        }

        private static int ReplaceDirectPawnFields(object target, Pawn oldPawn, Pawn newPawn)
        {
            if (target == null)
            {
                return 0;
            }

            int replacements = 0;
            foreach (FieldInfo field in InstanceFields(target.GetType()))
            {
                object value;
                try
                {
                    value = field.GetValue(target);
                }
                catch
                {
                    continue;
                }

                if (value == oldPawn && !field.IsInitOnly && field.FieldType.IsInstanceOfType(newPawn))
                {
                    try
                    {
                        field.SetValue(target, newPawn);
                        replacements++;
                    }
                    catch
                    {
                    }
                    continue;
                }

                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == oldPawn)
                        {
                            try
                            {
                                list[i] = newPawn;
                                replacements++;
                            }
                            catch
                            {
                            }
                        }
                    }
                    continue;
                }

                if (value is IDictionary dictionary)
                {
                    try
                    {
                        if (dictionary.Contains(oldPawn))
                        {
                            object oldValue = dictionary[oldPawn];
                            dictionary.Remove(oldPawn);
                            dictionary[newPawn] = oldValue;
                            replacements++;
                        }

                        foreach (DictionaryEntry entry in dictionary.Cast<DictionaryEntry>().ToList())
                        {
                            if (entry.Value == oldPawn)
                            {
                                dictionary[entry.Key] = newPawn;
                                replacements++;
                            }
                        }
                    }
                    catch
                    {
                    }
                    continue;
                }

                if (value is HashSet<Pawn> pawnSet && pawnSet.Remove(oldPawn))
                {
                    pawnSet.Add(newPawn);
                    replacements++;
                }
            }
            return replacements;
        }

        private static IEnumerable<FieldInfo> InstanceFields(Type type)
        {
            while (type != null && type != typeof(object))
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsStatic)
                    {
                        yield return field;
                    }
                }
                type = type.BaseType;
            }
        }

        private static int ReplaceRelationBacklinks(Pawn oldPawn, Pawn newPawn)
        {
            int replacements = 0;
            foreach (Pawn pawn in AllKnownPawns(oldPawn, newPawn))
            {
                if (pawn?.relations == null || pawn == oldPawn)
                {
                    continue;
                }

                bool changed = false;
                foreach (DirectPawnRelation relation in pawn.relations.DirectRelations.Where(relation => relation?.otherPawn == oldPawn))
                {
                    relation.otherPawn = newPawn;
                    changed = true;
                    replacements++;
                }

                ReKeyPregnancyApproach(pawn.relations, oldPawn, newPawn);
                if (changed)
                {
                    NotifyRelationsChanged(pawn.relations);
                }
            }
            return replacements;
        }

        private static void NotifyRelationsChanged(Pawn_RelationsTracker tracker)
        {
            if (tracker != null)
            {
                RelationsChangedMethod?.Invoke(tracker, null);
            }
        }

        private static void ReKeyPregnancyApproach(Pawn_RelationsTracker tracker, Pawn oldPawn, Pawn newPawn)
        {
            if (!(PregnancyApproachesField?.GetValue(tracker) is Dictionary<Pawn, PregnancyApproach> approaches)
                || !approaches.TryGetValue(oldPawn, out PregnancyApproach approach))
            {
                return;
            }

            approaches.Remove(oldPawn);
            approaches[newPawn] = approach;
        }

        private static List<Pawn> AllKnownPawns(Pawn first, Pawn second)
        {
            List<Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList();
            if (first != null && !pawns.Contains(first))
            {
                pawns.Add(first);
            }
            if (second != null && !pawns.Contains(second))
            {
                pawns.Add(second);
            }
            return pawns;
        }

        private static void DetachThing(Thing thing)
        {
            if (thing.Spawned)
            {
                thing.DeSpawn(DestroyMode.Vanish);
            }

            if (thing is Apparel apparel && apparel.Wearer?.apparel != null)
            {
                apparel.Wearer.apparel.Remove(apparel);
            }
            else if (thing is ThingWithComps equipment && equipment.ParentHolder is Pawn_EquipmentTracker equipmentTracker)
            {
                equipmentTracker.Remove(equipment);
            }
            else
            {
                thing.holdingOwner?.Remove(thing);
            }
        }

        private static void Warn(TransferContext context, string warning)
        {
            context.report.warnings.Add(warning);
            if (XMTSettings.LogBiohorror)
            {
                Log.Warning(LogPrefix + warning);
            }
        }
    }
}
