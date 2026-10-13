using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class NemesisMissionWorker_Ablative : NemesisMissionWorker_SwarmAssault
    {
        internal override IEnumerable<Thing> EligibleTargets(Pawn attacker, Map map)
            => map.listerBuildings.allBuildingsColonist.Where(NemesisGeneEvaluationWorker_Ablative.IsDefense);
        internal override bool IsEligibleTarget(Pawn attacker, Thing target, Map map)
            => target is Building building && NemesisGeneEvaluationWorker_Ablative.IsDefense(building);
    }

    public sealed class LordJob_NemesisAblative : LordJob_NemesisMission
    {
        public override bool Successful => Current.Game.GetComponent<GameComponent_Nemesis>().Trial(GeneticTrialId) is NemesisGeneticTrial trial
            && mission.GeneEvaluator.Evaluate(trial, lord.Map) >= 0.2f;
        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor) => false;
        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage) { Notify_Revealed(pawn); return false; }
        public override void Begin()
        {
            base.Begin();
            foreach (Pawn pawn in lord.ownedPawns) if (pawn.GetComp<CompCrawler>() is CompCrawler crawler) crawler.Crawling = true;
        }
        protected override void ReportOutcome()
        {
            // Last-member loss precedes gene death effects. The component outlives this lord.
            Current.Game.GetComponent<GameComponent_Nemesis>().QueueAblativeOutcome(GeneticTrialId);
        }
        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            foreach (Pawn pawn in pawns)
            {
                if (pawn.GetComp<CompCrawler>() is CompCrawler crawler) crawler.Crawling = true;
                if (pawn.InMentalState) pawn.mindState.mentalStateHandler.Reset();
            }
            if (Successful && !withdrawing) { Withdraw("defenses disrupted"); ReportOutcome(); }
        }
        protected override Job GetOperationalJob(Pawn pawn)
        {
            Thing target = pawn.Map.listerBuildings.allBuildingsColonist.Where(NemesisGeneEvaluationWorker_Ablative.IsDefense)
                .Where(building => ClimbUtility.CanReachByWalkingOrClimb(pawn, building, PathEndMode.Touch, Danger.Deadly))
                .OrderByDescending(building => building.def.building?.IsTurret == true || building.def.building?.isTrap == true)
                .ThenBy(building => pawn.Position.DistanceToSquared(building.Position)).FirstOrDefault();
            if (target == null) { Withdraw("no accessible defenses"); ReportOutcome(); return NemesisMissionUtility.ExitJob(pawn) ?? Wait(); }
            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            attack.canBashDoors = true; attack.canBashFences = true; attack.expiryInterval = 600;
            attack.locomotionUrgency = LocomotionUrgency.Sprint;
            return attack;
        }
    }
}
