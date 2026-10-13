using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype { 
    internal class JobDriver_PruneLarder : JobDriver
    {
        private float workDone;

        protected float xpPerTick = 0.085f;

        protected const TargetIndex LarderInd = TargetIndex.A;

        protected MeatballLarder Larder => (MeatballLarder)job.targetA.Thing;
        public static float WorkDonePerTick(Pawn actor, CompMeatBall meatball)
        {
            return actor.GetStatValue(StatDefOf.PlantWorkSpeed);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo target = job.GetTarget(TargetIndex.A);
            if (target.IsValid && !pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return toil;
            Toil cut = ToilMaker.MakeToil("MakeNewToils");
            cut.tickAction = delegate
            {
                Pawn actor = cut.actor;
                if (actor.skills != null)
                {
                    actor.skills.Learn(SkillDefOf.Plants, xpPerTick);
                }

                CompMeatBall meatBall = Larder.GetComp<CompMeatBall>();
                workDone += WorkDonePerTick(actor, meatBall);
                if (pawn.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.01f, InternalDefOf.NestTending);
                    pawn.needs.joy.GainJoy(0.01f, ExternalDefOf.Gaming_Dexterity);
                }
                if (workDone >= meatBall.harvestWork)
                {
                    meatBall.PruneMeatBall(pawn);
                    workDone = 0f;
                    
                    ReadyForNextToil();
                }
            };
            cut.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            cut.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            cut.defaultCompleteMode = ToilCompleteMode.Never;
            cut.WithEffect(EffecterDefOf.Surgery, TargetIndex.A);
            cut.WithProgressBar(TargetIndex.A, () => workDone / Larder.harvestWork, interpolateBetweenActorAndTarget: true);
            cut.PlaySustainerOrSound(() => SoundDefOf.Recipe_Surgery);
            cut.activeSkill = () => SkillDefOf.Plants;

            yield return cut;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workDone, "workDone", 0f);
        }
    }
}
