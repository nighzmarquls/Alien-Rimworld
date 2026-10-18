using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class PlaceWorker_HiveStructureUnlock : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef def, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            return DefDatabase<HiveStructureUnlockDef>.AllDefsListForReading.Any(u => u.building == def && HiveStructureUtility.Unlocked(u)) ? AcceptanceReport.WasAccepted : "XMT_StructureLocked".Translate();
        }
    }
}
