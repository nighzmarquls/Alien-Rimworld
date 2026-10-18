using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class CompStructureCreationCommands : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer && !(parent.Faction == null && XMTUtility.QueenIsPlayer()))
                yield break;
            foreach (HiveStructureUnlockDef def in HiveStructureUtility.Available.Where(d => d.buildingGizmo && d.mode == HiveCreationMode.Merge && d.sourceBuildings?.Allows(parent) == true))
            {
                var command = new Command_Action
                {
                    defaultLabel = def.LabelCap,
                    defaultDesc = def.description,
                    icon = def.Icon,
                    action = () => HiveStructureUtility.BeginMerge(def, parent)
                };
                if (!def.Worker.TargetAllowed(def, parent))
                    command.Disable("XMT_StructureSourceInvalid".Translate());
                yield return command;
            }
        }
    }
}
