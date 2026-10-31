using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public abstract class LordJob_Nemesis : LordJob_XMT
    {
        private List<Pawn> discovered = new List<Pawn>();
        public bool WasDiscovered(Pawn pawn) => discovered.Contains(pawn);

        public override void Notify_Revealed(Pawn pawn)
        {
            if (pawn != null && !discovered.Contains(pawn))
            {
                discovered.Add(pawn);
                NemesisLog.Detail("Lord", "First reveal: lord=" + GetType().Name
                    + " pawn=" + pawn + " map=" + pawn.MapHeld?.uniqueID + " tick=" + Find.TickManager.TicksGame);
            }
        }

        // Notifications may arrive inside a toil or job cleanup. These hooks must not start jobs.
        public override bool AllowRevealAttack(Pawn pawn, Thing discoverer) => true;
        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor) => true;
        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref discovered, "nemesisDiscovered", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                discovered ??= new List<Pawn>();
                discovered.RemoveAll(p => p == null);
            }
        }
    }
}
