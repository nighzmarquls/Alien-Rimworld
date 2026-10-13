using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal static class XMTQueenSitePatches
    {
        [HarmonyPatch(typeof(CaravanArrivalAction_VisitSite), "DoEnter", new[] { typeof(Caravan), typeof(Site) })]
        private static class Patch_CaravanArrivalAction_VisitSite_DoEnter
        {
            private static void Prefix(Caravan caravan, out List<Pawn> __state)
            {
                __state = caravan?.PawnsListForReading?.ToList() ?? new List<Pawn>();
            }

            private static void Postfix(Site site, List<Pawn> __state)
            {
                QueenSiteHomecomingUtility.ResolveArrival(site, __state);
            }
        }

        [HarmonyPatch(typeof(TransportersArrivalAction_VisitSite), nameof(TransportersArrivalAction_VisitSite.Arrived),
            new[] { typeof(List<ActiveTransporterInfo>), typeof(PlanetTile) })]
        private static class Patch_TransportersArrivalAction_VisitSite_Arrived
        {
            private static void Prefix(List<ActiveTransporterInfo> transporters, out List<Pawn> __state)
            {
                __state = transporters?
                    .SelectMany(info => info?.GetDirectlyHeldThings()?.OfType<Pawn>() ?? Enumerable.Empty<Pawn>())
                    .ToList() ?? new List<Pawn>();
            }

            private static void Postfix(Site ___site, List<Pawn> __state)
            {
                QueenSiteHomecomingUtility.ResolveArrival(___site, __state);
            }
        }
    }
}
