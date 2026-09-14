using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class JobDriver_AbductOffMap : JobDriver_AbductPawn
    {
        protected override IntVec3 FinalGoalCell => job.GetTarget(TargetIndex.B).Cell;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.carryTracker.CarriedThing == Victim) return true;
            if (!NemesisMissionUtility.ValidHost(Victim) || !FeralJobUtility.IsThingAvailableForJobBy(pawn, Victim)) return false;
            FeralJobUtility.ReserveThingForJob(pawn, job, Victim);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.AddFailCondition(() => Victim == null || Victim.Dead);
            Toil carry = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return Toils_Jump.JumpIf(carry, () => pawn.carryTracker.CarriedThing == Victim);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return AttemptGrab();
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return carry;
            Toil extract = ToilMaker.MakeToil("ExtractHostToWorld");
            extract.initAction = delegate
            {
                if (pawn.carryTracker.CarriedThing != Victim || !pawn.Position.OnEdge(pawn.Map))
                { EndJobWith(JobCondition.Incompletable); return; }
                LordJob_NemesisMission mission = pawn.GetLord()?.LordJob as LordJob_NemesisMission;
                bool playerHost = Victim.Faction == Faction.OfPlayer;
                if (!Current.Game.GetComponent<GameComponent_NemesisWorldPawns>().RetainExtractedHost(pawn, Victim))
                { EndJobWith(JobCondition.Incompletable); return; }
                mission?.Notify_Extracted(Victim, playerHost);
                Rot4 direction = pawn.Position.x == 0 ? Rot4.West : pawn.Position.x == pawn.Map.Size.x - 1 ? Rot4.East
                    : pawn.Position.z == 0 ? Rot4.South : Rot4.North;
                pawn.ExitMap(false, direction);
            };
            extract.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return extract;
        }
    }
}
