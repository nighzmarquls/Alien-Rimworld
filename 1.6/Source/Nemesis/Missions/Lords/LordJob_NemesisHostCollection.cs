using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisHostCollection : LordJob_NemesisMission
    {
        private List<Thing> knownThreats = new List<Thing>();
        private int lastViolenceTick = -1;
        private bool covertEnded;
        public override bool Successful => extracted > 0;
        public override bool CovertEnded => covertEnded;

        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor)
        {
            RememberThreat(aggressor);
            if (withdrawing || pawn.carryTracker?.CarriedThing is Pawn) return false;
            lastViolenceTick = Find.TickManager.TicksGame;
            return true;
        }

        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage)
        {
            Notify_Revealed(pawn);
            RememberThreat(damage.Instigator);
            lastViolenceTick = Find.TickManager.TicksGame;
            return pawn.carryTracker?.CarriedThing is not Pawn;
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            knownThreats.RemoveAll(target => !ValidThreat(target));
            if (pawns.All(pawn => pawn.Downed || WasDiscovered(pawn))) covertEnded = true;
            foreach (Pawn pawn in pawns.Where(value => !value.Downed))
            {
                if ((withdrawing || pawn.carryTracker?.CarriedThing is Pawn) && pawn.InMentalState)
                    pawn.mindState.mentalStateHandler.Reset();
                else if (pawn.MentalState is MentalState_XMT_MurderousRage rage
                    && (!ValidThreat(rage.target) || rage.target is Pawn targetPawn && targetPawn.Downed))
                    pawn.mindState.mentalStateHandler.Reset();
            }
            if (covertEnded && !pawns.Any(pawn => pawn.InMentalState || pawn.CurJobDef == JobDefOf.AttackMelee
                || pawn.CurJobDef == NemesisMissionUtility.AbductJob)
                && (lastViolenceTick < 0 || tick - lastViolenceTick > 600)) Withdraw("covert phase ended");
        }

        protected override Job GetOperationalJob(Pawn pawn)
        {
            Thing threat = ThreatFor(pawn);
            if (threat != null)
            {
                if (threat is Pawn downed && downed.Downed)
                {
                    Job capture = NemesisMissionUtility.ValidHost(downed) ? NemesisMissionUtility.ExtractionJob(pawn, downed) : null;
                    if (capture != null) return capture;
                }
                Job retaliation = RetaliationJob(pawn, threat);
                if (retaliation != null) return retaliation;
            }
            if (covertEnded) return Wait();
            return FindHostJob(pawn, opportunistic: false, requirePlayer: MustSecurePlayerHost(pawn)) ?? RouteJob(pawn);
        }

        private bool MustSecurePlayerHost(Pawn pawn)
        {
            if (HasPlayerHostClaim()) return false;
            Pawn designated = lord.ownedPawns.Where(member => member.Spawned && !member.Dead && !member.Downed
                && member.carryTracker?.CarriedThing is not Pawn).OrderBy(member => member.thingIDNumber).FirstOrDefault();
            return pawn == designated;
        }

        private Thing ThreatFor(Pawn pawn)
        {
            knownThreats.RemoveAll(target => !ValidThreat(target));
            HashSet<Thing> claimed = new HashSet<Thing>(lord.ownedPawns.Where(other => other != pawn)
                .Select(other => other.CurJob?.targetA.Thing).Where(target => target != null));
            return knownThreats.Where(target => !claimed.Contains(target)).OrderBy(target => pawn.Position.DistanceToSquared(target.Position)).FirstOrDefault()
                ?? knownThreats.OrderBy(target => pawn.Position.DistanceToSquared(target.Position)).FirstOrDefault();
        }

        private void RememberThreat(Thing threat) { if (ValidThreat(threat)) knownThreats.AddDistinct(threat); }
        private bool ValidThreat(Thing target) => target != null && !target.Destroyed && target.Spawned && target.Map == lord?.Map
            && (!(target is Pawn pawn) || !pawn.Dead);

        private static Job RetaliationJob(Pawn pawn, Thing target)
        {
            if (target == null || target is Pawn victim && victim.Downed || !pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) return null;
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.killIncappedTarget = false;
            job.maxNumMeleeAttacks = 3;
            job.expiryInterval = 600;
            return job;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref knownThreats, "knownThreats", LookMode.Reference);
            Scribe_Values.Look(ref lastViolenceTick, "lastViolenceTick", -1);
            Scribe_Values.Look(ref covertEnded, "covertEnded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) knownThreats ??= new List<Thing>();
        }
    }
}
