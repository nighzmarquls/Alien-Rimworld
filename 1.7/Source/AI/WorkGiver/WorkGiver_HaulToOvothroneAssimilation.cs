using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class WorkGiver_HaulToOvothroneAssimilation : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || !OvothroneAssimilationHaulUtility.AnyUsefulOvothroneQueen(pawn.Map, pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return OvothroneAssimilationHaulUtility.TryFindBestThroneFor(pawn, t, forced, out EggSack _, out CompQueenAssimilation _, out QueenAssimilationDef _, out int _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!OvothroneAssimilationHaulUtility.TryFindBestThroneFor(pawn, t, forced, out EggSack throne, out CompQueenAssimilation _, out QueenAssimilationDef _, out int count))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HaulToOvothroneAssimilation, t, throne, throne.InteractionCell);
            job.count = count;
            return job;
        }
    }

    internal static class OvothroneAssimilationHaulUtility
    {
        internal static bool AnyUsefulOvothroneQueen(Map map, Pawn hauler)
        {
            if (map == null)
            {
                return false;
            }

            foreach (EggSack throne in OvothronesOnMap(map))
            {
                if (TryGetAssimilationComp(throne, hauler, out Pawn _, out CompQueenAssimilation comp) && QueenHasUsefulAssimilationNeed(comp))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryFindBestThroneFor(Pawn pawn, Thing item, bool forced, out EggSack bestThrone, out CompQueenAssimilation bestComp, out QueenAssimilationDef bestDef, out int bestCount)
        {
            bestThrone = null;
            bestComp = null;
            bestDef = null;
            bestCount = 0;

            if (!CanBeHauledForAssimilation(pawn, item, forced))
            {
                return false;
            }

            float bestScore = float.MaxValue;
            foreach (EggSack throne in OvothronesOnMap(pawn.Map))
            {
                if (!TryGetAssimilationComp(throne, pawn, out Pawn queen, out CompQueenAssimilation comp)
                    || !QueenCanBenefitFromThing(item, queen, comp, out QueenAssimilationDef def, out int count)
                    || !CanReachAndReserveThrone(pawn, throne, forced))
                {
                    continue;
                }

                float score = pawn.Position.DistanceToSquared(item.Position) + item.Position.DistanceToSquared(throne.InteractionCell);
                if (bestThrone == null || score < bestScore)
                {
                    bestThrone = throne;
                    bestComp = comp;
                    bestDef = def;
                    bestCount = count;
                    bestScore = score;
                }
            }

            return bestThrone != null;
        }

        internal static bool TryGetAssimilationComp(EggSack throne, Pawn hauler, out Pawn queen, out CompQueenAssimilation comp)
        {
            queen = null;
            comp = null;

            if (throne == null || throne.Destroyed || !throne.Spawned || throne.Map == null)
            {
                return false;
            }

            queen = throne.Occupant;
            if (queen == null || queen.Destroyed || queen.Dead || queen.MapHeld != throne.MapHeld)
            {
                return false;
            }

            if (queen.Faction != Faction.OfPlayer || (hauler?.Faction != null && queen.Faction != hauler.Faction))
            {
                return false;
            }

            comp = queen.GetComp<CompQueenAssimilation>();
            return comp != null;
        }

        internal static bool QueenCanBenefitFromThing(Thing item, Pawn queen, CompQueenAssimilation comp, out QueenAssimilationDef def, out int count)
        {
            def = null;
            count = 0;

            if (item == null || item.Destroyed || queen == null || comp == null)
            {
                return false;
            }

            AcceptanceReport report = comp.CanAssimilate(item, out def);
            if (!report.Accepted)
            {
                return false;
            }

            if (def != null)
            {
                if (!QueenCanBenefitFromAssimilationDef(comp, def) || item.stackCount < def.consumeCount)
                {
                    return false;
                }

                count = def.consumeCount;
                return true;
            }

            List<QueenResourceGain> gains = QueenIngestibleResourceUtility.GetResourceGains(item, queen, comp);
            if (gains.Count == 0)
            {
                return false;
            }

            count = item.stackCount;
            return count > 0;
        }

        internal static bool QueenHasUsefulAssimilationNeed(CompQueenAssimilation comp)
        {
            return HasAnyUsefulAssimilationDef(comp) || HasAnyResourceSpace(comp);
        }

        private static bool CanBeHauledForAssimilation(Pawn pawn, Thing item, bool forced)
        {
            if (pawn?.Map == null || item == null || item.Destroyed || !item.Spawned || item.Map != pawn.Map)
            {
                return false;
            }

            if (item.def.category != ThingCategory.Item && !(item is Corpse))
            {
                return false;
            }

            return HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, item, forced);
        }

        private static bool CanReachAndReserveThrone(Pawn pawn, EggSack throne, bool forced)
        {
            if (pawn?.Map == null || throne == null || throne.Map != pawn.Map)
            {
                return false;
            }

            IntVec3 interactionCell = throne.InteractionCell;
            return interactionCell.IsValid
                && interactionCell.InBounds(pawn.Map)
                && interactionCell.Standable(pawn.Map)
                && pawn.CanReserveAndReach(interactionCell, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, forced)
                && pawn.CanReserve(throne, 1, -1, null, forced);
        }

        private static bool HasAnyUsefulAssimilationDef(CompQueenAssimilation comp)
        {
            if (comp == null)
            {
                return false;
            }

            foreach (QueenAssimilationDef def in DefDatabase<QueenAssimilationDef>.AllDefsListForReading)
            {
                if (def.thingDef != null && QueenCanBenefitFromAssimilationDef(comp, def))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyResourceSpace(CompQueenAssimilation comp)
        {
            if (comp == null)
            {
                return false;
            }

            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (comp.ResourceUnlocked(resource) && comp.GetResourceSpace(resource) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool QueenCanBenefitFromAssimilationDef(CompQueenAssimilation comp, QueenAssimilationDef def)
        {
            return def != null && def.HasPrerequisites(comp);
        }

        private static IEnumerable<EggSack> OvothronesOnMap(Map map)
        {
            if (map == null)
            {
                yield break;
            }

            List<Thing> things = map.listerThings.ThingsOfDef(XenoBuildingDefOf.XMT_Ovothrone);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is EggSack throne)
                {
                    yield return throne;
                }
            }
        }
    }
}
