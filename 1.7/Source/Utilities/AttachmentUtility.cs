using RimWorld;
using RimWorld.Planet;
using System;
using Verse;

namespace Xenomorphtype
{
    public enum AttachmentAttemptResult
    {
        Attached,
        InvalidTarget,
        AlreadyAttached,
        Dodged,
        Resisted,
        Interrupted,
        ApparelBlocked,
        AnimationBusy,
        NoBodyPart,
        TransferFailed
    }

    public static class AttachmentUtility
    {
        public static Func<Pawn, bool> ParticipantBusyEvaluator;

        public static bool ParticipantIsBusy(Pawn pawn)
        {
            return pawn != null && ParticipantBusyEvaluator?.Invoke(pawn) == true;
        }

        internal static bool CanAttach(Pawn attacker, Pawn host)
        {
            return attacker != null && host != null && attacker != host
                && !attacker.Dead && !attacker.Downed && !host.Dead
                && attacker.Spawned && host.Spawned && !ParticipantIsBusy(attacker) && !ParticipantIsBusy(host) && attacker.MapHeld != null
                && attacker.MapHeld == host.MapHeld
                && attacker.PositionHeld.AdjacentTo8WayOrInside(host.PositionHeld);
        }

        // Restore local ownership if all synchronous destinations fail.
        internal static bool TryReleaseHeldPawn(ThingOwner owner, Pawn pawn, Pawn host, Thing outerContainer = null)
        {
            if (owner == null || pawn == null || !owner.Contains(pawn))
            {
                return false;
            }

            try
            {
                Building_BioContainer container = BioContainerUtility.Resolve(outerContainer);
                if (host != null && !host.Dead && container != null && !container.Destroyed
                    && container.ContainedThing == null && container.TryAcceptPawn(pawn, medicalExtraction: true)
                    && HasReleaseDestination(pawn, owner))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Container release failed for {pawn} from {host}: {exception}");
            }
            if (HasReleaseDestination(pawn, owner))
            {
                return true;
            }

            try
            {
                Thing destination = host?.Corpse ?? (Thing)host;
                Map map = destination?.MapHeld;
                if (map != null && destination.PositionHeld.IsValid && destination.PositionHeld.InBounds(map)
                    && owner.TryDrop(pawn, destination.PositionHeld, map, ThingPlaceMode.Near, out Thing _))
                {
                    if (HasReleaseDestination(pawn, owner))
                    {
                        return true;
                    }
                }

            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Map release failed for {pawn} from {host}: {exception}");
            }
            if (HasReleaseDestination(pawn, owner))
            {
                return true;
            }

            try
            {
                Caravan caravan = host?.GetCaravan();
                if (caravan != null)
                {
                    owner.Remove(pawn);
                    caravan.AddPawn(pawn, true);
                    if (pawn.GetCaravan() == caravan)
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Caravan release failed for {pawn} from {host}: {exception}");
            }
            if (HasReleaseDestination(pawn, owner))
            {
                return true;
            }

            try
            {
                if (Find.WorldPawns != null)
                {
                    owner.Remove(pawn);
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    if (Find.WorldPawns.Contains(pawn))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] World release failed for {pawn} from {host}: {exception}");
            }

            // A destination may have accepted the pawn before throwing during notifications.
            if (HasReleaseDestination(pawn, owner))
            {
                return true;
            }
            if (!pawn.Destroyed && !owner.Contains(pawn))
            {
                owner.TryAdd(pawn);
            }
            return false;
        }

        private static bool HasReleaseDestination(Pawn pawn, ThingOwner originalOwner)
        {
            return !originalOwner.Contains(pawn) && (pawn.Spawned || pawn.GetCaravan() != null
                || Find.WorldPawns?.Contains(pawn) == true
                || (pawn.holdingOwner != null && pawn.holdingOwner != originalOwner));
        }
    }
}
