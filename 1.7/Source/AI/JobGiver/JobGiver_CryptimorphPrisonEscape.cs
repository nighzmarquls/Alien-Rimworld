using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobGiver_CryptimorphPrisonEscape : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompMatureMorph morph = pawn?.GetMorphComp();
            if (morph == null || !morph.TryGetPrisonEscapeJob(out Job job))
            {
                return null;
            }

            if (XMTSettings.LogJobGiver)
            {
                Log.Message("[XMT][JobGiver] " + pawn + " is escaping ordinary imprisonment with " + job + ".");
            }
            return job;
        }
    }
}
