using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class GameComponent_Xenomorph : GameComponent
    {
        public Pawn Queen = null;

        List<PlanetTile> CandidateTiles = new List<PlanetTile>();

        bool PlayerEmbryoInWorld = false;
        bool PlayerXenomorphInWorld = false;
        bool PlayerOvamorphInWorld = false;

        int XenoformingCheckInterval = 60000;
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

        private float _lastxenoforming = 0;
        private float _xenoforming = 0;
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
        private const float OvamorphSaturationLimit = 5;
        private const float OvamorphImpact = 0.1f;
        private const float EmbryoSaturationLimit = 10;
        private const float EmbryoImpact = 0.5f;
        private const float QueenAidImpact = 2f;

        private int _xenoformingStartTick = -1;


        public GameComponent_Xenomorph(Game game)
        {
            Queen = null;
            if (ModsConfig.AnomalyActive)
            {
      
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if(_xenoforming <= 0)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick >= nextXenoformingTick)
            {
                

                if (_xenoforming >= 0.5f && _xenoforming < 1.0f)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Iterating Embryo birth time");
                    }
                    _xenoforming += 0.1f;
                }
                else
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Iterating Egg encounter");
                    }
                    _xenoforming += 0.01f;
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
            if (_xenoforming >= 15)
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

                PlanetTile target = PlanetTile.Invalid;
                foreach(PlanetTile candidate in CandidateTiles)
                {
                    float score = XenoMapDefOf.XMT_DessicatedBlight.Worker.GetScore(XenoMapDefOf.XMT_DessicatedBlight, candidate.Tile, candidate);

                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("Xenoforming biome score: " + score);
                    }

                    if (score > 1)
                    {
                        GetCandidateNeighbors(candidate);
                        target = candidate;

                        target.Tile.PrimaryBiome = XenoMapDefOf.XMT_DessicatedBlight;
                        break;
                    }
                }

                if (target != PlanetTile.Invalid)
                {
                    CandidateTiles.Remove(target);
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
        }

        public override void LoadedGame()
        {

        }

        public override void StartedNewGame()
        {

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
            Scribe_Values.Look(ref PlayerOvamorphInWorld, "PlayerOvamorphInWorld", false);
            Scribe_Collections.Look(ref CandidateTiles, "CandidateTiles");
        }

        public void HandleMatureMorphDeath(Pawn pawn)
        {
            _xenoforming = Mathf.Max(_xenoforming - XenomorphImpact, 0);

            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for death of " + pawn + " total: " + _xenoforming);
            }
            EvaluateXenoforming();
        }
        public void ReleaseOvamorphOnWorld(Ovamorph ovamorph)
        {
            ovamorph.HatchNow();
            _xenoforming = Mathf.Min(OvamorphSaturationLimit, _xenoforming + (OvamorphImpact*ovamorph.stackCount));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + ovamorph + " leaving the map. total: " + _xenoforming);
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
