using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static System.Collections.Specialized.BitVector32;

namespace Xenomorphtype
{
    [Flags]
    internal enum XenoformingPawnAccountingState
    {
        None = 0,
        WorldXenoformingCounted = 1,
        QueenAidBorrowed = 2,
        SiteBorrowed = 4,
        DeathAccounted = 8
    }

    public class GameComponent_Xenomorph : GameComponent
    {
        public Pawn Queen = null;

        List<PlanetTile> CandidateTiles = new List<PlanetTile>();

        bool PlayerEmbryoInWorld = false;
        bool PlayerXenomorphInWorld = false;
        bool PlayerOvomorphInWorld = false;

        private List<string> deadMorphs = new List<string>();
        private Dictionary<string, XenoformingPawnAccountingState> xenoformingPawnAccounting = new Dictionary<string, XenoformingPawnAccountingState>();
        private List<string> ideologyResinBuildThingIds = new List<string>();

        const int XenoformingCheckInterval = 60000;
        public bool QueenInWorld
        {
            get
            {
                if(Queen != null)
                {
                    return WorldPawnsUtility.IsWorldPawn(Queen);
                }
                return false;
            }
        }

        private float _lastxenoforming;
        private float _xenoforming;
        public float Xenoforming
        {
            get
            {
                return _xenoforming;
            }
            set
            {

                if (value >= 0)
                {
                    _xenoforming = value;
                }
                else
                {
                    _xenoforming = 0;
                }
                if (XMTSettings.LogWorld)
                {
                    Log.Message("Adjusting Xenoforming by total: " + _xenoforming);
                }
                EvaluateXenoforming();
                
            }
        }
        float MutationProliferation = 0;

        int nextXenoformingTick = -1;

        private const float XenomorphImpact = 1f;
        private const float OvomorphSaturationLimit = 5;
        private const float OvomorphImpact = 0.1f;
        private const float EmbryoSaturationLimit = 10;
        private const float EmbryoImpact = 0.5f;
        private const float QueenAidCostPerPawn = 0.35f;
        private const int QueenAidCooldownTicks = 60000;
        private const int QueenAidWaveIntervalTicks = 1200;
        private const int QueenAidMaxWaves = 5;
        private const int QueenAidMaxActivePawns = 40;
        private const int QueenAidMaxPawnsPerWave = 8;
        private const float QueenAidThreatPawnFactor = 0.75f;
        private const float QueenAidMinimumXenoforming = 10f;

        private List<int> queenAidPawnIDs = new List<int>();
        private int nextQueenAidTick = -1;
        private int nextQueenAidWaveTick = -1;
        private int queenAidWavesRemaining = 0;
        private int queenAidSpawnedThisResponse = 0;
        private bool queenAidLetterSent = false;
        private bool queenAidResponseActive = false;
        private QueenAidThreatProfile queenAidThreatProfile;
        private Faction queenAidFaction;

        private int _xenoformingStartTick = -1;

        private List<Faction> _reprisalFactions;
        private float _totalReprisalRaidPoints;

        //countdown
        private const float ScreenFadeSeconds = 6f;
        private static float timeLeft = -1f;
        public static bool CountdownActivated => timeLeft > 0f;

        public GameComponent_Xenomorph(Game game)
        {
            Queen = null;
            if (ModsConfig.AnomalyActive)
            {
      
            }
        }

        private void BeginCountDown()
        {
            timeLeft = ScreenFadeSeconds;

            SoundDefOf.PlanetkillerImpact.PlayOneShot(Queen);
            ScreenFader.StartFade(Color.black, 6f);

   
        }

