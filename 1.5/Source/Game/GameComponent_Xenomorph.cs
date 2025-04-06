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

        bool PlayerEmbryoInWorld = false;
        bool PlayerXenomorphInWorld = false;
        bool PlayerOvamorphInWorld = false;
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

        private float _xenoforming = 0;
        public float Xenoforming => _xenoforming;
        float MutationProliferation = 0;

        int nextXenoformingTick = -1;

        private const float XenomorphImpact = 1f;
        private const float OvamorphSaturationLimit = 5;
        private const float OvamorphImpact = 0.1f;
        private const float EmbryoSaturationLimit = 10;
        private const float EmbryoImpact = 0.5f;

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

            if(Find.TickManager.TicksGame >= nextXenoformingTick)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("Evaluating current Xenoforming: " + _xenoforming);
                }

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

                nextXenoformingTick = Find.TickManager.TicksGame + 60000;
            }

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
            Scribe_Values.Look(ref nextXenoformingTick, "nextXenoformingTick", 0);
            Scribe_Values.Look(ref MutationProliferation, "MutationProliferation", 0);
            Scribe_Values.Look(ref PlayerEmbryoInWorld, "PlayerEmbryoInWorld", false);
            Scribe_Values.Look(ref PlayerXenomorphInWorld, "PlayerXenomorphInWorld", false);
            Scribe_Values.Look(ref PlayerOvamorphInWorld, "PlayerOvamorphInWorld", false);
        }

        public void HandleMatureMorphDeath(Pawn pawn)
        {
            _xenoforming = Mathf.Max(_xenoforming - XenomorphImpact, 0);

            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for death of " + pawn + " total: " + _xenoforming);
            }
        }
        public void ReleaseOvamorphOnWorld(Ovamorph ovamorph)
        {
           
            _xenoforming = Mathf.Min(OvamorphSaturationLimit, _xenoforming + (OvamorphImpact*ovamorph.stackCount));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + ovamorph + " leaving the map. total: " + _xenoforming);
            }
        }

        internal void ReleaseMutagenOnWorld(float intensity)
        {
            
            MutationProliferation += intensity;
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting mutation proliferation by " + intensity);
            }
        }

        internal void ReleaseXenomorphOnWorld(Pawn pawn)
        {
            
            _xenoforming += XenomorphImpact;
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map. total: " + _xenoforming);
            }
        }

        internal void ReleaseEmbryoOnWorld(Pawn pawn)
        {
            
            _xenoforming = Mathf.Min(EmbryoSaturationLimit, _xenoforming + (EmbryoImpact));
            if (XMTSettings.LogWorld)
            {
                Log.Message("Adjusting Xenoforming for " + pawn + " leaving the map with an embryo. total: " + _xenoforming);
            }
        }
    }
}
