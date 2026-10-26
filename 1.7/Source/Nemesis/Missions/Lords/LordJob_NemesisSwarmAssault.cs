using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisSwarmAssault : LordJob_NemesisMission
    {
        private bool targetsExhausted;
        private readonly List<Thing> targets = new List<Thing>();
        private int nextTargetRefresh;
        public override bool Successful => targetsExhausted || MeetsAttachmentThreshold(missionSuccessCount, initialMemberCount);

        internal static bool MeetsAttachmentThreshold(int successes, int initialMembers)
            => initialMembers > 0 && successes * 3 > initialMembers * 2;

        public override void Notify_ParasiteAttached(Pawn attacker, Pawn target)
        {
            IncrementMissionSuccess(1, "parasite attached");
            CheckObjectiveExhaustion();
            base.Notify_ParasiteAttached(attacker, target);
        }

        public override void Notify_TurretSubverted(Pawn attacker, Building_TurretGun turret)
        {
            IncrementMissionSuccess(1, "turret subverted");
            CheckObjectiveExhaustion();
            base.Notify_TurretSubverted(attacker, turret);
        }

        private void CheckObjectiveExhaustion()
        {
            // The last attachment may resolve the last member before the next lord tick.
            if (mission?.Worker is NemesisMissionWorker_SwarmAssault worker && lord?.Map != null
                && !worker.EligibleTargets(null, lord.Map).Any()) targetsExhausted = true;
        }

        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor) => false;

        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage)
        {
            Notify_Revealed(pawn);
            return false;
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            foreach (Pawn pawn in pawns.Where(pawn => !pawn.Downed && pawn.InMentalState))
            {
                pawn.mindState.mentalStateHandler.Reset();
            }
            if (withdrawing || tick < nextTargetRefresh) return;
            nextTargetRefresh = tick + 120;
            targets.Clear();
            if (mission.Worker is NemesisMissionWorker_SwarmAssault worker)
                targets.AddRange(worker.EligibleTargets(null, lord.Map));
            if (targets.Count == 0)
            {
                targetsExhausted = true;
                Withdraw("no eligible parasite targets remain");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetsExhausted, "targetsExhausted");
        }

        protected override Job GetOperationalJob(Pawn pawn)
        {
            NemesisMissionWorker_SwarmAssault worker = mission?.Worker as NemesisMissionWorker_SwarmAssault;
            if (worker == null || pawn?.Map == null)
            {
                return Wait();
            }
            NemesisMissionMember member = Member(pawn);
            if (Find.TickManager.TicksGame < member.nextCaptureTick) return Wait();
            member.nextCaptureTick = Find.TickManager.TicksGame + 120;

            Dictionary<Thing, int> claimCounts = lord.ownedPawns
                .Where(other => other != pawn && (other.CurJobDef == XenoWorkDefOf.XMT_ImplantHunt
                    || other.CurJobDef == XenoWorkDefOf.XMT_SubvertTurret))
                .Select(other => other.CurJob?.targetA.Thing).Where(claimed => claimed != null)
                .GroupBy(claimed => claimed).ToDictionary(group => group.Key, group => group.Count());
            Thing target = targets.Where(candidate => worker.IsEligibleTarget(pawn, candidate, pawn.Map))
                .OrderBy(candidate => claimCounts.TryGetValue(candidate, out int count) ? count : 0)
                .ThenByDescending(candidate => candidate is Pawn targetPawn && (targetPawn.Downed || !targetPawn.Awake()))
                .ThenBy(candidate => pawn.Position.DistanceToSquared(candidate.Position))
                .FirstOrDefault(candidate => ClimbUtility.CanReachByWalkingOrClimb(pawn, candidate, PathEndMode.Touch, Danger.Deadly));
            if (target != null)
            {
                JobDef jobDef = target is Building_TurretGun ? XenoWorkDefOf.XMT_SubvertTurret : XenoWorkDefOf.XMT_ImplantHunt;
                Job implant = JobMaker.MakeJob(jobDef, target);
                implant.locomotionUrgency = LocomotionUrgency.Sprint;
                return implant;
            }

            return Wait();
        }
    }
}
