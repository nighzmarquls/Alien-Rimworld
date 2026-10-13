using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobDriver_DoContainedBill : JobDriver_DoBill
    {
        private Thing OuterContainer => job.source as Thing;
        private Building_BioContainer Container => BioContainerUtility.Resolve(OuterContainer);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!base.TryMakePreToilReservations(errorOnFailed) || Container == null || Container.ContainedThing != null)
            {
                return false;
            }
            return !OuterContainer.Spawned || pawn.Reserve(OuterContainer, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFailCondition(() => Container == null || Container.Destroyed || Container.ContainedThing != null);

            Toil resumeBill = ToilMaker.MakeToil("BeginContainedSurgery");
            resumeBill.initAction = () => job.SetTarget(TargetIndex.C, LocalTargetInfo.Invalid);
            resumeBill.defaultCompleteMode = ToilCompleteMode.Instant;

            Toil setContainerTarget = ToilMaker.MakeToil("SelectBioContainer");
            setContainerTarget.initAction = () => job.SetTarget(TargetIndex.C, OuterContainer);
            setContainerTarget.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return setContainerTarget;

            Toil startCarry = Toils_Haul.StartCarryThing(TargetIndex.C, reserve: false, canTakeFromInventory: true);
            yield return Toils_Jump.JumpIf(startCarry, () => !OuterContainer.Spawned && OuterContainer.ParentHolder == pawn.inventory);
            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch).FailOnDestroyedOrNull(TargetIndex.C);

            Toil uninstallContainer = ToilMaker.MakeToil("UninstallBioContainer");
            uninstallContainer.initAction = delegate
            {
                if (job.GetTarget(TargetIndex.C).Thing is Building_BioContainer building && building.Spawned)
                {
                    MinifiedThing minified = MinifyUtility.Uninstall(building);
                    job.SetTarget(TargetIndex.C, minified);
                    job.source = minified;
                }
            };
            uninstallContainer.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return uninstallContainer;
            yield return startCarry;

            Toil setWorkCell = ToilMaker.MakeToil("ApproachBioContainerSurgerySite");
            setWorkCell.initAction = delegate
            {
                if (!BioContainerUtility.TryFindSurgicalCells(pawn, job.GetTarget(TargetIndex.A).Thing,
                    OuterContainer, out IntVec3 workCell, out IntVec3 _) ||
                    !FeralJobUtility.ReservePlaceForJob(pawn, job, workCell))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                job.SetTarget(TargetIndex.C, workCell);
            };
            setWorkCell.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return setWorkCell;
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);

            Toil setInstallCell = ToilMaker.MakeToil("SelectBioContainerInstallCell");
            setInstallCell.initAction = delegate
            {
                if (!BioContainerUtility.TryFindSurgicalInstallCell(pawn, job.GetTarget(TargetIndex.A).Thing,
                    OuterContainer, pawn.Position, out IntVec3 installCell) ||
                    !FeralJobUtility.ReservePlaceForJob(pawn, job, installCell))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                job.SetTarget(TargetIndex.C, installCell);
            };
            setInstallCell.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return setInstallCell;

            Toil placeContainer = ToilMaker.MakeToil("PlaceBioContainerForSurgery");
            placeContainer.initAction = delegate
            {
                Building_BioContainer placed = BioContainerUtility.InstallCarriedContainer(pawn,
                    job.GetTarget(TargetIndex.C).Cell);
                if (placed == null)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _, null);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                job.source = placed;
            };
            placeContainer.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return placeContainer;
            yield return resumeBill;

            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }
        }
    }
}
