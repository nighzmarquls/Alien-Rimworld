
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_AdvancedTame : JobDriver_InteractAnimal
    {
       

        protected override bool CanInteractNow => !TameUtility.TriedToTameTooRecently(Target);
        protected Pawn Target => HoldingPlatform == null ? (Pawn)job.targetA.Thing : HoldingPlatform.HeldPawn;
        protected virtual Building_HoldingPlatform HoldingPlatform => job.targetA.Thing as Building_HoldingPlatform;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Thing, job, 1, -1, null, errorOnFailed);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (job.GetTarget(TargetIndex.A).Thing is Pawn)
            {
                Func<bool> noLongerDesignated = () => !Target.IsAdvancedTameable();
                if (job.GetTarget(TargetIndex.C).HasThing)
                {
                    yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.C).FailOn(noLongerDesignated);
                    yield return Toils_Haul.TakeToInventory(TargetIndex.C, job.count).FailOn(noLongerDesignated);
                }

                foreach (Toil item in MakePawnToils())
                {
                    item.FailOn(noLongerDesignated);
                    yield return item;
                }
                yield return TamingUtility.TryRecruitPawn(TargetIndex.A);
            }
            else
            {
                Func<bool> noLongerDesignated = () => !Target.IsAdvancedTameable();
                if (job.GetTarget(TargetIndex.C).HasThing)
                {
                    yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.C).FailOn(noLongerDesignated);
                    yield return Toils_Haul.TakeToInventory(TargetIndex.C, job.count).FailOn(noLongerDesignated);
                }

                foreach (Toil item in MakeHoldingPlatformToils())
                {
                    item.FailOn(noLongerDesignated);
                    yield return item;
                }
                yield return TamingUtility.TryRecruitPawnOnPlatform(TargetIndex.A);
            }

        }

        IEnumerable<Toil> MakePawnToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !CanInteractNow);
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);

        }

        IEnumerable<Toil> MakeHoldingPlatformToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return TamingUtility.InteractWithTargetPawn(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOn(() => !CanInteractNow);
            yield return Toils_Interpersonal.SetLastInteractTime(TargetIndex.A);
        }
    }
}
