using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisScouting : LordJob_NemesisMission
    {
        private bool homeReached;
        public override bool Successful => missionSuccessCount > 0;

        public override void Notify_HostExtracted(Pawn victim, bool playerHost)
        {
            if (missionSuccessCount == 0) IncrementMissionSuccess(1, "host extracted");
        }

        public override void Notify_Revealed(Pawn pawn)
        {
            bool first = !WasDiscovered(pawn);
            base.Notify_Revealed(pawn);
            if (first) Withdraw("scout discovered");
        }

        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor) => false;

        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage)
        {
            Notify_Revealed(pawn);
            Withdraw("scout attacked");
            return false;
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            if (!homeReached && pawns.Exists(pawn => !pawn.Downed && lord.Map.areaManager.Home[pawn.Position]))
            {
                homeReached = true;
                if (missionSuccessCount == 0) IncrementMissionSuccess(1, "home area reached");
            }
            foreach (Pawn pawn in pawns)
                if (!pawn.Downed && (withdrawing || pawn.carryTracker?.CarriedThing is Pawn) && pawn.InMentalState)
                    pawn.mindState.mentalStateHandler.Reset();
        }

        protected override Job GetOperationalJob(Pawn pawn)
        {
            return FindHostJob(pawn, opportunistic: true, requirePlayer: false) ?? RouteJob(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref homeReached, "homeReached");
        }
    }
}
