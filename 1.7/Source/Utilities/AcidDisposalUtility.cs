using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class AcidDisposalUtility
    {
        private sealed class SearchState
        {
            public int mapId;
            public int tick;
            public int nextTick;
        }

        private static readonly ConditionalWeakTable<Pawn, SearchState> searches = new ConditionalWeakTable<Pawn, SearchState>();
        private static List<ThingDef> pitDefs;
        private static int pitDefCount = -1;
        internal static IEnumerable<CompAcidPit> PitsOnMap(Map map)
        {
            // Cache definition metadata only; use the map's native per-def index for live buildings.
            if (pitDefs == null || pitDefCount != DefDatabase<ThingDef>.DefCount)
            {
                pitDefCount = DefDatabase<ThingDef>.DefCount;
                pitDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.comps?.Any(c => c is CompProperties_AcidPit) == true).ToList();
            }

            foreach (ThingDef def in pitDefs)
                foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                {
                    CompAcidPit pit = thing.TryGetComp<CompAcidPit>();
                    if (pit != null)
                        yield return pit;
                }
        }

        private static bool ReadyFor(Pawn pawn, CompAcidPit pit) => pit.AcceptingMaterial && pit.parent.Faction == pawn.Faction && FeralJobUtility.IsThingAvailableForJobBy(pawn, pit.parent);
        public static bool AnyReadyPit(Pawn pawn) => pawn?.Map != null && PitsOnMap(pawn.Map).Any(p => ReadyFor(pawn, p));
        public static string DirectOrderFailure(Pawn pawn, CompAcidPit pit)
        {
            if (pit.Busy)
                return "XMT_AcidOccupied".Translate();
            if (!pit.AcceptingMaterial)
                return "XMT_AcidAcceptanceDisabled".Translate();
            if (pawn?.Map == null || !pit.parent.Spawned || pawn.Map != pit.parent.Map || pawn.Faction != pit.parent.Faction || pawn.Downed || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
                return "XMT_AcidCannotHaul".Translate();
            if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, pit.parent) || !pawn.CanReserveAndReach(pit.parent, PathEndMode.Touch, Danger.Some))
                return "XMT_AcidUnavailable".Translate();
            return null;
        }

        // Explicit player orders use the chosen destination, without entering the autonomous retry/cache path.
        public static Job FindJobForPit(Pawn pawn, CompAcidPit pit, out string reason)
        {
            reason = DirectOrderFailure(pawn, pit);
            if (reason != null)
                return null;
            Thing ingredient = XMTUtility.SearchRegionsForHaulable(pawn.GetRegion(), pawn, t => pit.CanAccept(t) && pawn.carryTracker.MaxStackSpaceEver(t.def) > 0 && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, true), Danger.Some);
            Job job = ingredient != null && pawn.CanReserveAndReach(ingredient, PathEndMode.ClosestTouch, Danger.Some) ? MakeJob(pawn, ingredient, pit) : null;
            if (job == null)
                reason = "XMT_AcidNoMaterial".Translate();
            return job;
        }

        public static IEnumerable<Thing> HaulCandidates(Pawn pawn)
        {
            if (pawn?.Map == null)
                yield break;
            var readyPits = PitsOnMap(pawn.Map).Where(p => ReadyFor(pawn, p)).ToList();
            if (readyPits.Count == 0)
                yield break;
            var seen = new HashSet<ThingDef>();
            foreach (CompAcidPit pit in readyPits)
            {
                // Like genepack hauling, query only relevant item definitions, not every haulable.
                foreach (ThingDef def in pit.Inputs.GetStoreSettings().filter.AllowedThingDefs)
                {
                    if (!seen.Add(def))
                        continue;
                    foreach (Thing item in pawn.Map.listerThings.ThingsOfDef(def))
                        if (readyPits.Any(p => p.CanAccept(item)) && FeralJobUtility.IsThingAvailableForJobBy(pawn, item))
                            yield return item;
                }
            }
        }

        public static bool TryFindPitFor(Pawn pawn, Thing item, bool forced, out CompAcidPit destination)
        {
            destination = null;
            if (pawn?.Map == null || item == null || !item.Spawned || item.Map != pawn.Map || item.def.category != ThingCategory.Item || !AnyReadyPit(pawn) || pawn.carryTracker.MaxStackSpaceEver(item.def) <= 0 || !HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, item, forced))
                return false;
            foreach (CompAcidPit pit in PitsOnMap(pawn.Map))
                if (ReadyFor(pawn, pit) && pit.CanAccept(item) && pawn.CanReserveAndReach(pit.parent, PathEndMode.Touch, Danger.Some))
                {
                    destination = pit;
                    return true;
                }

            return false;
        }

        public static Job MakeJob(Pawn pawn, Thing ingredient, CompAcidPit destination)
        {
            float unitMass = ResourceHelperUtility.ValueFor(ingredient, destination.Props.inputs, 1);
            int affordable = Mathf.Max(1, Mathf.FloorToInt((float)(destination.Acid / System.Math.Max(0.000001d, AcidPitEconomy.CostPerKg(destination.Props) * unitMass))));
            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_AcidDisposal, ingredient, destination.parent);
            job.count = Mathf.Min(ingredient.stackCount, affordable, pawn.carryTracker.MaxStackSpaceEver(ingredient.def));
            return job.count > 0 ? job : null;
        }

        public static bool WaitingToRetry(Pawn pawn)
        {
            if (pawn?.Map == null || !searches.TryGetValue(pawn, out SearchState state))
                return false;
            int now = Find.TickManager.TicksGame;
            return state.mapId == pawn.Map.uniqueID && now >= state.tick && now < state.nextTick;
        }

        public static Job FindJob(Pawn pawn, Thing item = null, bool forced = false)
        {
            if (pawn?.Map == null || pawn.Downed || (!forced && WaitingToRetry(pawn)))
                return null;
            SearchState state = searches.GetValue(pawn, _ => new SearchState());
            state.mapId = pawn.Map.uniqueID;
            state.tick = Find.TickManager.TicksGame;
            state.nextTick = state.tick + 250 + (pawn.thingIDNumber & 63);
            if (!AnyReadyPit(pawn))
                return null;
            Region region = pawn.GetRegion();
            if (region == null)
                return null;
            var pits = PitsOnMap(pawn.Map).Where(p => ReadyFor(pawn, p) && pawn.CanReserveAndReach(p.parent, PathEndMode.Touch, Danger.Some)).ToList();
            if (pits.Count == 0)
                return null;
            CompAcidPit destination = null;
            System.Predicate<Thing> accepts = candidate =>
            {
                if (!candidate.Spawned || candidate.Map != pawn.Map || candidate.def.category != ThingCategory.Item || pawn.carryTracker.MaxStackSpaceEver(candidate.def) <= 0)
                    return false;
                foreach (CompAcidPit pit in pits)
                    if (pit.CanAccept(candidate))
                    {
                        destination = pit;
                        return true;
                    }

                return false;
            };
            Thing ingredient = item == null ? XMTUtility.SearchRegionsForHaulable(region, pawn, accepts, Danger.Some) : accepts(item) && FeralJobUtility.IsThingAvailableForJobBy(pawn, item) ? item : null;
            if (ingredient == null || !pawn.CanReserveAndReach(ingredient, PathEndMode.Touch, Danger.Some))
                return null;
            Job job = MakeJob(pawn, ingredient, destination);
            if (job == null)
                return null;
            state.nextTick = state.tick;
            return job;
        }
    }

}
