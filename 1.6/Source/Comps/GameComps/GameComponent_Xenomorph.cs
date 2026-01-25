using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace Xenomorphtype
{
    public class GameComponent_Xenomorph : GameComponent
    {
        public Pawn Queen = null;

        List<PlanetTile> CandidateTiles = new List<PlanetTile>();

        bool PlayerEmbryoInWorld = false;
        bool PlayerXenomorphInWorld = false;
        bool PlayerOvomorphInWorld = false;

        private List<string> deadMorphs = new List<string>();

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

        private float _lastxenoforming = XMTSettings.InitialXenoforming*100;
        private float _xenoforming = XMTSettings.InitialXenoforming*100;
        public float Xenoforming
        {
            get
            {
                return _xenoforming;
            }
            set
            {
                if(DebugSettings.ShowDevGizmos)
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
                        Log.Message("Adjusting Xenoforming by DEBUG total: " + _xenoforming);
                    }
                    EvaluateXenoforming();
                }
            }
        }
        float MutationProliferation = 0;

        int nextXenoformingTick = -1;

        private const float XenomorphImpact = 1f;
        private const float OvomorphSaturationLimit = 5;
        private const float OvomorphImpact = 0.1f;
        private const float EmbryoSaturationLimit = 10;
        private const float EmbryoImpact = 0.5f;
        private const float QueenAidImpact = 0f;

        private int _xenoformingStartTick = -1;

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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Log.Message("Clearing Caches");
                XMTHiveUtility.ClearAllNestSites();
                InfiltrationUtility.ClearAllCaches();
                PawnCacheWrapper.ClearAllPawnCaches();
            }
        }

        public void HandleMatureMorphDeath(Pawn pawn)
        {
            if (!pawn.ageTracker.Adult)
            {
                return;
            }

            if (deadMorphs.Contains(pawn.ToString()))
            {
                return;
            }

            deadMorphs.Add(pawn.ToString());

            _xenoforming = Mathf.Max(_xenoforming - XenomorphImpact, 0);

            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for death of " + pawn.ToString() + " total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }
        public void ReleaseOvomorphOnWorld(Ovomorph Ovomorph)
        {
            Ovomorph.HatchNow();
            _xenoforming = Mathf.Min(OvomorphSaturationLimit, _xenoforming + (OvomorphImpact*Ovomorph.stackCount));
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
            
            _xenoforming += XenomorphImpact;
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        internal void ReleaseEmbryoOnWorld(Pawn pawn)
        {
            
            _xenoforming = Mathf.Min(EmbryoSaturationLimit, _xenoforming + (EmbryoImpact));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map with an embryo. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }

        internal void HandleQueenCallForAid()
        {
            _xenoforming = Mathf.Max(0, _xenoforming - (QueenAidImpact));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + Queen + " calling aid to the map. total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }
    }
}
