using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{ 
    internal class JobGiver_MurderXenomorph : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if(pawn.CurJob != null && (pawn.CurJob.def == JobDefOf.AttackMelee || pawn.CurJob.def == JobDefOf.PredatorHunt))
            {
                return null;
            }

            if (!(pawn.MentalState is MentalState_XMT_MurderousRage mentalState_MurderousRage) || !mentalState_MurderousRage.IsTargetStillValidAndReachable())
            {
                return null;
            }

            if (mentalState_MurderousRage.target.IsPsychologicallyInvisible())
            {
                return null;
            }
            else
            {
                Thing spawnedParentOrMe = mentalState_MurderousRage.target.SpawnedParentOrMe;

                if (pawn.needs.food.Starving)
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is starving and doing a predator hunt on " + spawnedParentOrMe);
                    }
                    pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack;
                    Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, spawnedParentOrMe);
                    job.killIncappedTarget = true;
                    //pawn.Map.designationManager.AddDesignation(new Designation(spawnedParentOrMe, DesignationDefOf.Hunt));

                    if (spawnedParentOrMe != mentalState_MurderousRage.target)
                    {
                        job.maxNumMeleeAttacks = 2;
                    }
                    return job;
                }
                else
                {
                    if (XMTSettings.LogJobGiver)
                    {
                        Log.Message(pawn + " is not hungry and going to murder " + spawnedParentOrMe);
                    }
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, spawnedParentOrMe);
                    job.canBashDoors = true;
                    job.killIncappedTarget = true;
                    if (spawnedParentOrMe != mentalState_MurderousRage.target)
                    {
                        job.maxNumMeleeAttacks = 2;
                    }

                    return job;
                }
            }
        }
    }
}
