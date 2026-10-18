using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class WorkGiver_HiveStructure : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForUndefined();
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        private IEnumerable<HiveStructureUnlockDef> CommandsFor(Pawn pawn) => HiveStructureUtility.Available.Where(d =>
            d.automatic && d.workType == def.workType && d.designation != null && d.Worker.ActorAllowed(d, pawn));

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn?.Map == null || pawn.GetComp<CompMatureMorph>() != null
                || !CommandsFor(pawn).Any(d => pawn.Map.designationManager.SpawnedDesignationsOfDef(d.designation).Any());
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (ShouldSkip(pawn))
            {
                yield break;
            }
            var seen = new HashSet<Thing>();
            foreach (HiveStructureUnlockDef command in CommandsFor(pawn))
            {
                foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(command.designation))
                {
                    Thing target = designation.target.Thing;
                    if (target != null && seen.Add(target))
                    {
                        yield return target;
                    }
                }
            }
        }

        private HiveStructureUnlockDef Match(Pawn actor, Thing target) => CommandsFor(actor).FirstOrDefault(d =>
            d.Worker.TargetAllowed(d, target) && target.Map.designationManager.DesignationOn(target, d.designation) != null);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return pawn?.Map != null && pawn.GetComp<CompMatureMorph>() == null && t?.Map == pawn.Map
                && !t.IsForbidden(pawn) && Match(pawn, t) != null
                && pawn.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some);
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn?.Map == null || pawn.GetComp<CompMatureMorph>() != null || t?.Map != pawn.Map)
            {
                return null;
            }
            HiveStructureUnlockDef unlock = Match(pawn, t);
            return unlock == null ? null : HiveStructureUtility.MakeJob(unlock, t);
        }
    }
}
