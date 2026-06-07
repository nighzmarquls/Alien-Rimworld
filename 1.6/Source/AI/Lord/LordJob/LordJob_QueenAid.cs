using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal class LordJob_QueenAid : LordJob
    {
        private const int AssaultTicks = 18000;
        private const int ClearThreatTicksBeforeLeaving = 2500;
        private const int QueenCareWaitRefreshInterval = 120;

        private Pawn queen;
        private QueenAidThreatProfile threatProfile;
        private IntVec3 defendPoint;
        private int noThreatsSinceTick = -1;
        private int nextQueenCareWaitTick = -1;

        public override bool AddFleeToil => false;

        public override bool NeverInRestraints => true;

        public LordJob_QueenAid()
        {
        }

        public LordJob_QueenAid(Pawn queen, QueenAidThreatProfile threatProfile)
        {
            this.queen = queen;
            this.threatProfile = threatProfile;
            defendPoint = queen?.PositionHeld ?? IntVec3.Invalid;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_Travel travel = new LordToil_Travel(defendPoint);
            travel.maxDanger = Danger.Deadly;
            travel.useAvoidGrid = false;
            stateGraph.StartingToil = travel;

            LordToil_AssaultColony assault = new LordToil_AssaultColony(attackDownedIfStarving: false, canPickUpOpportunisticWeapons: false);
            assault.useAvoidGrid = false;
            stateGraph.AddToil(assault);

            LordToil_ExitMapFighting exit = new LordToil_ExitMapFighting(LocomotionUrgency.Jog, canDig: true);
            exit.useAvoidGrid = true;
            stateGraph.AddToil(exit);

            Transition beginAssault = new Transition(travel, assault);
            beginAssault.AddTrigger(new Trigger_Memo("TravelArrived"));
            beginAssault.AddTrigger(new Trigger_PawnHarmed());
            beginAssault.AddTrigger(new Trigger_TicksPassed(1200));
            stateGraph.AddTransition(beginAssault);

            Transition timeout = new Transition(assault, exit);
            timeout.AddTrigger(new Trigger_TicksPassed(AssaultTicks));
            stateGraph.AddTransition(timeout);

            Transition allClear = new Transition(assault, exit);
            allClear.AddTrigger(new Trigger_QueenAidNoThreats());
            stateGraph.AddTransition(allClear);

            return stateGraph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref queen, "queen");
            Scribe_Deep.Look(ref threatProfile, "threatProfile");
            Scribe_Values.Look(ref defendPoint, "defendPoint");
            Scribe_Values.Look(ref noThreatsSinceTick, "noThreatsSinceTick", -1);
            Scribe_Values.Look(ref nextQueenCareWaitTick, "nextQueenCareWaitTick", -1);
        }

        public override bool ValidateAttackTarget(Pawn searcher, Thing target)
        {
            if (IsFoggedThreat(target))
            {
                return false;
            }

            return QueenAidThreatProfile.IsQueenAidThreat(queen, searcher, target, threatProfile);
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            PushDefenderUrgency();
            TryAssignQueenCareJob();
            TryAssignThreatAttackJobs();
        }

        public bool ShouldLeaveBecauseThreatsCleared()
        {
            if (queen == null || queen.Dead)
            {
                return true;
            }

            Thing protectionTarget = QueenProtectionTarget();
            if (protectionTarget == null || protectionTarget.Destroyed || !protectionTarget.Spawned)
            {
                return true;
            }

            if (HasReachableThreat())
            {
                noThreatsSinceTick = -1;
                return false;
            }

            if (TryAssignQueenCareJob())
            {
                return false;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (noThreatsSinceTick < 0)
            {
                noThreatsSinceTick = ticksGame;
                return false;
            }

            return ticksGame - noThreatsSinceTick >= ClearThreatTicksBeforeLeaving;
        }

        private bool TryAssignQueenCareJob()
        {
            if (queen == null || queen.Dead || lord?.ownedPawns == null)
            {
                return false;
            }

            Thing protectionTarget = QueenProtectionTarget();
            if (protectionTarget is Building building && TryAssignQueenContainerRepairJob(building))
            {
                return true;
            }

            if (!queen.Spawned)
            {
                return TryAssignQueenCarrierAttack();
            }

            bool queenNeedsTending = queen.health.HasHediffsNeedingTend();
            if (!queen.Downed && !queenNeedsTending)
            {
                return false;
            }

            List<Pawn> defenders = lord.ownedPawns
                .Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(queen.Position))
                .ToList();
            List<Pawn> tenders = defenders
                .OrderByDescending(MedicalSkillLevel)
                .ThenBy(pawn => pawn.Position.DistanceToSquared(queen.Position))
                .ToList();

            if (queenNeedsTending)
            {
                foreach (Pawn defender in tenders)
                {
                    if (IsTendingQueen(defender))
                    {
                        StandForCare(defender);
                        HoldQueenForCare(defender);
                        return true;
                    }
                }

                foreach (Pawn defender in defenders)
                {
                    if (defender.CurJobDef == JobDefOf.TendPatient && defender.CurJob?.targetA.Thing != queen)
                    {
                        defender.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }

                foreach (Pawn defender in tenders)
                {
                    if (TryStartQueenTendJob(defender))
                    {
                        return true;
                    }
                }
            }

            foreach (Pawn defender in defenders)
            {
                if (IsRescuingQueen(defender) || IsTendingQueen(defender))
                {
                    StandForCare(defender);
                    return true;
                }
            }

            foreach (Pawn defender in defenders)
            {
                if (TryStartQueenRescueJob(defender))
                {
                    return true;
                }
            }

            foreach (Pawn defender in tenders)
            {
                if (TryStartQueenTendJob(defender))
                {
                    return true;
                }
            }

            return queen.health.HasHediffsNeedingTend() || !queen.InBed();
        }

        private Thing QueenProtectionTarget()
        {
            if (queen == null)
            {
                return null;
            }

            if (queen.Spawned)
            {
                return queen;
            }

            Thing spawnedParent = queen.SpawnedParentOrMe;
            if (spawnedParent != null && spawnedParent != queen && spawnedParent.Spawned)
            {
                return spawnedParent;
            }

            return queen.ParentHolder as Thing;
        }

        private int MedicalSkillLevel(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
        }

        private bool IsRescuingQueen(Pawn defender)
        {
            return defender?.CurJobDef == JobDefOf.Rescue && defender.CurJob?.targetA.Thing == queen;
        }

        private bool IsTendingQueen(Pawn defender)
        {
            return defender?.CurJobDef == JobDefOf.TendPatient && defender.CurJob?.targetA.Thing == queen;
        }

        private bool IsRepairingQueenContainer(Pawn defender, Thing container)
        {
            return defender?.CurJobDef == JobDefOf.Repair && defender.CurJob?.targetA.Thing == container;
        }

        private void StandForCare(Pawn defender)
        {
            CompCrawler crawler = defender?.GetComp<CompCrawler>();
            if (crawler != null)
            {
                crawler.Crawling = false;
            }
        }

        private bool TryStartQueenRescueJob(Pawn defender)
        {
            if (!queen.Downed || queen.InBed() || !defender.CanReach(queen, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
            {
                return false;
            }

            if (!defender.CanReserve(queen, 1, -1, null, false))
            {
                return false;
            }

            Building_Bed bed = RestUtility.FindBedFor(queen, defender, checkSocialProperness: false)
                ?? RestUtility.FindBedFor(queen, defender, checkSocialProperness: false, ignoreOtherReservations: true);
            if (bed == null)
            {
                return false;
            }

            defender.mindState.mentalStateHandler.Reset();
            StandForCare(defender);
            Job rescueJob = JobMaker.MakeJob(JobDefOf.Rescue, queen, bed);
            rescueJob.count = 1;
            rescueJob.locomotionUrgency = LocomotionUrgency.Sprint;
            defender.jobs.StartJob(rescueJob, JobCondition.InterruptForced);
            return true;
        }

        private bool TryStartQueenTendJob(Pawn defender)
        {
            if (!queen.health.HasHediffsNeedingTend() || !defender.CanReach(queen, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
            {
                return false;
            }

            if (!defender.CanReserve(queen, 1, -1, null, false))
            {
                return false;
            }

            defender.mindState.mentalStateHandler.Reset();
            StandForCare(defender);
            Job tendJob = JobMaker.MakeJob(JobDefOf.TendPatient, queen);
            tendJob.count = 1;
            tendJob.locomotionUrgency = LocomotionUrgency.Sprint;
            defender.jobs.StartJob(tendJob, JobCondition.InterruptForced);
            HoldQueenForCare(defender);
            return true;
        }

        private void HoldQueenForCare(Pawn defender)
        {
            if (queen == null || !queen.Spawned || queen.Dead || queen.Downed || defender == null)
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextQueenCareWaitTick)
            {
                return;
            }

            PawnUtility.ForceWait(queen, QueenCareWaitRefreshInterval + 60, defender, maintainPosture: true, maintainSleep: false);
            nextQueenCareWaitTick = ticksGame + QueenCareWaitRefreshInterval;
        }

        private bool TryAssignQueenContainerRepairJob(Building container)
        {
            if (container == null || container.Destroyed || !container.Spawned || container.HitPoints >= container.MaxHitPoints || lord?.ownedPawns == null)
            {
                return false;
            }

            List<Pawn> defenders = lord.ownedPawns
                .Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed)
                .OrderByDescending(MedicalSkillLevel)
                .ThenBy(pawn => pawn.Position.DistanceToSquared(container.Position))
                .ToList();

            foreach (Pawn defender in defenders)
            {
                if (IsRepairingQueenContainer(defender, container))
                {
                    StandForCare(defender);
                    return true;
                }
            }

            foreach (Pawn defender in defenders)
            {
                if (!defender.CanReach(container, PathEndMode.Touch, Danger.Deadly, canBashDoors: true) || !defender.CanReserve(container, 1, -1, null, false))
                {
                    continue;
                }

                defender.mindState.mentalStateHandler.Reset();
                StandForCare(defender);
                Job repairJob = JobMaker.MakeJob(JobDefOf.Repair, container);
                repairJob.locomotionUrgency = LocomotionUrgency.Sprint;
                defender.jobs.StartJob(repairJob, JobCondition.InterruptForced);
                return true;
            }

            return true;
        }

        private void PushDefenderUrgency()
        {
            if (lord?.ownedPawns == null)
            {
                return;
            }

            foreach (Pawn defender in lord.ownedPawns)
            {
                Job job = defender?.CurJob;
                if (job != null)
                {
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    if (job.def == JobDefOf.Rescue || job.def == JobDefOf.TendPatient || job.def == JobDefOf.Repair)
                    {
                        StandForCare(defender);
                    }
                }
            }
        }

        private bool TryAssignQueenCarrierAttack()
        {
            Pawn carrier = FindQueenCarrier();
            if (carrier == null || carrier.Dead || carrier.Downed)
            {
                return false;
            }

            List<Pawn> defenders = lord.ownedPawns
                .Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(carrier.Position))
                .ToList();

            foreach (Pawn defender in defenders)
            {
                if (defender.CurJobDef == JobDefOf.AttackMelee && defender.CurJob?.targetA.Thing == carrier)
                {
                    return true;
                }
            }

            foreach (Pawn defender in defenders)
            {
                if (!defender.CanReach(carrier, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
                {
                    continue;
                }

                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, carrier);
                attackJob.locomotionUrgency = LocomotionUrgency.Sprint;
                defender.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                return true;
            }

            return false;
        }

        private Pawn FindQueenCarrier()
        {
            if (queen == null)
            {
                return null;
            }

            if (queen.SpawnedParentOrMe is Pawn directCarrier && directCarrier != queen && directCarrier.Spawned)
            {
                return directCarrier;
            }

            Map map = lord?.Map;
            if (map == null)
            {
                return null;
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn?.carryTracker?.CarriedThing == queen)
                {
                    return pawn;
                }
            }

            return null;
        }

        private void TryAssignThreatAttackJobs()
        {
            Map map = QueenProtectionTarget()?.MapHeld;
            if (map == null || lord?.ownedPawns == null)
            {
                return;
            }

            List<Thing> threats = QueenAidThreats(map).ToList();
            if (!threats.Any())
            {
                return;
            }

            Dictionary<Thing, int> assignedCounts = threats.ToDictionary(threat => threat, _ => 0);
            foreach (Pawn attacker in lord.ownedPawns.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed))
            {
                Thing target = attacker.CurJob?.targetA.Thing;
                if (attacker.CurJobDef == JobDefOf.AttackMelee && target != null && assignedCounts.ContainsKey(target))
                {
                    assignedCounts[target]++;
                }
            }

            foreach (Pawn defender in lord.ownedPawns.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed))
            {
                if (IsBusyWithQueenCare(defender) || IsAttackingValidThreat(defender))
                {
                    continue;
                }

                Thing threat = threats
                    .Where(target => defender.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
                    .OrderBy(target => assignedCounts.TryGetValue(target, out int count) ? count : 0)
                    .ThenBy(target => defender.Position.DistanceToSquared(target.Position))
                    .FirstOrDefault();
                if (threat == null)
                {
                    continue;
                }

                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, threat);
                attackJob.killIncappedTarget = true;
                attackJob.locomotionUrgency = LocomotionUrgency.Sprint;
                defender.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                assignedCounts[threat]++;
            }
        }

        private bool IsBusyWithQueenCare(Pawn defender)
        {
            Thing protectionTarget = QueenProtectionTarget();
            return IsRescuingQueen(defender) || IsTendingQueen(defender) || IsRepairingQueenContainer(defender, protectionTarget);
        }

        private bool IsAttackingValidThreat(Pawn defender)
        {
            Thing target = defender?.CurJob?.targetA.Thing;
            return defender?.CurJobDef == JobDefOf.AttackMelee && !IsFoggedThreat(target) && QueenAidThreatProfile.IsQueenAidThreat(queen, defender, target, threatProfile);
        }

        private bool HasReachableThreat()
        {
            Map map = QueenProtectionTarget()?.MapHeld;
            if (map == null || lord?.ownedPawns == null)
            {
                return false;
            }

            List<Pawn> defenders = lord.ownedPawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed).ToList();
            if (!defenders.Any())
            {
                return false;
            }

            foreach (Thing candidate in QueenAidThreats(map))
            {
                if (defenders.Any(defender => defender.CanReach(candidate, PathEndMode.Touch, Danger.Deadly, canBashDoors: true)))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<Thing> QueenAidThreats(Map map)
        {
            if (map == null || lord?.ownedPawns == null)
            {
                yield break;
            }

            Pawn searcher = lord.ownedPawns.FirstOrDefault(pawn => pawn != null && !pawn.Dead);
            if (searcher == null)
            {
                yield break;
            }

            foreach (Pawn candidate in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsFoggedThreat(candidate) && QueenAidThreatProfile.IsQueenAidThreat(queen, searcher, candidate, threatProfile))
                {
                    yield return candidate;
                }
            }

            foreach (Building_TurretGun turret in map.listerThings.GetThingsOfType<Building_TurretGun>())
            {
                if (!IsFoggedThreat(turret) && QueenAidThreatProfile.IsQueenAidThreat(queen, searcher, turret, threatProfile))
                {
                    yield return turret;
                }
            }
        }

        private static bool IsFoggedThreat(Thing target)
        {
            return target != null && target.Spawned && target.MapHeld != null && target.PositionHeld.Fogged(target.MapHeld);
        }
    }

    internal class QueenAidThreatProfile : IExposable
    {
        private const float AnimalThreatRadius = 60f;

        private int aggressorThingID = -1;
        private Faction aggressorFaction;
        private PawnKindDef aggressorKindDef;
        private ThingDef aggressorThingDef;
        private bool aggressorIsAnimal;
        private bool aggressorIsMechanoid;
        private bool aggressorIsHumanlike;
        private bool aggressorIsPlayerFaction;
        private IntVec3 lastThreatPosition = IntVec3.Invalid;

        public bool AggressorIsHumanlike => aggressorIsHumanlike;

        public bool AggressorIsPlayerFaction => aggressorIsPlayerFaction;

        public QueenAidThreatProfile()
        {
        }

        public QueenAidThreatProfile(Pawn aggressor)
        {
            aggressorThingID = aggressor?.thingIDNumber ?? -1;
            aggressorFaction = aggressor?.Faction;
            aggressorKindDef = aggressor?.kindDef;
            aggressorThingDef = aggressor?.def;
            aggressorIsAnimal = aggressor?.RaceProps?.Animal ?? false;
            aggressorIsMechanoid = aggressor?.RaceProps?.IsMechanoid ?? false;
            aggressorIsHumanlike = aggressor?.RaceProps?.Humanlike ?? false;
            aggressorIsPlayerFaction = aggressor?.Faction == Faction.OfPlayer;
            if (aggressor != null && aggressor.Spawned)
            {
                lastThreatPosition = aggressor.Position;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref aggressorThingID, "aggressorThingID", -1);
            Scribe_References.Look(ref aggressorFaction, "aggressorFaction");
            Scribe_Defs.Look(ref aggressorKindDef, "aggressorKindDef");
            Scribe_Defs.Look(ref aggressorThingDef, "aggressorThingDef");
            Scribe_Values.Look(ref aggressorIsAnimal, "aggressorIsAnimal", false);
            Scribe_Values.Look(ref aggressorIsMechanoid, "aggressorIsMechanoid", false);
            Scribe_Values.Look(ref aggressorIsHumanlike, "aggressorIsHumanlike", false);
            Scribe_Values.Look(ref aggressorIsPlayerFaction, "aggressorIsPlayerFaction", false);
            Scribe_Values.Look(ref lastThreatPosition, "lastThreatPosition");
        }

        public static bool IsQueenAidThreat(Pawn queen, Pawn searcher, Thing target, QueenAidThreatProfile profile)
        {
            if (queen == null || searcher == null || target == null || target.Destroyed)
            {
                return false;
            }

            if (target is Pawn pawn)
            {
                if (pawn == queen || pawn == searcher || pawn.Dead || pawn.Downed)
                {
                    return false;
                }

                if (XMTUtility.IsXenomorphFriendly(pawn) || XMTUtility.IsMorphing(pawn) || XMTUtility.HasEmbryo(pawn) || XMTUtility.IsXenomorph(pawn))
                {
                    return false;
                }

                if (profile != null && target.thingIDNumber == profile.aggressorThingID)
                {
                    return true;
                }

                if (target.HostileTo(queen) || target.HostileTo(searcher))
                {
                    return true;
                }

                if (profile == null)
                {
                    return false;
                }

                if (profile.aggressorFaction != null && pawn.Faction == profile.aggressorFaction)
                {
                    return true;
                }

                if (profile.aggressorIsPlayerFaction && pawn.Faction == Faction.OfPlayer)
                {
                    return true;
                }

                if (profile.aggressorIsMechanoid && pawn.RaceProps.IsMechanoid)
                {
                    return profile.aggressorFaction != null && pawn.Faction == profile.aggressorFaction;
                }

                if (profile.aggressorIsHumanlike && pawn.RaceProps.Humanlike && pawn.Faction == profile.aggressorFaction)
                {
                    return true;
                }

                if (profile.aggressorIsAnimal && pawn.RaceProps.Animal)
                {
                    bool sameKind = pawn.kindDef == profile.aggressorKindDef || pawn.def == profile.aggressorThingDef;
                    bool nearQueen = pawn.Spawned && queen.Spawned && pawn.Position.DistanceToSquared(queen.Position) <= AnimalThreatRadius * AnimalThreatRadius;
                    bool nearOriginalThreat = profile.lastThreatPosition.IsValid && pawn.Spawned && pawn.Position.DistanceToSquared(profile.lastThreatPosition) <= AnimalThreatRadius * AnimalThreatRadius;
                    return sameKind || nearQueen || nearOriginalThreat;
                }

                return false;
            }

            if (target is Building_TurretGun turret)
            {
                if (!turret.Active || turret.IsMannable)
                {
                    return false;
                }

                if (profile != null && target.thingIDNumber == profile.aggressorThingID)
                {
                    return true;
                }

                if (turret.Faction != null && profile?.aggressorFaction != null && turret.Faction == profile.aggressorFaction)
                {
                    return true;
                }
            }

            return target.HostileTo(queen) || target.HostileTo(searcher);
        }
    }

    internal class Trigger_QueenAidNoThreats : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick)
            {
                return false;
            }

            return lord?.LordJob is LordJob_QueenAid queenAid && queenAid.ShouldLeaveBecauseThreatsCleared();
        }
    }
}
