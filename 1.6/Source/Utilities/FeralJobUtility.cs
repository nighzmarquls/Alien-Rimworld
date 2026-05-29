
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FeralJobUtility
    {
        internal static bool IsPlaceAvailableForJobBy(Pawn pawn, IntVec3 cell)
        {
            if (ForbidUtility.CaresAboutForbidden(pawn, false))
            {
                if (cell.IsForbidden(pawn))
                {
                    return false;
                }
                if (!cell.InAllowedArea(pawn))
                {
                    return false;
                }
            }

            if(pawn.MapHeld.physicalInteractionReservationManager.IsReserved(cell))
            {
                return false;
            }

            if (pawn.Faction != null)
            {
                return !pawn.MapHeld.reservationManager.IsReserved(cell);
            }
            return true;
        }

        internal static bool IsThingAvailableForJobBy(Pawn pawn, Thing thing)
        {
            if (pawn == null)
            {
                return false;
            }

            if(pawn.MapHeld == null)
            {
                return false;
            }

            if (ForbidUtility.CaresAboutForbidden(pawn, false))
            {
                if (thing.IsForbidden(pawn))
                {
                    return false;
                }
                if (!thing.Position.InAllowedArea(pawn))
                {
                    return false;
                }
            }

            if (pawn.Faction != null && pawn.MapHeld.reservationManager.IsReserved(thing))
            {
                return false;
            }
            else if (pawn.MapHeld.physicalInteractionReservationManager.IsReserved(thing))
            {
                return false;
            }
            
            return true;
        }

        internal static void ReservePlaceForJob(Pawn pawn, Job job, IntVec3 place)
        {
            pawn.Reserve(place, job);
        }

        internal static void ReserveThingForJob(Pawn pawn, Job job, Thing thing)
        {
            pawn.MapHeld.physicalInteractionReservationManager.Reserve(pawn,job,thing);
            if (pawn.Faction != null)
            {
                pawn.MapHeld.reservationManager.Reserve(pawn, job, thing);
            }
        }

        internal static void ClearFeralJobReservationsForTarget(Map map, Thing target)
        {
            if (map == null || target == null)
            {
                return;
            }

            if (map.reservationManager.IsReserved(target))
            {
                map.reservationManager.ReleaseAllForTarget(target);
            }

            if (map.physicalInteractionReservationManager.IsReserved(target))
            {
                map.physicalInteractionReservationManager.ReleaseAllForTarget(target);
            }
        }

        internal static void ClearFeralJobReservationsForTarget(Thing target)
        {
            ClearFeralJobReservationsForTarget(target?.MapHeld, target);
        }

        internal static void ClearFeralJobReservationsForTarget(Map map, LocalTargetInfo target)
        {
            if (map == null || !target.IsValid)
            {
                return;
            }

            if (target.Thing != null)
            {
                ClearFeralJobReservationsForTarget(map, target.Thing);
            }
        }

        internal static void ClearFeralJobReservationsClaimedBy(Map map, Pawn reserver)
        {
            if (map == null || reserver == null)
            {
                return;
            }

            map.reservationManager.ReleaseAllClaimedBy(reserver);
            map.physicalInteractionReservationManager.ReleaseAllClaimedBy(reserver);
        }

        internal static void ClearFeralJobReservationsClaimedBy(Pawn reserver)
        {
            ClearFeralJobReservationsClaimedBy(reserver?.MapHeld, reserver);
        }
    }
}
