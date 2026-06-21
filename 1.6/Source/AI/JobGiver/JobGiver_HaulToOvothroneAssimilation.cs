using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobGiver_HaulToOvothroneAssimilation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || !OvothroneAssimilationHaulUtility.AnyUsefulOvothroneQueen(pawn.Map, pawn))
            {
                return null;
            }

            Thing bestItem = null;
            EggSack bestThrone = null;
            int bestCount = 0;
            float bestScore = float.MaxValue;

            foreach (Thing thing in pawn.Map.listerThings.AllThings)
            {
                if (!OvothroneAssimilationHaulUtility.TryFindBestThroneFor(pawn, thing, forced: false, out EggSack throne, out CompQueenAssimilation _, out QueenAssimilationDef _, out int count))
                {
                    continue;
                }

                float score = pawn.Position.DistanceToSquared(thing.Position) + thing.Position.DistanceToSquared(throne.InteractionCell);
                if (bestItem == null || score < bestScore)
                {
                    bestItem = thing;
                    bestThrone = throne;
                    bestCount = count;
                    bestScore = score;
                }
            }

            if (bestItem == null || bestThrone == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_HaulToOvothroneAssimilation, bestItem, bestThrone, bestThrone.InteractionCell);
            job.count = bestCount;
            return job;
        }
    }
}
