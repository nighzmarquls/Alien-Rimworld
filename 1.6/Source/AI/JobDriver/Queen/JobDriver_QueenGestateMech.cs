using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    internal class JobDriver_QueenGestateMech : JobDriver
    {
        private float workDone;

        private RecipeDef Recipe => QueenMechGestationUtility.RecipeForProduct(job.thingDefToCarry);
        private IntVec3 GestationCell => job.GetTarget(TargetIndex.A).Cell;
        private IntVec3 LayingCell => job.GetTarget(TargetIndex.B).Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!TryFindGestationCells(out IntVec3 gestationCell, out IntVec3 layingCell))
            {
                return false;
            }

            job.SetTarget(TargetIndex.A, gestationCell);
            job.SetTarget(TargetIndex.B, layingCell);
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() =>
            {
                RecipeDef recipe = Recipe;
                return recipe == null || !QueenMechGestationUtility.CanGestate(pawn, recipe, out string _);
            });

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            Toil gestate = ToilMaker.MakeToil("QueenGestateMech");
            gestate.atomicWithPrevious = true;
            gestate.handlingFacing = true;
            gestate.initAction = delegate
            {
                pawn.Rotation = GetLayingFacing(GestationCell);
            };
            gestate.tickAction = delegate
            {
                pawn.Rotation = GetLayingFacing(GestationCell);
                workDone += 1f / Mathf.Max(1, gestate.defaultDuration);
            };
            gestate.defaultCompleteMode = ToilCompleteMode.Delay;
            gestate.defaultDuration = QueenMechGestationUtility.GestationTicks(pawn, Recipe);
            gestate.WithProgressBar(TargetIndex.A, () => workDone, interpolateBetweenActorAndTarget: true);
            gestate.PlaySustainerOrSound(() => SoundDefOf.Vomit);
            yield return gestate;

            yield return Toils_General.Do(FinishGestation);
        }

        private bool TryFindGestationCells(out IntVec3 gestationCell, out IntVec3 layingCell)
        {
            Map map = pawn.Map;
            LocalTargetInfo selectedTarget = job.GetTarget(TargetIndex.A);
            if (selectedTarget.IsValid && CanUseGestationCell(selectedTarget.Cell, map) && TryFindLayingCell(selectedTarget.Cell, out layingCell))
            {
                gestationCell = selectedTarget.Cell;
                return true;
            }

            List<IntVec3> candidates = GenRadial.RadialCellsAround(pawn.Position, 2.9f, false).ToList();
            candidates.Shuffle();
            candidates.SortBy(cell => cell.DistanceToSquared(pawn.Position));

            foreach (IntVec3 candidate in candidates)
            {
                if (!CanUseGestationCell(candidate, map))
                {
                    continue;
                }

                if (TryFindLayingCell(candidate, out layingCell))
                {
                    gestationCell = candidate;
                    return true;
                }
            }

            gestationCell = IntVec3.Invalid;
            layingCell = IntVec3.Invalid;
            return false;
        }

        private bool TryFindLayingCell(IntVec3 gestationCell, out IntVec3 layingCell)
        {
            Map map = pawn.Map;
            List<IntVec3> adjacentCells = GenRadial.RadialCellsAround(gestationCell, 1f, false).ToList();
            adjacentCells.Shuffle();
            adjacentCells.SortBy(cell => cell == pawn.Position ? 0 : 1);
            foreach (IntVec3 adjacentCell in adjacentCells)
            {
                if (adjacentCell.InBounds(map)
                    && adjacentCell.Standable(map)
                    && (adjacentCell == pawn.Position || adjacentCell.GetFirstPawn(map) == null)
                    && map.reachability.CanReach(pawn.Position, adjacentCell, PathEndMode.OnCell, TraverseMode.PassDoors, Danger.Deadly))
                {
                    layingCell = adjacentCell;
                    return true;
                }
            }

            layingCell = IntVec3.Invalid;
            return false;
        }

        private bool CanUseGestationCell(IntVec3 cell, Map map)
        {
            return cell.InBounds(map)
                && cell.Standable(map)
                && cell.GetFirstPawn(map) == null
                && cell.GetEdifice(map) == null;
        }

        private Rot4 GetLayingFacing(IntVec3 targetCell)
        {
            IntVec3 dif = targetCell - pawn.Position;
            if (dif.x < 0)
            {
                return Rot4.East;
            }
            if (dif.x > 0)
            {
                return Rot4.West;
            }
            if (dif.z < 0)
            {
                return Rot4.North;
            }
            return Rot4.South;
        }

        private void FinishGestation()
        {
            RecipeDef recipe = Recipe;
            if (recipe == null)
            {
                return;
            }

            if (!QueenMechGestationUtility.TryFinishGestation(pawn, recipe, GestationCell, pawn.Map, out Pawn _, out string reason))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                }
            }
        }
    }
}
