using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    internal class XenoformingComp : WorldObjectComp
    {
        private bool _settlementAttacked = false;
        private bool _distressQuestSent = false;
        private bool _removeWhenMapClears = false;
        private int _distressQuestId = -1;
        private string _distressSignalTag;

        public bool SettlementAttacked => _settlementAttacked;
        public bool DistressQuestSent => _distressQuestSent;
        public bool RemoveWhenMapClears => _removeWhenMapClears;
        public int DistressQuestId => _distressQuestId;
        public string DistressSignalTag => _distressSignalTag;

        float AttackTolerance = 0;
        int AttackInterval = 60000;

        int DistressSignalSendTick = int.MaxValue;
        int SettlementDestroyedTick = int.MaxValue;
        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            AttackTolerance = Rand.Range(10, 75);
        }
        
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);

            if(parent.Faction == Faction.OfPlayer)
            {
                return;
            }

            if (!parent.IsHashIntervalTick(AttackInterval))
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;

            if (_settlementAttacked)
            {
                if(tick >= SettlementDestroyedTick)
                {
                    SettlementDestroyedTick = int.MaxValue;
                    XenoformingUtility.ConfirmDestroyedSettlement(parent as Settlement);
                    return;
                }
                if(tick >= DistressSignalSendTick)
                {
                    DistressSignalSendTick = int.MaxValue;
                    SettlementDestroyedTick = tick + AttackInterval * 10;

                    XenoformingUtility.AddDistressSignal(parent as Settlement, SettlementDestroyedTick);
                    return;
                }
                
            }    

            if (XenoformingUtility.XenoformingMeets(AttackTolerance))
            {
                if(Rand.Chance(XenoformingUtility.ChanceByXenoforming(XMTSettings.SiteAttackChance)))
                {
                    _settlementAttacked = true;

                    if (XMTSettings.LogWorld)
                    {
                        Log.Message("[XMT][World] " + parent + " was attacked by cryptimorphs!");
                    }

                    if(XMTUtility.QueenIsPlayer())
                    {
                        XenoformingUtility.AddReprisal(parent.Faction, StorytellerUtility.DefaultSiteThreatPointsNow() * 4); 
                    }
                   
                    DistressSignalSendTick = tick +AttackInterval;
                }
                

            }

        }

        public void Notify_DistressSignalSent(int questId, string signalTag, int settlementDestroyedTick)
        {
            _distressQuestSent = true;
            _distressQuestId = questId;
            _distressSignalTag = signalTag;
            SettlementDestroyedTick = settlementDestroyedTick;
        }

        public void Notify_Investigated()
        {
            _settlementAttacked = false;
            _distressQuestSent = false;
            _distressQuestId = -1;
            _distressSignalTag = null;
            DistressSignalSendTick = int.MaxValue;
            SettlementDestroyedTick = int.MaxValue;
            _removeWhenMapClears = true;
        }

        public void Notify_DestroyedConfirmed()
        {
            _settlementAttacked = false;
            _distressQuestSent = false;
            _distressQuestId = -1;
            _distressSignalTag = null;
            DistressSignalSendTick = int.MaxValue;
            SettlementDestroyedTick = int.MaxValue;
            _removeWhenMapClears = false;
        }

        public void Notify_RemoveWhenMapClears()
        {
            _removeWhenMapClears = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _settlementAttacked, "settlementAttacked", false);
            Scribe_Values.Look(ref _distressQuestSent, "distressQuestSent", false);
            Scribe_Values.Look(ref _removeWhenMapClears, "removeWhenMapClears", false);
            Scribe_Values.Look(ref _distressQuestId, "distressQuestId", -1);
            Scribe_Values.Look(ref _distressSignalTag, "distressSignalTag");
            Scribe_Values.Look(ref AttackTolerance, "attackTolerance", 0);
            Scribe_Values.Look(ref DistressSignalSendTick, "distressSignalSendTick", int.MaxValue);
            Scribe_Values.Look(ref SettlementDestroyedTick, "settlementDestroyedTick", int.MaxValue);
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            MapParent mapParent = parent as MapParent;

            if (mapParent.HasMap && mapParent.Faction == Faction.OfPlayer)
            {
                Command_Action increaseAction = new Command_Action();
                increaseAction.defaultLabel = "Increase Xenoforming";
                increaseAction.defaultDesc = "increase Xenoforming of World";
                increaseAction.action = delegate
                {
                    XenoformingUtility.IncreaseXenoforming(1.0f);
                };
                increaseAction.Order = 3000f;
                yield return increaseAction;

                Command_Action decreaseAction = new Command_Action();
                decreaseAction.defaultLabel = "Decrease Xenoforming";
                decreaseAction.defaultDesc = "Decrease Xenoforming of World";
                decreaseAction.action = delegate
                {
                    XenoformingUtility.DecreaseXenoforming(1.0f);
                };
                decreaseAction.Order = 3001f;
                yield return decreaseAction;
            }

        }
    }
}