        private void EndGame()
        {
            if(Queen == null)
            {
                return;
            }

            StringBuilder stringBuilder = new StringBuilder();
            List<Pawn> list = (from p in Queen.MapHeld.mapPawns.PawnsInFaction(Faction.OfPlayer)
                               where p.GetMorphComp() != null
                               select p).ToList();
            foreach (Pawn item in list)
            {
                if (!item.Dead && !item.IsQuestLodger())
                {
                    stringBuilder.AppendLine("   " + item.LabelCap);
                    Find.StoryWatcher.statsRecord.colonistsLaunched++;
                }
            }

            GameVictoryUtility.ShowCredits(GameVictoryUtility.MakeEndCredits("XMT_GameOverXenoformedIntro".Translate(), "XMT_GameOverXenoformedEnding".Translate(), stringBuilder.ToString(), "XMT_GameOverChildren", list), SongDefOf.EndCreditsSong, exitToMainMenu: true, 2.5f);
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            if (CountdownActivated)
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f)
                {
                    EndGame();
                }
                return;
            }

        }
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            QueenAidTick();

            if (_xenoforming <= 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick >= nextXenoformingTick)
            {
                
                if(_xenoforming >= 1.0f)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Iterating Embryo birth time");
                    }
                    _xenoforming += (_xenoforming/100) * XMTSettings.XenoformingGrowthFactor;
                }
                if (_xenoforming >= 0.5f)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Iterating Embryo birth time");
                    }
                    _xenoforming += 0.1f * XMTSettings.XenoformingGrowthFactor;
                }
                else
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Iterating Egg encounter");
                    }
                    _xenoforming += 0.01f * XMTSettings.XenoformingGrowthFactor;
                }

                nextXenoformingTick = tick + XenoformingCheckInterval;

                EvaluateXenoforming();
                BiomeXenoformingImpact();
                LaunchCachedReprisal();
                
            }
        }

        private void GetCandidateNeighbors(PlanetTile origin)
        {
            List<PlanetTile> outNeighbor = new List<PlanetTile>();
            Find.WorldGrid.Surface.GetTileNeighbors(origin, outNeighbor);

            foreach (PlanetTile tile in outNeighbor)
            {
                if (tile.Tile.PrimaryBiome == XenoMapDefOf.XMT_DessicatedBlight)
                {
                    continue;
                }
                CandidateTiles.AddUnique(tile);
            }
        }

        public void BiomeXenoformingImpact()
        {
            if (_xenoforming >= 10)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("Xenoforming Biomes");
                }

                if (CandidateTiles == null || CandidateTiles.Count == 0)
                {
                    CandidateTiles = new List<PlanetTile> { };

                    Map playerMap = Find.AnyPlayerHomeMap;

                    if (playerMap != null)
                    {
                        GetCandidateNeighbors(playerMap.Tile);
                    }
                }

                int candidatesPicked = 0;
                int maxCandidatesForXenoforming = Mathf.FloorToInt( (Xenoforming * Xenoforming) * XMTSettings.BiomeSpreadFactor);
                List<PlanetTile> targetTiles = new List<PlanetTile>();
                List<PlanetTile> safeCandidateList = new List<PlanetTile>();

                CandidateTiles.CopyToList(safeCandidateList);
                safeCandidateList.Shuffle();

                bool shouldSpawnQueenNest = _xenoforming >= 25 && (!XMTUtility.QueenIsPlayer());
                bool noQueenIncident = true;

                if (shouldSpawnQueenNest)
                {
                    foreach(Quest quest in Find.QuestManager.ActiveQuestsListForReading)
                    {
                        if(quest.root.defName == "XMT_OpportunitySite_QueenNest")
                        {
                            noQueenIncident = false;
                            break;
                        }
                    }
                }

                foreach (PlanetTile candidate in safeCandidateList)
                {
                    float score = XenoMapDefOf.XMT_DessicatedBlight.Worker.GetScore(XenoMapDefOf.XMT_DessicatedBlight, candidate.Tile, candidate);

                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Xenoforming biome score: " + score);
                    }

                    if (score > 1)
                    {
                        GetCandidateNeighbors(candidate);
                        targetTiles.Add(candidate);
                        candidatesPicked += 1;

                        if (candidatesPicked > maxCandidatesForXenoforming)
                        {
                            break;
                        }
                    }
                }

                foreach(PlanetTile target in targetTiles)
                {
                    target.Tile.PrimaryBiome = XenoMapDefOf.XMT_DessicatedBlight;

                    if(shouldSpawnQueenNest && noQueenIncident)
                    {
                        Map map = Find.AnyPlayerHomeMap;
                        if (map == null)
                        {
                            return;
                        }

                        IncidentParms parms = new IncidentParms
                        {
                            target = Find.World,
                            forced = true,
                            points = StorytellerUtility.DefaultThreatPointsNow(map) * 4
                        };
                        FiringIncident queenIncident =  new FiringIncident(XenoMapDefOf.XMT_GiveQuest_queenNest, null, parms);

                        Find.Storyteller.TryFire(queenIncident);
                        noQueenIncident = false;
                    }
                    CandidateTiles.Remove(target);
                }

                if (targetTiles.Count > 0)
                {
                    List<WorldDrawLayer> drawLayers = Find.WorldGrid.Surface.WorldDrawLayers;
                    foreach (WorldDrawLayer layer in drawLayers)
                    {
                        layer.SetDirty();
                    }
                }
            }
        }

        public void EvaluateXenoforming()
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message("Evaluating current Xenoforming: " + _xenoforming);
            }

            if (_xenoforming == _lastxenoforming)
            {
                return;
            }

            if(_lastxenoforming <= 0 && _xenoforming > 0)
            {
                _xenoformingStartTick = Find.TickManager.TicksGame;

                //TODO: Message Xenomorph released on planet.

                if (XMTSettings.LogWorld)
                {
                    Log.Message("Xenoforming has begun with: " + _xenoforming);
                }
            }

            if (_xenoforming <= 0)
            {
                _xenoformingStartTick = -1;

                //TODO: Message Xenomorph Eradication.

                if (XMTSettings.LogWorld)
                {
                    Log.Message("Xenoforming has ended.");
                }
            }
            _lastxenoforming = _xenoforming;

            if(Queen == null)
            {
                return;
            }

            if(Queen.Faction == Faction.OfPlayerSilentFail)
            {
                if(_lastxenoforming >= 100)
                {
                    BeginCountDown();
                }
            }
        }

        public override void LoadedGame()
        {
           
        }
        public override void StartedNewGame()
        {
            Log.Message("Clearing Caches");
            XMTHiveUtility.ClearAllNestSites();
            InfiltrationUtility.ClearAllCaches();
            PawnCacheWrapper.ClearAllPawnCaches();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _xenoforming, "Xenoforming", 0);
            Scribe_Values.Look(ref _lastxenoforming, "lastXenoforming", 0);
            Scribe_Values.Look(ref _xenoformingStartTick, "xenoformingStartTick", -1);
            Scribe_Values.Look(ref nextXenoformingTick, "nextXenoformingTick", 0);
            Scribe_Values.Look(ref MutationProliferation, "MutationProliferation", 0);
            Scribe_Values.Look(ref PlayerEmbryoInWorld, "PlayerEmbryoInWorld", false);
            Scribe_Values.Look(ref PlayerXenomorphInWorld, "PlayerXenomorphInWorld", false);
            Scribe_Values.Look(ref PlayerOvomorphInWorld, "PlayerOvomorphInWorld", false);
            Scribe_Collections.Look(ref CandidateTiles, "CandidateTiles");
            Scribe_Collections.Look(ref deadMorphs, "deadMorphs");
            Scribe_Collections.Look(ref xenoformingPawnAccounting, "xenoformingPawnAccounting", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref ideologyResinBuildThingIds, "ideologyResinBuildThingIds");
            Scribe_Collections.Look(ref queenAidPawnIDs, "queenAidPawnIDs");
            Scribe_Values.Look(ref nextQueenAidTick, "nextQueenAidTick", -1);
            Scribe_Values.Look(ref nextQueenAidWaveTick, "nextQueenAidWaveTick", -1);
            Scribe_Values.Look(ref queenAidWavesRemaining, "queenAidWavesRemaining", 0);
            Scribe_Values.Look(ref queenAidSpawnedThisResponse, "queenAidSpawnedThisResponse", 0);
            Scribe_Values.Look(ref queenAidLetterSent, "queenAidLetterSent", false);
            Scribe_Values.Look(ref queenAidResponseActive, "queenAidResponseActive", false);
            Scribe_Deep.Look(ref queenAidThreatProfile, "queenAidThreatProfile");
            Scribe_References.Look(ref queenAidFaction, "queenAidFaction");

            Scribe_Values.Look(ref _totalReprisalRaidPoints, "_totalReprisalRaidPoints", 0);
            Scribe_Collections.Look(ref _reprisalFactions, "ReprisalFactions",LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Log.Message("Clearing Caches");
                XMTHiveUtility.ClearAllNestSites();
                InfiltrationUtility.ClearAllCaches();
                PawnCacheWrapper.ClearAllPawnCaches();
                xenoformingPawnAccounting ??= new Dictionary<string, XenoformingPawnAccountingState>();
                deadMorphs ??= new List<string>();
                queenAidPawnIDs ??= new List<int>();
                foreach (int thingID in queenAidPawnIDs)
                {
                    AddPawnAccountingState(ThingIDAccountingKey(thingID), XenoformingPawnAccountingState.QueenAidBorrowed);
                }
                if (XMTSettings.LogWorld)
                {
                    Log.Message("loaded " + _reprisalFactions.Count + " factions for a total reprisal raid points: " + _totalReprisalRaidPoints);
                }
            }

            
        }

        public void CacheReprisal(Faction faction, float points)
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message("Caching Reprisal for " + faction + " with " + points + " points");
            }
            if (_reprisalFactions == null)
            {
                _reprisalFactions = new List<Faction>();
            }

            if (!faction.temporary)
            {
                _reprisalFactions.AddUnique(faction);
            }
            _totalReprisalRaidPoints += points;
        }
        public void LaunchCachedReprisal()
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message("Checking for Reprisal");
            }

            Pawn queen = XMTUtility.GetQueen();
            if(queen == null)
            {
                return;
            }

            Map map = queen.MapHeld;
            if (map == null)
            {
                return;
            }

            if (_totalReprisalRaidPoints < StorytellerUtility.DefaultThreatPointsNow(map))
            {
                return;
            }

            if(_reprisalFactions == null || _reprisalFactions.Count == 0)
            {
                return;
            }

            List<Faction> orderedFactions= new List<Faction>();
            orderedFactions.Add(_reprisalFactions[0]);
            foreach(Faction faction in _reprisalFactions)
            {
                if(!faction.def.canStageAttacks)
                {
                    continue;
                }
                FactionDef bestFaction = orderedFactions[0].def;
                if (bestFaction.techLevel < faction.def.techLevel)
                {
                    if(orderedFactions.Contains(faction))
                    {
                        orderedFactions.Remove(faction);
                    }
                    orderedFactions.Insert(0, faction);
                    continue;
                }

                if(!bestFaction.canSiege && faction.def.canSiege)
                {
                    if (orderedFactions.Contains(faction))
                    {
                        orderedFactions.Remove(faction);
                    }
                    orderedFactions.Insert(0, faction);
                    continue;
                }
                orderedFactions.AddUnique(faction);
            }
            Faction leaderFaction = orderedFactions[0];
            bool attemptSiege = false;
            float raidpoints = Mathf.Min(StorytellerUtility.DefaultThreatPointsNow(map) * 4, _totalReprisalRaidPoints);
            float TicksToArrive = Rand.Range(30000, 60000);

            foreach (Faction faction in orderedFactions)
            {
                IncidentParms parms = new IncidentParms
                {
                    target = map,
                    faction = faction,
                    forced = true,
                    points = raidpoints
                };

                if (_xenoforming > 50)
                {
                    if(!leaderFaction.AllyOrNeutralTo(faction) && faction != leaderFaction)
                    {
                        continue;
                    }
                    parms.raidStrategy = ExternalDefOf.ImmediateAttackBreachingSmart;
                }

                if(faction != leaderFaction)
                {
                    parms.silent = true;
                }

                
                if (attemptSiege && faction.def.canSiege)
                {
                    parms.raidStrategy = ExternalDefOf.Siege;

                    Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + Mathf.FloorToInt(TicksToArrive), parms, 120000);
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(faction + " is sending a siege of points: " + parms.points + " in reprisal to losing a settlement. Arriving in " + TicksToArrive / 2400 + " hours");
                    }
                }
                else
                {
                    Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + Mathf.FloorToInt(TicksToArrive), parms, 120000);
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(faction + " is sending a raid of points: " + parms.points + " in reprisal to losing a settlement. Arriving in " + TicksToArrive / 2400 + " hours");
                    }
                }
                _totalReprisalRaidPoints = Mathf.Max(0, _totalReprisalRaidPoints - raidpoints);

                if(_totalReprisalRaidPoints <= 0)
                {
                    break;
                }
                raidpoints = Mathf.Min(raidpoints * 0.75f, _totalReprisalRaidPoints);
            }
        }
        public void HandleMatureMorphDeath(Pawn pawn)
        {
            if (pawn == null || !pawn.ageTracker.Adult)
            {
                return;
            }

            string key = PawnAccountingKey(pawn);
            if (HasPawnAccountingState(key, XenoformingPawnAccountingState.DeathAccounted) || deadMorphs.Contains(pawn.ToString()))
            {
                return;
            }

            bool borrowedQueenAid = RemovePawnAccountingState(key, XenoformingPawnAccountingState.QueenAidBorrowed) || RemovePawnAccountingState(ThingIDAccountingKey(pawn.thingIDNumber), XenoformingPawnAccountingState.QueenAidBorrowed);
            bool borrowedSitePawn = RemovePawnAccountingState(key, XenoformingPawnAccountingState.SiteBorrowed);
            bool countedWorldPawn = HasPawnAccountingState(key, XenoformingPawnAccountingState.WorldXenoformingCounted);

            AddPawnAccountingState(key, XenoformingPawnAccountingState.DeathAccounted);
            deadMorphs.AddDistinct(pawn.ToString());

            if (borrowedQueenAid)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("Queen aid pawn " + pawn + " died; aid cost remains spent and normal xenoforming death adjustment is skipped.");
                }
                return;
            }

            bool adjustedXenoforming = !borrowedSitePawn || countedWorldPawn;
            if (adjustedXenoforming)
            {
                _xenoforming = Mathf.Max(_xenoforming - XenomorphImpact, 0);
            }

            if (adjustedXenoforming && XMTUtility.QueenIsPlayer())
            {
                Messages.Message("XMT_XenoformingLowered".Translate(), MessageTypeDefOf.NegativeEvent);
            }

            if (adjustedXenoforming && XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for death of " + pawn.ToString() + " total: " + _xenoforming);
            }
            if (adjustedXenoforming)
            {
                EvaluateXenoforming();
            }
        }
        public void ReleaseOvomorphOnWorld(Ovomorph Ovomorph)
        {
            Ovomorph.HatchNow();
            _xenoforming = Mathf.Max(_xenoforming, Mathf.Min(OvomorphSaturationLimit, _xenoforming + (OvomorphImpact*Ovomorph.stackCount)));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + Ovomorph + " leaving the map. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        internal void ReleaseMutagenOnWorld(float intensity)
        {
            
            MutationProliferation += intensity;
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting mutation proliferation by " + intensity);
            }
            EvaluateXenoforming();
        }

        internal void ReleaseXenomorphOnWorld(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            string key = PawnAccountingKey(pawn);
            if (RemovePawnAccountingState(key, XenoformingPawnAccountingState.QueenAidBorrowed) || RemovePawnAccountingState(ThingIDAccountingKey(pawn.thingIDNumber), XenoformingPawnAccountingState.QueenAidBorrowed))
            {
                _xenoforming += QueenAidCostPerPawn;
                if (XMTSettings.LogWorld)
                {
                    Log.Message("Queen aid pawn " + pawn + " returned to the xenoforming pool. Xenoforming total: " + _xenoforming);
                }
                EvaluateXenoforming();
                return;
            }

            if (RemovePawnAccountingState(key, XenoformingPawnAccountingState.SiteBorrowed))
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("World cryptimorph " + pawn + " returned from a site without changing xenoforming.");
                }
                return;
            }

            if (HasPawnAccountingState(key, XenoformingPawnAccountingState.WorldXenoformingCounted))
            {
                return;
            }

            _xenoforming += XenomorphImpact;
            AddPawnAccountingState(key, XenoformingPawnAccountingState.WorldXenoformingCounted);

            if(XMTUtility.QueenIsPlayer())
            {
                Messages.Message("XMT_XenoformingRaised".Translate(), MessageTypeDefOf.PositiveEvent);
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        internal void ReleaseEmbryoOnWorld(Pawn pawn)
        {
            
            _xenoforming = Mathf.Max(_xenoforming,Mathf.Min(EmbryoSaturationLimit, _xenoforming + (EmbryoImpact)));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map with an embryo. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        private void QueenAidTick()
        {
            if (!queenAidResponseActive)
            {
                return;
            }

            Pawn queen = XMTUtility.GetQueen();
            Thing protectionTarget = QueenAidProtectionTarget(queen);
            if (queen == null || queen.Dead || protectionTarget == null || protectionTarget.Destroyed || !protectionTarget.Spawned || protectionTarget.MapHeld == null || _xenoforming < QueenAidMinimumXenoforming)
            {
                EndQueenAidResponse(startCooldown: true);
                return;
            }

            if (queenAidThreatProfile == null || CountQueenAidThreats(queen, queenAidThreatProfile) <= 0)
            {
                EndQueenAidResponse(startCooldown: true);
                return;
            }

            if (queenAidWavesRemaining <= 0)
            {
                EndQueenAidResponse(startCooldown: true);
                return;
            }

            if (Find.TickManager.TicksGame < nextQueenAidWaveTick)
            {
                return;
            }

            if (!SpawnQueenAidWave(queen, queenAidThreatProfile))
            {
                EndQueenAidResponse(startCooldown: true);
                return;
            }

            queenAidWavesRemaining--;
            nextQueenAidWaveTick = queenAidWavesRemaining > 0 ? Find.TickManager.TicksGame + QueenAidWaveIntervalTicks : -1;
        }

        private void EndQueenAidResponse(bool startCooldown)
        {
            queenAidResponseActive = false;
            queenAidWavesRemaining = 0;
            queenAidSpawnedThisResponse = 0;
            queenAidLetterSent = false;
            nextQueenAidWaveTick = -1;
            queenAidThreatProfile = null;
            if (startCooldown)
            {
                nextQueenAidTick = Find.TickManager.TicksGame + QueenAidCooldownTicks;
            }
        }

        internal bool TryQueenCallForAid(Pawn aggressor)
        {
            return TryQueenCallForAid(XMTUtility.GetQueen(), aggressor);
        }

        internal bool TryQueenCallForAid(Pawn queen, Pawn aggressor)
        {
            Thing protectionTarget = QueenAidProtectionTarget(queen);
            if (queen == null || queen.Dead || protectionTarget == null || protectionTarget.Destroyed || !protectionTarget.Spawned || protectionTarget.MapHeld == null)
            {
                return false;
            }

            if (_xenoforming < QueenAidMinimumXenoforming)
            {
                return false;
            }

            if (queenAidResponseActive)
            {
                return false;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (nextQueenAidTick > ticksGame)
            {
                return false;
            }

            if (ActiveQueenAidPawnCount(protectionTarget.MapHeld) >= QueenAidMaxActivePawns)
            {
                return false;
            }

            queenAidThreatProfile = new QueenAidThreatProfile(aggressor);
            queenAidResponseActive = true;
            queenAidSpawnedThisResponse = 0;
            queenAidLetterSent = false;
            queenAidWavesRemaining = QueenAidMaxWaves;
            StartQueenDefensePanic(protectionTarget.MapHeld);

            if (!SpawnQueenAidWave(queen, queenAidThreatProfile))
            {
                EndQueenAidResponse(startCooldown: false);
                return false;
            }

            queenAidWavesRemaining--;
            nextQueenAidWaveTick = queenAidWavesRemaining > 0 ? ticksGame + QueenAidWaveIntervalTicks : -1;
            return true;
        }

        private bool SpawnQueenAidWave(Pawn queen, QueenAidThreatProfile threatProfile)
        {
            Thing protectionTarget = QueenAidProtectionTarget(queen);
            Map map = protectionTarget?.MapHeld;
            if (map == null)
            {
                return false;
            }

            IntVec3 entryCell;
            if (!TryFindQueenAidEntryCell(queen, out entryCell))
            {
                return false;
            }

            int desiredCount = DesiredQueenAidTotal(queen, threatProfile);
            int remainingDesiredCount = Mathf.Max(0, desiredCount - queenAidSpawnedThisResponse);
            int activeCapacity = QueenAidMaxActivePawns - ActiveQueenAidPawnCount(map);
            int affordableCount = Mathf.FloorToInt(Mathf.Max(0f, _xenoforming - QueenAidMinimumXenoforming) / QueenAidCostPerPawn);
            int count = Mathf.Min(remainingDesiredCount, QueenAidMaxPawnsPerWave, activeCapacity, affordableCount);

            if (count <= 0)
            {
                return false;
            }

            List<Pawn> spawnedPawns = new List<Pawn>();
            Rot4 rot = Rot4.FromAngleFlat((map.Center - entryCell).AngleFlat);
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = TakeWorldCryptimorphForUse(XenoformingPawnAccountingState.QueenAidBorrowed, queen.Faction?.IsPlayer ?? false) ?? XenoformingUtility.GenerateFeralXenomorph();
                Faction defenderFaction = GetQueenAidFaction(queen.Faction);
                if (defenderFaction != null)
                {
                    pawn.SetFaction(defenderFaction);
                }

                IntVec3 loc = CellFinder.RandomClosewalkCellNear(entryCell, map, 10);
                GenSpawn.Spawn(pawn, loc, map, rot);
                CompCrawler crawler = pawn.GetComp<CompCrawler>();
                if (crawler != null)
                {
                    crawler.Crawling = true;
                }
                pawn.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + QueenAidCooldownTicks;
                spawnedPawns.Add(pawn);
                AddPawnAccountingState(pawn, XenoformingPawnAccountingState.QueenAidBorrowed);
            }

            if (!spawnedPawns.Any())
            {
                return false;
            }

            _xenoforming = Mathf.Max(0, _xenoforming - (QueenAidCostPerPawn * spawnedPawns.Count));
            queenAidSpawnedThisResponse += spawnedPawns.Count;
            Faction lordFaction = spawnedPawns[0].Faction;
            LordMaker.MakeNewLord(lordFaction, new LordJob_QueenAid(queen, threatProfile), map, spawnedPawns);
            SendQueenAidLetter(queen, spawnedPawns);

            if (XMTSettings.LogWorld)
            {
                Log.Message("Queen aid wave spawned " + spawnedPawns.Count + " cryptimorphs for " + queen + " against " + CountQueenAidThreats(queen, threatProfile) + " threats. Xenoforming total: " + _xenoforming);
            }

            EvaluateXenoforming();
            return true;
        }

        private bool TryFindQueenAidEntryCell(Pawn queen, out IntVec3 entryCell)
        {
            entryCell = IntVec3.Invalid;
            Thing protectionTarget = QueenAidProtectionTarget(queen);
            Map map = protectionTarget?.MapHeld;
            if (map == null || protectionTarget == null)
            {
                return false;
            }

            List<IntVec3> candidates = map.AllCells
                .Where(cell => IsQueenAidEntryCandidate(cell, map, protectionTarget))
                .OrderBy(cell => cell.DistanceToSquared(protectionTarget.Position))
                .Take(80)
                .ToList();

            if (candidates.Any())
            {
                entryCell = candidates.RandomElement();
                return true;
            }

            return RCellFinder.TryFindRandomPawnEntryCell(out entryCell, map, CellFinder.EdgeRoadChance_Animal);
        }

        private bool IsQueenAidEntryCandidate(IntVec3 cell, Map map, Thing protectionTarget)
        {
            if (cell.x != 0 && cell.z != 0 && cell.x != map.Size.x - 1 && cell.z != map.Size.z - 1)
            {
                return false;
            }

            if (!cell.Standable(map) || cell.Fogged(map) || cell.GetFirstPawn(map) != null)
            {
                return false;
            }

            return map.reachability.CanReach(cell, protectionTarget.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors));
        }

        private Faction GetQueenAidFaction(Faction queenFaction)
        {
            if (queenAidFaction == null || queenAidFaction.def != InternalDefOf.XMT_QueenAidDefenders)
            {
                queenAidFaction = Find.FactionManager.FirstFactionOfDef(InternalDefOf.XMT_QueenAidDefenders);
            }

            if (queenAidFaction == null)
            {
                queenAidFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(InternalDefOf.XMT_QueenAidDefenders));
                Find.FactionManager.Add(queenAidFaction);
            }

            AlignQueenAidFactionToQueen(queenAidFaction, queenFaction);
            return queenAidFaction;
        }

        private void AlignQueenAidFactionToQueen(Faction defenderFaction, Faction queenFaction)
        {
            if (defenderFaction == null || queenFaction == null || defenderFaction == queenFaction)
            {
                return;
            }

            defenderFaction.TryMakeInitialRelationsWith(queenFaction);
            queenFaction.TryMakeInitialRelationsWith(defenderFaction);

            FactionRelation defenderRelation = defenderFaction.RelationWith(queenFaction, allowNull: false);
            defenderRelation.kind = FactionRelationKind.Ally;

            FactionRelation queenRelation = queenFaction.RelationWith(defenderFaction, allowNull: false);
            queenRelation.kind = FactionRelationKind.Ally;
        }

        private void StartQueenDefensePanic(Map map)
        {
            if (map == null)
            {
                return;
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == null || pawn.Dead || pawn.Downed || pawn == Queen || !XMTUtility.IsXenomorph(pawn))
                {
                    continue;
                }

                pawn.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
            }
        }

        private void SendQueenAidLetter(Pawn queen, List<Pawn> spawnedPawns)
        {
            if (queenAidLetterSent || spawnedPawns.NullOrEmpty())
            {
                return;
            }

            if (queen?.Faction == Faction.OfPlayer)
            {
                TaggedString label = "XMT_LetterLabelQueenDefendersArrived".Translate();
                TaggedString text = "XMT_QueenDefendersArrived".Translate(InternalDefOf.XMT_FeralStarbeastKind.GetLabelPlural());
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, new LookTargets(spawnedPawns));
            }
            else if (queenAidThreatProfile != null && queenAidThreatProfile.AggressorIsPlayerFaction && queenAidThreatProfile.AggressorIsHumanlike)
            {
                TaggedString label = "XMT_LetterLabelQueenAidScreech".Translate();
                TaggedString text = "XMT_QueenAidScreech".Translate();
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, new LookTargets(spawnedPawns));
            }
            else
            {
                return;
            }

            queenAidLetterSent = true;
        }

        private int DesiredQueenAidTotal(Pawn queen, QueenAidThreatProfile threatProfile)
        {
            int threatCount = Mathf.Max(1, CountQueenAidThreats(queen, threatProfile));
            int desired = 2 + Mathf.CeilToInt(threatCount * QueenAidThreatPawnFactor);
            int affordableTotal = Mathf.FloorToInt(Mathf.Max(0f, _xenoforming - QueenAidMinimumXenoforming) / QueenAidCostPerPawn) + queenAidSpawnedThisResponse;
            return Mathf.Clamp(Mathf.Min(desired, affordableTotal), 1, QueenAidMaxActivePawns);
        }

        private int CountQueenAidThreats(Pawn queen, QueenAidThreatProfile threatProfile)
        {
            Thing protectionTarget = QueenAidProtectionTarget(queen);
            Map map = protectionTarget?.MapHeld;
            if (map == null || threatProfile == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (IsVisibleQueenAidThreat(queen, pawn, threatProfile))
                {
                    count++;
                }
            }

            foreach (Building_TurretGun turret in map.listerThings.GetThingsOfType<Building_TurretGun>())
            {
                if (IsVisibleQueenAidThreat(queen, turret, threatProfile))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsVisibleQueenAidThreat(Pawn queen, Thing target, QueenAidThreatProfile threatProfile)
        {
            if (target != null && target.Spawned && target.MapHeld != null && target.PositionHeld.Fogged(target.MapHeld))
            {
                return false;
            }

            return QueenAidThreatProfile.IsQueenAidThreat(queen, queen, target, threatProfile);
        }

        private Thing QueenAidProtectionTarget(Pawn queen)
        {
            if (queen == null)
            {
                return null;
            }

            if (queen.Spawned)
            {
                return queen;
            }

            Thing spawnedParent = queen.SpawnedParentOrMe;
            if (spawnedParent != null && spawnedParent != queen && spawnedParent.Spawned)
            {
                return spawnedParent;
            }

            return queen.ParentHolder as Thing;
        }

        private int ActiveQueenAidPawnCount(Map map)
        {
            if (map == null)
            {
                return 0;
            }

            return map.mapPawns.AllPawnsSpawned.Count(pawn => pawn != null && !pawn.Dead && !pawn.Downed && HasPawnAccountingState(pawn, XenoformingPawnAccountingState.QueenAidBorrowed));
        }

        internal Pawn TakeWorldCryptimorphForUse(XenoformingPawnAccountingState useState, bool allowPlayerPioneers)
        {
            List<Pawn> candidates = Find.WorldPawns.AllPawnsAlive
                .Where(pawn => IsWorldCryptimorphCandidate(pawn, allowPlayerPioneers))
                .OrderBy(pawn => pawn.Faction == Faction.OfPlayer ? 0 : 1)
                .ThenBy(_ => Rand.Value)
                .ToList();

            Pawn selected = candidates.FirstOrDefault();
            if (selected == null)
            {
                return null;
            }

            AddPawnAccountingState(selected, useState);
            if (XMTSettings.LogWorld)
            {
                Log.Message("Using world cryptimorph " + selected + " for " + useState + ".");
            }
            return selected;
        }

        private bool IsWorldCryptimorphCandidate(Pawn pawn, bool allowPlayerPioneers)
        {
            if (pawn == null || pawn.Spawned || pawn.Dead || pawn.Downed || CaravanUtility.IsCaravanMember(pawn))
            {
                return false;
            }

            if (!allowPlayerPioneers && pawn.Faction == Faction.OfPlayer)
            {
                return false;
            }

            if (!XMTUtility.IsXenomorph(pawn) || pawn == Queen || !pawn.ageTracker.Adult)
            {
                return false;
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return false;
            }

            return !HasPawnAccountingState(pawn, XenoformingPawnAccountingState.QueenAidBorrowed | XenoformingPawnAccountingState.SiteBorrowed | XenoformingPawnAccountingState.DeathAccounted);
        }

        internal Pawn GetWorldOrGeneratedCryptimorphForSite(bool allowPlayerPioneers)
        {
            Pawn pawn = TakeWorldCryptimorphForUse(XenoformingPawnAccountingState.SiteBorrowed, allowPlayerPioneers);
            return pawn ?? XenoformingUtility.GenerateFeralXenomorph();
        }

        internal bool IsQueenAidDefender(Pawn pawn)
        {
            return HasPawnAccountingState(pawn, XenoformingPawnAccountingState.QueenAidBorrowed) || (pawn != null && HasPawnAccountingState(ThingIDAccountingKey(pawn.thingIDNumber), XenoformingPawnAccountingState.QueenAidBorrowed));
        }

        private string PawnAccountingKey(Pawn pawn)
        {
            return pawn?.ThingID;
        }

        private static string ThingIDAccountingKey(int thingIDNumber)
        {
            return "thingIDNumber:" + thingIDNumber;
        }

        private bool HasPawnAccountingState(Pawn pawn, XenoformingPawnAccountingState state)
        {
            return pawn != null && HasPawnAccountingState(PawnAccountingKey(pawn), state);
        }

        private bool HasPawnAccountingState(string key, XenoformingPawnAccountingState state)
        {
            if (key.NullOrEmpty() || xenoformingPawnAccounting == null || !xenoformingPawnAccounting.TryGetValue(key, out XenoformingPawnAccountingState existing))
            {
                return false;
            }

            return (existing & state) != XenoformingPawnAccountingState.None;
        }

        private void AddPawnAccountingState(Pawn pawn, XenoformingPawnAccountingState state)
        {
            if (pawn != null)
            {
                AddPawnAccountingState(PawnAccountingKey(pawn), state);
            }
        }

        private void AddPawnAccountingState(string key, XenoformingPawnAccountingState state)
        {
            if (key.NullOrEmpty())
            {
                return;
            }

            xenoformingPawnAccounting ??= new Dictionary<string, XenoformingPawnAccountingState>();
            xenoformingPawnAccounting.TryGetValue(key, out XenoformingPawnAccountingState existing);
            xenoformingPawnAccounting[key] = existing | state;
        }

        private bool RemovePawnAccountingState(string key, XenoformingPawnAccountingState state)
        {
            if (key.NullOrEmpty() || xenoformingPawnAccounting == null || !xenoformingPawnAccounting.TryGetValue(key, out XenoformingPawnAccountingState existing))
            {
                return false;
            }

            if ((existing & state) == XenoformingPawnAccountingState.None)
            {
                return false;
            }

            XenoformingPawnAccountingState updated = existing & ~state;
            if (updated == XenoformingPawnAccountingState.None)
            {
                xenoformingPawnAccounting.Remove(key);
            }
            else
            {
                xenoformingPawnAccounting[key] = updated;
            }
            return true;
        }

        internal void HandleQueenCallForAid()
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + Queen + " calling aid to the map. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        public void RegisterIdeologyResinBuild(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            if (ideologyResinBuildThingIds == null)
            {
                ideologyResinBuildThingIds = new List<string>();
            }

            ideologyResinBuildThingIds.AddDistinct(thing.ThingID);
        }

        public void UnregisterIdeologyResinBuild(Thing thing)
        {
            if (thing == null || ideologyResinBuildThingIds == null)
            {
                return;
            }

            ideologyResinBuildThingIds.Remove(thing.ThingID);
        }

        public bool IsIdeologyResinBuild(Thing thing)
        {
            if (thing == null || ideologyResinBuildThingIds == null)
            {
                return false;
            }

            return ideologyResinBuildThingIds.Contains(thing.ThingID);
        }
    }
}
