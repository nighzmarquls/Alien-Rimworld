using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_ElectroMetabolicCharge : JobDriver
    {
        private const float FoodCostPerTick = 0.00024f;
        private const float EnergyGainPerFood = 25f;
        private const float MinFoodLevel = 0.1f;
        private const int MechWaitRefreshInterval = 120;

        private static NeedDef mechEnergyDef;
        private Mote mechChargingMote;
        private Mote pawnChargingMote;
        private int nextMechWaitRefreshTick;

        private Pawn Mech => (Pawn)TargetThingA;

        private static NeedDef MechEnergyDef
        {
            get
            {
                if (mechEnergyDef == null)
                {
                    mechEnergyDef = DefDatabase<NeedDef>.GetNamedSilentFail("MechEnergy");
                }
                return mechEnergyDef;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.needs?.food == null || pawn.needs.food.CurLevel <= MinFoodLevel);
            this.FailOn(() => !TryGetMechEnergy(Mech, out Need energy) || energy.CurLevelPercentage >= TargetChargeLevel(Mech));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            Toil charge = ToilMaker.MakeToil("ElectroMetabolicCharge");
            charge.initAction = delegate
            {
                ForceMechWait();
            };
            charge.tickAction = delegate
            {
                if (!TryGetMechEnergy(Mech, out Need energy))
                {
                    ReadyForNextToil();
                    return;
                }

                float targetLevel = TargetChargeLevel(Mech);
                if (energy.CurLevelPercentage >= targetLevel || pawn.needs.food.CurLevel <= MinFoodLevel)
                {
                    ReadyForNextToil();
                    return;
                }

                float foodToSpend = Mathf.Min(FoodCostPerTick, pawn.needs.food.CurLevel - MinFoodLevel);
                float energyToGain = Mathf.Min(foodToSpend * EnergyGainPerFood, energy.MaxLevel - energy.CurLevel);

                pawn.needs.food.CurLevel -= foodToSpend;
                energy.CurLevel += energyToGain;
                ForceMechWait();
                MaintainChargingMote(Mech, ref mechChargingMote);
                MaintainChargingMote(pawn, ref pawnChargingMote);
            };
            charge.WithProgressBar(TargetIndex.A, () => TryGetMechEnergy(Mech, out Need energy) ? energy.CurLevelPercentage : 0f);
            charge.defaultCompleteMode = ToilCompleteMode.Never;
            yield return charge;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        internal static bool TryGetMechEnergy(Pawn mech, out Need energy)
        {
            energy = null;
            NeedDef needDef = MechEnergyDef;
            if (mech?.needs == null || needDef == null)
            {
                return false;
            }

            energy = mech.needs.TryGetNeed(needDef);
            return energy != null;
        }

        internal static float TargetChargeLevel(Pawn mech)
        {
            MechanitorControlGroup controlGroup = MechanitorUtility.GetMechControlGroup(mech);
            if (controlGroup == null)
            {
                return MechanitorControlGroup.DefaultMechRechargeThresholds.max;
            }

            if (controlGroup.WorkMode != null && controlGroup.WorkMode.ignoreGroupChargeLimits)
            {
                return 1f;
            }

            return controlGroup.mechRechargeThresholds.max;
        }

        internal static float StartChargeLevel(Pawn mech)
        {
            MechanitorControlGroup controlGroup = MechanitorUtility.GetMechControlGroup(mech);
            if (controlGroup == null)
            {
                return MechanitorControlGroup.DefaultMechRechargeThresholds.min;
            }

            if (controlGroup.WorkMode != null && controlGroup.WorkMode.ignoreGroupChargeLimits)
            {
                return 1f;
            }

            return controlGroup.mechRechargeThresholds.min;
        }

        internal static bool WantsElectroMetabolicCharge(Pawn mech)
        {
            if (!TryGetMechEnergy(mech, out Need energy))
            {
                return false;
            }

            return energy.CurLevelPercentage < StartChargeLevel(mech);
        }

        private void ForceMechWait()
        {
            if (Mech == null || Mech.Destroyed || Find.TickManager.TicksGame < nextMechWaitRefreshTick)
            {
                return;
            }

            PawnUtility.ForceWait(Mech, MechWaitRefreshInterval + 60, pawn, maintainPosture: true, maintainSleep: false);
            nextMechWaitRefreshTick = Find.TickManager.TicksGame + MechWaitRefreshInterval;
        }

        private void MaintainChargingMote(Thing target, ref Mote mote)
        {
            if (target == null || target.Map == null)
            {
                return;
            }

            if (mote == null || mote.Destroyed)
            {
                mote = MoteMaker.MakeAttachedOverlay(target, ThingDefOf.Mote_MechCharging, Vector3.zero, 1f, -1f);
            }
            else
            {
                mote.Maintain();
            }
        }
    }
}
