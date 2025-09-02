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

    public class JobDriver_AbductHost : JobDriver_ClimbToPosition
    {

        private const TargetIndex HaulableInd = TargetIndex.A;

        private const TargetIndex StoreCellInd = TargetIndex.B;

        private const float GrabTicksFinish = 30;
        private float GrabTicks = 0;
        private float GrabProgress = 0;

        private float CocoonTicksFinish = 350;
        private float CocoonTicks = 0;
        private float CocoonProgress = 0;

        private bool FailedGrab = false;

        protected override IntVec3 FinalGoalCell => job.GetTarget(TargetIndex.B).Cell;
        public Thing ToHaul => job.GetTarget(TargetIndex.A).Thing;
        public Pawn Victim => job.GetTarget(TargetIndex.A).Pawn;

        protected virtual bool DropCarriedThingIfNotTarget => false;

        public override void ExposeData()
        {
            base.ExposeData();
      
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public bool IsNoLongerValidTarget()
        {
            return FailedGrab;
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


        private Toil AttemptGrab()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                CompMatureMorph matureMorph = pawn.GetMorphComp();
                if (matureMorph != null)
                {
                    if(!matureMorph.InitiateGrabCheck(Victim))
                    {
                        FailedGrab = true;
                    }
                }
            };
            toil.tickAction = delegate
            {
                GrabTicks+= 1;
                GrabProgress = (GrabTicks / GrabTicksFinish);
                if (GrabTicks >= GrabTicksFinish)
                {
                    ReadyForNextToil();
                }
                
            };
            toil.AddFinishAction(delegate
            {
                
                CompMatureMorph matureMorph = pawn.GetMorphComp();
                if (matureMorph != null)
                {
                    if (GrabProgress >= 1 && !FailedGrab)
                    {
                        matureMorph.TryGrab(Victim);
                    }
                }
               
            });
            toil.WithProgressBar(TargetIndex.A, () => GrabProgress);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private Toil AttemptCocoon()
        {
            Toil toil = ToilMaker.MakeToil("FormingCocoon");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                if(pawn.Position.GetTerrain(pawn.Map) != InternalDefOf.HiveFloor)
                {
                    CocoonTicksFinish = InternalDefOf.Hivemass.statBases.GetStatValueFromList(StatDefOf.WorkToBuild, 10f);
                }
            };
            toil.tickAction = delegate
            {
                Pawn actor = pawn;
                CocoonTicks += 1;
                CocoonProgress = (CocoonTicks / CocoonTicksFinish);
                if (actor?.needs?.food != null)
                {
                    actor.needs.food.CurLevel = actor.needs.food.CurLevel - HiveUtility.HiveHungerCostPerTick;

                    if (actor.needs.food.Starving)
                    {
                        Hediff Malnutrition = actor.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);

                        if (Malnutrition != null)
                        {
                            Malnutrition.Severity += 0.001f;
                            actor.workSettings.Disable(WorkTypeDefOf.Construction);
                        }
                        ReadyForNextToil();
                        return;
                    }
                }

                if (CocoonTicks >= CocoonTicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (CocoonProgress >= 1)
                {
                    CompMatureMorph matureMorph = pawn.GetMorphComp();
                    if (matureMorph != null)
                    {
                        Victim.MapHeld.designationManager.TryRemoveDesignationOn(Victim, XenoWorkDefOf.XMT_Abduct);
                        matureMorph.TryCocooning(Victim);
                    }
                }
            });
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithProgressBar(TargetIndex.A, () => CocoonProgress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            return toil;
        }
    }
}
