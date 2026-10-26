using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class XenoformedBiomeCollectionDef : Def
    {
        public List<BiomeDef> biomes = new List<BiomeDef>();
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (biomes.NullOrEmpty() || biomes.Any(biome => biome == null)) yield return defName + ": requires valid biomes.";
        }
    }

    internal static class NemesisHarvestUtility
    {
        internal static JobDef CollectJob => DefDatabase<JobDef>.GetNamed("XMT_NemesisCollectJelly");

        internal static List<PlanetTile> AdjacentXenoformedTiles(Map map)
        {
            List<PlanetTile> neighbors = new List<PlanetTile>();
            if (map == null || !XenoformingUtility.IsSurfaceBiomeTile(map.Tile)) return neighbors;
            map.Tile.Layer.GetTileNeighbors(map.Tile, neighbors);
            neighbors.RemoveAll(tile => !XenoformingUtility.IsXenoformedBiome(tile.Tile.PrimaryBiome));
            return neighbors;
        }

        internal static Job ExitJob(Pawn pawn, IntVec3 entry)
        {
            Rot4 side = CellRect.WholeMap(pawn.Map).GetClosestEdge(entry);
            foreach (IntVec3 cell in CellRect.WholeMap(pawn.Map).GetCellsOnEdge(side).InRandomOrder().Take(24))
                if (cell.Standable(pawn.Map) && ClimbUtility.CanReachByWalkingOrClimb(pawn, cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    job.exitMapOnArrival = true;
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    return job;
                }
            return NemesisMissionUtility.ExitJob(pawn);
        }
    }

    public sealed class NemesisMissionWorker_Harvest : NemesisMissionWorker
    {
        public override bool CanTarget(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => base.CanTarget(def, component, map) && XenoformingUtility.IsSurfaceBiomeTile(map.Tile)
                && XenoformingUtility.CanXenoformBiome(map.Tile)
                && !XenoformingUtility.IsXenoformedBiome(map.Biome)
                && !map.GetComponent<MapComponent_NemesisHarvest>().Completed
                && NemesisHarvestUtility.AdjacentXenoformedTiles(map).Count > 0;

        public override float Weight(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => base.Weight(def, component, map) * AdjacencyMultiplier(def.workerSettings,
                NemesisHarvestUtility.AdjacentXenoformedTiles(map).Count);

        internal static float AdjacencyMultiplier(NemesisMissionSettings settings, int neighbors)
            => Mathf.Min(settings.maximumAdjacencyMultiplier, 1f + Mathf.Max(0, neighbors - 1) * settings.adjacentBiomeWeight);

        public override int PartySize(NemesisMissionDef def, GameComponent_Nemesis component, Map map, bool active)
            => NemesisMissionWorker_SwarmAssault.ThreatPointPopulation(def, base.PartySize(def, component, map, active),
                StorytellerUtility.DefaultThreatPointsNow(map), def.workerSettings.pawnKind.combatPower);

        public override List<IntVec3> PrepareRoute(NemesisMissionDef def, GameComponent_Nemesis component, Map map)
            => new List<IntVec3>();

        public override bool TryFindEntryCell(NemesisMissionDef def, GameComponent_Nemesis component, Map map,
            bool ignoreLight, out IntVec3 entry)
        {
            foreach (PlanetTile neighbor in NemesisHarvestUtility.AdjacentXenoformedTiles(map).InRandomOrder())
            {
                Rot4 side = Rot4.FromAngleFlat(map.Tile.Layer.GetHeadingFromTo(map.Tile, neighbor));
                foreach (IntVec3 cell in CellRect.WholeMap(map).GetCellsOnEdge(side).InRandomOrder())
                    if (cell.Standable(map) && cell.GetRegion(map) != null
                        && (ignoreLight || XMTHiveUtility.IsLightSuitableAt(cell, map)))
                    { entry = cell; return true; }
            }
            entry = IntVec3.Invalid;
            return false;
        }

        public override IEnumerable<string> ConfigErrors(NemesisMissionDef def)
        {
            if (def.workerSettings.pawnKind?.race?.GetCompProperties<CompJellyMakerProperties>() == null
                || def.workerSettings.pawnKind.combatPower <= 0f)
                yield return def.defName + ": harvesting requires a jelly-making pawn kind with positive combat power.";
            if (def.workerSettings.harvestCellsPerTick < 1 || def.workerSettings.collectionDurationTicks < 0
                || def.workerSettings.adjacentBiomeWeight < 0f || def.workerSettings.maximumAdjacencyMultiplier < 1f)
                yield return def.defName + ": invalid harvest settings.";
        }
    }

    public sealed class JobDriver_NemesisCollectJelly : JobDriver_ClimbToPosition
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing target = job.targetA.Thing;
            if (target == null || !FeralJobUtility.IsThingAvailableForJobBy(pawn, target)) return false;
            FeralJobUtility.ReserveThingForJob(pawn, job, target);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            Toil take = Toils_Haul.TakeToInventory(TargetIndex.A, thing => Mathf.Min(job.count,
                MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing)));
            System.Action nativeTake = take.initAction;
            take.initAction = () =>
            {
                Thing target = job.targetA.Thing;
                if (target == null || target.Destroyed || !target.Spawned || pawn.inventory == null
                    || MassUtility.CountToPickUpUntilOverEncumbered(pawn, target) < 1)
                { EndJobWith(JobCondition.Incompletable); return; }
                nativeTake();
            };
            yield return take;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class NemesisHarvestIngestedPatch
    {
        private static void Postfix(Thing __instance, Pawn ingester, float __result)
        {
            if (__result > 0f && ingester?.GetLord()?.LordJob is LordJob_NemesisHarvest mission
                && __instance.def == ingester.GetComp<CompJellyMaker>()?.GetJellyProduct()) mission.NotifyJellyUsed(1);
        }
    }
}
