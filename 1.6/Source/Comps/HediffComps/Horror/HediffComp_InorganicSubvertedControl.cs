using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class HediffComp_InorganicSubvertedControl : HediffComp
    {
        private Faction originalFaction;
        private Pawn originalOverseer;
        private bool hadOriginalFaction;
        private bool hadOriginalOverseer;
        private bool originalStateStored;
        private bool restored;
        private bool goodwillApplied;
        private bool missionRetreat;
        private IntVec3 retreatCell = IntVec3.Invalid;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_References.Look(ref originalOverseer, "originalOverseer", saveDestroyedThings: false);
            Scribe_Values.Look(ref hadOriginalFaction, "hadOriginalFaction", false);
            Scribe_Values.Look(ref hadOriginalOverseer, "hadOriginalOverseer", false);
            Scribe_Values.Look(ref originalStateStored, "originalStateStored", false);
            Scribe_Values.Look(ref restored, "restored", false);
            Scribe_Values.Look(ref goodwillApplied, "goodwillApplied", false);
            Scribe_Values.Look(ref missionRetreat, "missionRetreat", false);
            Scribe_Values.Look(ref retreatCell, "retreatCell", IntVec3.Invalid);
        }

        public void BeginMissionRetreat(IntVec3 cell)
        {
            missionRetreat = true;
            if (cell.IsValid)
            {
                retreatCell = cell;
            }
            if (Pawn?.InMentalState == true)
            {
                Pawn.mindState.mentalStateHandler.Reset();
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!missionRetreat || Pawn == null || !Pawn.Spawned || Pawn.Dead || Pawn.Downed || !Pawn.IsHashIntervalTick(90))
            {
                return;
            }

            if (Pawn.CurJobDef == JobDefOf.AttackMelee || Pawn.CurJobDef == JobDefOf.AttackStatic)
            {
                return;
            }

            Pawn target = Pawn.Map.mapPawns.AllPawnsSpawned
                .Where(candidate => candidate != Pawn && !candidate.Dead && !candidate.Downed
                    && !XMTUtility.IsXenomorphFriendly(candidate) && !XMTUtility.IsXenomorph(candidate)
                    && candidate.Position.DistanceToSquared(Pawn.Position) <= 144f)
                .OrderBy(candidate => candidate.Position.DistanceToSquared(Pawn.Position)).FirstOrDefault();
            if (target != null)
            {
                Verb verb = Pawn.TryGetAttackVerb(target);
                if (verb != null)
                {
                    Job attack = JobMaker.MakeJob(verb.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
                    attack.expiryInterval = 300;
                    attack.locomotionUrgency = LocomotionUrgency.Jog;
                    if (verb.IsMeleeAttack)
                    {
                        attack.maxNumMeleeAttacks = 2;
                    }
                    Pawn.jobs.StartJob(attack, JobCondition.InterruptForced);
                    return;
                }
            }

            if (Pawn.CurJobDef == JobDefOf.Goto && Pawn.CurJob?.exitMapOnArrival == true)
            {
                return;
            }

            if (!retreatCell.IsValid || !retreatCell.InBounds(Pawn.Map)
                || !Pawn.CanReach(retreatCell, PathEndMode.OnCell, Danger.Deadly))
            {
                NemesisMissionUtility.ExitCell(Pawn, out retreatCell);
            }
            if (retreatCell.IsValid)
            {
                Job retreat = JobMaker.MakeJob(JobDefOf.Goto, retreatCell);
                retreat.exitMapOnArrival = true;
                retreat.locomotionUrgency = LocomotionUrgency.Jog;
                Pawn.jobs.StartJob(retreat, JobCondition.InterruptForced);
            }
        }

        public void StoreOriginalState()
        {
            if (originalStateStored || Pawn == null)
            {
                return;
            }

            originalFaction = Pawn.Faction;
            originalOverseer = MechanitorUtility.GetOverseer(Pawn);
            hadOriginalFaction = originalFaction != null;
            hadOriginalOverseer = originalOverseer != null;
            originalStateStored = true;
        }

        public void NotifyPlayerSubversion(Pawn queen)
        {
            if (goodwillApplied || queen?.Faction == null || originalFaction == null || originalFaction == queen.Faction || originalFaction.IsPlayer)
            {
                return;
            }

            originalFaction.TryAffectGoodwillWith(queen.Faction, -25, reason: HistoryEventDefOf.AttackedMember);
            goodwillApplied = true;
        }

        public void RestoreOriginalState()
        {
            if (restored || Pawn == null)
            {
                return;
            }

            restored = true;
            bool wasMissionRetreat = missionRetreat;
            missionRetreat = false;
            InorganicSubversionUtility.RestoreAllSuppressedAttachmentBandwidth(Pawn);
            InorganicSubversionUtility.StopSubvertedBerserk(Pawn);

            if (!Pawn.Dead)
            {
                InorganicSubversionUtility.RemoveOverseerRelationSilently(Pawn);

                if (hadOriginalFaction)
                {
                    if (originalFaction != null && Pawn.Faction != originalFaction)
                    {
                        Pawn.SetFaction(originalFaction);
                    }
                }
                else if (Pawn.Faction != null)
                {
                    Pawn.SetFaction(null);
                }

                if (hadOriginalOverseer && originalOverseer != null && !originalOverseer.Dead && Pawn.OverseerSubject != null)
                {
                    InorganicSubversionUtility.AddOverseerRelation(originalOverseer, Pawn);
                    originalOverseer.mechanitor?.AssignPawnControlGroup(Pawn, MechWorkModeDefOf.Work);
                    originalOverseer.mechanitor?.Notify_BandwidthChanged();
                }


                if (wasMissionRetreat && Pawn.CurJob?.exitMapOnArrival == true)
                {
                    Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
                }
            }
        }

        public override void CompPostPostRemoved()
        {
            RestoreOriginalState();
            base.CompPostPostRemoved();
        }
    }

    public class HediffCompProperties_InorganicSubvertedControl : HediffCompProperties
    {
        public HediffCompProperties_InorganicSubvertedControl()
        {
            compClass = typeof(HediffComp_InorganicSubvertedControl);
        }
    }
}
