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
                if (InorganicSubversionUtility.IsValidSubverterTarget(Parent, pawn))
                {
                    return pawn;
                }
            }

            return null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!XMTUtility.QueenIsPlayer())
            {
                yield break;
            }

            TargetingParameters ImplantParameters = TargetingParameters.ForPawns();

            ImplantParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == Parent)
                {
                    return false;
                }

                
                if (!InorganicSubversionUtility.IsValidSubverterTarget(Parent, target.Thing as Pawn))
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action ImplantHost_Action = new Command_Action();
            ImplantHost_Action.defaultLabel = "XMT_Implant".Translate();
            ImplantHost_Action.defaultDesc = "XMT_ImplantDescription".Translate();
            ImplantHost_Action.icon = Implant;
            ImplantHost_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(ImplantParameters, delegate (LocalTargetInfo target)
                {
                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ImplantHunt, target);
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
