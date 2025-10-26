using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class HibernationCocoon : Building_CryptosleepCasket
    {
        public override void Open()
        {
            base.Open();
            Destroy();
        }
        public override bool Accepts(Thing thing)
        {
            if (ContainedThing != null)
            {
                return false;
            }
            if (XMTUtility.IsXenomorph(thing))
            {
                return true;
            }
            return false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Faction == null || !Faction.IsPlayer)
            {
                yield break;
            }

            Command_Action command_Action = new Command_Action();
            command_Action.action = Open;
            command_Action.defaultLabel = "CommandPodEject".Translate();
            command_Action.defaultDesc = "CommandPodEjectDesc".Translate();

            command_Action.hotKey = KeyBindingDefOf.Misc8;
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Abilities/Starbeast_Leap");
            yield return command_Action;
        }
    }

}
