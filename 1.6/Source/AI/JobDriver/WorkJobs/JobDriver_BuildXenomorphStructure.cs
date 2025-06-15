using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using Verse.Noise;

namespace Xenomorphtype { 
    internal class JobDriver_BuildXenomorphStructure : JobDriver
    {
        private float IncreasedDifficulty = 0;
        private float TicksFinish => (pawn.CurJob.plantDefToSow != null ? pawn.CurJob.plantDefToSow.statBases.GetStatValueFromList(StatDefOf.WorkToBuild,250) : 60) + IncreasedDifficulty;
        private ThingDef BuildingDef => pawn.CurJob.plantDefToSow;
        private float Ticks = 0;
        private float Progress = 0;
        protected float xpPerTick = 0.085f;
        
        public IntVec3 BuildCell
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Cell;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        public bool IsNoLongerValidTarget()
        {
            return BuildingDef == null;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(IsNoLongerValidTarget);

            Toil toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch).FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return DoToilBuilding();
        }

        private Toil DoToilBuilding()
        {
            Toil toil = ToilMaker.MakeToil("AttemptGrab");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                Thing obstruction = BuildCell.GetEdifice(pawn.Map);
                if (obstruction != null)
                {
                    IncreasedDifficulty = obstruction.HitPoints / 5;
                }
            };
            toil.tickAction = delegate
            {
                Ticks += pawn.GetStatValue(StatDefOf.ConstructionSpeedFactor);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Construction, xpPerTick);
                }
                Progress = (Ticks / TicksFinish);

                if (pawn?.needs?.food != null)
                {
                    pawn.needs.food.CurLevel = pawn.needs.food.CurLevel - HiveUtility.HiveHungerCostPerTick;

                    if (pawn.needs.food.Starving)
                    {
                        Hediff Malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);

                        if (Malnutrition != null)
                        {
                            Malnutrition.Severity += 0.001f;
                            pawn?.workSettings.Disable(WorkTypeDefOf.Construction);
                        }
                        ReadyForNextToil();
                        return;
                    }
                }
                if (Ticks >= TicksFinish)
                {
                    ReadyForNextToil();
                }

            };
            toil.AddFinishAction(delegate
            {
                if (Progress >= 1)
                {
                    Building finishedBuilding = GenSpawn.Spawn(BuildingDef, BuildCell, pawn.Map, WipeMode.FullRefund) as Building;
                    finishedBuilding.SetFaction(pawn.Faction);
                }
            });
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }
    }
}
