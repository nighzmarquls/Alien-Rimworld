using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class XenoformingUtility
    {

        private static GameComponent_Xenomorph gameComponent => Current.Game.GetComponent<GameComponent_Xenomorph>();

        private static Texture2D InvestigateTex => ContentFinder<Texture2D>.Get("UI/Commands/OfferGifts");

        private const float StrangeHillMinimumTemperature = -20f;

        internal static bool IsXenoformedBiome(BiomeDef biome)
        {
            return biome != null && DefDatabase<XenoformedBiomeCollectionDef>.AllDefsListForReading
                .Any(collection => collection.biomes.Contains(biome));
        }

        internal static BiomeDef XenoformingBiomeFor(Tile tile)
        {
            if (tile == null || tile.WaterCovered || tile.temperature < StrangeHillMinimumTemperature) return null;
            if (tile.temperature <= 0f) return XenoMapDefOf.XMT_ChilledWaste;
            // Preserve the broad source habitat during live conversion. Before world-gen biome
            // selection, and for unfamiliar mod biomes, the climate fallback needs no world cache.
            switch (tile.PrimaryBiome?.defName)
            {
                case "Tundra":
                case "BorealForest":
                case "ColdBog":
                case "XMT_ChilledWaste":
                    return XenoMapDefOf.XMT_ChilledWaste;
                case "Grasslands":
                case "TemperateForest":
                case "TemperateSwamp":
                case "TropicalRainforest":
                case "TropicalSwamp":
                case "Glowforest":
                case "XMT_BarrenDustbowl":
                    return XenoMapDefOf.XMT_BarrenDustbowl;
                case "Desert":
                case "ExtremeDesert":
                case "AridShrubland":
                    return XenoMapDefOf.XMT_DessicatedBlight;
            }
            return tile.rainfall < 600f ? XenoMapDefOf.XMT_DessicatedBlight : XenoMapDefOf.XMT_BarrenDustbowl;
        }

        internal static float XenoformingBiomeScore(BiomeDef biome, Tile tile)
        {
            if (biome == null || tile == null || XenoformingBiomeFor(tile) != biome) return -100f;
            if (!XenoformingMeets(10f)) return 0f;
            float temperatureStrength = biome == XenoMapDefOf.XMT_ChilledWaste
                ? Mathf.Max(1f, -tile.temperature) : tile.temperature;
            float score = ChanceByXenoforming(1f) + temperatureStrength * 2.7f;
            return Find.TickManager.TicksGame <= 0 ? score * (GetXenoforming() / 100f) : score;
        }

        private static readonly HarmonyLib.AccessTools.FieldRef<WorldDrawLayerBase, List<LayerSubMesh>> WorldSubMeshes =
            HarmonyLib.AccessTools.FieldRefAccess<WorldDrawLayerBase, List<LayerSubMesh>>("subMeshes");

        internal static void ClearEmptyWorldTerrainMeshes(WorldDrawLayerBase layer, MeshParts parts)
        {
            if (!(layer is WorldDrawLayer_Terrain) || (parts & (MeshParts.Verts | MeshParts.Tris)) == 0) return;
            // Native ClearSubMeshes clears CPU lists, but FinalizeMesh skips empty submeshes.
            // Clear their actual geometry before RegenerateWorldMeshColliders reuses every mesh;
            // otherwise old triangles remain raycastable with an empty triangle-to-tile table.
            foreach (LayerSubMesh subMesh in WorldSubMeshes(layer))
                if (subMesh.verts.Count == 0) subMesh.mesh.Clear();
        }

        internal static int CleanupInvalidWorldPawns(PawnKindDef pawnKind, string source)
        {
            if (pawnKind == null || Find.WorldPawns == null) return 0;
            int inspected = 0;
            int removed = 0;
            int protectedCount = 0;
            int valid = 0;
            GameComponent_NemesisWorldPawns retained = Current.Game?.GetComponent<GameComponent_NemesisWorldPawns>();
            GameComponent_Xenomorph component = Current.Game?.GetComponent<GameComponent_Xenomorph>();

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive.Where(candidate => candidate?.kindDef == pawnKind).ToList())
            {
                inspected++;
                if (WorldPawnCleanupProtection(pawn, retained) != null)
                {
                    protectedCount++;
                    continue;
                }

                string invalidity = WorldPawnInvalidity(pawn, pawnKind);
                if (invalidity == null)
                {
                    valid++;
                    continue;
                }

                int relationCount = pawn.relations?.DirectRelations?.Count ?? 0;
                string pawnId = pawn.ThingID ?? "unknown";
                string label;
                try { label = pawn.LabelShortCap; }
                catch { label = pawnId; }
                bool registeredQueen = component?.Queen == pawn;
                XenoformingPawnAccountingState accounting = XenoformingPawnAccountingState.None;
                try
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                    accounting = component?.ForgetWorldPawnForCleanup(pawn) ?? XenoformingPawnAccountingState.None;
                }
                catch (Exception exception)
                {
                    Log.Error("[XMT][World] Failed to discard invalid world pawn during "
                        + (source ?? "unspecified cleanup") + ": pawn=" + label + " id=" + pawnId
                        + " kind=" + pawnKind.defName + " reason=" + invalidity + " error=" + exception);
                    continue;
                }
                removed++;
                Log.Warning("[XMT][World] Removed invalid world pawn during " + (source ?? "unspecified cleanup")
                    + ": pawn=" + label + " id=" + pawnId + " kind=" + pawnKind.defName
                    + " reason=" + invalidity + " relations=" + relationCount + " accounting=" + accounting
                    + " registeredQueen=" + registeredQueen);
            }

            Log.Warning("[XMT][World] World-pawn cleanup during " + (source ?? "unspecified cleanup")
                + ": kind=" + pawnKind.defName + " inspected=" + inspected + " removed=" + removed
                + " protected=" + protectedCount + " valid=" + valid);
            return removed;
        }

        private static string WorldPawnCleanupProtection(Pawn pawn, GameComponent_NemesisWorldPawns retained)
        {
            if (Current.Game?.GetComponent<GameComponent_Nemesis>()?.ProtectGeneticPawn(pawn) == true) return "Nemesis recovery/trial";
            if (pawn.Spawned) return "spawned";
            if (pawn.Faction == Faction.OfPlayerSilentFail || pawn.HostFaction == Faction.OfPlayerSilentFail)
                return "player-owned";
            if (pawn.ParentHolder != null) return "held";
            if (CaravanUtility.IsCaravanMember(pawn)) return "caravan";
            if (pawn.questTags.NullOrEmpty() == false) return "quest-tagged";
            if (Find.WorldPawns.ForcefullyKeptPawns.Contains(pawn)) return "force-kept";
            if (retained?.RetainedPawns.Any(record => record?.pawn == pawn) == true) return "Nemesis-retained";
            WorldPawnSituation situation = Find.WorldPawns.GetSituation(pawn);
            return situation != WorldPawnSituation.Free && situation != WorldPawnSituation.None
                ? "world situation " + situation : null;
        }

        private static string WorldPawnInvalidity(Pawn pawn, PawnKindDef pawnKind)
        {
            try
            {
                List<string> reasons = new List<string>();
                if (pawn.Destroyed) reasons.Add("destroyed");
                if (pawn.Downed) reasons.Add("downed");
                if (pawn.def == null) reasons.Add("missing race");
                if (pawnKind.race != null && pawn.def != pawnKind.race) reasons.Add("kind/race mismatch");
                if (pawn.health == null) reasons.Add("missing health tracker");
                if (pawn.ageTracker == null) reasons.Add("missing age tracker");
                if (pawn.mindState == null) reasons.Add("missing mind state");
                if (pawn.relations == null) reasons.Add("missing relations tracker");
                if (pawn.health != null && !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                    reasons.Add("incapable of moving");
                if (pawn.relations?.DirectRelations?.Any(relation => relation == null
                    || relation.def == null || relation.otherPawn == null) == true)
                    reasons.Add("malformed direct relation");
                return reasons.Count > 0 ? string.Join(", ", reasons) : null;
            }
            catch (Exception exception)
            {
                return "validation threw " + exception.GetType().Name + ": " + exception.Message;
            }
        }

        public static bool ShouldSeedBiomeFromPlayerHome()
        {
            return XMTUtility.PlayerFactionIsCryptimorph();
        }

        public static bool IsStrangeHillSite(Site site)
        {
            return site != null && !site.Destroyed
                && site.parts.Any(part => part?.def?.defName == "XMT_Hivesite_Small"
                    || part?.def?.defName == "XMT_Hivesite_Large");
        }

        internal const int SpreadBatchInterval = 15;
        internal const int SpreadAttemptsPerBatch = 32;
        internal const int SpreadConversionsPerBatch = 4;
        private const int SpreadInitializationBatch = 128;

        internal static int BiomeSpreadAllowance(float xenoforming, int converted, int eligibleLand, float factor)
        {
            if (xenoforming < 10f || factor <= 0f) return 0;
            float level = Mathf.Clamp(xenoforming, 0f, 100f);
            float settingScale = factor / 0.01f;
            float levelScale = level / 25f;
            // At defaults: 1% of convertible land/day at 25%, 4% at 50%, 16% at 100%.
            // Tile density and generated-world size scale the budget, not the work per tick.
            float worldBaseline = eligibleLand * 0.01f * levelScale * levelScale * settingScale;
            float baseline = Mathf.Max(level * level * factor, worldBaseline);
            return Mathf.FloorToInt(Mathf.Max(baseline, converted * 0.1f * settingScale));
        }

        internal static void AddBiomeSeed(XenoformingSpreadState state, PlanetTile tile)
        {
            if (tile.Valid) state.seeds.AddUnique(tile);
        }

        private static void AddBiomeFrontier(XenoformingSpreadState state, PlanetTile tile)
        {
            if (IsSurfaceBiomeTile(tile) && !tile.Tile.WaterCovered
                && !IsXenoformedBiome(tile.Tile.PrimaryBiome) && state.membership.Add(tile))
            {
                if (HasLoadedMap(tile)) state.deferred.Add(tile);
                else state.frontier.Add(tile);
            }
        }

        private static void AddBiomeNeighbors(XenoformingSpreadState state, PlanetTile tile)
        {
            state.neighbors.Clear();
            Find.WorldGrid.Surface.GetTileNeighbors(tile, state.neighbors);
            foreach (PlanetTile neighbor in state.neighbors) AddBiomeFrontier(state, neighbor);
        }

        private static PlanetTile PopBiomeTile(List<PlanetTile> tiles, bool random = false)
        {
            int index = random ? Rand.Range(0, tiles.Count) : tiles.Count - 1;
            PlanetTile result = tiles[index];
            tiles[index] = tiles[tiles.Count - 1];
            tiles.RemoveAt(tiles.Count - 1);
            return result;
        }

        internal static void BiomeSpreadTick(GameComponent_Xenomorph component)
        {
            int tick = Find.TickManager.TicksGame;
            if (tick % SpreadBatchInterval != 0 || component.Xenoforming < 10f) return;
            XenoformingSpreadState state = component.biomeSpread;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (!state.membershipReady)
            {
                int total = state.frontier.Count + state.deferred.Count;
                for (int i = 0; i < SpreadInitializationBatch && state.membershipCursor < total; i++)
                {
                    int index = state.membershipCursor++;
                    state.membership.Add(index < state.frontier.Count ? state.frontier[index]
                        : state.deferred[index - state.frontier.Count]);
                    if (watch.ElapsedMilliseconds >= 2) return;
                }
                if (state.membershipCursor < total) return;
                state.membershipReady = true;
            }
            if (!state.initialized)
            {
                var surface = Find.WorldGrid.Surface;
                for (int i = 0; i < SpreadInitializationBatch && state.scanCursor < surface.TilesCount; i++)
                {
                    PlanetTile tile = surface.PlanetTileForID(state.scanCursor++);
                    if (XenoformingBiomeFor(tile.Tile) != null) state.eligibleLandTiles++;
                    if (IsXenoformedBiome(tile.Tile.PrimaryBiome))
                    {
                        BiomeDef variant = XenoformingBiomeFor(tile.Tile);
                        if (variant != null && tile.Tile.PrimaryBiome != variant && !HasLoadedMap(tile))
                        {
                            tile.Tile.PrimaryBiome = variant;
                            state.graphicsDirty = true;
                        }
                        state.convertedTiles++;
                        AddBiomeNeighbors(state, tile);
                    }
                    if (watch.ElapsedMilliseconds >= 2) return;
                }
                if (state.scanCursor < surface.TilesCount) return;
                state.initialized = true;
                if (XMTSettings.LogWorld)
                    Log.Message("[XMT][World] Biome frontier initialized: " + state.convertedTiles
                        + " converted tiles, " + state.eligibleLandTiles + " convertible land tiles.");
            }

            if (state.conversionsRemaining <= 0 && state.cycleRequested)
            {
                state.cycleRequested = false;
                state.conversionsRemaining = Mathf.Min(Find.WorldGrid.Surface.TilesCount,
                    BiomeSpreadAllowance(component.Xenoforming, state.convertedTiles, state.eligibleLandTiles, XMTSettings.BiomeSpreadFactor));
                if (XMTSettings.LogWorld)
                    Log.Message("[XMT][World] Biome spread allowance: " + state.conversionsRemaining
                        + " tiles at " + component.Xenoforming.ToString("0.##") + "% xenoforming; "
                        + state.convertedTiles + "/" + state.eligibleLandTiles + " convertible land tiles changed.");
                state.attemptsRemaining = Mathf.Max(256, state.conversionsRemaining * 16);
                state.deferredToReactivate = state.deferred.Count;
                state.collectingSites = true;
                state.siteCursor = 0;
                state.queenIncidentChecked = false;
                foreach (Map map in Find.Maps)
                    if (map.IsPlayerHome && IsSurfaceBiomeTile(map.Tile) && ShouldSeedBiomeFromPlayerHome())
                        AddBiomeNeighbors(state, map.Tile);
                if (!state.initialScatterCompleted && !ShouldSeedBiomeFromPlayerHome()) SeedInitialBiomeScatter(state);
            }

            if (state.conversionsRemaining > 0 && XMTSettings.BiomeSpreadFactor > 0f)
            {
                // Reactivate only the previous cycle's failures before adding this cycle's failures.
                for (int i = 0; i < SpreadInitializationBatch && state.deferredToReactivate > 0; i++)
                {
                    state.frontier.Add(PopBiomeTile(state.deferred));
                    state.deferredToReactivate--;
                }
                if (state.deferredToReactivate > 0) return;
                if (state.collectingSites)
                {
                    List<Site> sites = Find.WorldObjects.Sites;
                    for (int i = 0; i < 16 && state.siteCursor < sites.Count; i++)
                    {
                        Site site = sites[state.siteCursor++];
                        if (IsStrangeHillSite(site) && IsSurfaceBiomeTile(site.Tile)
                            && !IsXenoformedBiome(site.Tile.Tile.PrimaryBiome))
                            state.hillSeeds.AddUnique(site.Tile);
                    }
                    state.collectingSites = state.siteCursor < sites.Count;
                    if (state.collectingSites) return;
                }
                int conversions = 0;
                for (int attempts = 0; attempts < SpreadAttemptsPerBatch && conversions < SpreadConversionsPerBatch
                    && state.conversionsRemaining > 0 && state.attemptsRemaining > 0; attempts++)
                {
                    bool hill = state.hillSeeds.Count > 0;
                    PlanetTile tile;
                    if (hill) tile = PopBiomeTile(state.hillSeeds);
                    else if (state.seeds.Count > 0) tile = PopBiomeTile(state.seeds);
                    else if (state.frontier.Count > 0)
                    {
                        tile = PopBiomeTile(state.frontier, random: true);
                        state.membership.Remove(tile);
                    }
                    else { state.conversionsRemaining = 0; break; }
                    state.attemptsRemaining--;
                    if (IsSurfaceBiomeTile(tile) && IsXenoformedBiome(tile.Tile.PrimaryBiome))
                    {
                        // A seed may have been reached from another outbreak first.
                        if (hill) AddBiomeNeighbors(state, tile);
                    }
                    else if (!HasLoadedMap(tile) && CanXenoformBiome(tile, hill))
                    {
                        Current.Game.GetComponent<GameComponent_Nemesis>()?.SampleBiome(tile);
                        tile.Tile.PrimaryBiome = XenoformingBiomeFor(tile.Tile);
                        state.convertedTiles++;
                        state.conversionsRemaining--;
                        conversions++;
                        state.graphicsDirty = true;
                        AddBiomeNeighbors(state, tile);
                    }
                    else if (!hill && IsSurfaceBiomeTile(tile) && !tile.Tile.WaterCovered && state.membership.Add(tile))
                        state.deferred.Add(tile);
                    if (watch.ElapsedMilliseconds >= 2) break;
                }
                if (state.attemptsRemaining <= 0) state.conversionsRemaining = 0;
                if (conversions > 0 && !state.queenIncidentChecked)
                {
                    state.queenIncidentChecked = true;
                    TryOfferQueenNest(component);
                }
            }
            if (state.graphicsDirty && tick >= state.nextGraphicsTick)
            {
                // Biomes affect terrain materials. Do not regenerate unrelated world layers per batch.
                Find.WorldGrid.Surface.SetDirty<WorldDrawLayer_Terrain>();
                state.graphicsDirty = false;
                state.nextGraphicsTick = tick + 60000;
            }
        }

        private static void SeedInitialBiomeScatter(XenoformingSpreadState state)
        {
            List<Map> homes = Find.Maps.Where(map => map.IsPlayerHome && IsSurfaceBiomeTile(map.Tile)).ToList();
            if (homes.Count == 0) return;
            // Bounded local walks replace the old breadth-first search and world-object snapshot.
            for (int attempt = 0; attempt < 24 && state.initialScatterCount < 3; attempt++)
            {
                PlanetTile home = homes.RandomElement().Tile;
                PlanetTile tile = home;
                int steps = Rand.RangeInclusive(4, 12);
                for (int step = 0; step < steps; step++)
                {
                    state.neighbors.Clear();
                    Find.WorldGrid.Surface.GetTileNeighbors(tile, state.neighbors);
                    if (state.neighbors.Count == 0) break;
                    tile = state.neighbors.RandomElement();
                }
                float distance = Find.WorldGrid.Surface.ApproxDistanceInTiles(home, tile);
                if (distance < 4f || distance > 12f || !CanXenoformBiome(tile)
                    || Find.WorldObjects.AnyMapParentAt(tile) || state.seeds.Contains(tile)) continue;
                AddBiomeSeed(state, tile);
                state.initialScatterCount++;
            }
            state.initialScatterCompleted = state.initialScatterCount >= 3;
        }

        private static void TryOfferQueenNest(GameComponent_Xenomorph component)
        {
            if (component.Xenoforming < 25f || XMTUtility.QueenIsPlayer()
                || Find.QuestManager.ActiveQuestsListForReading.Any(quest => quest.root?.defName == "XMT_OpportunitySite_QueenNest")) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            IncidentParms parms = new IncidentParms { target = Find.World, forced = true,
                points = StorytellerUtility.DefaultThreatPointsNow(map) * 4 };
            Find.Storyteller.TryFire(new FiringIncident(XenoMapDefOf.XMT_GiveQuest_queenNest, null, parms));
        }

        internal static float DistantHillChance(float xenoforming)
        {
            return xenoforming < 50f ? 0f : Mathf.Lerp(0.5f, 0.9f, Mathf.InverseLerp(50f, 100f, xenoforming));
        }

        internal static bool TryFindDistantHillTile(out PlanetTile tile)
        {
            var surface = Find.WorldGrid.Surface;
            for (int attempt = 0; attempt < 64 && surface.TilesCount > 0; attempt++)
            {
                PlanetTile candidate = surface.PlanetTileForID(Rand.Range(0, surface.TilesCount));
                if (candidate.Tile.WaterCovered || candidate.Tile.temperature < StrangeHillMinimumTemperature
                    || Find.WorldObjects.AnyMapParentAt(candidate)) continue;
                // This helper checks one tile and its immediate neighbors, never a land-route search.
                if (!TileFinder.IsValidTileForNewSettlement(candidate)) continue;
                bool nearby = false;
                foreach (Map map in Find.Maps)
                    if (map.IsPlayerHome && IsSurfaceBiomeTile(map.Tile)
                        && surface.ApproxDistanceInTiles(map.Tile, candidate) < 40f) { nearby = true; break; }
                if (nearby) continue;
                tile = candidate;
                return true;
            }
            tile = PlanetTile.Invalid;
            return false;
        }

        public static bool IsSurfaceBiomeTile(PlanetTile tile)
        {
            return tile.Valid && tile.Layer == Find.WorldGrid.Surface && Find.WorldGrid.InBounds(tile);
        }

        public static bool CanXenoformBiome(PlanetTile tile, bool strangeHill = false)
        {
            if (!XenoformingMeets(10f) || !IsSurfaceBiomeTile(tile) || tile.Tile.WaterCovered)
            {
                return false;
            }

            // Spread suitability is shared by all variants, not the desert worker's warm-climate score.
            return XenoformingBiomeFor(tile.Tile) != null;
        }

        internal static bool HasLoadedMap(PlanetTile tile) => Find.Maps.Any(map => map.Tile == tile);

        private static readonly HarmonyLib.AccessTools.FieldRef<MixedBiomeMapComponent, bool[]> MixedBiomeGrid =
            HarmonyLib.AccessTools.FieldRefAccess<MixedBiomeMapComponent, bool[]>("biomeGrid");
        private static readonly HarmonyLib.AccessTools.FieldRef<MixedBiomeMapComponent, BiomeDef> MixedSecondaryBiome =
            HarmonyLib.AccessTools.FieldRefAccess<MixedBiomeMapComponent, BiomeDef>("secondaryBiome");
        private static readonly HarmonyLib.AccessTools.FieldRef<Tile, bool?> TileHasSecondaryBiome =
            HarmonyLib.AccessTools.FieldRefAccess<Tile, bool?>("tmpHasSecondaryBiome");
        private static readonly HarmonyLib.AccessTools.FieldRef<Tile, BiomeDef> TileSecondaryBiome =
            HarmonyLib.AccessTools.FieldRefAccess<Tile, BiomeDef>("tmpSecondaryBiome");
        private static readonly HarmonyLib.AccessTools.FieldRef<WaterBodyTracker, bool> AnyFish =
            HarmonyLib.AccessTools.FieldRefAccess<WaterBodyTracker, bool>("anyBodyContainsFish");

        internal static bool CompleteHarvestXenoforming(Map map)
        {
            if (map == null || !CanXenoformBiome(map.Tile) || IsXenoformedBiome(map.Biome)) return false;
            BiomeDef biome = XenoformingBiomeFor(map.TileInfo);
            if (biome == null) return false;
            Current.Game.GetComponent<GameComponent_Nemesis>()?.SampleBiome(map.Tile);
            map.TileInfo.PrimaryBiome = biome;
            foreach (TileMutatorDef mutator in map.TileInfo.Mutators.Where(mutator => mutator.Worker is TileMutatorWorker_MixedBiome).ToList())
                map.TileInfo.RemoveMutator(mutator);
            TileHasSecondaryBiome(map.TileInfo) = false;
            TileSecondaryBiome(map.TileInfo) = null;
            if (map.MixedBiomeComp != null)
            {
                MixedBiomeGrid(map.MixedBiomeComp) = null;
                MixedSecondaryBiome(map.MixedBiomeComp) = null;
            }
            if (map.waterBodyTracker != null)
            {
                foreach (WaterBody body in map.waterBodyTracker.Bodies)
                {
                    body.SetFishTypes();
                    body.Population = 0f;
                }
                AnyFish(map.waterBodyTracker) = false;
            }
            XenoformingSpreadState state = gameComponent.biomeSpread;
            // The initialization scan counts cells it has not reached yet itself.
            if (state.initialized || map.Tile.tileId < state.scanCursor) state.convertedTiles++;
            AddBiomeNeighbors(state, map.Tile);
            Find.WorldGrid.Surface.SetDirty<WorldDrawLayer_Terrain>();
            Find.LetterStack.ReceiveLetter("XMT_HarvestCompleteLabel".Translate(),
                "XMT_HarvestCompleteText".Translate(map.Parent.LabelCap, biome.LabelCap), LetterDefOf.NegativeEvent,
                new TargetInfo(map.Center, map));
            return true;
        }

        public static Command InvestigateCommand(Caravan caravan, Settlement settlement)
        {
            return new Command_Action
            {
                defaultLabel = "XMT_CommandInvestigateSite".Translate(),
                defaultDesc = "XMT_CommandInvestigateSiteDesc".Translate(),
                icon = InvestigateTex,
                action = delegate
                {
                    if (!settlement.HasMap)
                    {
                        LongEventHandler.QueueLongEvent(delegate
                        {
                            InvestigateSettlement(settlement, caravan);
                        }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
                    }
                    else
                    {
                        InvestigateSettlement(settlement, caravan);
                    }

                }
            };
        }

        public static void SettlementCounterAttack(Settlement settlement, Caravan caravan)
        {
            SurfaceTile tile = Find.WorldGrid[settlement.Tile.tileId];
            
            bool GeneratedMap = !settlement.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            TaggedString letterLabel = "XMT_LetterLabelCaravanAttackedByBase".Translate();
            TaggedString letterText = "XMT_LetterCaravanAttackedByBase".Translate(caravan.Label, settlement.Label.ApplyTag(TagType.Settlement, settlement.Faction.GetUniqueLoadID())).CapitalizeFirst();

            if (GeneratedMap)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }

            if (settlement.Faction != null)
            {
                FactionRelationKind playerRelationKind = settlement.Faction.PlayerRelationKind;
                Faction.OfPlayer.TryAffectGoodwillWith(settlement.Faction, Faction.OfPlayer.GoodwillToMakeHostile(settlement.Faction), canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.AttackedSettlement);
                settlement.Faction.TryAppendRelationKindChangedInfo(ref letterText, playerRelationKind, settlement.Faction.PlayerRelationKind);
            }
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NegativeEvent, caravan.PawnsListForReading, settlement.Faction);
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Center, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
        }

        public static void InvestigateSettlement(Settlement settlement, Caravan caravan)
        {
            SurfaceTile tile = Find.WorldGrid[settlement.Tile.tileId];
            XenoformingComp comp = null;
            bool questInvitedInvestigation = settlement.TryGetComponent(out comp) && comp.DistressQuestSent;

            if (!tile.Mutators.Contains(XenoMapDefOf.XMT_SettlementAftermath))
            {
                tile.AddMutator(XenoMapDefOf.XMT_SettlementAftermath);
            }

            bool GeneratedMap = !settlement.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            TaggedString letterLabel = "XMT_LetterLabelCaravanEnteredAftermathBase".Translate();
            TaggedString letterText = "XMT_LetterCaravanEnteredAftermathBase".Translate(caravan.Label, settlement.Label.ApplyTag(TagType.Settlement, settlement.Faction.GetUniqueLoadID())).CapitalizeFirst();

            if (GeneratedMap)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, caravan.PawnsListForReading, settlement.Faction);
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);

            if (questInvitedInvestigation)
            {
                if (!comp.DistressSignalTag.NullOrEmpty())
                {
                    QuestUtility.SendQuestTargetSignals(new List<string> { comp.DistressSignalTag }, "Investigated");
                }

                AwardDistressInvestigationGoodwill(settlement);
                gameComponent.RemoveCachedDistressSignal(settlement, comp.DistressQuestId, comp.DistressSignalTag);
                comp.Notify_Investigated();
            }
        }

        public static bool CellIsFertile(IntVec3 cell, Map map)
        {
            TerrainDef foundTerrain = cell.GetTerrain(map);
            
            if(foundTerrain.affordances.Contains(InternalDefOf.Resin))
            {
                return false;
            }

            return foundTerrain.fertility > 0 || (foundTerrain.driesTo != null && foundTerrain.driesTo.fertility > 0);
        }
        protected static TerrainDef DegradeTerrain(TerrainDef terrainDef)
        {
            if (terrainDef.fertility > 1.0 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility == 1.0))
            {
                return TerrainDefOf.Soil;
            }

            if (terrainDef.fertility == 1.0 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility == 1.0))
            {
                return TerrainDefOf.Gravel;
            }

            if (terrainDef.fertility > 0.5 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility > 0.5))
            {
                return TerrainDefOf.Sand;
            }

            return InternalDefOf.BarrenDust;

        }

        public static float ValueOfTerrainOnCell(Map map,IntVec3 cell, out TerrainDef degraded, TerrainDef startingTerrain = null)
        {
            if (startingTerrain == null)
            {
                startingTerrain = map.terrainGrid.TerrainAt(cell);
            }
            degraded = DegradeTerrain(startingTerrain);
            return startingTerrain.fertility - degraded.fertility;
        }
        public static float DegradeTerrainOnCell(Map map, IntVec3 cell, TerrainDef startingTerrain = null)
        {
            if (startingTerrain == InternalDefOf.HiveFloor)
            {
                map.terrainGrid.RemoveTopLayer(cell);
                return 0.5f;
            }
            else if (startingTerrain == InternalDefOf.HeavyHiveFloor)
            {
                map.terrainGrid.RemoveTopLayer(cell);
                return 1;
            }

            float drainedFertility = ValueOfTerrainOnCell(map, cell, out TerrainDef degraded ,startingTerrain);
            map.terrainGrid.SetTerrain(cell, degraded);
            return drainedFertility;
        }

        public static bool XenoformingMeets(float minXenoforming)
        {
            return gameComponent.Xenoforming >= minXenoforming;
        }

        public static void ReleaseEmbryoOnWorld(Pawn pawn)
        {
            gameComponent.ReleaseEmbryoOnWorld(pawn);
        }
        public static void HandleXenoformingImpact(Pawn pawn)
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message("[XMT][World] " + pawn + " is being evaluated for xenoforming impact");
            }
            if (pawn != null)
            {
                if(CaravanUtility.IsCaravanMember(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + pawn + " is in a caravan. Skipping.");
                    }
                    return;
                }
                if (XMTUtility.IsXenomorph(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + pawn + " is a xenomorph");
                    }
                    gameComponent.ReleaseXenomorphOnWorld(pawn);
                }
                else
                {
                    if(XMTUtility.HasEmbryo(pawn))
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message("[XMT][World] " + pawn + " has an embryo");
                        }
                        gameComponent.ReleaseEmbryoOnWorld(pawn);
                    }
                    float essence = BioUtility.GetXenomorphInfluence(pawn);
                    if (essence > 0)
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message("[XMT][World] " + pawn + " has mutations");
                        }
                        gameComponent.ReleaseMutagenOnWorld(essence);
                    }
                }

                ThingOwner carriedThings = pawn.inventory.GetDirectlyHeldThings();

                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] checking inventory for " + pawn);
                }
                if (carriedThings != null)
                {
                    foreach (Thing carriedThing in carriedThings)
                    {
                        HandleXenoformingImpact(carriedThing);
                    }
                }

            }
        }

        public static void HandleXenoformingImpact(HibernationCocoon hibernationCocoon)
        {
            HandleXenoformingImpact((SelfOccupyingBuilding)hibernationCocoon);
        }

        public static void HandleXenoformingImpact(SelfOccupyingBuilding selfOccupyingBuilding)
        {
            if (selfOccupyingBuilding != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] " + selfOccupyingBuilding + " is a self-occupying building");
                }
                HandleXenoformingImpact(selfOccupyingBuilding.ContainedThing as Pawn);
            }


        }
        public static void HandleXenoformingImpact(Ovomorph Ovomorph)
        {

            if (Ovomorph != null)
            {
                if (Ovomorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + Ovomorph + " is viable Ovomorph");
                    }
                    gameComponent.ReleaseOvomorphOnWorld(Ovomorph);
                    return;
                }
            }
        }

        public static float GetXenoforming()
        {
            return gameComponent.Xenoforming;
        }
        public static void HandleXenoformingImpact(Thing thing)
        {
            if(thing == null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] thing is null");
                }
                return;
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message("[XMT][World] " + thing + " is being evaluated for xenoforming impact");
            }

            if(thing is StarbeastCorpse corpse)
            {
                if(corpse.NotActuallyDead)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + thing + " is a not actually dead corpse!");
                    }
                    HandleXenoformingImpact(corpse.InnerPawn);
                    return;
                }
            }

            MinifiedThing minifiedThing = thing as MinifiedThing;

            if (minifiedThing != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] " + thing + " is a minified thing");
                }
                HandleXenoformingImpact(minifiedThing.InnerThing);
                return;
            }

            Ovomorph Ovomorph = thing as Ovomorph;

            if (Ovomorph != null)
            {
                if (Ovomorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + thing + " is viable Ovomorph");
                    }
                    gameComponent.ReleaseOvomorphOnWorld(Ovomorph);
                    return;
                }
            }

            SelfOccupyingBuilding selfOccupyingBuilding = thing as SelfOccupyingBuilding;

            if(selfOccupyingBuilding != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] " + thing + " is a self-occupying building");
                }
                HandleXenoformingImpact(selfOccupyingBuilding.ContainedThing as Pawn);
            }
            
            XMTGenePack genePack = thing as XMTGenePack;

            if (genePack != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] " + thing + " is a genepack");
                }
                gameComponent.ReleaseMutagenOnWorld(genePack.Potency*genePack.stackCount);
                return;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                HandleXenoformingImpact(pawn);
            }
        }

        internal static void HandleMatureMorphDeath(Pawn deadMorph)
        {
            if(deadMorph.def == InternalDefOf.XMT_Larva)
            {
                return;
            }

            gameComponent.HandleMatureMorphDeath(deadMorph);
        }

        internal static float ChanceByXenoforming(float chance)
        {
            return chance * (gameComponent.Xenoforming / 100);
        }

        internal static void SetXenoforming(float v)
        {
            gameComponent.Xenoforming = v;
        }

        internal static void IncreaseXenoforming(float v)
        {
            gameComponent.Xenoforming += v;
            Messages.Message("DEBUG: Xenoforming Increased to: " + gameComponent.Xenoforming, MessageTypeDefOf.NeutralEvent);

        }

        internal static void DecreaseXenoforming(float v)
        {
            gameComponent.Xenoforming -= v;
            Messages.Message("DEBUG: Xenoforming Decreased to: " + gameComponent.Xenoforming, MessageTypeDefOf.NeutralEvent);

        }

        internal static bool QueenCalledForAid(Pawn aggressor = null)
        {
            if(XMTUtility.QueenPresent())
            {
                return gameComponent.TryQueenCallForAid(aggressor);
            }
            return false;
        }

        internal static bool QueenCalledForAid(Pawn queen, Pawn aggressor)
        {
            if (queen == null)
            {
                return QueenCalledForAid(aggressor);
            }

            return gameComponent.TryQueenCallForAid(queen, aggressor);
        }

        public static Pawn GenerateFeralXenomorph()
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                               XenoPawnKindDefOf.XMT_FeralStarbeastKind, faction: null, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, fixedGender: Gender.Female);

            request.ForceNoIdeo = true;
            request.ForceNoBackstory = true;
            request.ForceNoGear = true;
            request.ForceBaselinerChance = 100;
            request.FixedBiologicalAge = InternalDefOf.XMT_Starbeast_AlienRace.race.lifeStageAges
                .First(stage => stage.def.developmentalStage == DevelopmentalStage.Adult).minAge;

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            BioUtility.ClearGenes(ref pawn);

            GeneSet genes = new GeneSet();
            BioUtility.ExtractCryptimorphGenesToGeneset(ref genes, BioUtility.GetWorldAppropriateXenotype().genes);
            BioUtility.ExtractGenesToGeneset(ref genes, GeneLineageUtility.Defaults);
            BioUtility.InsertGenesetToPawn(genes, ref pawn);
            return pawn;
        }

        public static Pawn GetWorldOrGeneratedFeralXenomorphForSite(bool allowPlayerPioneers = true)
        {
            return gameComponent.GetWorldOrGeneratedCryptimorphForSite(allowPlayerPioneers);
        }

        public static bool IsQueenAidDefender(Pawn pawn)
        {
            return gameComponent.IsQueenAidDefender(pawn);
        }

        public static RoyalEvolutionSet ResolveFeralQueenEvolutionSet(RoyalEvolutionSet explicitSet = null)
        {
            if (explicitSet != null)
            {
                return explicitSet;
            }

            GameComponent_Nemesis nemesis = Current.Game?.GetComponent<GameComponent_Nemesis>();
            if (nemesis?.Awakened == true && nemesis.CurrentStance?.evolutionSet != null)
            {
                return nemesis.CurrentStance.evolutionSet;
            }

            return RoyalEvolutionDefOf.BaseQueenSet;
        }

        public static Pawn GenerateFeralQueen(RoyalEvolutionSet advancementSet = null, RoyalEvolutionDef stopAfter = null)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                                   XenoPawnKindDefOf.XMT_RoyaltyKind, faction: null, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, fixedGender: Gender.Female);

            request.ForceNoIdeo = true;
            request.ForceNoBackstory = true;
            request.ForceNoGear = true;
            request.ForceBaselinerChance = 100;
            request.Faction = null;
            Pawn newQueen = PawnGenerator.GeneratePawn(request);


            BioUtility.ClearGenes(ref newQueen);
            newQueen.genes.SetXenotype(XenotypeDefOf.Baseliner);

            GeneSet genes = new GeneSet();
            BioUtility.ExtractGenesToGeneset(ref genes, BioUtility.GetWorldAppropriateXenotype().genes);
            BioUtility.ExtractGenesToGeneset(ref genes, InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.geneList);
            BioUtility.InsertGenesetToPawn(genes, ref newQueen);

            int advancements = QueenProgressionUtility.FeralQueenEvolutionPoints(GetXenoforming());
            advancementSet = ResolveFeralQueenEvolutionSet(advancementSet);

            if(newQueen.GetComp<CompQueen>() is CompQueen comp)
            {
                comp.RecieveProgress(advancements);

                foreach(RoyalEvolutionDef evo in QueenProgressionUtility.PredictedFeralQueenEvolutions(advancementSet, GetXenoforming(), stopAfter))
                {
                    comp.AddEvolution(evo);
                }
            }

            newQueen.GetComp<CompQueenAssimilation>()?.ApplyNpcProgression(advancementSet, GetXenoforming());
            Current.Game.GetComponent<GameComponent_Nemesis>().ApplyQueenGenome(newQueen);

            return newQueen;
        }

        internal static void PrepareQueenForOvoThrone(Pawn queen)
        {
            CompQueen compQueen = queen?.GetComp<CompQueen>();
            if (compQueen == null)
            {
                return;
            }

            if (RoyalEvolutionDefOf.BaseQueenSet?.evolutions == null)
            {
                return;
            }

            compQueen.RecieveProgress(10);
            foreach (RoyalEvolutionDef evolution in RoyalEvolutionDefOf.BaseQueenSet.evolutions)
            {
                if (evolution != null && !compQueen.ChosenEvolutions.Contains(evolution))
                {
                    compQueen.AddEvolution(evolution);
                }

                if (evolution == RoyalEvolutionDefOf.Evo_OvoThrone)
                {
                    break;
                }
            }
        }

        internal static void EnsureQueenHasOvoThrone(Pawn queen)
        {
            CompQueen compQueen = queen?.GetComp<CompQueen>();
            if (compQueen == null || RoyalEvolutionDefOf.Evo_OvoThrone == null)
            {
                return;
            }

            if (!compQueen.ChosenEvolutions.Contains(RoyalEvolutionDefOf.Evo_OvoThrone))
            {
                compQueen.AddEvolution(RoyalEvolutionDefOf.Evo_OvoThrone);
            }
        }

        internal static void AddReprisal(Faction faction, float points)
        {
            gameComponent.CacheReprisal(faction, points);
        }

        internal static void AddDistressSignal(Settlement settlement, int settlementDestroyedTick)
        {
            if (settlement == null || settlement.Destroyed || XenoMapDefOf.XMT_SettlementDistressSignal == null)
            {
                return;
            }

            if (!settlement.TryGetComponent(out XenoformingComp comp))
            {
                return;
            }

            string signalTag = comp.DistressSignalTag;
            if (signalTag.NullOrEmpty())
            {
                signalTag = "XMT_DistressSignal_" + settlement.ID + "_" + settlementDestroyedTick;
            }
            QuestUtility.AddQuestTag(settlement, signalTag);

            Slate slate = new Slate();
            slate.Set("settlement", settlement);
            slate.Set("faction", settlement.Faction);
            slate.Set("settlementLabel", settlement.Label);
            slate.Set("settlement_label", settlement.Label);
            slate.Set("timeoutTicks", Mathf.Max(1, settlementDestroyedTick - Find.TickManager.TicksGame));
            slate.Set("distressSignalTag", signalTag);
            slate.Set("investigatedSignal", signalTag + ".Investigated");
            slate.Set("expiredSignal", signalTag + ".Expired");

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(XenoMapDefOf.XMT_SettlementDistressSignal, slate);
            int questId = quest?.id ?? -1;
            comp.Notify_DistressSignalSent(questId, signalTag, settlementDestroyedTick);
            gameComponent.CacheDistressSignal(settlement, questId, signalTag, settlementDestroyedTick);
        }

        internal static void ConfirmDestroyedSettlement(Settlement settlement)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            XenoformingComp comp = null;
            settlement.TryGetComponent(out comp);
            if (comp != null)
            {
                if (!comp.DistressSignalTag.NullOrEmpty())
                {
                    QuestUtility.SendQuestTargetSignals(new List<string> { comp.DistressSignalTag }, "Expired");
                }

                EndDistressQuest(comp.DistressQuestId, QuestEndOutcome.Fail);
                gameComponent.RemoveCachedDistressSignal(settlement, comp.DistressQuestId, comp.DistressSignalTag);
            }

            if (settlement.HasMap)
            {
                comp?.Notify_RemoveWhenMapClears();
                return;
            }

            RemoveDestroyedSettlement(settlement, comp);
        }

        internal static bool IsSurfaceConquestSettlement(Settlement settlement)
        {
            return settlement != null && !settlement.Destroyed && settlement.Faction != null
                && !settlement.Faction.IsPlayer && IsSurfaceBiomeTile(settlement.Tile);
        }

        internal static bool HasOrbitalSettlement(Faction faction, IEnumerable<Settlement> settlements)
        {
            return faction != null && settlements.Any(settlement => settlement != null && !settlement.Destroyed
                && settlement.Faction == faction && settlement.Tile.Valid && settlement.Tile.LayerDef.isSpace);
        }

        internal static void BeginConquest(GameComponent_Xenomorph component)
        {
            if (component.conquestStarted || component.victoryShown || component.Queen?.Faction?.IsPlayer != true) return;
            // Resolve the def before changing any world objects.
            if (DefDatabase<SitePartDef>.GetNamedSilentFail("XMT_Hivesite_Small") == null) return;
            component.conquestStarted = true;
            component.conquestSettlements = Find.WorldObjects.Settlements.Where(IsSurfaceConquestSettlement).ToList();
        }

        internal static void ConquestUpdate(GameComponent_Xenomorph component)
        {
            // Update also runs while paused, so a paused victory can finish preparing its world.
            if (!component.conquestStarted && component.Xenoforming >= 100f) BeginConquest(component);
            if (!component.conquestStarted) return;
            if (!component.conquestCompleted && component.conquestSettlements.Count > 0)
            {
                int index = component.conquestSettlements.Count - 1;
                Settlement settlement = component.conquestSettlements[index];
                if (IsSurfaceConquestSettlement(settlement))
                {
                    settlement.TryGetComponent(out XenoformingComp comp);
                    if (comp == null)
                    {
                        Log.ErrorOnce("[XMT] Surface settlement lacks its XenoformingComp; conquest cannot safely replace it.", 79351420);
                        return;
                    }
                    comp.Notify_ReplaceWithHill();
                    ConfirmDestroyedSettlement(settlement);
                }
                component.conquestSettlements.RemoveAt(index);
            }
            // Replacement tiles are saved independently, including after an occupied map unloads.
            if (component.conquestHillTiles.Count > 0)
            {
                PlanetTile tile = component.conquestHillTiles[component.conquestHillTiles.Count - 1];
                if (TryCreateConquestHill(tile)) component.conquestHillTiles.RemoveAt(component.conquestHillTiles.Count - 1);
                return;
            }
            if (!component.conquestCompleted && component.conquestSettlements.Count == 0)
            {
                List<Settlement> settlements = Find.WorldObjects.Settlements;
                HashSet<Faction> defeated = new HashSet<Faction>();
                foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
                {
                    if (faction.IsPlayer || faction.Hidden || faction.temporary || !faction.def.humanlikeFaction
                        || faction.def == InternalDefOf.XMT_PlayerHive || HasOrbitalSettlement(faction, settlements)) continue;
                    faction.defeated = true;
                    defeated.Add(faction);
                }
                // Native queued incidents can otherwise fire even after their faction is defeated.
                ConquestIncidentQueue(Find.Storyteller.incidentQueue).RemoveAll(incident =>
                    incident.FiringIncident?.parms?.faction != null && defeated.Contains(incident.FiringIncident.parms.faction));
                component.conquestCompleted = true;
            }
            if (component.conquestCompleted && !component.victoryShown && component.Queen?.Faction?.IsPlayer == true
                && !GameComponent_Xenomorph.CountdownActivated)
                component.FinishConquestPreparation();
        }

        private static readonly HarmonyLib.AccessTools.FieldRef<IncidentQueue, List<QueuedIncident>> ConquestIncidentQueue =
            HarmonyLib.AccessTools.FieldRefAccess<IncidentQueue, List<QueuedIncident>>("queuedIncidents");

        private static bool TryCreateConquestHill(PlanetTile tile)
        {
            if (!IsSurfaceBiomeTile(tile)) return true;
            if (IsStrangeHillSite(Find.WorldObjects.SiteAt(tile))) return true;
            // A deferred settlement keeps its tile until its map has actually been removed.
            if (Find.WorldObjects.AnyMapParentAt(tile)) return false;
            SitePartDef part = DefDatabase<SitePartDef>.GetNamed("XMT_Hivesite_Small");
            Site site = SiteMaker.MakeSite(part, tile, null, ifHostileThenMustRemainHostile: false,
                threatPoints: Mathf.Max(part.minThreatPoints, StorytellerUtility.DefaultSiteThreatPointsNow()));
            Find.WorldObjects.Add(site);
            AddBiomeSeed(gameComponent.biomeSpread, tile);
            return true;
        }

        internal static void RemoveDestroyedSettlementIfReady(Settlement settlement)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            if (!settlement.TryGetComponent(out XenoformingComp comp) || !comp.RemoveWhenMapClears || settlement.HasMap)
            {
                return;
            }

            RemoveDestroyedSettlement(settlement, comp);
        }

        private static void RemoveDestroyedSettlement(Settlement settlement, XenoformingComp comp)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            PlanetTile seedTile = settlement.Tile;
            bool replaceWithHill = comp?.ReplaceWithHill == true && IsSurfaceBiomeTile(seedTile);
            if (replaceWithHill) gameComponent.conquestHillTiles.AddUnique(seedTile);
            comp?.Notify_DestroyedConfirmed();
            settlement.Destroy();
            if (settlement.Destroyed)
            {
                gameComponent.RegisterDestroyedSettlementBiomeSeed(seedTile);
            }
        }

        private static void EndDistressQuest(int questId, QuestEndOutcome outcome)
        {
            if (questId < 0)
            {
                return;
            }

            Quest quest = Find.QuestManager.ActiveQuestsListForReading.FirstOrDefault(activeQuest => activeQuest != null && activeQuest.id == questId);
            quest?.End(outcome, sendLetter: false, playSound: false);
        }

        private static void AwardDistressInvestigationGoodwill(Settlement settlement)
        {
            Faction faction = settlement?.Faction;
            if (faction == null || faction == Faction.OfPlayer || faction.HostileTo(Faction.OfPlayer) || faction.temporary)
            {
                return;
            }

            int goodwill = faction.PlayerRelationKind == FactionRelationKind.Ally ? 10 : 5;
            Faction.OfPlayer.TryAffectGoodwillWith(faction, goodwill, canSendMessage: true, canSendHostilityLetter: false);
        }
    }
}
