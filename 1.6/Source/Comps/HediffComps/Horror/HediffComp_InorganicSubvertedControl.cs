using RimWorld;
using Verse;

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
