using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    public class CompInorganicSubverterHostHunter : CompHostHunter
    {
        static private Texture2D Implant => ContentFinder<Texture2D>.Get("UI/Abilities/Implant");
        public override bool ShouldHunt()
        {
            return XMTUtility.GetQueen() != null && base.ShouldHunt();
        }

        public override Pawn GetPreyTarget()
        {
            List<Pawn> pawns = parent.Map.mapPawns.AllPawnsSpawned.ToList();
            pawns.Shuffle();
            foreach (Pawn pawn in pawns)
            {
                if(pawn.def == Parent.def)
                {
                    continue;
                }

                if (InorganicSubversionUtility.IsValidSubverterTarget(Parent, pawn))
                {
                    return pawn;
                }
            }

            return null;
        }

        public override Thing GetHuntTarget()
        {
            List<Thing> targets = parent.Map.mapPawns.AllPawnsSpawned
                .Where(pawn => InorganicSubversionUtility.IsValidSubverterTarget(Parent, pawn)).Cast<Thing>().ToList();
            targets.AddRange(parent.Map.listerThings.GetThingsOfType<Building_TurretGun>()
                .Where(turret => XMT_IFFUtility.IsValidSubverterTurretTarget(Parent, turret, parent.Map)));
            return targets.RandomElementWithFallback();
        }

        public override void StartHuntTarget(Thing target)
        {
            if (target is Building_TurretGun)
            {
                Parent.jobs.StartJob(JobMaker.MakeJob(XenoWorkDefOf.XMT_SubvertTurret, target), JobCondition.InterruptForced);
                return;
            }

            base.StartHuntTarget(target);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!XMTUtility.QueenIsPlayer())
            {
                yield break;
            }

            TargetingParameters ImplantParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true
            };

            ImplantParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }

                
                bool validPawn = target.Thing is Pawn pawn
                    && InorganicSubversionUtility.IsValidSubverterTarget(Parent, pawn);
                bool validTurret = target.Thing is Building_TurretGun turret
                    && XMT_IFFUtility.IsValidSubverterTurretTarget(Parent, turret, target.Map);
                if (!validPawn && !validTurret)
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action ImplantHost_Action = new Command_Action();
            ImplantHost_Action.defaultLabel = "XMT_SubverterAttach".Translate();
            ImplantHost_Action.defaultDesc = "XMT_SubverterAttachDescription".Translate();
            ImplantHost_Action.icon = Implant;
            ImplantHost_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(ImplantParameters, delegate (LocalTargetInfo target)
                {
                    JobDef jobDef = target.Thing is Building_TurretGun
                        ? XenoWorkDefOf.XMT_SubvertTurret
                        : XenoWorkDefOf.XMT_ImplantHunt;
                    Job job = JobMaker.MakeJob(jobDef, target);
                    FeralJobUtility.ClearFeralJobReservationsForTarget(target.Thing);
                    FeralJobUtility.ReserveThingForJob(Parent, job, target.Thing);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                });

            };

            yield return ImplantHost_Action;
        }

        public override bool TryResist(Pawn target)
        {
            if(InorganicSubversionUtility.IsSubverted(target))
            {
                return true;
            }

            if (Rand.Chance(XMTUtility.GetDefendGrappleChance(Parent, target)))
            {
                return true;
            }

            return false;
        }
    }

    public class CompProperties_InorganicSubverterHostHunter : CompHostHunterProperties
    {
        public CompProperties_InorganicSubverterHostHunter()
            : base(typeof(CompInorganicSubverterHostHunter))
        {
        }

        public CompProperties_InorganicSubverterHostHunter(Type compClass)
            : base(compClass)
        {
        }
    }
}
