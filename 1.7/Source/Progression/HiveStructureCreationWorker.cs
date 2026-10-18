using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class HiveStructureCreationWorker
    {
        public virtual bool ActorAllowed(HiveStructureUnlockDef def, Pawn pawn) => def.actorRules.Allows(pawn) && !pawn.Downed;
        public virtual bool TargetAllowed(HiveStructureUnlockDef def, Thing target)
        {
            if (target == null || !target.Spawned || target.Destroyed)
                return false;
            if (def.mode == HiveCreationMode.ApplyHediff)
                return target is Pawn pawn && def.targetRules.Allows(pawn) && !pawn.health.hediffSet.HasHediff(def.appliedHediff);
            return def.mode == HiveCreationMode.Merge && def.sourceBuildings?.Allows(target) == true && (target.TryGetComp<CompOrganicGrowth>()?.TotalBodySize() ?? 0f) >= def.minSourceBodySize;
        }

        public virtual bool Complete(HiveStructureUnlockDef def, Pawn actor, Thing target)
        {
            if (!ActorAllowed(def, actor))
                return false;
            if (def.mode == HiveCreationMode.SelfTransform && target is HiveStructureSite site)
                return site.TryTransform(actor);
            if (!TargetAllowed(def, target) || actor.Map != target.Map || !actor.CanReachImmediate(target, PathEndMode.Touch))
                return false;
            if (def.mode == HiveCreationMode.Merge)
                return XMTUtility.TransformThingIntoThing(target, def.building, out _, actor, actor);
            if (def.mode != HiveCreationMode.ApplyHediff || target is not Pawn host)
                return false;
            if (!XMTUtility.IsTargetImmobile(host) && Rand.Chance(XMTUtility.GetDodgeChance(host, actor.IsPsychologicallyInvisible())))
                return false;
            Hediff hediff = HediffMaker.MakeHediff(def.appliedHediff, host);
            if (actor.Faction == Faction.OfPlayer)
                hediff.SetVisible();
            hediff.TryGetComp<HediffComp_BuildingMorphing>()?.InheritFromInjector(actor);
            host.health.AddHediff(hediff);
            if (def.targetMemory != null)
                XMTUtility.GiveInteractionMemory(host, def.targetMemory, actor);
            if (def.actorMemory != null)
                XMTUtility.GiveMemory(actor, def.actorMemory);
            actor.needs?.joy?.GainJoy(def.nestJoy, InternalDefOf.NestTending);
            if (def.designation != null)
                host.Map.designationManager.TryRemoveDesignationOn(host, def.designation);
            return true;
        }
    }
}
