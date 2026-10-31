using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class XMT_TrackedPawn : IExposable
    {
        public Pawn pawn;
        public Faction captor;
        public bool playerOrigin;
        public bool addedRetention;
        public int registeredTick;
        public List<string> reasons = new List<string>();
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref captor, "captor");
            Scribe_Values.Look(ref playerOrigin, "playerOrigin");
            Scribe_Values.Look(ref addedRetention, "addedRetention");
            Scribe_Values.Look(ref registeredTick, "registeredTick");
            Scribe_Collections.Look(ref reasons, "reasons", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) reasons ??= new List<string>();
        }
    }

    public sealed class XMT_CompanionLink : IExposable
    {
        public Pawn owner;
        public Pawn companion;
        public XMT_CompanionDef definition;
        public int priority;
        public int dueTick = -1;
        public int attempts;
        public bool recovering;
        public bool satisfied;
        public bool ended;
        public bool caravanRefused;
        public string operationId;
        public bool Active => !ended && owner != null && !owner.Destroyed && definition != null;
        public Thing Target => owner?.Dead == true ? owner.Corpse : owner;
        public void ExposeData()
        {
            Scribe_References.Look(ref owner, "owner");
            Scribe_References.Look(ref companion, "companion");
            Scribe_Defs.Look(ref definition, "definition");
            Scribe_Values.Look(ref priority, "priority");
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Values.Look(ref attempts, "attempts");
            Scribe_Values.Look(ref recovering, "recovering");
            Scribe_Values.Look(ref satisfied, "satisfied");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref caravanRefused, "caravanRefused");
            Scribe_Values.Look(ref operationId, "operationId");
        }
    }

    public sealed class XMT_WatcherSchedule : IExposable
    {
        public XMT_WatcherDef definition;
        public int nextTick;
        public void ExposeData() { Scribe_Defs.Look(ref definition, "definition"); Scribe_Values.Look(ref nextTick, "nextTick"); }
    }

    public sealed partial class GameComponent_XMTWorldState : GameComponent, IThingHolder, ISuspendableThingHolder
    {
        private List<XMT_TrackedPawn> pawns = new List<XMT_TrackedPawn>();
        private Dictionary<Pawn, XMT_TrackedPawn> pawnIndex;
        private List<XMT_CompanionLink> companions = new List<XMT_CompanionLink>();
        private List<XMT_WatcherSchedule> schedules = new List<XMT_WatcherSchedule>();
        private ThingOwner<Thing> offsiteThings;
        private int nextOperationId;
        public IReadOnlyList<XMT_TrackedPawn> Pawns => pawns ??= new List<XMT_TrackedPawn>();
        public IReadOnlyList<XMT_CompanionLink> Companions => companions ??= new List<XMT_CompanionLink>();
        public IThingHolder ParentHolder => null;
        public bool IsContentsSuspended => false;
        public GameComponent_XMTWorldState(Game game) { offsiteThings = new ThingOwner<Thing>(this, false); }
        public ThingOwner GetDirectlyHeldThings() => offsiteThings;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, offsiteThings);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawns, "XMT_trackedPawns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars) pawnIndex = null;
            Scribe_Collections.Look(ref companions, "XMT_companions", LookMode.Deep);
            Scribe_Collections.Look(ref schedules, "XMT_watcherSchedules", LookMode.Deep);
            Scribe_Deep.Look(ref offsiteThings, "XMT_offsiteThings", this);
            Scribe_Values.Look(ref nextOperationId, "XMT_nextOperationId");
            ExposeBroadcasts();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<XMT_TrackedPawn>(); companions ??= new List<XMT_CompanionLink>();
                schedules ??= new List<XMT_WatcherSchedule>(); offsiteThings ??= new ThingOwner<Thing>(this, false);
                schedules.RemoveAll(s => s?.definition == null);
                pawns.RemoveAll(p => p?.pawn == null);
                pawnIndex = null;
            }
        }

        private XMT_TrackedPawn Record(Pawn pawn)
        {
            if (pawn == null) return null;
            pawns ??= new List<XMT_TrackedPawn>();
            if (pawnIndex == null)
            {
                pawnIndex = new Dictionary<Pawn, XMT_TrackedPawn>(ReferenceEqualityComparer.Instance);
                foreach (XMT_TrackedPawn record in pawns)
                    if (record?.pawn != null) pawnIndex.TryAdd(record.pawn, record);
            }
            return pawnIndex.TryGetValue(pawn, out XMT_TrackedPawn found) ? found : null;
        }
        public XMT_TrackedPawn Track(Pawn pawn, string reason, Faction captor = null, bool adoptRetention = false)
        {
            if (pawn == null || pawn.Destroyed || reason.NullOrEmpty()) return null;
            XMT_TrackedPawn record = Record(pawn);
            if (record == null)
            {
                record = new XMT_TrackedPawn { pawn = pawn, registeredTick = Find.TickManager.TicksGame,
                    playerOrigin = pawn.Faction == Faction.OfPlayer };
                pawns.Add(record);
                pawnIndex.Add(pawn, record);
            }
            record.reasons ??= new List<string>();
            if (!record.reasons.Contains(reason)) record.reasons.Add(reason);
            if (captor != null) record.captor = captor;
            if (adoptRetention) record.addedRetention = true;
            Preserve(record);
            return record;
        }

        public void Release(Pawn pawn, string reason)
        {
            XMT_TrackedPawn record = Record(pawn);
            if (record == null) return;
            record.reasons ??= new List<string>();
            record.reasons.Remove(reason);
            if (reason == "captured") record.captor = null;
            if (record.reasons.Count != 0) return;
            if (record.addedRetention && Find.WorldPawns != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
            pawns.Remove(record);
            pawnIndex.Remove(pawn);
        }
        public bool Protects(Pawn pawn) => Record(pawn)?.reasons?.Count > 0;
        internal bool ReservedOutsideNemesis(Pawn pawn) => Record(pawn)?.reasons?.Any(r => r != null
            && (r.StartsWith("companion") || r == "captured" || r == "nemesis-host")) == true;
        public bool BlockWorldReuse(Pawn pawn)
        {
            XMT_TrackedPawn record = Record(pawn);
            return record != null && (record.captor != null || record.reasons?.Any(r => r != null
                && (r.StartsWith("companion:") || r == "captured" || r == "nemesis-host" || r == "nemesis-recovery")) == true);
        }
        public bool Claimed(Pawn pawn) => pawn != null && (companions.Any(c => c.operationId != null && (c.companion == pawn || c.owner == pawn))
            || Find.Maps.Any(m => m.GetComponent<MapComponent_XMTWorldOperations>().Claims(pawn)));

        private static void Preserve(XMT_TrackedPawn record)
        {
            Pawn pawn = record.pawn;
            if (record.reasons.Count == 0 || pawn == null || pawn.Destroyed || pawn.Dead || Find.WorldPawns == null || !Find.WorldPawns.Contains(pawn)) return;
            if (!Find.WorldPawns.ForcefullyKeptPawns.Contains(pawn))
            { Find.WorldPawns.ForcefullyKeptPawns.Add(pawn); record.addedRetention = true; }
        }

        public void RegisterCompanion(Pawn owner, XMT_CompanionDef definition, int priority)
        {
            if (owner == null || definition == null) return;
            companions ??= new List<XMT_CompanionLink>();
            XMT_CompanionLink link = companions.FirstOrDefault(c => !c.ended && c.owner == owner && c.definition?.role == definition.role);
            if (link != null)
            {
                if (priority <= link.priority || link.companion != null) return;
                link.definition = definition; link.priority = priority; return;
            }
            companions.Add(new XMT_CompanionLink { owner = owner, definition = definition, priority = priority });
            Track(owner, "companion-owner:" + definition.role);
        }

        public Pawn ResolveCompanion(XMT_CompanionLink link, IEnumerable<Pawn> additionalCandidates = null)
        {
            if (link.companion != null) return link.companion;
            if (link.definition.source is XMT_CompanionSource_Bio named && named.bio != null)
            {
                // Search only at first resolution, not on each pawn/job validator. A matching bio must also be unclaimed.
                IEnumerable<Pawn> candidates = (additionalCandidates ?? Enumerable.Empty<Pawn>()).Concat(Find.WorldPawns.AllPawnsAliveOrDead)
                    .Concat(Find.Maps.SelectMany(m => m.mapPawns.AllPawns))
                    .Concat(Find.Maps.SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Select(c => c.InnerPawn)));
                link.companion = candidates.FirstOrDefault(p => p != link.owner && named.Matches(p)
                    && XMT_PawnStateUtility.StructurallyValid(p)
                    && !companions.Any(c => c != link && c.companion == p));
            }
            link.companion ??= link.definition.source.Create(link.owner, link.definition);
            if (link.companion == null) return null;
            if (link.definition.yearsAfterOwnerBirth >= 0)
                link.companion.ageTracker.AgeChronologicalTicks = (long)(Math.Max(0f,
                    link.owner.ageTracker.AgeChronologicalYearsFloat - link.definition.yearsAfterOwnerBirth) * GenDate.TicksPerYear);
            ((XMT_CompanionRelationWorker)Activator.CreateInstance(link.definition.relationWorkerClass))
                .Assign(link.owner, link.companion, link.definition);
            Track(link.companion, "companion:" + link.owner.ThingID + ":" + link.definition.role);
            if (!link.companion.Spawned && link.companion.ParentHolder == null && !Find.WorldPawns.Contains(link.companion))
                Find.WorldPawns.PassToWorld(link.companion, PawnDiscardDecideMode.KeepForever);
            return link.companion;
        }

        public string BeginOperation(XMT_CompanionLink link, Map map)
        {
            if (link.operationId != null || Claimed(link.owner) || Claimed(link.companion)) return null;
            link.operationId = "XMT_Operation_" + (++nextOperationId);
            map.GetComponent<MapComponent_XMTWorldOperations>().Add(link.operationId);
            return link.operationId;
        }
        public void EndOperation(XMT_CompanionLink link, Map map, bool success)
        {
            map?.GetComponent<MapComponent_XMTWorldOperations>()?.Remove(link.operationId);
            link.operationId = null;
            link.satisfied = success;
            if (success) Reunited(link);
            link.caravanRefused = false;
            link.dueTick = Find.TickManager.TicksGame + Days(link.definition.retryAfterWithdrawalDays);
        }
        public void Reunited(XMT_CompanionLink link)
        {
            link.satisfied = true; link.dueTick = -1; link.caravanRefused = false;
            ((XMT_CompanionRelationWorker)Activator.CreateInstance(link.definition.relationWorkerClass))
                .NotifyReunited(link.owner, link.companion, link.definition);
        }
        internal static int Days(IntRange range) => range.RandomInRange * GenDate.TicksPerDay;

        public bool RetainThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.Spawned) return false;
            return thing.holdingOwner != null
                ? thing.holdingOwner.TryTransferToContainer(thing, offsiteThings, canMergeWithExistingStacks: false)
                : offsiteThings.TryAdd(thing, canMergeWithExistingStacks: false);
        }

        public override void GameComponentTick()
        {
            offsiteThings.DoTick();
            int tick = Find.TickManager.TicksGame;
            if (tick % 250 != 0) return;
            foreach (XMT_TrackedPawn record in pawns.ToList())
            {
                Pawn pawn = record.pawn;
                if (pawn == null || pawn.Destroyed)
                {
                    if (record.addedRetention && pawn != null) Find.WorldPawns?.ForcefullyKeptPawns.Remove(pawn);
                    if (pawn != null) pawnIndex?.Remove(pawn);
                    pawns.Remove(record); continue;
                }
                if ((pawn.Spawned || CaravanUtility.IsCaravanMember(pawn)) && !pawn.IsPrisoner && !pawn.IsSlave && !XMTContainmentUtility.IsHeld(pawn))
                {
                    record.captor = null;
                    Release(pawn, "captured");
                }
                Preserve(record);
            }
            foreach (XMT_WatcherDef definition in DefDatabase<XMT_WatcherDef>.AllDefsListForReading)
            {
                XMT_WatcherSchedule schedule = schedules.FirstOrDefault(s => s.definition == definition);
                if (schedule == null) { schedule = new XMT_WatcherSchedule { definition = definition }; schedules.Add(schedule); }
                if (tick < schedule.nextTick) continue;
                schedule.nextTick = tick + definition.intervalTicks;
                try { definition.Worker.Tick(this, tick); }
                catch (Exception error)
                {
                    schedule.nextTick = tick + Math.Max(2500, definition.intervalTicks);
                    Log.ErrorOnce("[XMT][WorldActivities] Watcher " + definition.defName + ": " + error, definition.defName.GetHashCode());
                }
            }
        }
    }

    public sealed class MapComponent_XMTWorldOperations : MapComponent
    {
        private List<string> operations = new List<string>();
        private List<Lord> deployments = new List<Lord>();
        public MapComponent_XMTWorldOperations(Map map) : base(map) { }
        public void Add(string id) { if (id != null && !operations.Contains(id)) operations.Add(id); }
        public void Remove(string id) { operations.Remove(id); }
        public bool Contains(string id) => operations.Contains(id) && deployments.Any(l =>
            l?.LordJob is LordJob_XMTRecovery recovery && recovery.OperationId == id);
        public void Register(Lord lord) { if (!deployments.Contains(lord)) deployments.Add(lord); }
        public void Remove(Lord lord) { deployments.Remove(lord); }
        public bool Claims(Pawn pawn) => deployments.Any(l => l != null && map.lordManager.lords.Contains(l)
            && (l.ownedPawns.Contains(pawn) || (l.LordJob as LordJob_XMT)?.ObjectivePawn == pawn));
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref operations, "XMT_operations", LookMode.Value);
            Scribe_Collections.Look(ref deployments, "XMT_deployments", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) operations ??= new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) deployments ??= new List<Lord>();
        }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            deployments = map.lordManager.lords.Where(l => l.LordJob is LordJob_XMT).ToList();
        }
        public override void MapRemoved()
        {
            foreach (Lord lord in deployments.ToList()) (lord?.LordJob as LordJob_XMTRecovery)?.Cancel();
            deployments.Clear(); operations.Clear();
            base.MapRemoved();
        }
    }

    public sealed class XMT_Watcher_Companions : XMT_WatcherWorker
    {
        public override void Tick(GameComponent_XMTWorldState state, int tick)
        {
            foreach (XMT_CompanionLink link in state.Companions.ToList())
            {
                if (link.definition?.watcher != Definition) continue;
                if (link.ended) continue;
                if (!link.Active || link.companion?.Dead == true || link.owner.Dead && (link.owner.Corpse == null || link.owner.Corpse.Destroyed))
                { link.ended = true; state.Release(link.owner, "companion-owner:" + link.definition?.role);
                    state.Release(link.companion, "companion:" + link.owner?.ThingID + ":" + link.definition?.role); continue; }
                if (link.operationId != null)
                {
                    if (Find.Maps.Any(m => m.GetComponent<MapComponent_XMTWorldOperations>().Contains(link.operationId))) continue;
                    state.EndOperation(link, null, false);
                }
                Thing target = link.Target;
                Map map = target?.MapHeld;
                bool captive = XMT_PawnStateUtility.NeedsCompanionRecovery(link);
                bool friendly = !link.owner.Dead && link.owner.Faction == Faction.OfPlayer && !captive;
                if (link.companion?.Spawned == true && (!captive || link.companion.MapHeld != map))
                {
                    if (link.companion.MapHeld != map) continue;
                    if (!friendly || link.companion.Faction == Faction.OfPlayer)
                    { if (!link.satisfied) state.Reunited(link); continue; }
                }
                Caravan caravan = target?.GetCaravan();
                if (!friendly && !captive && !(caravan != null && (link.owner.IsPrisoner || link.owner.IsSlave || link.owner.Dead)))
                { link.dueTick = -1; link.satisfied = false; continue; }
                if (link.satisfied) { link.satisfied = false; link.dueTick = -1; }
                if (link.dueTick < 0 || link.recovering != !friendly)
                {
                    link.recovering = !friendly;
                    if (link.recovering && link.companion != null)
                        ((XMT_CompanionRelationWorker)Activator.CreateInstance(link.definition.relationWorkerClass))
                            .NotifySeparated(link.owner, link.companion, link.definition);
                    link.dueTick = tick + GameComponent_XMTWorldState.Days(friendly ? link.definition.FriendlyDelay : link.definition.RecoveryDelay);
                }
                if (tick < link.dueTick || link.definition.retryCap >= 0 && link.attempts >= link.definition.retryCap) continue;
                if (!XMT_PawnStateUtility.StructurallyValid(link.owner)) continue;
                if (caravan != null && link.recovering) { XMT_CompanionCaravanUtility.TryEncounter(state, link, caravan, tick); continue; }
                if (!XMT_PawnStateUtility.PlayerHeldMap(map)) continue;
                Pawn companion = state.ResolveCompanion(link);
                if (!XMT_PawnStateUtility.Available(companion, state, map)) continue;
                XMT_ResponseDef response = friendly ? link.definition.friendlyResponse : link.definition.recoveryResponse;
                try
                {
                    XMT_ResponseResult result = ((XMT_CompanionResponseWorker)response.ResponseWorker).Execute(state, link, map);
                    if (result != XMT_ResponseResult.Completed) link.dueTick = tick + GenDate.TicksPerDay;
                }
                catch (Exception error)
                {
                    state.EndOperation(link, map, false);
                    Log.ErrorOnce("[XMT][Companions] Deployment failed: " + error, link.owner.thingIDNumber ^ 1895291);
                }
            }
        }
    }

    public static class XMT_PawnStateUtility
    {
        public static bool NeedsCompanionRecovery(XMT_CompanionLink link)
        {
            Pawn owner = link?.owner;
            if (owner == null) return false;
            Thing target = link.Target;
            return owner.IsPrisonerOfColony || owner.IsSlaveOfColony
                || owner.Dead && PlayerHeldMap(target?.MapHeld)
                || XMTContainmentUtility.Holder(owner)?.Faction == Faction.OfPlayer
                || target?.GetCaravan() != null && (owner.IsPrisoner || owner.IsSlave || owner.Dead);
        }
        public static bool PlayerHeldMap(Map map) => map != null && (map.IsPlayerHome || map.Parent?.Faction == Faction.OfPlayer
            || map.mapPawns.FreeColonistsSpawnedCount > 0);
        public static bool StructurallyValid(Pawn pawn) => StructuralInvalidity(pawn) == null;
        public static string StructuralInvalidity(Pawn pawn, PawnKindDef expectedKind = null)
        {
            if (pawn == null) return "missing pawn";
            try
            {
                List<string> reasons = new List<string>();
                if (pawn.Destroyed) reasons.Add("destroyed");
                if (pawn.def?.race == null) reasons.Add("missing race");
                if (expectedKind?.race != null && pawn.def != expectedKind.race) reasons.Add("kind/race mismatch");
                if (pawn.health == null) reasons.Add("missing health tracker");
                if (pawn.ageTracker == null) reasons.Add("missing age tracker");
                if (pawn.mindState == null) reasons.Add("missing mind state");
                if (pawn.relations == null) reasons.Add("missing relations tracker");
                if (pawn.relations?.DirectRelations.Any(r => r?.def == null || r.otherPawn == null) == true) reasons.Add("malformed direct relation");
                return reasons.Count == 0 ? null : string.Join(", ", reasons);
            }
            catch (Exception error) { return "validation threw " + error.GetType().Name + ": " + error.Message; }
        }
        public static bool Available(Pawn pawn, GameComponent_XMTWorldState state, Map existingMap = null) => AvailabilityReason(pawn, state, existingMap) == null;
        public static string AvailabilityReason(Pawn pawn, GameComponent_XMTWorldState state, Map existingMap = null)
        {
            string invalid = StructuralInvalidity(pawn);
            if (invalid != null) return invalid;
            if (pawn.Dead) return "dead";
            if (pawn.Downed) return "incapacitated";
            if (pawn.Spawned && (existingMap == null || pawn.Map != existingMap)) return "spawned on another map";
            if (!pawn.Spawned && pawn.ParentHolder != null) return "held in a container";
            if (pawn.IsPrisoner || pawn.IsSlave) return "companion is itself a prisoner/slave";
            if (CaravanUtility.IsCaravanMember(pawn)) return "traveling in a caravan";
            if (state.Claimed(pawn)) return "claimed by a deployment";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return "incapable of movement";
            return null;
        }
    }
}
