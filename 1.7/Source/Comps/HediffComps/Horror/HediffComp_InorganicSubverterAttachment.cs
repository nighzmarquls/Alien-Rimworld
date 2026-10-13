using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    internal class HediffComp_InorganicSubverterAttachment : HediffComp_PawnAttachement
    {
        private const float Influence = 1f;
        private bool initialized;
        private bool bandwidthSuppressed;
        private bool missionAssault;
        private IntVec3 retreatCell = IntVec3.Invalid;

        public bool BandwidthSuppressed => bandwidthSuppressed;
        internal float SubversionInfluence => Influence;
        internal bool MissionAssault => missionAssault;
        internal IntVec3 RetreatCell => retreatCell;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref bandwidthSuppressed, "bandwidthSuppressed", false);
            Scribe_Values.Look(ref missionAssault, "missionAssault", false);
            Scribe_Values.Look(ref retreatCell, "retreatCell", IntVec3.Invalid);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                initialized = false;
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            Pawn attacher = dinfo?.Instigator as Pawn;
            if (attacher?.GetLord()?.LordJob is LordJob_NemesisSwarmAssault mission)
            {
                missionAssault = true;
                retreatCell = mission.EntryCell;
            }
            base.CompPostPostAdd(dinfo);
            if (!ValidInorganicHost())
            {
                PawnRelease();
                Pawn.health.RemoveHediff(parent);
                return;
            }

            PopulateInfluence();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (!ValidInorganicHost())
            {
                PawnRelease();
                Pawn.health.RemoveHediff(parent);
                return;
            }

            if (!initialized)
            {
                PopulateInfluence();
            }
        }

        public override void PawnRelease(bool killPawn = false)
        {
            if (bandwidthSuppressed)
            {
                InorganicSubversionUtility.RestoreAttachmentBandwidth(this);
            }

            base.PawnRelease(killPawn);
        }

        protected override void PostPawnReleased(Pawn released, bool killPawn)
        {
            Pawn host = Pawn;
            RemoveInfluence();
            InorganicSubversionUtility.NotifySubverterLoadChanged(host);

            if (!missionAssault || released == null || released.Dead || !released.Spawned)
            {
                return;
            }

            Lord missionLord = released.GetLord();
            if (missionLord?.LordJob is LordJob_NemesisSwarmAssault)
            {
                missionLord.RemovePawn(released);
            }

            CompInorganicSubverterHostHunter hunter = released.GetComp<CompInorganicSubverterHostHunter>();
            Thing target = hunter?.GetHuntTarget();
            if (target != null)
            {
                hunter.StartHuntTarget(target);
            }
        }

        public override void CompPostPostRemoved()
        {
            Pawn host = Pawn;
            if (bandwidthSuppressed)
            {
                InorganicSubversionUtility.RestoreAttachmentBandwidth(this);
            }

            RemoveInfluence();

            InorganicSubversionUtility.NotifySubverterLoadChanged(host, ignoredAttachment: this);
            base.CompPostPostRemoved();
        }

        internal void SetBandwidthSuppressed(bool value)
        {
            bandwidthSuppressed = value;
        }

        private void PopulateInfluence()
        {
            if (initialized || Pawn == null)
            {
                return;
            }

            Pawn.UpdateInorganicSubversionLoad(Pawn.InorganicSubversionLoad() + Influence);
            initialized = true;
            InorganicSubversionUtility.NotifySubverterLoadChanged(Pawn, recalculateLoad: false);
        }

        private void RemoveInfluence()
        {
            if (!initialized || Pawn == null)
            {
                return;
            }

            Pawn.UpdateInorganicSubversionLoad(Mathf.Max(0f, Pawn.InorganicSubversionLoad() - Influence));
            initialized = false;
        }

        private bool ValidInorganicHost()
        {
            return Pawn != null && !Pawn.Dead && XMTUtility.IsInorganic(Pawn);
        }
    }

    public class HediffCompProperties_InorganicSubverterAttachment : HediffCompProperties_PawnAttachement
    {
        public HediffCompProperties_InorganicSubverterAttachment()
        {
            compClass = typeof(HediffComp_InorganicSubverterAttachment);
        }
    }
}
