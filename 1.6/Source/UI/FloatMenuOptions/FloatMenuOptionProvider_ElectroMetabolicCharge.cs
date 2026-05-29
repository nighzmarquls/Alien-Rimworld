using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_ElectroMetabolicCharge : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!ModsConfig.BiotechActive)
            {
                return null;
            }

            Pawn pawn = context.FirstSelectedPawn;
            Pawn mech = clickedThing as Pawn;
            if (pawn == null || mech == null || !XMTUtility.IsXenomorph(pawn) || !pawn.ageTracker.Adult)
            {
                return null;
            }

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_ElectroMetabolicCatalyst))
            {
                return null;
            }

            if (!mech.RaceProps.IsMechanoid || !JobDriver_ElectroMetabolicCharge.TryGetMechEnergy(mech, out Need energy))
            {
                return null;
            }

            if (mech.Faction != Faction.OfPlayer && !MechanitorUtility.IsPlayerOverseerSubject(mech))
            {
                return null;
            }

            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_ChargeMech".Translate(mech.LabelShort), delegate
            {
                FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, mech);
                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ElectroMetabolicCharge, mech);
                FeralJobUtility.ReserveThingForJob(pawn, job, mech);
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }, MenuOptionPriority.High), pawn, mech);

            if (pawn.needs?.food == null || pawn.needs.food.CurLevel <= 0.1f)
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_TooHungry".Translate(pawn.LabelShort);
            }
            else if (energy.CurLevelPercentage >= JobDriver_ElectroMetabolicCharge.TargetChargeLevel(mech))
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_MechCharged".Translate(mech.LabelShort);
            }
            else if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, mech) || !pawn.CanReach(mech, PathEndMode.Touch, Danger.Deadly))
            {
                option.Disabled = true;
                option.tooltip = "CannotReach".Translate(mech.LabelShort);
            }

            return option;
        }
    }
}
