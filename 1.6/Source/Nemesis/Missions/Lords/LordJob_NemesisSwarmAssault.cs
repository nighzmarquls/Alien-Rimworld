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
        public override bool Successful => MeetsAttachmentThreshold(missionSuccessCount, initialMemberCount);
        protected override bool EnforceMaximumDuration => false;

        internal static bool MeetsAttachmentThreshold(int successes, int initialMembers)
            => initialMembers > 0 && successes * 3 > initialMembers * 2;

        public override void Notify_ParasiteAttached(Pawn attacker, Pawn target)
        {
            IncrementMissionSuccess(1, "parasite attached");
            base.Notify_ParasiteAttached(attacker, target);
        }

        public override void Notify_TurretSubverted(Pawn attacker, Building_TurretGun turret)
        {
            IncrementMissionSuccess(1, "turret subverted");
            base.Notify_TurretSubverted(attacker, turret);
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
        }

        protected override Job GetOperationalJob(Pawn pawn)
        {
            NemesisMissionWorker_SwarmAssault worker = mission?.Worker as NemesisMissionWorker_SwarmAssault;
            if (worker == null || pawn?.Map == null)
            {
                return Wait();
            }

            Dictionary<Thing, int> claimCounts = lord.ownedPawns
                .Where(other => other != pawn && (other.CurJobDef == XenoWorkDefOf.XMT_ImplantHunt
                    || other.CurJobDef == XenoWorkDefOf.XMT_SubvertTurret))
                .Select(other => other.CurJob?.targetA.Thing).Where(claimed => claimed != null)
                .GroupBy(claimed => claimed).ToDictionary(group => group.Key, group => group.Count());
            Thing target = worker.EligibleTargets(pawn, pawn.Map)
                .Where(candidate => ClimbUtility.CanReachByWalkingOrClimb(pawn, candidate, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(candidate => claimCounts.TryGetValue(candidate, out int count) ? count : 0)
                .ThenByDescending(candidate => candidate is Pawn targetPawn && (targetPawn.Downed || !targetPawn.Awake()))
                .ThenBy(candidate => pawn.Position.DistanceToSquared(candidate.Position)).FirstOrDefault();
            if (target != null)
            {
                JobDef jobDef = target is Building_TurretGun ? XenoWorkDefOf.XMT_SubvertTurret : XenoWorkDefOf.XMT_ImplantHunt;
                Job implant = JobMaker.MakeJob(jobDef, target);
                implant.locomotionUrgency = LocomotionUrgency.Sprint;
                return implant;
            }

            Thing combatTarget = pawn.Map.mapPawns.AllPawnsSpawned.Where(candidate => candidate != pawn
                    && candidate.GetLord() != lord && !candidate.Dead && !candidate.Downed
                    && !XMTUtility.IsXenomorphFriendly(candidate) && !XMTUtility.IsXenomorph(candidate)
                    && ClimbUtility.CanReachByWalkingOrClimb(pawn, candidate, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(candidate => pawn.Position.DistanceToSquared(candidate.Position)).FirstOrDefault();
            combatTarget ??= pawn.Map.listerBuildings.allBuildingsColonist.Where(building => building.Spawned && !building.Destroyed
                    && ClimbUtility.CanReachByWalkingOrClimb(pawn, building, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(building => pawn.Position.DistanceToSquared(building.Position)).FirstOrDefault();
            if (combatTarget == null)
            {
                return Wait();
            }

            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, combatTarget);
            attack.canBashDoors = true;
            attack.killIncappedTarget = false;
            attack.locomotionUrgency = LocomotionUrgency.Sprint;
            attack.expiryInterval = 600;
            return attack;
        }
    }
}
