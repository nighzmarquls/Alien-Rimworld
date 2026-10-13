using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class QueenSiteHomecomingUtility
    {
        private const string QueenSiteDefName = "XMT_Hivesite_Queen";

        internal static void ResolveArrival(Site site, IEnumerable<Pawn> arrivingPawns)
        {
            if (site == null || site.Destroyed || !site.HasMap
                || !site.parts.Any(part => part?.def?.defName == QueenSiteDefName))
            {
                return;
            }

            Map map = site.Map;
            Pawn siteQueen = FindSiteQueen(map);
            if (siteQueen == null || siteQueen.Dead || siteQueen.Destroyed)
            {
                return;
            }

            List<Pawn> arrivals = arrivingPawns?.Where(pawn => pawn != null).Distinct().ToList()
                ?? new List<Pawn>();
            if (PlayerColonyShouldBeCryptimorph() && !AnyActivePlayerQueen())
            {
                AdoptQueenSite(map, siteQueen);
                return;
            }

            if (Faction.OfPlayerSilentFail?.def != InternalDefOf.XMT_PlayerHive
                && arrivals.Any(pawn => pawn.Faction == Faction.OfPlayerSilentFail && XMTUtility.IsXenomorph(pawn)))
            {
                HiveMapComponent hiveComponent = map.GetComponent<HiveMapComponent>();
                if (hiveComponent.TryMarkMotherWarningSent())
                {
                    Find.LetterStack.ReceiveLetter(
                        "XMT_QueenMotherWarningLetterTitle".Translate(),
                        "XMT_QueenMotherWarningLetterDescription".Translate(),
                        LetterDefOf.NegativeEvent,
                        new LookTargets(siteQueen));
                }
            }
        }

        private static bool PlayerColonyShouldBeCryptimorph()
        {
            List<Pawn> colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(pawn => pawn != null && !pawn.Dead)
                .ToList();
            return colonists.Count > 0 && colonists.All(XMTUtility.IsXenomorph);
        }

        private static bool AnyActivePlayerQueen()
        {
            if (PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.Any(IsFreePlayerQueen))
            {
                return true;
            }

            foreach (Map map in Find.Maps.Where(map => map.IsPlayerHome))
            {
                foreach (SelfOccupyingBuilding holder in map.spawnedThings.OfType<SelfOccupyingBuilding>())
                {
                    if (holder.ContainedThing is Pawn pawn && IsFreePlayerQueen(pawn))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsFreePlayerQueen(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed
                && pawn.Faction == Faction.OfPlayerSilentFail
                && pawn.GuestStatus != GuestStatus.Slave
                && pawn.GuestStatus != GuestStatus.Prisoner
                && XMTUtility.IsQueen(pawn);
        }

        private static Pawn FindSiteQueen(Map map)
        {
            Pawn queen = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn => XMTUtility.IsQueen(pawn));
            if (queen != null)
            {
                return queen;
            }

            return map.spawnedThings.OfType<SelfOccupyingBuilding>()
                .Select(holder => holder.ContainedThing as Pawn)
                .FirstOrDefault(pawn => pawn != null && XMTUtility.IsQueen(pawn));
        }

        private static void AdoptQueenSite(Map map, Pawn queen)
        {
            Faction player = Faction.OfPlayer;
            if (player.def != InternalDefOf.XMT_PlayerHive)
            {
                player.def = InternalDefOf.XMT_PlayerHive;
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && XMTUtility.IsXenomorph(pawn)).ToList())
            {
                ConvertCryptimorphToPlayer(pawn, player);
            }

            foreach (SelfOccupyingBuilding holder in map.spawnedThings.OfType<SelfOccupyingBuilding>())
            {
                if (holder.ContainedThing is Pawn occupant && XMTUtility.IsXenomorph(occupant))
                {
                    ConvertCryptimorphToPlayer(occupant, player);
                }
            }

            HiveMapComponent hiveComponent = map.GetComponent<HiveMapComponent>();
            hiveComponent.CaptureGeneratedCryptimorphStructures();
            foreach (Thing structure in hiveComponent.GeneratedCryptimorphStructures.ToList())
            {
                if (structure.Faction != player)
                {
                    structure.SetFaction(player);
                }
            }

            if (queen.Faction != player)
            {
                queen.SetFaction(player);
            }
            XMTUtility.DeclareQueen(queen);

            SettleInExistingMapUtility.Settle(map);
            Find.LetterStack.ReceiveLetter(
                "XMT_QueenHomecomingLetterTitle".Translate(),
                "XMT_QueenHomecomingLetterDescription".Translate(queen.Named("PAWN")),
                LetterDefOf.PositiveEvent,
                new LookTargets(queen));
        }

        private static void ConvertCryptimorphToPlayer(Pawn pawn, Faction player)
        {
            if (pawn.Faction == player)
            {
                return;
            }

            if (pawn.Spawned && pawn.CurJob != null)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            pawn.SetFaction(player);
        }
    }
}
