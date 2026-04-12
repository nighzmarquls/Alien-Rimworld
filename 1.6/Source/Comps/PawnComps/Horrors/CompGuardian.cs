using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static AlienRace.ExtendedGraphics.ConditionMood;
using static HarmonyLib.Code;


namespace Xenomorphtype
{
    public class CompGaurdian : ThingComp
    {
        Pawn protectedWard = null;
        int nextWardCheckTick = -1;

        CompGaurdianProperties Props => props as CompGaurdianProperties;
        Pawn Parent => parent as Pawn;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref protectedWard, "protectedWard");
            Scribe_Values.Look(ref nextWardCheckTick, "nextWardCheckTick");
        }


        static private Texture2D targetingTexture => ContentFinder<Texture2D>.Get("UI/Abilities/XMT_SelectWard");
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Downed)
            {
                yield break;
            }

            TargetingParameters TargetPawnParameters = TargetingParameters.ForPawns();

            TargetPawnParameters.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                if (!target.HasThing)
                {
                    return false;
                }

                if (target.Thing is Pawn testPawn)
                {
                    if (XMTUtility.IsInorganic(testPawn) || XMTUtility.IsXenomorph(testPawn))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action TargetPawn_Action = new Command_Action();
            TargetPawn_Action.defaultLabel = "XMT_Guard_Target".Translate();
            TargetPawn_Action.defaultDesc = "XMT_Guard_Target_Description".Translate();
            TargetPawn_Action.icon = targetingTexture;
            TargetPawn_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(TargetPawnParameters, delegate (LocalTargetInfo target)
                {
                    if(target.Pawn is Pawn targetedPawn)
                    {
                        protectedWard = targetedPawn;

                        RelationsUtility.TryDevelopBondRelation(protectedWard, Parent, 100);
                        if (TrainableUtility.CanBeMaster(protectedWard, Parent))
                        {
                            Parent.training.Train(TrainableDefOf.Tameness, protectedWard, true);
                            Parent.training.Train(TrainableDefOf.Obedience, protectedWard, true);
                            Parent.playerSettings.Master = protectedWard;
                            Parent.playerSettings.followDrafted = true;
                            Parent.playerSettings.followFieldwork = true;
                        }
                    }
                });

            };
            yield return TargetPawn_Action;
        }

        public override void CompTickInterval(int delta)
        {
            if (!Parent.Spawned)
            {
                return;
            }

            if(Parent.Downed)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick > nextWardCheckTick)
            {
                nextWardCheckTick = tick + Mathf.CeilToInt(Props.wardCheckHourInterval * 2500);

                if (protectedWard == null)
                {
                    return;
                }

                if(protectedWard.Dead)
                {
                    Parent.mindState.mentalBreaker.TryDoMentalBreak("XMT_Guard_Coma_Reason".Translate(), MentalBreakDefOf.Catatonic);
                    return;
                }

                if (protectedWard.MapHeld != Parent.MapHeld)
                {
                    Parent.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad, "XMT_Guard_Sad_Reason".Translate(), forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                    return;
                }
                else
                {
                    if (Parent.mindState.mentalStateHandler.InMentalState)
                    {
                        Parent.mindState.mentalStateHandler.Reset();
                    }
                }


                if (TrainableUtility.CanBeMaster(protectedWard, Parent))
                {
                    Parent.training.Train(TrainableDefOf.Tameness, protectedWard, true);
                    Parent.training.Train(TrainableDefOf.Obedience, protectedWard, true);
                    Parent.playerSettings.Master = protectedWard;
                    Parent.playerSettings.followDrafted = true;
                    Parent.playerSettings.followFieldwork = true;
                }

                if (protectedWard.Downed)
                {
                    if (!protectedWard.InBed() && (Parent.CurJobDef != JobDefOf.Rescue && Parent.CurJobDef != JobDefOf.Nuzzle && Parent.CurJobDef != JobDefOf.TendPatient))
                    {
                        Building_Bed building_Bed = RestUtility.FindBedFor(protectedWard, Parent, checkSocialProperness: false);
                        if (building_Bed == null)
                        {
                            building_Bed = RestUtility.FindBedFor(protectedWard, Parent, checkSocialProperness: false, ignoreOtherReservations: true);
                        }

                        if (building_Bed == null)
                        {
                            building_Bed = RestUtility.FindBedFor(Parent, Parent, checkSocialProperness: false, ignoreOtherReservations: true);

                            if (building_Bed == null && !protectedWard.health.HasHediffsNeedingTend())
                            {
                                Job comfortJob = JobMaker.MakeJob(JobDefOf.Nuzzle, protectedWard);
                                comfortJob.count = 1;
                                Parent.jobs.StartJob(comfortJob, JobCondition.InterruptForced);
                                return;
                            }
                        }
                        else
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.Rescue, protectedWard, building_Bed);
                            job.count = 1;
                            Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                            return;
                        }
                    }

                    if(protectedWard.health.HasHediffsNeedingTend())
                    {
                        Job tendJob = JobMaker.MakeJob(JobDefOf.TendPatient, protectedWard);
                        tendJob.count = 1;
                        Parent.jobs.StartJob(tendJob, JobCondition.InterruptForced);
                        return;
                    }

      
                    Job guardJob = JobMaker.MakeJob(JobDefOf.GotoWander, protectedWard);
                    Parent.jobs.TryTakeOrderedJob(guardJob);

                    return;
                    
                }

                if (protectedWard.LastAttackedTarget != null)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, protectedWard.LastAttackedTarget);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);

                    return;
                }

                if (protectedWard.CurJobDef == JobDefOf.FleeAndCower || protectedWard.CurJobDef == JobDefOf.FleeAndCowerShort)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.Goto, protectedWard);
                    Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                   
                    return;
                }

               
            }
        }
    }

    public class CompGaurdianProperties : CompProperties
    {
        public float wardCheckHourInterval = 0.25f;
        public CompGaurdianProperties()
        {
            this.compClass = typeof(CompGaurdian);
        }
        public CompGaurdianProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
