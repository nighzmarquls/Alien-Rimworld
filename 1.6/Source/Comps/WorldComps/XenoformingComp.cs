using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class XenoformingComp : WorldObjectComp
    {
        private bool _settlementAttacked = false;

        public bool SettlementAttacked => _settlementAttacked;

        float AttackTolerance = 0;

        int nextTickCheck = -1;
        int AttackInterval = 60000;
        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            AttackTolerance = Rand.Range(10, 75);
        }
        
        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            if(_settlementAttacked)
            {
                return;
            }

            if(parent.Faction == Faction.OfPlayer)
            {
                return;
            }

            if(_settlementAttacked)
            {
                return;
            }    

            if (XenoformingUtility.XenoformingMeets(AttackTolerance))
            {
                if(parent.IsHashIntervalTick(AttackInterval))
                {
                    if(Rand.Chance(XenoformingUtility.ChanceByXenoforming(XMTSettings.SiteAttackChance)))
                    {
                        _settlementAttacked = true;

                        if (XMTSettings.LogWorld)
                        {
                            Log.Message(parent + " was attacked by cryptimorphs!");
                        }
                    }
                }

            }

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
