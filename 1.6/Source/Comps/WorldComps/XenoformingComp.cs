using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class XenoformingComp : WorldObjectComp
    {
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
