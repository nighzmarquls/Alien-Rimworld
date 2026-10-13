using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class JobGiver_NemesisMission : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return (pawn.GetLord()?.LordJob as LordJob_NemesisMission)?.GetMissionJob(pawn);
        }
    }
}
