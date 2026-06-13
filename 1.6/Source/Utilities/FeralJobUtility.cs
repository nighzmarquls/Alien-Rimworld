
using RimWorld;
using System;
using System.Collections;
using System.Reflection;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FeralJobUtility
    {
        private static readonly FieldInfo reservationManagerReservationsField = typeof(ReservationManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo physicalInteractionReservationsField = typeof(PhysicalInteractionReservationManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic);

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

            if (pawn.MapHeld.physicalInteractionReservationManager.IsReserved(cell) && !pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, cell))
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
            if (pawn == null || thing == null)
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

        internal static bool ReservePlaceForJob(Pawn pawn, Job job, IntVec3 place)
        {
            if (pawn == null || job == null || pawn.MapHeld == null || !place.InBounds(pawn.MapHeld))
            {
                return false;
            }

            if (!pawn.MapHeld.physicalInteractionReservationManager.IsReservedBy(pawn, place))
            {
                if (pawn.MapHeld.physicalInteractionReservationManager.IsReserved(place))
                {
                    return false;
                }

                pawn.MapHeld.physicalInteractionReservationManager.Reserve(pawn, job, place);
            }

            if (pawn.Faction != null && !pawn.MapHeld.reservationManager.IsReserved(place))
            {
                return pawn.Reserve(place, job);
            }

            return true;
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

        internal static void ClearFeralJobReservationsForTarget(Map map, IntVec3 target)
        {
            if (map == null || !target.IsValid || !target.InBounds(map))
            {
                return;
            }

            if (map.physicalInteractionReservationManager.IsReserved(target))
            {
                map.physicalInteractionReservationManager.ReleaseAllForTarget(target);
            }
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
                return;
            }

            if (target.Cell.IsValid)
            {
                ClearFeralJobReservationsForTarget(map, target.Cell);
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

        internal static void ForceClearFeralJobReservationsClaimedBy(Map map, Pawn reserver)
        {
            if (map == null || reserver == null)
            {
                return;
            }

            ClearFeralJobReservationsClaimedBy(map, reserver);
            RemoveClaimedReservations(reservationManagerReservationsField?.GetValue(map.reservationManager) as IList, reserver);
            RemoveClaimedReservations(physicalInteractionReservationsField?.GetValue(map.physicalInteractionReservationManager) as IList, reserver);
        }

        private static void RemoveClaimedReservations(IList reservations, Pawn reserver)
        {
            if (reservations == null || reserver == null)
            {
                return;
            }

            for (int i = reservations.Count - 1; i >= 0; i--)
            {
                object reservation = reservations[i];
                FieldInfo claimantField = reservation?.GetType().GetField("claimant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (claimantField?.GetValue(reservation) == reserver)
                {
                    reservations.RemoveAt(i);
                }
            }
        }

        internal static void ClearFeralJobReservationsClaimedBy(Pawn reserver)
        {
            ClearFeralJobReservationsClaimedBy(reserver?.MapHeld, reserver);
        }

        internal static void ClearFeralPhysicalInteractionReservationsClaimedBy(Map map, Pawn reserver, Job job)
        {
            if (map == null || reserver == null || job == null)
            {
                return;
            }

            map.physicalInteractionReservationManager.ReleaseClaimedBy(reserver, job);
        }
    }
}
