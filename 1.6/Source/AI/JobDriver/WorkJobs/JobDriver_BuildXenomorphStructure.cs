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
        private ThingDef buildingDef;
        private float TicksFinish => (BuildingDef != null ? BuildingDef.statBases.GetStatValueFromList(StatDefOf.WorkToBuild,250) : 60) + IncreasedDifficulty;
        private ThingDef BuildingDef => buildingDef ?? job?.plantDefToSow;
        private float Ticks = 0;
        private float Progress = 0;
        private bool constructionCompleted;
        private bool workStarted;
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
            IntVec3 buildCell = job.GetTarget(TargetIndex.A).Cell;
            if (pawn.MapHeld != null && pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, buildCell))
            {
                return true;
            }

            return FeralJobUtility.ReservePlaceForJob(pawn, job, buildCell);
        }

        public bool IsNoLongerValidTarget()
        {
            return BuildingDef == null;
        }

        internal void NotifyCleanup(JobCondition condition)
        {
            if (condition == JobCondition.Incompletable && !workStarted && !constructionCompleted)
            {
                pawn.GetMorphComp()?.NotifyPathFailure(new LocalTargetInfo(BuildCell), job);
                XMTNestBuildingUtility.NotifyHiveBuildJobFailed(pawn, BuildCell, BuildingDef, NestBuildStage.ClaimFloor);
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            buildingDef = job?.plantDefToSow;
            AddFailCondition(IsNoLongerValidTarget);

            if (!pawn.Position.AdjacentTo8WayOrInside(BuildCell))
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            }

            yield return DoToilBuilding();
        }

        private Toil DoToilBuilding()
        {
            Toil toil = ToilMaker.MakeToil("AttemptBuilding");
            toil.atomicWithPrevious = true;
            toil.initAction = delegate
            {
                workStarted = true;
                buildingDef = job?.plantDefToSow ?? buildingDef;
                Thing obstruction = BuildCell.GetEdifice(pawn.Map);
                if (obstruction != null)
                {
                    IncreasedDifficulty = obstruction.HitPoints / 5;
                }

                if (Prefs.DevMode && job != null && job.playerForced)
                {
                    Messages.Message("Forced hive build started: " + BuildingDef + " at " + BuildCell + ".", MessageTypeDefOf.NeutralEvent, false);
                }
            };
            toil.tickIntervalAction = delegate (int delta)
            {
                Ticks += (pawn.GetStatValue(StatDefOf.ConstructionSpeedFactor)*delta);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Construction, xpPerTick * delta);
                }
                Progress = (Ticks / TicksFinish);

                if (!BioUtility.PerformBioconstructionCost(pawn))
                {
                    if (Prefs.DevMode && job != null && job.playerForced)
                    {
                        Messages.Message("Forced hive build failed: bioconstruction cost at " + BuildCell + ".", MessageTypeDefOf.RejectInput, false);
                    }
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (Ticks >= TicksFinish)
                {
                    CompleteConstruction();
                }

            };
            toil.WithProgressBar(TargetIndex.A, () => Progress);
            toil.WithEffect(InternalDefOf.ResinBuild, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            return toil;
        }

        private void CompleteConstruction()
        {
            if (constructionCompleted)
            {
                return;
            }

            constructionCompleted = true;
            ThingDef defToBuild = BuildingDef;
            if (defToBuild == null || pawn?.Map == null || !BuildCell.InBounds(pawn.Map))
            {
                if (Prefs.DevMode && job != null && job.playerForced)
                {
                    Messages.Message("Forced hive build failed: invalid target at " + BuildCell + ".", MessageTypeDefOf.RejectInput, false);
                }
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if (pawn.Position == BuildCell)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = BuildCell + direction;
                    if (adjacent.InBounds(pawn.Map) && adjacent.Standable(pawn.Map) && adjacent.GetFirstPawn(pawn.Map) == null)
                    {
                        pawn.Position = adjacent;
                        break;
                    }
                }
            }

            Rot4 rotation = Rot4.North;
            if (defToBuild.building != null && defToBuild.building.isAttachment)
            {
                foreach (IntVec3 direction in GenAdj.CardinalDirections)
                {
                    IntVec3 adjacent = BuildCell + direction;
                    if (!adjacent.InBounds(pawn.Map))
                    {
                        continue;
                    }

                    Building wall = adjacent.GetEdifice(pawn.Map);
                    if (wall?.def?.building != null && wall.def.building.supportsWallAttachments)
                    {
                        rotation = Rot4.FromIntVec3(direction);
                        break;
                    }
                }
            }

            Building blocker = BuildCell.GetEdifice(pawn.Map);
            if (blocker != null)
            {
                XMT_SabotageReplacementPair replacement = null;
                bool hasReplacement = XMTSabotageReplacementUtility.TryGetReplacement(blocker.def, out replacement);

                if (hasReplacement && replacement.replacementFloorThing != null)
                {
                    GenSpawn.Spawn(replacement.replacementFloorThing, BuildCell, pawn.Map, WipeMode.VanishOrMoveAside);
                    pawn.Map.terrainGrid.SetTerrain(BuildCell, InternalDefOf.HiveFloor);
                }
            }
            Building finishedBuilding = GenSpawn.Spawn(defToBuild, BuildCell, pawn.Map, rotation, WipeMode.FullRefund) as Building;
            finishedBuilding?.SetFaction(pawn.Faction);
            XMTNestBuildingUtility.NotifyHiveBuildJobSucceeded(pawn, BuildCell, defToBuild, NestBuildStage.ClaimFloor);
            XMTNestBuildingUtility.NotifyHiveConstructionCompleted(pawn.Map, BuildCell, defToBuild);
            if (Prefs.DevMode && job != null && job.playerForced)
            {
                Messages.Message("Forced hive build completed: " + defToBuild + " at " + BuildCell + ".", MessageTypeDefOf.TaskCompletion, false);
            }
            EndJobWith(JobCondition.Succeeded);
        }
    }
}
