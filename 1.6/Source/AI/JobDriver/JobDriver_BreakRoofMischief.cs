using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class JobDriver_BreakRoofMischief : JobDriver
    {
        private const int BreakTicks = 180;

        private IntVec3 RoofCell => job.GetTarget(TargetIndex.A).Cell;
        private IntVec3 ApproachCell => job.GetTarget(TargetIndex.B).Cell;
        private bool loggedFailCondition;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message("[Alien|Rimworld] Roof break mischief TryMakePreToilReservations for " + pawn + ": roof=" + RoofCell + ", approach=" + ApproachCell + ", Job=" + job);

            if (!ApproachCell.IsValid)
            {
                Log.Message("[Alien|Rimworld] Roof break mischief reservation failed for " + pawn + ": invalid approach cell. Job=" + job);
                return false;
            }

            if (!FeralJobUtility.ReservePlaceForJob(pawn, job, ApproachCell))
            {
                Log.Message("[Alien|Rimworld] Roof break mischief reservation failed for " + pawn + ": could not reserve approach " + ApproachCell + ". Job=" + job);
                return false;
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(ShouldFailRoofBreakMischief);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            Toil breakRoof = Toils_General.Wait(BreakTicks);
            breakRoof.WithProgressBarToilDelay(TargetIndex.A);
            yield return breakRoof;

            yield return Toils_General.Do(delegate
            {
                Map map = pawn.Map;
                RoofDef roof = map.roofGrid.RoofAt(RoofCell);
                if (roof == null || roof.isThickRoof)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                CollapseRoofCell(map);
                DropPawnThroughOpenedRoof(map);
                XMTMischiefUtility.NotifyMischiefCompleted(pawn, 0.12f);

                EndJobWith(JobCondition.Succeeded);
                return;
   
            });
        }

        private bool ShouldFailRoofBreakMischief()
        {
            string reason = RoofBreakFailReason();
            if (reason == null)
            {
                return false;
            }

            if (!loggedFailCondition)
            {
                loggedFailCondition = true;
                Log.Message("[Alien|Rimworld] Roof break mischief failed for " + pawn + ": " + reason + ". Job=" + job);
            }

            return true;
        }

        private string RoofBreakFailReason()
        {
            if (pawn?.Map == null)
            {
                return "pawn has no map";
            }

            if (ApproachCell == RoofCell)
            {
                return "approach cell equals roof cell";
            }

            if (pawn.Map.roofGrid.RoofAt(RoofCell) == null)
            {
                return "roof already gone";
            }

            if (!XMTMischiefUtility.HasRemovableRoofAt(pawn.Map, RoofCell))
            {
                return "roof is not removable";
            }

            if (!XMTMischiefUtility.IsDarkEnoughForMischief(ApproachCell, pawn.Map))
            {
                return "approach cell is too lit for stealth-safe roof mischief";
            }

            return null;
        }

        private void CollapseRoofCell(Map map)
        {
            List<Thing> crushedThings = new List<Thing>();
            RoofCollapserImmediate.DropRoofInCells(RoofCell, map, crushedThings);
            if (map.roofGrid.RoofAt(RoofCell) != null)
            {
                Log.Message("[Alien|Rimworld] Roof collapse did not clear roof at " + RoofCell + "; clearing roof after applying collapse consequences.");
                map.roofGrid.SetRoof(RoofCell, null);
            }
        }

        private void DropPawnThroughOpenedRoof(Map map)
        {
            if (pawn == null || !pawn.Spawned || map == null || !RoofCell.Standable(map))
            {
                return;
            }

            pawn.pather.StopDead();
            pawn.Position = RoofCell;
            pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
            map.mapDrawer.MapMeshDirty(RoofCell, MapMeshFlagDefOf.Things);
        }
    }
}
