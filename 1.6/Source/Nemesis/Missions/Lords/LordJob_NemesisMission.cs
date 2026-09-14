using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class NemesisMissionMember : IExposable
    {
        public Pawn pawn;
        public int routeIndex;
        public int nextCaptureTick;
        public bool routeInitialized;
        internal int nextDecisionLogTick;
        internal string lastDecision;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref routeIndex, "routeIndex");
            Scribe_Values.Look(ref nextCaptureTick, "nextCaptureTick");
            Scribe_Values.Look(ref routeInitialized, "routeInitialized");
        }
    }

    public abstract class LordJob_NemesisMission : LordJob_Nemesis
    {
        protected NemesisMissionDef mission;
        protected List<IntVec3> route = new List<IntVec3>();
        protected List<NemesisMissionMember> members = new List<NemesisMissionMember>();
        protected int extracted;
        protected int playerHostsExtracted;
        protected bool withdrawing;
        private bool activeNemesis;
        private int deployedTick;
        private bool outcomeReported;
        private string withdrawalReason;

        public NemesisMissionDef Mission => mission;
        public IReadOnlyList<IntVec3> Route => route;
        public abstract bool Successful { get; }
        public int Extracted => extracted;
        public bool Withdrawing => withdrawing;
        public virtual bool CovertEnded => false;
        public string WithdrawalReason => withdrawalReason;
        public override bool AddFleeToil => false;
        public override bool NeverInRestraints => true;

        public void Initialize(NemesisMissionDef missionDef, bool active, List<IntVec3> approachRoute)
        { mission = missionDef; activeNemesis = active; route = approachRoute ?? new List<IntVec3>(); }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.StartingToil = new LordToil_NemesisMission();
            return graph;
        }

        public void Begin()
        {
            deployedTick = Find.TickManager.TicksGame;
            InitializeRouteSectors();
            NemesisLog.Detail("Lord", "Begin mission=" + mission?.defName + " map=" + lord.Map.uniqueID
                + " routes=" + string.Join(", ", route) + " members=" + lord.ownedPawns.Count + " active=" + activeNemesis);
        }

        public override void Notify_PawnAdded(Pawn pawn) { base.Notify_PawnAdded(pawn); Member(pawn); }

        protected NemesisMissionMember Member(Pawn pawn)
        {
            NemesisMissionMember member = members.FirstOrDefault(value => value.pawn == pawn);
            if (member == null) { member = new NemesisMissionMember { pawn = pawn }; members.Add(member); }
            return member;
        }

        private void InitializeRouteSectors()
        {
            if (route.Count == 0 || lord == null) return;
            List<Pawn> pawns = lord.ownedPawns.Where(pawn => pawn != null && !pawn.Dead).ToList();
            for (int index = 0; index < pawns.Count; index++)
            {
                NemesisMissionMember member = Member(pawns[index]);
                if (member.routeInitialized) continue;
                member.routeIndex = index % route.Count;
                member.routeInitialized = true;
                NemesisLog.Detail("Lord", "Route sector pawn=" + pawns[index] + " index=" + member.routeIndex + "/" + route.Count);
            }
        }

        public override bool AllowRevealAttack(Pawn pawn, Thing discoverer) => false;

        public void Notify_Extracted(Pawn victim, bool playerHost)
        {
            extracted++;
            if (playerHost) playerHostsExtracted++;
            if (mission?.extractionEvidence != null)
                Current.Game?.GetComponent<GameComponent_Nemesis>()?.RecordMapEvidence(mission.extractionEvidence, 1f,
                    lord?.Map, mission, "host extracted by " + mission.defName);
            NemesisLog.Detail("Mission", "Extracted host=" + victim + " mission=" + mission?.defName + " total=" + extracted);
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            base.Notify_PawnLost(pawn, condition);
            if (!lord.ownedPawns.Any(other => other != pawn && other.Spawned && !other.Dead && !other.Downed))
            {
                Withdraw("all members lost or incapacitated");
                ReportOutcome();
            }
        }

        protected void ReportOutcome()
        {
            if (outcomeReported) return;
            outcomeReported = true;
            NemesisLog.Detail("Mission", "Completed " + mission?.defName + " map=" + lord?.Map?.uniqueID
                + " success=" + Successful + " extracted=" + extracted + " reason=" + withdrawalReason);
            Current.Game?.GetComponent<GameComponent_Nemesis>()?.NotifyMissionEnded();
        }

        public void Withdraw(string reason)
        {
            if (withdrawing) return;
            withdrawing = true;
            withdrawalReason = reason;
            NemesisLog.Detail("Mission", "Withdrawing " + mission?.defName + " map=" + lord?.Map?.uniqueID + " reason=" + reason);
            if (lord != null)
                foreach (Pawn pawn in lord.ownedPawns.Where(value => value.Spawned && !value.Dead && !value.Downed && !value.InMentalState))
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (mission == null) { Withdraw("missing mission definition"); return; }
            int tick = Find.TickManager.TicksGame;
            if (tick % 30 != 0) return;
            InitializeRouteSectors();
            List<Pawn> pawns = lord.ownedPawns.Where(pawn => pawn.Spawned && !pawn.Dead).ToList();
            if (!pawns.Any(pawn => !pawn.Downed)) { Withdraw("all members incapacitated"); ReportOutcome(); return; }
            if (tick - deployedTick >= mission.maximumDurationTicks) Withdraw("mission duration elapsed");
            if (!NemesisMissionUtility.MapDark(lord.Map)) Withdraw("unsuitable lighting");
            foreach (Pawn exposed in pawns.Where(pawn => !pawn.Downed && !WasDiscovered(pawn)
                && !XMTHiveUtility.IsLightSuitableAt(pawn.Position, lord.Map)))
                exposed.GetComp<CompStealth>()?.ForceVisible();
            TickMission(pawns, tick);
            if (XMTSettings.LogNemesis && tick % 600 == 0)
                foreach (Pawn pawn in pawns)
                {
                    NemesisMissionMember member = Member(pawn);
                    NemesisLog.Detail("Lord", "Status mission=" + mission.defName + " pawn=" + pawn + " pos=" + pawn.Position
                        + " job=" + pawn.CurJobDef + " target=" + pawn.CurJob?.targetA + " route=" + member.routeIndex + "/" + route.Count
                        + " carrying=" + pawn.carryTracker?.CarriedThing + " lastDecision=" + member.lastDecision);
                }
        }

        protected abstract void TickMission(List<Pawn> pawns, int tick);
        protected abstract Job GetOperationalJob(Pawn pawn);

        internal Job GetMissionJob(Pawn pawn)
        {
            Job job = pawn.carryTracker?.CarriedThing is Pawn carried
                ? NemesisMissionUtility.ExtractionJob(pawn, carried) ?? Wait()
                : withdrawing ? NemesisMissionUtility.ExitJob(pawn) ?? Wait() : GetOperationalJob(pawn);
            LogDecision(pawn, job);
            return job;
        }

        protected Job FindHostJob(Pawn pawn, bool opportunistic, bool requirePlayer)
        {
            NemesisMissionMember member = Member(pawn);
            if (Find.TickManager.TicksGame < member.nextCaptureTick) return null;
            HashSet<Pawn> claimed = new HashSet<Pawn>(lord.ownedPawns.Where(other => other != pawn
                && other.CurJobDef == NemesisMissionUtility.AbductJob).Select(other => other.CurJob.targetA.Pawn).Where(host => host != null));
            Pawn target = NemesisMissionUtility.FindHost(pawn, mission.workerSettings.localSearchRadius, opportunistic, requirePlayer, claimed);
            if (target == null) { member.nextCaptureTick = Find.TickManager.TicksGame + 120; return null; }
            Job job = NemesisMissionUtility.ExtractionJob(pawn, target);
            if (job != null) NemesisLog.Detail("Mission", "Capture selected pawn=" + pawn + " host=" + target + " mission=" + mission.defName);
            return job;
        }

        protected bool HasPlayerHostClaim()
        {
            return playerHostsExtracted > 0 || lord.ownedPawns.Any(pawn => pawn.CurJobDef == NemesisMissionUtility.AbductJob
                && pawn.CurJob.targetA.Pawn?.Faction == Faction.OfPlayer);
        }

        protected Job RouteJob(Pawn pawn)
        {
            if (route.Count == 0) return Wait();
            NemesisMissionMember member = Member(pawn);
            int stride = Mathf.Max(1, lord.ownedPawns.Count(value => value.Spawned && !value.Dead && !value.Downed));
            if (member.routeIndex >= route.Count) return Wait();
            IntVec3 destination = route[member.routeIndex];
            if (pawn.Position.DistanceToSquared(destination) <= 225f)
            {
                member.routeIndex += stride;
                if (member.routeIndex >= route.Count) return Wait();
                destination = route[member.routeIndex];
            }
            List<IntVec3> cells = GenRadial.RadialCellsAround(destination, 14f, true)
                .Where(cell => cell.DistanceToSquared(destination) >= 36f && cell.InBounds(pawn.Map) && cell.Standable(pawn.Map)
                    && cell.DistanceToSquared(pawn.Position) >= 4f
                    && XMTHiveUtility.IsLightSuitableAt(cell, pawn.Map)
                    && ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly)).ToList();
            if (cells.Count == 0) return Wait();
            int slot = Mathf.Max(0, members.IndexOf(member));
            Job travel = JobMaker.MakeJob(JobDefOf.Goto, cells[(slot * 17 + member.routeIndex * 7) % cells.Count]);
            travel.locomotionUrgency = LocomotionUrgency.Walk;
            travel.expiryInterval = 600;
            return travel;
        }

        protected static Job Wait()
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait); job.expiryInterval = 120; return job;
        }

        private void LogDecision(Pawn pawn, Job job)
        {
            if (!XMTSettings.LogNemesis) return;
            NemesisMissionMember member = Member(pawn);
            string decision = "lord=" + GetType().Name + " job=" + job?.def + " target=" + job?.targetA
                + " route=" + member.routeIndex + "/" + route.Count + " withdrawing=" + withdrawing
                + " carrying=" + pawn.carryTracker?.CarriedThing;
            int tick = Find.TickManager.TicksGame;
            if (decision == member.lastDecision && tick < member.nextDecisionLogTick) return;
            member.lastDecision = decision; member.nextDecisionLogTick = tick + 600;
            NemesisLog.Detail("Lord", "Decision mission=" + mission?.defName + " pawn=" + pawn + " pos=" + pawn.Position + " " + decision);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref mission, "mission");
            Scribe_Values.Look(ref activeNemesis, "activeNemesis");
            Scribe_Collections.Look(ref route, "route", LookMode.Value);
            Scribe_Collections.Look(ref members, "members", LookMode.Deep);
            Scribe_Values.Look(ref extracted, "extracted");
            Scribe_Values.Look(ref playerHostsExtracted, "playerHostsExtracted");
            Scribe_Values.Look(ref withdrawing, "withdrawing");
            Scribe_Values.Look(ref deployedTick, "deployedTick");
            Scribe_Values.Look(ref outcomeReported, "outcomeReported");
            Scribe_Values.Look(ref withdrawalReason, "withdrawalReason");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                route ??= new List<IntVec3>(); members ??= new List<NemesisMissionMember>(); members.RemoveAll(value => value?.pawn == null);
            }
        }
    }

    public sealed class LordToil_NemesisMission : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in lord.ownedPawns) pawn.mindState.duty = new PawnDuty(XenoWorkDefOf.XMT_NemesisMission);
        }
    }
}
