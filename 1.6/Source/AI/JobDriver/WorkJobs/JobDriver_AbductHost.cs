using RimWorld;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{

    public class JobDriver_AbductHost : JobDriver_AbductPawn
    {

        private const TargetIndex HaulableInd = TargetIndex.A;

        private const TargetIndex StoreCellInd = TargetIndex.B;

        private float CocoonTicksFinish = 350;
        private float CocoonTicks = 0;
        private float CocoonProgress = 0;

        protected override IntVec3 FinalGoalCell => job.GetTarget(TargetIndex.B).Cell;
        public Thing ToHaul => job.GetTarget(TargetIndex.A).Thing;

        protected virtual bool DropCarriedThingIfNotTarget => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CocoonTicks, "cocoonTicks");
            Scribe_Values.Look(ref CocoonTicksFinish, "cocoonTicksFinish", 350f);
            CocoonProgress = CocoonTicks / Mathf.Max(1f, CocoonTicksFinish);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public bool IsNoLongerValidTarget()
        {
            if (job.GetTarget(TargetIndex.C).IsValid &&
                !XMTZoneUtility.PreferredHostDestinationStillValid(pawn, FinalGoalCell))
            {
                XMTZoneUtility.WarnPreferredHostDestinationLost(pawn.Map);
                return true;
            }

            if (XMTHiveUtility.IsCellValidCocoon(FinalGoalCell, pawn.Map, fast:true))
            {
                return FailedGrab;
            }
            else
            {
                Messages.Message("XMT_NoRoomToCocoon".Translate(), MessageTypeDefOf.NegativeEvent);
                return true;
            }
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFailCondition(IsNoLongerValidTarget);

            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnAggroMentalState(TargetIndex.A);
           
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return AttemptGrab();
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return AttemptCocoon();
        }


        private Toil AttemptCocoon()
        {
            Toil toil = ToilMaker.MakeToil("FormingCocoon");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                if(pawn.Position.GetTerrain(pawn.Map) != InternalDefOf.HiveFloor)
                {
                    CocoonTicksFinish = XenoBuildingDefOf.Hivemass.statBases.GetStatValueFromList(StatDefOf.WorkToBuild, 10f);
                }
            };
            toil.tickAction = delegate
            {
                Pawn actor = pawn;
                CocoonTicks += 1;
                CocoonProgress = (CocoonTicks / CocoonTicksFinish);
                if (actor?.needs?.food != null)
                {
                    actor.needs.food.CurLevel = actor.needs.food.CurLevel - XMTHiveUtility.HiveHungerCostPerTick;

                    if (actor.needs.food.Starving)
                    {
                        Hediff Malnutrition = actor.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);

                        if (Malnutrition != null)
                        {
                            Malnutrition.Severity += 0.001f;
                            actor.workSettings?.Disable(WorkTypeDefOf.Construction);
                        }
                        ReadyForNextToil();
                        return;
                    }
                }

                if (CocoonTicks >= CocoonTicksFinish)
                {
                    if (Victim == null || Victim.Dead || pawn.carryTracker.CarriedThing != Victim)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    CompMatureMorph matureMorph = pawn.GetMorphComp();
                    if (matureMorph != null)
                    {
                        Victim.MapHeld.designationManager.TryRemoveDesignationOn(Victim, XenoWorkDefOf.XMT_Abduct);
                        matureMorph.TryCocooning(Victim);
                    }
                    ReadyForNextToil();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => CocoonProgress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            return toil;
        }
    }
}
