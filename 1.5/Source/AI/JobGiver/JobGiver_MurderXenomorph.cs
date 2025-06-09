using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{ 
    internal class JobGiver_MurderXenomorph : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if(pawn.CurJob != null && pawn.CurJob.def == JobDefOf.AttackMelee)
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
