using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisAssault : LordJob_NemesisMission
    {
        private static readonly FloatRange DesiredDamageRange = new FloatRange(0.25f, 0.35f);
        private static readonly IntRange AssaultTimeBeforeGiveUp = new IntRange(26000, 38000);
        private LordToil exitToil;

        public override bool Successful => missionSuccessCount > 0;
        protected override bool EnforceMaximumDuration => false;
        public override bool AddFleeToil => true;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_AssaultColony assault = new LordToil_AssaultColony(
                attackDownedIfStarving: false, canPickUpOpportunisticWeapons: false);
            assault.useAvoidGrid = true;
            graph.StartingToil = assault;
            exitToil = new LordToil_ExitMapFighting(LocomotionUrgency.Jog, canDig: true) { useAvoidGrid = true };
            graph.AddToil(exitToil);

            Transition damageSatisfied = new Transition(assault, exitToil);
            damageSatisfied.AddTrigger(new Trigger_FractionColonyDamageTaken(DesiredDamageRange.RandomInRange, 900f));
            damageSatisfied.AddPreAction(new TransitionAction_Custom(new Action(() =>
            {
                Notify_AssaultObjectiveReached();
                Withdraw("colony damage objective reached");
                ReportOutcome();
            })));
            graph.AddTransition(damageSatisfied);

            Transition timeout = new Transition(assault, exitToil);
            timeout.AddTrigger(new Trigger_TicksPassed(AssaultTimeBeforeGiveUp.RandomInRange));
            timeout.AddPreAction(new TransitionAction_Custom(new Action(() =>
            {
                Withdraw("assault timeout");
                ReportOutcome();
            })));
            graph.AddTransition(timeout);
            return graph;
        }

        public override void Notify_AssaultObjectiveReached()
        {
            if (missionSuccessCount == 0) IncrementMissionSuccess(1, "colony damage threshold reached");
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            if (withdrawing && exitToil != null)
            {
                lord.GotoToil(exitToil);
                ReportOutcome();
            }
        }

        protected override Job GetOperationalJob(Pawn pawn) => Wait();
    }
}
