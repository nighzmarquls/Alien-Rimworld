using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class JobDriver_TraumaCower : JobDriver
    {
        internal const int CowerTicks = 180;
        // Includes the cower itself, leaving seven seconds of recovery afterward.
        internal const int RecoveryTicks = 600;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override string GetReport()
        {
            return "XMT_TraumaCowerReport".Translate();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.InMentalState);
            // Native Wait stops pathing; the delay is saved by JobDriver without custom state.
            yield return Toils_General.Wait(CowerTicks);
        }
    }
}
