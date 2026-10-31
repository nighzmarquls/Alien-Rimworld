using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    // Shared reaction surface. Strategic owners decide what an event means.
    public abstract class LordJob_XMT : LordJob
    {
        public virtual XMT_ResponseDef Response => null;
        public virtual Pawn ObjectivePawn => null;
        public virtual bool AllowRevealAttack(Pawn pawn, Thing discoverer) => true;
        public virtual bool AllowThreatResponse(Pawn pawn, Thing aggressor) => true;
        public virtual bool Notify_MissionDamage(Pawn pawn, DamageInfo damage) => true;
        public virtual void Notify_Revealed(Pawn pawn) { }
    }

    public abstract class XMT_CompanionSupportWorker
    {
        public abstract List<Pawn> Generate(Map map, float points, out Faction faction);
    }
    public sealed class XMT_CompanionSupport_Mercenaries : XMT_CompanionSupportWorker
    {
        public override List<Pawn> Generate(Map map, float points, out Faction faction)
        {
            faction = Find.FactionManager.AllFactionsListForReading.Where(f => !f.defeated && !f.temporary
                && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && f.def.canStageAttacks
                && PawnGroupMakerUtility.CanGenerateAnyNormalGroup(f, points))
                .OrderByDescending(f => f.def.techLevel).FirstOrDefault();
            if (faction == null) return new List<Pawn>();
            return PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms { faction = faction, tile = map.Tile,
                groupKind = PawnGroupKindDefOf.Combat, points = points, generateFightersOnly = true }).ToList();
        }
    }

    public class XMT_Response_FriendlyReunion : XMT_CompanionResponseWorker
    {
        public override XMT_ResponseResult Execute(GameComponent_XMTWorldState state, XMT_CompanionLink link, Map map)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, 0f)) return XMT_ResponseResult.Deferred;
            Pawn companion = state.ResolveCompanion(link);
            companion.GetLord()?.Notify_PawnLost(companion, PawnLostCondition.ForcedToJoinOtherLord);
            companion.SetFaction(Faction.OfPlayer);
            if (!companion.Spawned) GenSpawn.Spawn(companion, entry, map);
            state.Reunited(link);
            Find.LetterStack.ReceiveLetter("XMT_CompanionArrivedLabel".Translate(),
                "XMT_CompanionArrived".Translate(companion.Named("COMPANION"), link.owner.Named("OWNER")), LetterDefOf.PositiveEvent, companion);
            return XMT_ResponseResult.Completed;
        }
    }

    public class XMT_Response_Recovery : XMT_CompanionResponseWorker
    {
        public override XMT_ResponseResult Execute(GameComponent_XMTWorldState state, XMT_CompanionLink link, Map map)
        {
            Thing target = link.Target?.SpawnedParentOrMe;
            if (target?.Map != map) return XMT_ResponseResult.Deferred;
            Faction cover = Find.FactionManager.AllFactionsVisibleInViewOrder.FirstOrDefault(f => !f.IsPlayer && !f.defeated
                && !f.temporary && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer));
            bool drop = cover == null && link.definition.allowDropPods;
            IntVec3 entry;
            if (drop)
            {
                if (!DropCellFinder.TryFindDropSpotNear(target.Position, map, out entry, allowFogged: false, canRoofPunch: true))
                    return XMT_ResponseResult.Deferred;
            }
            else if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f)) return XMT_ResponseResult.Deferred;
            Pawn companion = state.ResolveCompanion(link);
            bool alreadyPresent = companion.Spawned;
            if (alreadyPresent) drop = false;
            string id = state.BeginOperation(link, map);
            if (id == null) return XMT_ResponseResult.Deferred;
            List<Pawn> party = new List<Pawn> { companion };
            Faction faction = cover ?? Faction.OfAncientsHostile;
            if (drop && link.definition.supportWorkerClass != null)
            {
                var support = (XMT_CompanionSupportWorker)Activator.CreateInstance(link.definition.supportWorkerClass);
                party.AddRange(support.Generate(map, StorytellerUtility.DefaultThreatPointsNow(map) * link.definition.supportPointsFactor, out Faction mercenaries));
                faction = mercenaries ?? faction;
            }
            link.attempts++;
            LordJob_XMTRecovery operation = new LordJob_XMTRecovery(link, cover == null);
            Lord lord = LordMaker.MakeNewLord(faction, operation, map);
            foreach (Pawn pawn in party)
            {
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                pawn.SetFaction(faction); lord.AddPawn(pawn);
            }
            if (drop)
            {
                DropPodUtility.DropThingsNear(entry, map, party.Cast<Thing>(), 110, false, true, true, true, true, faction);
                Find.LetterStack.ReceiveLetter("XMT_CompanionAssaultLabel".Translate(),
                    "XMT_CompanionAssault".Translate(companion.Named("COMPANION"), link.owner.Named("OWNER")), LetterDefOf.ThreatBig, target);
            }
            else
            {
                foreach (Pawn pawn in party.Where(p => !p.Spawned)) GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                Find.LetterStack.ReceiveLetter(cover != null ? "XMT_CompanionVisitorLabel".Translate() : "XMT_CompanionAssaultLabel".Translate(),
                    cover != null ? "XMT_CompanionVisitor".Translate(companion.Named("COMPANION"), faction.Named("FACTION"))
                    : "XMT_CompanionAssault".Translate(companion.Named("COMPANION"), link.owner.Named("OWNER")),
                    cover != null ? LetterDefOf.NeutralEvent : LetterDefOf.ThreatBig, companion);
            }
            return XMT_ResponseResult.Completed;
        }
    }

    public sealed class LordJob_XMTRecovery : LordJob_XMT
    {
        private Pawn owner;
        private Pawn companion;
        private bool hostile;
        private bool announced;
        private bool breachReported;
        private bool released;
        private bool wasSlave;
        private bool withdrawing;
        private int nextDecisionTick;
        private int nextBreachSearchTick;
        private Thing barrier;
        private bool finished;
        private bool changingFaction;
        private Faction originalOwnerFaction;
        private Faction originalCompanionFaction;
        public LordJob_XMTRecovery() { }
        public LordJob_XMTRecovery(XMT_CompanionLink link, bool hostile)
        {
            owner = link.owner; companion = link.companion; this.hostile = hostile; wasSlave = owner.IsSlaveOfColony;
            originalOwnerFaction = owner.SlaveFaction ?? owner.Faction; originalCompanionFaction = companion.Faction;
        }
        internal XMT_CompanionLink Link => Current.Game.GetComponent<GameComponent_XMTWorldState>().Companions
            .FirstOrDefault(c => c.owner == owner && c.companion == companion && c.operationId != null);
        public override XMT_ResponseDef Response => Link?.definition.recoveryResponse;
        public override Pawn ObjectivePawn => finished ? null : owner;
        internal string OperationId => Link?.operationId;
        internal string DebugDescription => "Recovery lord: hostile=" + hostile + " announced=" + announced + " released=" + released
            + " wasSlave=" + wasSlave + " withdrawing=" + withdrawing + " finished=" + finished + " barrier=" + barrier
            + " nextDecision=" + nextDecisionTick + " nextBreachSearch=" + nextBreachSearchTick;
        internal Thing Target => owner?.Dead == true ? owner.Corpse : owner;
        internal Thing Location => Target?.SpawnedParentOrMe;
        public override bool AddFleeToil => false;
        public override bool CanOpenAnyDoor(Pawn pawn) => !hostile;
        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph(); graph.StartingToil = new LordToil_XMTRecovery(); return graph;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "XMT_owner"); Scribe_References.Look(ref companion, "XMT_companion");
            Scribe_Values.Look(ref hostile, "XMT_hostile"); Scribe_Values.Look(ref announced, "XMT_announced");
            Scribe_Values.Look(ref breachReported, "XMT_breachReported");
            Scribe_Values.Look(ref released, "XMT_released"); Scribe_Values.Look(ref withdrawing, "XMT_withdrawing");
            Scribe_Values.Look(ref wasSlave, "XMT_wasSlave");
            Scribe_References.Look(ref originalOwnerFaction, "XMT_originalOwnerFaction");
            Scribe_References.Look(ref originalCompanionFaction, "XMT_originalCompanionFaction");
            Scribe_Values.Look(ref nextDecisionTick, "XMT_nextDecisionTick"); Scribe_References.Look(ref barrier, "XMT_barrier");
            Scribe_Values.Look(ref finished, "XMT_finished");
        }
        public void ExposeHostility()
        {
            if (hostile) return;
            hostile = true;
            changingFaction = true;
            try
            {
                lord.faction = Faction.OfAncientsHostile;
                foreach (Pawn pawn in lord.ownedPawns.ToList())
                {
                    if (pawn == owner && !released) continue;
                    pawn.SetFaction(lord.faction);
                    if (!lord.ownedPawns.Contains(pawn)) lord.AddPawn(pawn);
                }
            }
            finally { changingFaction = false; }
        }
        public void Announce(bool physicalBreach)
        {
            if (physicalBreach) ExposeHostility();
            if (wasSlave && Target?.MapHeld == lord.Map && lord.Map.areaManager.Home[Target.PositionHeld])
            {
                if (physicalBreach && !breachReported)
                {
                    breachReported = true;
                    Find.LetterStack.ReceiveLetter("XMT_CompanionBreachLabel".Translate(),
                        "XMT_CompanionBreach".Translate(companion.Named("COMPANION")), LetterDefOf.ThreatSmall, companion);
                }
                return;
            }
            if (announced) return;
            announced = true;
            Find.LetterStack.ReceiveLetter("XMT_JailbreakLabel".Translate(),
                "XMT_Jailbreak".Translate(companion.Named("COMPANION"), owner.Named("OWNER")), LetterDefOf.ThreatSmall, new LookTargets(owner, companion));
        }
        public void DoorOpened(Building_Door door)
        {
            if (wasSlave || Location?.Map != lord.Map) return;
            Room room = Location.GetRoom();
            if (room != null && GenAdj.CellsAdjacent8Way(door).Any(c => c.InBounds(lord.Map) && c.GetRoom(lord.Map) == room)) Announce(false);
        }
        internal void Released()
        {
            if (owner.Dead) { released = true; return; }
            if (XMTContainmentUtility.IsHeld(owner) && !XMTContainmentUtility.Eject(owner)) return;
            owner.guest?.SetGuestStatus(null);
            if (owner.Faction != companion.Faction) owner.SetFaction(companion.Faction);
            owner.jobs?.StopAll();
            released = true;
            if (!wasSlave) Announce(false);
            if (owner.GetLord() != lord)
            {
                owner.GetLord()?.Notify_PawnLost(owner, PawnLostCondition.ForcedToJoinOtherLord);
                lord.AddPawn(owner);
            }
        }
        private bool ExitCell(Pawn pawn, out IntVec3 result) => CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(lord.Map)
            && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, canBashDoors: hostile), lord.Map, 0f, out result);
        internal Job JobFor(Pawn pawn)
        {
            if (pawn.Downed || !pawn.Spawned) return null;
            XMT_CompanionLink link = Link;
            if (link == null) { withdrawing = true; }
            if (withdrawing || pawn != companion && pawn != owner)
            {
                if (hostile && !withdrawing)
                {
                    Job combat = XMT_RecoveryCombat.Job(pawn);
                    if (combat != null) return combat;
                }
                if (!ExitCell(pawn, out IntVec3 exit)) return Wait();
                Job leave = JobMaker.MakeJob(JobDefOf.Goto, exit); leave.exitMapOnArrival = true; return leave;
            }
            if (pawn == owner)
            {
                if (!released && !owner.IsSlaveOfColony) return null;
                if (!ExitCell(pawn, out IntVec3 exit)) return Wait();
                Job leave = JobMaker.MakeJob(JobDefOf.Goto, exit); leave.exitMapOnArrival = true; leave.locomotionUrgency = LocomotionUrgency.Sprint;
                return leave;
            }
            if (Target == null || Target.Destroyed) { withdrawing = true; return Wait(); }
            if (pawn.carryTracker.CarriedThing == Target || released && (owner.Dead || owner.Downed))
            {
                if (!ExitCell(pawn, out IntVec3 exit)) return BreachJob(pawn, true);
                Job extract = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("XMT_ExtractCompanion"), Target, exit);
                extract.count = 1; return extract;
            }
            if (released)
            {
                if (!owner.Spawned) return Wait();
                if (!ExitCell(owner, out _)) return BreachJob(pawn, true);
                if (pawn.Position.DistanceToSquared(owner.Position) > 25f)
                    return JobMaker.MakeJob(JobDefOf.Goto, owner);
                return Wait();
            }
            if (owner.IsSlaveOfColony && owner.Spawned && !owner.Downed)
            {
                Released(); return Wait();
            }
            Thing location = Location;
            if (location == null || location.Map != lord.Map) return Wait();
            if (!pawn.CanReach(location, PathEndMode.Touch, Danger.Deadly)) return BreachJob(pawn);
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("XMT_FreeCompanion"), location, owner);
        }
        private static Job Wait() { Job job = JobMaker.MakeJob(JobDefOf.Wait); job.expiryInterval = 90; return job; }
        private Job BreachJob(Pawn pawn, bool escaping = false)
        {
            if (barrier != null && barrier.Spawned && !pawn.CanReach(barrier, PathEndMode.Touch, Danger.Deadly)) barrier = null;
            if (barrier == null || barrier.Destroyed || !barrier.Spawned)
            {
                if (Find.TickManager.TicksGame < nextBreachSearchTick) return Wait();
                // One bounded route search, cached across job requests. Native pass-all-destroyable path selects practical access.
                LocalTargetInfo destination = escaping ? new LocalTargetInfo(CellFinder.RandomEdgeCell(lord.Map)) : new LocalTargetInfo(Location ?? Target);
                PathFinderCostTuning tuning = PathFinderCostTuning.DefaultTuning;
                tuning.costBlockedWallBase = tuning.costBlockedDoor = 0;
                tuning.costBlockedWallExtraPerHitPoint = tuning.costBlockedDoorPerHitPoint = 1f;
                using (PawnPath path = lord.Map.pathFinder.FindPathNow(pawn.Position, destination, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings), tuning, PathEndMode.Touch, null))
                {
                    barrier = path.Found ? path.NodesReversed.AsEnumerable().Reverse().Select(c => c.GetEdifice(lord.Map))
                        .FirstOrDefault(b => b != null && b != Location && b != Target && b.def.useHitPoints
                            && (b.def.passability == Traversability.Impassable || b is Building_Door door && !door.FreePassage)) : null;
                }
                nextBreachSearchTick = barrier == null ? Find.TickManager.TicksGame + 1200 : 0;
            }
            return barrier == null ? Wait() : JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("XMT_RecoveryBreach"), barrier);
        }
        public override void LordJobTick()
        {
            if (!announced && wasSlave && released && owner?.Spawned == true && !lord.Map.areaManager.Home[owner.Position]) Announce(false);
            if (finished || Find.TickManager.TicksGame < nextDecisionTick) return;
            nextDecisionTick = Find.TickManager.TicksGame + 90;
            XMT_CompanionLink link = Link;
            if (link == null || companion == null || companion.Dead || Target == null) { Finish(false); return; }
            if (released && owner.Spawned && owner.Downed && !lord.ownedPawns.Contains(owner)) lord.AddPawn(owner);
            if (link.definition.WithdrawalWorker.ShouldWithdraw(companion)) withdrawing = true;
            if (!companion.Spawned && companion.ParentHolder == null && Find.WorldPawns.Contains(companion)) Finish(false);
        }
        internal void Extracted(Pawn carrier, Thing thing)
        {
            GameComponent_XMTWorldState state = Current.Game.GetComponent<GameComponent_XMTWorldState>();
            if (thing is Pawn pawn)
            {
                carrier.carryTracker.innerContainer.Remove(pawn);
                pawn.SetFaction(originalOwnerFaction);
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }
            else if (!state.RetainThing(thing)) return;
            if (wasSlave && !announced) Announce(false);
            XMT_CompanionLink link = Link;
            if (thing is Corpse corpse && link != null)
                ((XMT_CorpseOutcomeWorker)Activator.CreateInstance(link.definition.corpseOutcomeWorkerClass)).Recovered(state, link, corpse);
            Finish(true);
        }
        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            base.Notify_PawnLost(pawn, condition);
            if (changingFaction) return;
            if (pawn == owner && condition == PawnLostCondition.ExitedMap && released)
            { pawn.SetFaction(originalOwnerFaction); Finish(true); }
            else if (pawn == companion && condition == PawnLostCondition.ExitedMap)
            { pawn.SetFaction(originalCompanionFaction); Finish(false); }
            else if (pawn == companion && condition != PawnLostCondition.ForcedToJoinOtherLord) Finish(false);
        }
        private void Finish(bool success)
        {
            if (finished) return;
            XMT_CompanionLink link = Link; finished = true; withdrawing = true;
            if (link != null) Current.Game.GetComponent<GameComponent_XMTWorldState>().EndOperation(link, lord.Map, success);
        }
        internal void Cancel() => Finish(false);
        internal void NotifyMapDeparture(Pawn pawn)
        {
            if (pawn == owner && released && wasSlave && !announced) Announce(false);
        }
    }

    public sealed class LordToil_XMTRecovery : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns) pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("XMT_RecoveryDuty"));
        }
    }
    public sealed class JobGiver_XMTRecovery : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => (pawn.GetLord()?.LordJob as LordJob_XMTRecovery)?.JobFor(pawn);
    }
    internal sealed class XMT_RecoveryCombat : JobGiver_AIFightEnemies
    {
        private static readonly XMT_RecoveryCombat instance = new XMT_RecoveryCombat();
        internal static Job Job(Pawn pawn) => instance.TryGiveJob(pawn);
    }

    public sealed class JobDriver_XMTFreeCompanion : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(120);
            yield return Toils_General.Do(() => (pawn.GetLord()?.LordJob as LordJob_XMTRecovery)?.Released());
        }
    }
    public sealed class JobDriver_XMTRecoveryBreach : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil breach = ToilMaker.MakeToil("XMT_BreachUntilOpen"); breach.defaultCompleteMode = ToilCompleteMode.Never;
            breach.tickAction = () =>
            {
                Thing target = job.targetA.Thing;
                if (target == null || target.Destroyed || !target.Spawned)
                { ReadyForNextToil(); return; }
                if (!pawn.stances.FullBodyBusy) pawn.TryGetAttackVerb(target)?.TryStartCastOn(target);
            };
            yield return breach;
        }
    }
    public sealed class JobDriver_XMTExtractCompanion : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.carryTracker.CarriedThing == job.targetA.Thing
            || pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            Toil carry = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return Toils_Jump.JumpIf(carry, () => pawn.carryTracker.CarriedThing == job.targetA.Thing);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return carry;
            yield return Toils_General.Do(() =>
            {
                if (!pawn.Position.OnEdge(pawn.Map) || pawn.carryTracker.CarriedThing != job.targetA.Thing) return;
                (pawn.GetLord()?.LordJob as LordJob_XMTRecovery)?.Extracted(pawn, job.targetA.Thing);
                pawn.ExitMap(false, pawn.Rotation);
            });
        }
    }
}
